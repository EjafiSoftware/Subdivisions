# PRD: Pure tracing core behind an IBoundaryGraph seam

## Summary

Extract the border-tracing logic (path-finding, tessellation, polygon cleanup, ring
assembly) out of the Burst job that reaches directly into the game's ECS lookups, and put
it behind a small `IBoundaryGraph` seam in a new pure assembly. The trace then runs as
managed code consuming the seam, and can be exercised by a normal `dotnet test` suite with
an in-memory graph - no game launch required.

## Problem

The genuinely hard, regression-prone code in the mod (A* boundary tracing, neck
resolution, simple-polygon validation, adaptive tessellation) currently has its interface
made of the game's ECS lookups: `ComponentLookup<Edge>`, `ComponentLookup<Curve>`,
`BufferLookup<ConnectedEdge>`, plus `NetBoundary` reading `Composition`/`NetCompositionData`.
Those lookups are only populated by a running game, so the only way to verify a trace today
is to launch Cities: Skylines II and try it by hand. There is no test suite. Every fix to
the tracing rules is a manual, slow, error-prone round trip.

## Goal

A deep tracing module with a small interface and a real test surface:

- One entry point, `BorderTracer.Trace(points, graph)`, with the path-finder, tessellator,
  and polygon cleanup as internal implementation detail.
- The seam, `IBoundaryGraph`, satisfied by two adapters: `EcsBoundaryGraph` in production,
  `ArrayBoundaryGraph` in tests. Two adapters justify the seam.
- A `dotnet test` suite that pins the tracing behavior, including the two non-obvious rules
  the codebase depends on (same-kind-only tracing; no edge traced twice / disjoint closing
  arc).

## Non-goals

- Preserving Burst on the trace. The trace is a dirty-gated, local, interactive workload;
  Burst is not load-bearing here (see Performance).
- Testing the `CompositionState` -> `NetworkKind` classification. That logic reads a
  `Game.dll` enum, so it stays in the production adapter and is verified in-game.
- Reworking snapping, the UI bridge, or district creation beyond what falls out of moving
  the trace to the main thread.

## Decisions

These were settled by walking the design tree. Each is load-bearing.

1. **Test environment: plain `dotnet test`, core avoids Native collections.** `NativeList`
   and friends P/Invoke into Unity's native allocator, which is not loaded in a standalone
   test process. The core's algorithms use managed collections so the code tests actually
   execute never touches a Native container.

2. **Three projects, seam enforced by the compiler.**
   - `Subdivisions.Core` - pure: `IBoundaryGraph`, `BorderTracer`, and the algorithms.
     References only `Unity.Mathematics`, `Colossal.Mathematics`, `Unity.Entities`. Does not
     reference `Game` or `Unity.Collections`, so re-coupling the trace to ECS is a build
     error, not a review miss. Public surface: `BorderTracer` + the seam types (`IBoundaryGraph`,
     `EdgeEnds`, `SnapPoint`, `NetworkKind`, `TraceResult`) plus the pure curve/segment helpers
     `Geometry` and `NetSnap`, which the mod's snapping reuses. The path-finder, tessellator,
     `Polygon`, and `MinHeap` stay `internal` (reachable to tests via `InternalsVisibleTo`).
   - `Subdivisions` - the mod: jobs, ECS, `EcsBoundaryGraph`, tool systems. References Core.
   - `Subdivisions.Tests` - net48, NUnit. References Core; hosts `ArrayBoundaryGraph` and
     the `BoundaryGraphBuilder`. Does not import the modding toolchain (`Mod.props`/
     `Mod.targets`); it deploys nothing. The math DLLs (`Unity.Mathematics`,
     `Colossal.Mathematics`, `Unity.Entities`) are referenced with `Private=true`
     (CopyLocal), which makes MSBuild copy them plus their transitive Unity closure into the
     test output dir; standard runtime probing then resolves them with no `AssemblyResolve`
     shim (see Spike result). `Unity.Collections.dll` lands in the output too - harmless,
     since copying the DLL does not trigger native init; only allocating a Native container
     at runtime would, which the executed core code never does.

3. **Managed core, Burst dropped on the trace (option Y).** The trace runs managed; pool
   the working buffers to keep steady-state GC at zero. Only `CreateDistrictJob`'s old work
   remains, and it moves to the main thread (see decision 6). The struct-generic
   dual-instantiation route that would preserve Burst (option Z) is rejected as
   disproportionate plumbing for microseconds on a cold path.

4. **`IBoundaryGraph` shape.** Entity handles (no opaque-id remapping), a small return type
   instead of tuples, methods are verbs and properties are nouns.

   ```csharp
   public readonly struct EdgeEnds
   {
       public Entity Start { get; }
       public Entity End { get; }
       public EdgeEnds(Entity start, Entity end) { Start = start; End = end; }
   }

   public interface IBoundaryGraph
   {
       EdgeEnds GetEndpoints(Entity edge);
       Bezier4x3 GetCurve(Entity edge);
       bool IsBoundary(Entity edge);
       NetworkKind GetKind(Entity edge);
       NeighborEnumerator GetNeighbors(Entity node);
   }
   ```

   Classification (`IsBoundary`/`GetKind`) lives behind the seam: each adapter decides
   however it likes. `EcsBoundaryGraph` keeps the existing `NetBoundary`
   `CompositionState` -> `NetworkKind` mapping (do not narrow it - the tool is
   network-agnostic). `ArrayBoundaryGraph` stores the flag and kind per edge directly.

5. **Allocation-free neighbors via count + index.** A* hits a node's neighbors on every
   settled node. Originally specced as a struct enumerator, but that collides with `Entity`
   handles + a non-generic interface + `Core` not referencing game types: a `Core`-defined
   enumerator can't wrap `EcsBoundaryGraph`'s `DynamicBuffer<ConnectedEdge>` (a `Game.Net`
   type), and returning an enumerator interface would box. Since Burst is dropped (Y),
   interface dispatch is free, so the seam exposes `int GetNeighborCount(Entity node)` +
   `Entity GetNeighborAt(Entity node, int index)`. Same zero-allocation outcome, no generics,
   no game type across the seam. (The generic `IBoundaryGraph<TEnum>` route would restore
   enumerator ergonomics but is unnecessary without Burst.)

6. **Synchronous main-thread invocation (option Y2), main-thread creation.** A non-Burst
   `IJob` cannot hold managed fields, so the trace cannot stay "a job." `RingPreview` calls
   `BorderTracer.Trace` directly on dirty frames. District creation records the `Node`
   buffer + `CreationDefinition` straight into the `ToolOutputBarrier` command buffer on the
   main thread; no job, no `NativeList<float3>` conversion. The per-frame definition emit the
   live preview depends on still happens every frame - only where it happens changes, not
   that it happens. `applyMode` still flips to `Apply` only on a valid close.

7. **`BorderTracer` is an instance, not a static class.** It owns the pooled working buffers
   (ring `List<float3>`, A* maps, the heap) and reuses them across rebuilds. `RingPreview`
   holds one; tests new up their own. `TraceResult.Ring` is a view over the tracer's owned
   buffer, valid only until the next `Trace` call - documented as part of the interface.

   ```csharp
   public readonly struct TraceResult
   {
       public IReadOnlyList<float3> Ring { get; }  // empty when !IsValid; valid until next Trace
       public bool IsValid { get; }
   }
   ```

8. **Test surface: through `Trace`, with `[InternalsVisibleTo]` as a scalpel.** The golden
   suite asserts on `TraceResult` from `BorderTracer.Trace`. `Subdivisions.Core` exposes
   `[InternalsVisibleTo("Subdivisions.Tests")]` so the nastiest invariants (path-finder
   used-edges avoidance, `Polygon.ResolveNecks`) can be pinned directly when a ring-level
   assertion is too coarse to localize a regression. Internal tests are the exception, not
   the main suite.

9. **Test graph construction: a fluent builder.** `BoundaryGraphBuilder` (in the test
   project) takes node positions and edges-by-node-id, synthesizes straight beziers
   (control points at 1/3 and 2/3), wires adjacency, and defaults `IsBoundary = true` with a
   per-edge `NetworkKind`. A curved-edge overload supplies an explicit `Bezier4x3` when the
   tessellator needs exercising. `ArrayBoundaryGraph` is the in-memory adapter it builds.

## Architecture

```
Subdivisions.Core            (pure; no Game, no Unity.Collections)
  IBoundaryGraph  ............ the seam
  EdgeEnds, NetworkKind, SnapPoint, TraceResult
  BorderTracer  ............. the one deep entry point  (instance, pooled buffers)
    BoundaryPathFinder  ..... internal: A* over the boundary subgraph
    CurveTessellator  ....... internal: adaptive bezier -> polyline
    Polygon, Geometry  ...... internal: cleanup + segment math (managed List<float2>)
    MinHeap  ................ internal: managed binary heap

Subdivisions                 (the mod)
  EcsBoundaryGraph  ......... adapter: wraps ComponentLookup/BufferLookup + NetBoundary
  RingPreview  ............. calls BorderTracer synchronously on dirty; caches the ring
  SubdivisionsToolSystem  .. raycast, input; records creation on the main thread
  (DistrictBuilder dissolves)

Subdivisions.Tests           (net48, NUnit; no toolchain)
  ArrayBoundaryGraph  ...... in-memory adapter
  BoundaryGraphBuilder  .... fluent construction
  golden + scalpel tests
```

Data flow per dirty frame: cursor snap -> control points -> `RingPreview.Update` ->
`BorderTracer.Trace(points, EcsBoundaryGraph)` -> cached `TraceResult` -> main-thread ECB
records the `CreationDefinition`. On a valid close, `applyMode = Apply` commits that frame's
definition.

## Performance

The trace is dirty-gated (`RingPreview.NeedsRebuild`: control points change, or hover moves
more than 0.5 m onto a different edge), runs over a local working set (edges within ~20 m,
A* capped at `MaxVisited = 4000` / `MaxPathCost`), and so runs a few times per second during
active mouse movement, never on idle frames. Managed main-thread cost is estimated at
~0.1-2 ms per rebuild (Burst would make it ~0.02-0.4 ms); both are acceptable for an
interactive tool. GC is controlled by pooling the tracer's buffers. If a dense-city
measurement ever shows multi-millisecond rebuilds, revisit option Z (struct-generic
dual-instantiation to restore Burst) - not before.

## Testing strategy

Golden cases assert on `TraceResult` via `BorderTracer.Trace`:

- Same-edge arc: two points on one edge trace the sub-curve between them.
- Two points on a loop take the disjoint closing arc (no edge traced twice), enclosing the
  block instead of collapsing to a zero-area sliver.
- Different-kind endpoints connect with a straight line, not a doomed path-find.
- Network <-> area and free points connect straight.
- Neck/sliver rings are resolved or rejected (`ResolveNecks`).
- Self-intersecting result reports `IsValid == false` (apply must never commit it).
- Adaptive tessellation: a curved edge yields a polyline within deviation tolerance.

Scalpel tests (`[InternalsVisibleTo]`): path-finder used-edges avoidance; `Polygon`
cleanup primitives.

In-game verification covers what tests cannot reach: the live preview fill, the apply
commit, and the `CompositionState` -> `NetworkKind` classification in `EcsBoundaryGraph`.

## Migration plan

1. Stand up `Subdivisions.Core`. Move pure `Geometry` and `NetSnap` math and `Polygon` in;
   port `Polygon` off `NativeList<float2>` to `List<float2>`. Define `IBoundaryGraph`,
   `EdgeEnds`, `SnapPoint`, managed `MinHeap`.
2. Port `BoundaryPathFinder`, `CurveTessellator`, and the ring-build logic
   (`Execute`/`TryBuildRing`/`Segment`) into the managed `BorderTracer`, consuming the seam.
3. Add `Subdivisions.Tests` + `BoundaryGraphBuilder` + the golden suite first, so the new
   core is pinned before the live tool is rewired.
4. Wire the mod: `EcsBoundaryGraph` adapter, `RingPreview` calls `BorderTracer`
   synchronously, creation moves to the main thread. `DistrictBuilder` dissolves.
5. In-game verification of preview, apply, and classification.

## Risks

- **No characterization test of the old Burst path.** Behavior preservation rests on the
  golden suite pinning the new core (step 3) plus the existing in-game check (step 5), not on
  an old/new diff. Mitigation: encode the two non-obvious tracing rules as explicit tests.
- **Unity.Entities load in a test process.** RETIRED by spike: a standalone net48
  `dotnet test` process loads `Unity.Entities` and `Colossal.Mathematics` and uses `Entity`
  as a `Dictionary`/`HashSet` key plus `MathUtils.Position` on a `Bezier4x3` with no native
  init crash. Referencing the math DLLs with `Private=true` copies them and their transitive
  Unity closure (15 DLLs) into the test output, so standard probing resolves everything - no
  `AssemblyResolve` shim required. The opaque-int-handle fallback is no longer needed.
- **Main-thread trace hitch in a dense city.** Covered by the Performance fallback above.

## Acceptance criteria

- `dotnet test Subdivisions.Tests` runs green without the game installed running, covering
  every golden case above.
- `Subdivisions.Core` has no reference to `Game` or `Unity.Collections`.
- In-game: snapping, live preview fill, traced borders (including the disjoint closing arc),
  and apply behave as before the refactor.
- No steady-state GC allocation from the trace during continuous cursor movement.

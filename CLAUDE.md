# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Subdivisions is a mod for **Cities: Skylines II** (CS2). It adds a network-following district
tool: the user drops boundary control points, points snap to networks and existing area borders,
and the border traced between two snapped points hugs the network instead of cutting straight
across. The mod has two halves — a C# side that runs inside the game, and a React/TypeScript UI
module that adds the toolbar button. The C# side is itself split: a pure `Subdivisions.Core`
library holds the border-tracing algorithms behind an `IBoundaryGraph` seam (unit-tested out of
game), and the `Subdivisions` mod adapts that core to the game's ECS.

The tool is **network-agnostic, not roads-only**: it snaps to and traces along any
non-infrastructure network (roads, rail/tracks, pedestrian paths, surface) plus existing district
and map-tile borders. Don't "fix" tracing edge cases by excluding a network type (e.g. tracks)
from `NetBoundary.IsBoundary` — infrastructure (power/water) is already excluded because it lacks
road/track/pedestrian/surface lane flags.

## Build & deploy

Three C# projects plus the UI module:
- **`Subdivisions.Core`** — pure border-tracing library. References only `Unity.Mathematics`,
  `Colossal.Mathematics`, `Unity.Entities`; no `Game` or `Unity.Collections`, so re-coupling the
  trace to ECS is a build error. Resolves the Managed folder itself, deploys nothing.
- **`Subdivisions`** — the mod. References `Core` (`ProjectReference`) and the game DLLs via the
  toolchain.
- **`Subdivisions.Tests`** — net48 NUnit suite for `Core`, runs without the game.

The mod relies on the CS2 modding toolchain, located via the user environment variable
`CSII_TOOLPATH` and imported as `Mod.props`/`Mod.targets` in the `.csproj`. Game DLLs (`Game`,
`Colossal.*`, `Unity.*`) come from the installed game's `Managed` folder (resolved by `Mod.props`
for the mod; by a `CSII_MANAGEDPATH`/Steam/Xbox fallback in `Core` and `Tests`), not from NuGet.

```bash
# Build + deploy the mod to the local mods folder (Debug). Builds Subdivisions.Core first via
# ProjectReference; the toolchain's DeployWIP copies both DLLs, and an AfterTargets hook rebuilds
# the UI bundle next to the .dll.
dotnet build Subdivisions/Subdivisions.csproj

# Release build (used for publishing to PDX Mods)
dotnet build Subdivisions/Subdivisions.csproj -c Release

# Run the tracing-core unit tests (no game required)
dotnet test Subdivisions.Tests/Subdivisions.Tests.csproj
```

### UI module

```bash
cd Subdivisions/UI/subdivisions
npm install      # one-time; the MSBuild UI hook is skipped if node_modules is absent
npm run build    # webpack build -> bundle the C# DeployWIP step copies into place
npm run dev      # webpack --watch
```

Important: the toolchain's `DeployWIP` wipes the deploy folder and copies only the C# artifacts,
deleting the webpack UI bundle. The `BuildSubdivisionsUI` target in the `.csproj` re-runs
`npm run build` after `DeployWIP` so the `.mjs` always lands next to the `.dll`. This only fires
when `node_modules` exists, so run `npm install` once.

`Subdivisions.Core` has a `dotnet test` suite (the border tracer, path-finder, polygon cleanup).
The in-game-only parts — snapping against live ECS, the live preview fill, apply, and the
`CompositionState`→`NetworkKind` classification inside `EcsBoundaryGraph` — are still verified by
running the mod.

## Architecture

### Entry point & lifecycle

`Mod.cs` (`IMod`) is the entry point. `OnLoad` registers settings + localization and schedules
the two ECS systems into the game's update loop:
- `SubdivisionsToolSystem` at `SystemUpdatePhase.ToolUpdate`
- `SubdivisionsUISystem` at `SystemUpdatePhase.UIUpdate`

Bump `Mod.Version` when releasing; `Properties/PublishConfiguration.xml` holds PDX Mods metadata.

### The tool system (`Systems/SubdivisionsToolSystem.cs`)

This is the heart of the mod, a `ToolBaseSystem`. It owns only the **tool lifecycle, raycast,
and input dispatch**. Everything else is delegated to collaborator objects constructed in
`OnCreate` (via the private `CreateCollaborators` helper) and held as fields:

- **`RoadNetwork`** — wraps the net `SearchSystem` and ECS component lookups (`Edge`, `Curve`,
  `ConnectedEdge`, `Composition`, …). Refreshed each frame; collects boundary-qualified edges
  near the cursor and builds a `NetBoundary` helper.
- **`AreaIndex`** — analogous wrapper for existing areas/districts (lets the tool snap to other
  district borders).
- **`CursorSnapper`** — given a raycast hit, returns a `SnapPoint`: snaps to a road/area vertex
  first (within `NodeSnapDistance`), else projects onto the nearest road curve or area segment
  (within `SnapDistance`), else returns the free hit position. It only orchestrates: candidate
  generation is delegated to `NetSnapSource` and `AreaSnapSource`, each feeding a shared
  `SnapAccumulator` (nearest-so-far tracker), so the priority is vertices-across-both-sources
  first, then edges.
- **`ControlPointRing`** — owns the persistent list of dropped control points and the
  close-detection geometry (`CanClose`/`IsNearStart`).
- **`PreviewRenderer`** — draws the in-progress ring + hover point via `OverlayRenderSystem`.
- **`EcsBoundaryGraph`** — the production `IBoundaryGraph` adapter (`Subdivisions.Core`). Serves
  the tracer edge endpoints, curves, neighbors, and the `IsBoundary`/`GetKind` classification from
  `RoadNetwork`'s lookups + `NetBoundary`. Refreshed each frame.
- **`RingPreview`** — owns the cached traced ring + validity and the rebuild lifecycle
  (`NeedsRebuild`, dirty flags). On a dirty frame it calls `BorderTracer.Trace` (synchronous, main
  thread), copies the ring out, and every frame re-emits it as a `CreationDefinition`.

Input model (in `OnUpdate`): `ToolInputReader` maps the frame's raw input to one
`ToolEditAction`. Left-click adds a control point; left-click near the first point (within
`CloseRadius`, needs ≥3 points) closes the ring and applies; right-click removes the last point.
Apply is gated on `GetAllowApply()` **and** the cached ring's validity (`RingPreview.IsValid`,
from `TraceResult.IsValid`) so degenerate/self-intersecting shapes are never committed.

**Live preview + apply mechanism (do not "optimize" away the per-frame creation).** Every frame
with ≥3 points the tool emits a `CreationDefinition` from the cached ring; the game tessellates
that into the live filled-district preview. `applyMode` governs the outcome: `ApplyMode.Clear`
(default) discards the temp geometry each frame; on a valid close, `applyMode` flips to
`ApplyMode.Apply` and that frame's definition commits. Emitting the definition every frame is
**required** for the preview — it is not an entity leak, and removing it both kills the live fill
and breaks creation.

What *is* cached is the expensive part: `BorderTracer.Trace` (graph trace + path-find) only re-runs
when the control points or snapped hover actually change (`NeedsRebuild`); on unchanged frames the
cached `_ring` is cheaply re-emitted. The trace is synchronous managed code on the main thread (not
a Burst job) — dirty-gated and local, so its cost is negligible; `RingPreview` pools its buffers to
keep steady-state GC at zero. Creation is recorded straight into the `ToolOutputBarrier` command
buffer on the main thread (no job).

### The tracing core (`Subdivisions.Core`)

The border-tracing logic lives in the pure `Subdivisions.Core` library, consumed through the
**`IBoundaryGraph`** seam (`GetEndpoints`/`GetCurve`/`IsBoundary`/`GetKind` + allocation-free
neighbors via `GetNeighborCount`/`GetNeighborAt`). Two adapters satisfy it: `EcsBoundaryGraph`
(production, in the mod) and `ArrayBoundaryGraph` (tests, built with `BoundaryGraphBuilder`). The
core never sees `Game` types or ECS lookups, which is what makes it testable out of game.

- **`BorderTracer`** — the one deep entry point. `Trace(points, graph) → TraceResult { Ring,
  IsValid }` turns ordered `SnapPoint` control points into a closed `float3` ring, valid when it is
  ≥3 verts and simple. An instance owns pooled buffers and reuses them across rebuilds; `Ring` is a
  view valid only until the next `Trace`. `Execute`/`TryBuildRing`/`Segment` + polygon cleanup is
  orchestration; the heavy lifting is `BoundaryPathFinder` (A* over the boundary subgraph via a
  managed `MinHeap`), `CurveTessellator` (adaptive bezier→ring flattening), and `Polygon`/`Geometry`
  (cleanup, segment intersection, perpendicular distance). All managed (`List`/`Dictionary`/
  `HashSet`), so the suite runs in a plain `dotnet test` process.

Two non-obvious tracing rules in `BorderTracer.Segment` (both are deliberate UX fixes — don't
regress them; each has an explicit test):
- **Same-kind-only tracing.** A segment is traced along the network *only* when both endpoints
  are on the same network kind (`GetKind`: Road/Track/Pedestrian/Surface). Different kinds,
  network↔area, or free points connect with a **straight line** instead. Path-finding only works
  within one connected network, so a rail→road segment must not attempt a (doomed) trace — that
  produced self-intersecting rings.
- **No edge traced twice (disjoint closing arc).** A per-ring `usedEdges` set makes each segment's
  path-find avoid edges already claimed by earlier segments. This is what lets two points on the
  same road loop enclose the block between them: the closing border is forced onto the *other*
  arc instead of retracing the first and collapsing to a zero-area sliver.

The classification itself (`CompositionState` → `NetworkKind`, the network-agnostic rule) stays in
the mod's `NetBoundary` — it reads a `Game.dll` enum the core can't see — and `EcsBoundaryGraph`
hands the decided `NetworkKind` across the seam. `NetBoundary` and the `Nearby*Collector` structs
remain Burst-friendly mod-side helpers used by snapping/collection. See `docs/prd-tracing-core-seam.md`
for the full design and `CONTEXT.md` for the vocabulary.

### District creation recipe (`Domain/AreaDefinitionCreation.cs`)

A CS2 district *is* an Area. To create one you emit, on a new entity: a closed `Node` dynamic
buffer (last node == first), a `CreationDefinition` pointing at the `DistrictPrefab` entity, and
an `Updated` tag. The game's area-generation systems then tessellate and commit on apply. This
mirrors the recipe used by the AreaBucket mod. `RingPreview` records this directly into the
`ToolOutputBarrier` command buffer on the main thread (it takes an `IReadOnlyList<float3>` ring).

### UI bridge

`Systems/SubdivisionsUISystem.cs` (`UISystemBase`) bridges React and the tool. It exposes a
`subdivisions.active` getter binding and a `subdivisions.toggle` trigger. `Toggle` activates the
tool with the first available `DistrictPrefab`, or switches back to the default tool. It tracks
active state by subscribing to the vanilla `ToolSystem.EventToolChanged`.

The React side (`UI/subdivisions/src/`) appends `SubdivisionsButton` to the `GameTopLeft`
toolbar slot via `ModRegistrar`. The `types/*.d.ts` are the game's UI API typings (gitignored
`*.d.ts` rule does not apply to these committed type stubs).

## Research policy & reference mods

CS2's modding API is sparsely documented and the game DLLs are large. **Before reverse-engineering
game assemblies, research the question first** — in this order:

1. The official CS2 modding wiki / web search for the API, component, or pattern.
2. The reference mods below — open-source mods that already solve adjacent problems. Read how
   they call the API rather than guessing from decompiled `Game.dll`.
3. Only then fall back to inspecting the game DLLs directly.

Reference mods (use them as exemplars for mod structure, ECS/tool patterns, and API usage):

- **[Network Tools](https://github.com/lucarager/CS2-NetworkTools)** — net edge/node manipulation, snapping, and working with the road graph
  (`Edge`, `Curve`, `ConnectedEdge`, compositions). Closest analog to this mod's road-following
  border tracing.
- **[Anarchy](https://github.com/yenyang/Anarchy)** — `ToolBaseSystem` lifecycle, custom tool registration, raycasting, apply/validation
  gating, and the React UI bridge (`UISystemBase` bindings, toolbar buttons).
- **[Area Bucket](https://github.com/Cmyna/AreaBucket)** — area/district creation from a polygon. This mod's `Domain/AreaDefinitionCreation.cs`
  already mirrors its recipe (`Node` buffer + `CreationDefinition` + `Updated`); consult it for
  area generation, tessellation, and `AreaTypeMask` details.
- **[ExtraLib](https://github.com/AlphaGaming7780/ExtraLib)** — shared dependency-library mod
  (C# + TypeScript). Reference for cross-mod infrastructure patterns: shared utilities, UI
  components/SCSS, and localization scaffolding rather than a specific gameplay feature.

## Conventions

- Localization strings live in `BasicLocale.cs` (an `IDictionarySource`).
- The **mod** uses Unity DOTS/ECS heavily: `NativeList`/`NativeArray` with explicit `Allocator`
  and disposal, `ComponentLookup`/`BufferLookup` refreshed each frame. Mind allocator lifetimes.
  **`Subdivisions.Core`** is the opposite — pure managed (`List`/`Dictionary`/`HashSet`), no
  `Native*` containers (they P/Invoke into Unity's native allocator, which isn't loaded in a
  standalone test process), no Burst.
- C# language version is 9.0, target framework `net48` across all three projects. `net48` lacks
  `System.Index`, so use `list[list.Count - 1]` not `list[^1]` in `Core` (the mod gets a Unity
  polyfill; `Core` does not).

# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Subdivisions is a mod for **Cities: Skylines II** (CS2). It adds a network-following district
tool: the user drops control points that snap to networks and existing area borders, and the
border traced between two snapped points hugs the network instead of cutting straight across.
There are two halves - a C# side that runs in the game and a React/TypeScript UI module for the
toolbar button. The C# side splits further: a pure `Subdivisions.Core` library holds the tracing
algorithms behind an `IBoundaryGraph` seam (unit-tested out of game), and the `Subdivisions` mod
adapts it to the game's ECS.

The tool is **network-agnostic**: it snaps to and traces along any
non-infrastructure network (roads, rail/tracks, pedestrian paths, surface) plus existing district
and map-tile borders. Don't "fix" tracing edge cases by excluding a network type (e.g. tracks)
from `NetBoundary.IsBoundary` - infrastructure (power/water) is already excluded for lacking
road/track/pedestrian/surface lane flags.

`CONTEXT.md` is the domain glossary - the ubiquitous language (border, control point, snap,
boundary subgraph, the seams) used throughout the code and these docs. Read it first.

## Build & deploy

Three C# projects plus the UI module:
- **`Subdivisions.Core`** - pure tracing library. References only `Unity.Mathematics`,
  `Colossal.Mathematics`, `Unity.Entities` (no `Game` or `Unity.Collections`), so re-coupling the
  trace to ECS is a build error.
- **`Subdivisions`** - the mod. References `Core` and the game DLLs via the toolchain.
- **`Subdivisions.Tests`** - net48 NUnit suite for `Core`, runs without the game.

The mod needs the CS2 modding toolchain (`CSII_TOOLPATH`, imported as `Mod.props`/`Mod.targets`).
Game DLLs come from the installed game's `Managed` folder (resolved by `Mod.props` for the mod; by
a `CSII_MANAGEDPATH`/Steam/Xbox fallback in `Core` and `Tests`), not NuGet.

```bash
# Build + deploy the mod (Debug). Builds Core first; DeployWIP copies both DLLs and an
# AfterTargets hook rebuilds the UI bundle next to the .dll.
dotnet build Subdivisions/Subdivisions.csproj

# Release build (for publishing to PDX Mods)
dotnet build Subdivisions/Subdivisions.csproj -c Release

# Run the Core unit tests (no game required)
dotnet test Subdivisions.Tests/Subdivisions.Tests.csproj
```

### UI module

```bash
cd Subdivisions/UI/subdivisions
npm install      # one-time; the MSBuild UI hook is skipped if node_modules is absent
npm run build    # webpack build
npm run dev      # webpack --watch
```

`DeployWIP` wipes the deploy folder and copies only the C# artifacts, deleting the webpack bundle.
The `BuildSubdivisionsUI` target re-runs `npm run build` after `DeployWIP` so the `.mjs` lands next
to the `.dll` - but only when `node_modules` exists, so run `npm install` once.

The `dotnet test` suite covers the border tracer, path-finder, and polygon cleanup. In-game-only
parts (snapping against live ECS, the preview fill, apply, the `CompositionState`->`NetworkKind`
classification in `EcsBoundaryGraph`) are verified by running the mod.

## Architecture

### Entry point & lifecycle

`Mod.cs` (`IMod`) is the entry point. `OnLoad` registers settings + localization and schedules two
ECS systems:
- `SubdivisionsToolSystem` at `SystemUpdatePhase.ToolUpdate`
- `SubdivisionsUISystem` at `SystemUpdatePhase.UIUpdate`

When releasing, bump the version in all four places by hand (deriving them from one source via
MSBuild was rejected as too fragile):
- `Mod.cs` `Version` const
- `Subdivisions.csproj` `<Version>`
- `Properties/PublishConfiguration.xml` `ModVersion` (changelog lives here)
- `UI/subdivisions/mod.json` `version`

### The tool system (`Systems/SubdivisionsToolSystem.cs`)

The heart of the mod, a `ToolBaseSystem`. It owns only the **tool lifecycle, raycast, and input
dispatch**; everything else is delegated to collaborators constructed in `OnCreate` and held as
fields:

- **`RoadNetwork`** - wraps the net `SearchSystem` and ECS lookups (`Edge`, `Curve`,
  `ConnectedEdge`, `Composition`, ...). Refreshed each frame; collects boundary-qualified edges
  near the cursor and builds a `NetBoundary`.
- **`AreaIndex`** - the same for existing areas/districts (snap to other district borders).
- **`CursorSnapper`** - turns a raycast hit into a `SnapPoint`: a road/area vertex first (within
  `NodeSnapDistance`), else the nearest road curve or area segment (within `SnapDistance`), else
  the free hit. It orchestrates only; candidates come from `NetSnapSource` and `AreaSnapSource`
  feeding a shared `SnapAccumulator`, so priority is vertices first, then edges.
- **`ControlPointRing`** - the dropped control points and close-detection geometry
  (`CanClose`/`IsNearStart`).
- **`PreviewRenderer`** - draws the in-progress ring + hover point via `OverlayRenderSystem`.
- **`EcsBoundaryGraph`** - the production `IBoundaryGraph` adapter. Serves the tracer endpoints,
  curves, neighbors, and `IsBoundary`/`GetKind` from `RoadNetwork` + `NetBoundary`. Refreshed each
  frame.
- **`RingPreview`** - owns the cached traced ring + validity and the rebuild lifecycle. On a dirty
  frame it calls `BorderTracer.Trace`, copies the ring out, and every frame re-emits it as a
  `CreationDefinition`.

Input (in `OnUpdate`): `ToolInputReader` maps the frame's raw input to one `ToolEditAction`.
Left-click adds a point; left-click near the first point (within `CloseRadius`, needs >=3 points)
closes and applies; right-click removes the last point. Apply is gated on `GetAllowApply()` **and**
`RingPreview.IsValid` so degenerate/self-intersecting shapes are never committed.

**Live preview + apply (do not "optimize" away the per-frame creation).** Every frame with >=3
points the tool emits a `CreationDefinition` from the cached ring; the game tessellates it into the
live filled preview. `ApplyMode.Clear` (default) discards the temp geometry each frame; on a valid
close, `applyMode` flips to `ApplyMode.Apply` and that frame commits. Emitting every frame is
required for the preview - removing it kills the fill and breaks creation. What *is* cached is the
expensive part: `BorderTracer.Trace` re-runs only when control points or the snapped hover change
(`NeedsRebuild`); otherwise the cached `_ring` is cheaply re-emitted. The trace is synchronous
managed code on the main thread (dirty-gated and local, so cheap); `RingPreview` pools buffers to
keep steady-state GC at zero.

### The tracing core (`Subdivisions.Core`)

The tracing logic lives in pure `Subdivisions.Core`, consumed through the **`IBoundaryGraph`** seam
(`GetEndpoints`/`GetCurve`/`IsBoundary`/`GetKind` + allocation-free neighbors via
`GetNeighborCount`/`GetNeighborAt`). Two adapters satisfy it: `EcsBoundaryGraph` (production) and
`ArrayBoundaryGraph` (tests, built with `BoundaryGraphBuilder`). The core never sees `Game` types
or ECS lookups - that's what makes it testable out of game.

`BorderTracer` is the one deep entry point: `Trace(points, graph) -> TraceResult { Ring, IsValid }`
turns ordered `SnapPoint`s into a closed `float3` ring, valid when >=3 verts and simple. It pools
buffers across rebuilds; `Ring` is a view valid only until the next `Trace`. The orchestration
(`TryBuildRing`/`Segment` + cleanup) is thin; the heavy lifting is `BoundaryPathFinder` (A* over the
boundary subgraph via a managed `MinHeap`), `CurveTessellator` (bezier->ring flattening), and
`Polygon` (cleanup). Geometry primitives come from the game's `Colossal.Mathematics.MathUtils` -
note `MathUtils.Intersect` treats a touching/collinear contact as an intersection, so
`Polygon.IsSimple` rejects degenerate self-touching rings (stricter than proper-crossing).

Two non-obvious rules in `BorderTracer.Segment` (deliberate UX fixes, each with a test - don't
regress):
- **Same-kind-only tracing.** A segment traces along the network only when both endpoints share a
  network kind (`GetKind`: Road/Track/Pedestrian/Surface). Different kinds, network<->area, or free
  points connect with a **straight line**. Path-finding only works within one connected network, so
  a rail->road trace is doomed and produced self-intersecting rings.
- **No edge traced twice.** A per-ring `usedEdges` set makes each segment avoid edges already
  claimed. This lets two points on the same road loop enclose the block between them: the closing
  border is forced onto the *other* arc instead of retracing the first and collapsing to a sliver.

The classification (`CompositionState`->`NetworkKind`) stays in the mod's `NetBoundary` (it reads a
`Game.dll` enum the core can't see); `EcsBoundaryGraph` hands the decided `NetworkKind` across the
seam. See `CONTEXT.md` for the vocabulary.

### District creation recipe (`Domain/AreaDefinitionCreation.cs`)

A CS2 district *is* an Area. To create one, emit on a new entity: a closed `Node` buffer (last ==
first), a `CreationDefinition` pointing at the `DistrictPrefab`, and an `Updated` tag. The game's
area systems tessellate and commit on apply. `RingPreview` records this straight into the
`ToolOutputBarrier` command buffer (it takes an `IReadOnlyList<float3>` ring).

### UI bridge

`Systems/SubdivisionsUISystem.cs` (`UISystemBase`) bridges React and the tool, exposing a
`subdivisions.active` getter and a `subdivisions.toggle` trigger. `Toggle` activates the tool with
the first available `DistrictPrefab` or switches back to the default tool, tracking state via
`ToolSystem.EventToolChanged`. The React side (`UI/subdivisions/src/`) appends `SubdivisionsButton`
to the `GameTopLeft` toolbar slot via `ModRegistrar`. The committed `types/*.d.ts` are the game's
UI typings (exempt from the gitignored `*.d.ts` rule).

## Research policy & reference mods

CS2's modding API is sparsely documented. Before reverse-engineering game assemblies, research in
order: (1) the official CS2 modding wiki / web search; (2) the reference mods below; (3) only then
the game DLLs.

- **[Network Tools](https://github.com/lucarager/CS2-NetworkTools)** - net edge/node manipulation,
  snapping, the road graph (`Edge`, `Curve`, `ConnectedEdge`, compositions). Closest analog to this
  mod's border tracing.
- **[Anarchy](https://github.com/yenyang/Anarchy)** - `ToolBaseSystem` lifecycle, tool
  registration, raycasting, apply/validation gating, the React UI bridge.
- **[ExtraLib](https://github.com/AlphaGaming7780/ExtraLib)** - shared dependency-library mod
  (C# + TypeScript). Cross-mod infrastructure patterns: shared utilities, UI/SCSS, localization
  scaffolding.

## Conventions

- Localization strings live in `BasicLocale.cs` (an `IDictionarySource`).
- Tests (`Subdivisions.Tests`, NUnit) are **Detroit/classicist**: exercise the real `BorderTracer`
  through real collaborators and the in-memory `ArrayBoundaryGraph` fake (via `BoundaryGraphBuilder`)
  - no mocking. Assert on **state**, never on calls. Use **AwesomeAssertions** (`value.Should()...`)
  and **AutoBogus** `AutoFaker`. Every test must confirm a behavior, not just exercise a library.
  Name tests `Method_Scenario_Expected`. AutoBogus is the net48-compatible faker;
  `Soenneker.Utils.AutoBogus` is modern-.NET only.
- The **mod** uses Unity DOTS/ECS: `NativeList`/`NativeArray` with explicit `Allocator` and
  disposal, `ComponentLookup`/`BufferLookup` refreshed each frame - mind allocator lifetimes.
  **`Subdivisions.Core`** is the opposite: pure managed (`List`/`Dictionary`/`HashSet`), no
  `Native*`, no Burst.
- C# 9.0, `net48` across all projects. `net48` lacks `System.Index`, so use `list[list.Count - 1]`
  not `list[^1]` in `Core` (the mod gets a Unity polyfill; `Core` doesn't).

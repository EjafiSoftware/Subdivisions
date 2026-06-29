# Domain glossary

Ubiquitous language for Subdivisions. Architecture vocabulary (module, interface, depth,
seam, adapter, leverage, locality) comes from the `/codebase-design` skill; this file names
the domain.

## Tracing

- **Border** - the closed ring traced between the user's control points; hugs networks and
  existing area borders instead of cutting straight across.
- **Control point** - a boundary point the user drops. Snaps to a network/area vertex, a
  network curve, an area segment, or stays free. See `SnapPoint`.
- **Snap** - resolving a raycast hit to the nearest vertex (first) or edge projection
  (second), else the free hit. See the snapping seam below.
- **Free point** - a control point that snapped to nothing; it sits on arbitrary terrain
  (`OnNet` and `OnArea` both false).
- **Snapped point** - a control point on a network edge (`OnNet`) or an existing area border
  (`OnArea`).
- **Boundary subgraph** - the network edges that qualify as borders (non-infrastructure:
  road/track/pedestrian/surface), the graph the path-finder walks.
- **Same-kind-only tracing** - a segment is traced along the network only when both
  endpoints sit on the same `NetworkKind`; otherwise the endpoints connect with a straight
  line. Cross-kind, network-to-area, and free points never attempt a (doomed) path-find.
- **Disjoint closing arc** - no edge is traced twice within one ring; the closing segment is
  forced onto the other arc so two points on one loop enclose the block between them rather
  than collapsing to a sliver.

## The tracing core seam

- **`IBoundaryGraph`** - the seam the tracer consumes: endpoints, curve, neighbors, and the
  `IsBoundary`/`GetKind` classification of an edge. Classification lives behind it.
- **`BorderTracer`** - the one deep entry point. `Trace(points, graph) -> TraceResult`.
  Owns pooled buffers; path-finder, tessellator, and polygon cleanup are internal.
- **`EcsBoundaryGraph`** - production adapter wrapping the game's ECS lookups and the
  `CompositionState` -> `NetworkKind` mapping.
- **`ArrayBoundaryGraph`** - in-memory adapter for tests, built via `BoundaryGraphBuilder`.
- **`NetworkKind`** - Road, Track, Pedestrian, Surface, or None. The classification the core
  consumes instead of game composition enums.

## The snapping seam

- **`ISnapSource`** - the seam the cursor snapper consumes: a source feeds its candidates -
  edge endpoints (vertices) and nearest points on edges - into a shared accumulator. How
  candidates are collected (ECS lookups, native buffers) stays behind the adapter.
- **`NetSnapSource` / `AreaSnapSource`** - production adapters over the road network and the
  area index. `FakeSnapSource` is the in-memory adapter for tests.
- **`SnapResolver`** - the pure entry point. `Resolve(sources, query) -> SnapPoint`: collects
  vertices across all sources first, returns the winner if any, else collects edges. Vertices
  always beat edges; nearest wins within each pass.
- **`SnapAccumulator`** - nearest-so-far tracker, order-independent. Keeps the nearest
  candidate of any kind and the nearest matching the preference separately, so a coincident
  pair resolves the same regardless of arrival order.
- **Coincidence radius** - the slop within which two candidates count as the same spot; the
  preferred kind wins only when it sits this close to the nearest.
- **`SnapPreference`** - which kind (Net or Area) wins a coincident pair. The default comes
  from settings; **continuity** overrides it - if the previous control point was on an area
  (not a network), this point prefers Area too, so a border drawn along a district edge does
  not flip onto a network that happens to be coincident.
- **`SnapQuery` / `SnapSettings`** - one snap request (cursor hit, previous point, settings)
  and the stable configuration (snap radii, coincidence radius, default preference).

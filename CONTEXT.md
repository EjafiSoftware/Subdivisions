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
  (second), else the free hit.
- **Boundary subgraph** - the network edges that qualify as borders (non-infrastructure:
  road/track/pedestrian/surface), the graph the path-finder walks.
- **Same-kind-only tracing** - a segment is traced along the network only when both
  endpoints sit on the same `NetworkKind`; otherwise the endpoints connect with a straight
  line. Cross-kind, network-to-area, and free points never attempt a (doomed) path-find.
- **Disjoint closing arc** - no edge is traced twice within one ring; the closing segment is
  forced onto the other arc so two points on one loop enclose the block between them rather
  than collapsing to a sliver.

## The tracing core seam (PRD: pure tracing core)

- **`IBoundaryGraph`** - the seam the tracer consumes: endpoints, curve, neighbors, and the
  `IsBoundary`/`GetKind` classification of an edge. Classification lives behind it.
- **`BorderTracer`** - the one deep entry point. `Trace(points, graph) -> TraceResult`.
  Owns pooled buffers; path-finder, tessellator, and polygon cleanup are internal.
- **`EcsBoundaryGraph`** - production adapter wrapping the game's ECS lookups and the
  `CompositionState` -> `NetworkKind` mapping.
- **`ArrayBoundaryGraph`** - in-memory adapter for tests, built via `BoundaryGraphBuilder`.
- **`NetworkKind`** - Road, Track, Pedestrian, Surface, or None. The classification the core
  consumes instead of game composition enums.

using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Subdivisions.Systems.SubdivisionsToolJobs;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Road-following district tool. The user drops boundary control points; a point dropped on a
    /// road snaps to it, and the border between two snapped points is traced along the road graph so
    /// it hugs the network instead of cutting straight across. Left-click adds a point (or closes the
    /// ring when clicked near the first point), right-click removes the last point.
    ///
    /// The system itself only owns the tool lifecycle, input, and the control-point list; snapping,
    /// preview rendering, and district building are delegated to dedicated collaborators.
    /// </summary>
    public partial class SubdivisionsToolSystem : ToolBaseSystem
    {
        private const string ToolId = "Subdivisions";

        private const float CloseRadius = 18f;

        private const float HoverRebuildEpsilon = 0.5f;

        private DistrictPrefab _selectedPrefab;

        private RoadNetwork _roads;
        private AreaIndex _areas;
        private CursorSnapper _snapper;
        private PreviewRenderer _renderer;
        private DistrictBuilder _builder;

        private NativeList<SnapPoint> _controlPoints;

        private NativeList<float3> _ring;
        private NativeReference<bool> _ringValid;
        private SnapPoint _lastBuiltHover;
        private bool _lastIncludeHover;
        private bool _hasBuild;
        private bool _pointsDirty;
        private JobHandle _previewHandle;

        public override string toolID => ToolId;

        protected override void OnCreate()
        {
            base.OnCreate();

            var barrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            var prefabs = World.GetOrCreateSystemManaged<PrefabSystem>();
            var netSearch = World.GetOrCreateSystemManaged<SearchSystem>();
            var areaSearch = World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>();
            var overlay = World.GetOrCreateSystemManaged<OverlayRenderSystem>();

            _roads = new RoadNetwork(
                netSearch,
                GetComponentLookup<Edge>(true),
                GetComponentLookup<Curve>(true),
                GetBufferLookup<ConnectedEdge>(true),
                GetComponentLookup<Owner>(true),
                GetComponentLookup<Deleted>(true),
                GetComponentLookup<Composition>(true),
                GetComponentLookup<NetCompositionData>(true));
            _areas = new AreaIndex(
                areaSearch,
                GetBufferLookup<Game.Areas.Node>(true),
                GetComponentLookup<Game.Areas.District>(true),
                GetComponentLookup<Game.Areas.MapTile>(true),
                GetComponentLookup<Temp>(true),
                GetComponentLookup<Deleted>(true));
            _snapper = new CursorSnapper(_roads, _areas);
            _renderer = new PreviewRenderer(overlay);
            _builder = new DistrictBuilder(barrier, prefabs, _roads);

            _controlPoints = new NativeList<SnapPoint>(Allocator.Persistent);
            _ring = new NativeList<float3>(Allocator.Persistent);
            _ringValid = new NativeReference<bool>(Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            _previewHandle.Complete();
            if (_controlPoints.IsCreated)
            {
                _controlPoints.Dispose();
            }
            if (_ring.IsCreated)
            {
                _ring.Dispose();
            }
            if (_ringValid.IsCreated)
            {
                _ringValid.Dispose();
            }
            base.OnDestroy();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            applyAction.shouldBeEnabled = true;
            secondaryApplyAction.shouldBeEnabled = true;
            requireAreas = Game.Areas.AreaTypeMask.Districts;
            _controlPoints.Clear();
            ResetBuildCache();
        }

        protected override void OnStopRunning()
        {
            applyAction.shouldBeEnabled = false;
            secondaryApplyAction.shouldBeEnabled = false;
            requireAreas = Game.Areas.AreaTypeMask.None;
            _previewHandle.Complete();
            _controlPoints.Clear();
            ResetBuildCache();
            base.OnStopRunning();
        }

        public override PrefabBase GetPrefab() => _selectedPrefab;

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            if (prefab is DistrictPrefab districtPrefab)
            {
                _selectedPrefab = districtPrefab;
                return true;
            }
            return false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Terrain;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!GetRaycastResult(out var raycastPoint) || _selectedPrefab is null)
            {
                applyMode = ApplyMode.Clear;
                return inputDeps;
            }

            var hit = raycastPoint.m_HitPosition;
            _roads.Refresh(this);
            _areas.Refresh(this);
            var hover = _snapper.Snap(hit);

            var canClose = _controlPoints.Length >= 3 && IsNearStart(hover._position);
            _renderer.Draw(_controlPoints, hover, canClose);

            applyMode = ApplyMode.Clear;

            var tryClose = false;
            if (secondaryApplyAction.WasPressedThisFrame())
            {
                RemoveLastPoint();
            }
            else if (applyAction.WasPressedThisFrame())
            {
                if (canClose)
                {
                    tryClose = true;
                }
                else
                {
                    _controlPoints.Add(hover);
                    _pointsDirty = true;
                }
            }

            var handle = UpdatePreview(hover, !canClose, inputDeps);

            if (tryClose)
            {
                handle.Complete();
                if (GetAllowApply() && _ringValid.Value)
                {
                    applyMode = ApplyMode.Apply;
                    _controlPoints.Clear();
                    ResetBuildCache();
                }
            }

            _previewHandle = handle;
            return handle;
        }

        private JobHandle UpdatePreview(SnapPoint hover, bool includeHover, JobHandle inputDeps)
        {
            if (_controlPoints.Length + (includeHover ? 1 : 0) < 3)
            {
                _hasBuild = false;
                return inputDeps;
            }

            var handle = inputDeps;
            if (NeedsRebuild(hover, includeHover))
            {
                handle = ScheduleBuild(hover, includeHover, inputDeps);
                _lastBuiltHover = hover;
                _lastIncludeHover = includeHover;
                _hasBuild = true;
                _pointsDirty = false;
            }

            return _builder.CreateDistrict(_ring, _selectedPrefab, handle);
        }

        private JobHandle ScheduleBuild(SnapPoint hover, bool includeHover, JobHandle inputDeps)
        {
            var points = new NativeList<SnapPoint>(Allocator.TempJob);
            foreach (var point in _controlPoints)
            {
                points.Add(point);
            }
            if (includeHover)
            {
                points.Add(hover);
            }

            var handle = _builder.BuildRing(points, _ring, _ringValid, inputDeps);
            points.Dispose(handle);
            return handle;
        }

        private bool NeedsRebuild(SnapPoint hover, bool includeHover)
        {
            if (!_hasBuild || _pointsDirty || includeHover != _lastIncludeHover)
            {
                return true;
            }
            if (!includeHover)
            {
                return false;
            }
            return hover._edge != _lastBuiltHover._edge
                || math.distance(hover._position.xz, _lastBuiltHover._position.xz) > HoverRebuildEpsilon;
        }

        private void ResetBuildCache()
        {
            _hasBuild = false;
            _pointsDirty = true;
        }

        private void RemoveLastPoint()
        {
            if (_controlPoints.Length > 0)
            {
                _controlPoints.RemoveAt(_controlPoints.Length - 1);
                _pointsDirty = true;
            }
        }

        private bool IsNearStart(float3 position)
        {
            return _controlPoints.Length > 0
                && math.distance(position.xz, _controlPoints[0]._position.xz) < CloseRadius;
        }
    }
}

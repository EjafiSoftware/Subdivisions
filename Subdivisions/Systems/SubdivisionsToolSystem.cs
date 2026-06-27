using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using Unity.Entities;
using Unity.Jobs;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Road-following district tool. The user drops boundary control points; a point dropped on a
    /// road snaps to it, and the border between two snapped points is traced along the road graph so
    /// it hugs the network instead of cutting straight across. Left-click adds a point (or closes the
    /// ring when clicked near the first point), right-click removes the last point.
    ///
    /// The system owns only the tool lifecycle, raycast, and input; snapping, control-point storage,
    /// preview/build caching, and district creation are delegated to dedicated collaborators.
    /// </summary>
    public partial class SubdivisionsToolSystem : ToolBaseSystem
    {
        private const string ToolId = "Subdivisions";

        private const float CloseRadius = 18f;

        private DistrictPrefab _selectedPrefab;

        private RoadNetwork _roads;
        private AreaIndex _areas;
        private CursorSnapper _snapper;
        private PreviewRenderer _renderer;
        private DistrictBuilder _builder;

        private ControlPointRing _points;
        private RingPreview _preview;
        private JobHandle _previewHandle;

        public override string toolID => ToolId;

        protected override void OnCreate()
        {
            base.OnCreate();

            CreateCollaborators();
            _points = new ControlPointRing();
            _preview = new RingPreview(_builder);
        }

        private void CreateCollaborators()
        {
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
        }

        protected override void OnDestroy()
        {
            _previewHandle.Complete();
            _points?.Dispose();
            _preview?.Dispose();
            base.OnDestroy();
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            applyAction.shouldBeEnabled = true;
            secondaryApplyAction.shouldBeEnabled = true;
            requireAreas = Game.Areas.AreaTypeMask.Districts;
            _points.Clear();
            _preview.Reset();
        }

        protected override void OnStopRunning()
        {
            applyAction.shouldBeEnabled = false;
            secondaryApplyAction.shouldBeEnabled = false;
            requireAreas = Game.Areas.AreaTypeMask.None;
            _previewHandle.Complete();
            _points.Clear();
            _preview.Reset();
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

            var canClose = _points.CanClose(hover._position, CloseRadius);
            _renderer.Draw(_points.Points, hover, canClose);

            applyMode = ApplyMode.Clear;

            var action = ToolInputReader.Read(applyAction, secondaryApplyAction, canClose);
            if (action == ToolEditAction.RemoveLast)
            {
                if (_points.RemoveLast())
                {
                    _preview.MarkPointsDirty();
                }
            }
            else if (action == ToolEditAction.AddPoint)
            {
                _points.Add(hover);
                _preview.MarkPointsDirty();
            }

            var handle = _preview.Update(_points, hover, !canClose, _selectedPrefab, inputDeps);

            if (action == ToolEditAction.Close)
            {
                handle.Complete();
                if (GetAllowApply() && _preview.IsValid)
                {
                    applyMode = ApplyMode.Apply;
                    _points.Clear();
                    _preview.Reset();
                }
            }

            _previewHandle = handle;
            return handle;
        }
    }
}

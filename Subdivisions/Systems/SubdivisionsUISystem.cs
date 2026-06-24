using Colossal.UI.Binding;
using Game.Prefabs;
using Game.Tools;
using Game.UI;
using Unity.Entities;

namespace Subdivisions.Systems
{
    /// <summary>
    /// Bridges the React toolbar UI and the tool. Exposes whether the tool is active
    /// and a trigger to toggle it.
    /// </summary>
    public partial class SubdivisionsUISystem : UISystemBase
    {
        private const string Group = "subdivisions";

        private SubdivisionsToolSystem _toolSystem;
        private ToolSystem _vanillaToolSystem;
        private PrefabSystem _prefabSystem;

        private EntityQuery _districtPrefabQuery;

        private bool _active;

        protected override void OnCreate()
        {
            base.OnCreate();

            _toolSystem = World.GetOrCreateSystemManaged<SubdivisionsToolSystem>();
            _vanillaToolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            _districtPrefabQuery = GetEntityQuery(ComponentType.ReadOnly<DistrictData>());

            _vanillaToolSystem.EventToolChanged += OnToolChanged;

            AddUpdateBinding(new GetterValueBinding<bool>(Group, "active", () => _active));
            AddBinding(new TriggerBinding(Group, "toggle", Toggle));
        }

        private void OnToolChanged(ToolBaseSystem tool)
        {
            _active = tool == _toolSystem;
        }

        private void Toggle()
        {
            if (_active)
            {
                _vanillaToolSystem.activeTool = _vanillaToolSystem.tools.Find(t => t is DefaultToolSystem) ?? _vanillaToolSystem.activeTool;
                return;
            }

            if (TryGetDistrictPrefab(out var prefab) && _toolSystem.TrySetPrefab(prefab))
            {
                _vanillaToolSystem.activeTool = _toolSystem;
            }
        }

        private bool TryGetDistrictPrefab(out PrefabBase prefab)
        {
            prefab = null;
            using var entities = _districtPrefabQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
            foreach (var entity in entities)
            {
                if (_prefabSystem.TryGetPrefab(entity, out PrefabBase candidate) && candidate is DistrictPrefab)
                {
                    prefab = candidate;
                    return true;
                }
            }
            return false;
        }

        protected override void OnDestroy()
        {
            if (_vanillaToolSystem is not null)
            {
                _vanillaToolSystem.EventToolChanged -= OnToolChanged;
            }
            base.OnDestroy();
        }
    }
}

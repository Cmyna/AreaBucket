using Colossal.UI.Binding;
using Game.Input;
using Game.Tools;
using Game.UI;
using Unity.Mathematics;

namespace AreaBucket.Systems
{
    public partial class AreaBucketToolUISystem : UISystemBase
    {

        private AreaBucketToolSystem _bucketToolSystem;

        private ToolSystem _toolSystem;

        protected override void OnCreate()
        {
            base.OnCreate();

            _toolSystem = World.GetOrCreateSystemManaged<ToolSystem>();
            _bucketToolSystem = World.GetOrCreateSystemManaged<AreaBucketToolSystem>();

            // AddUpdateBinding(new GetterValueBinding<bool>(Mod.ToolId, "showpanel", () => _toolSystem.activeTool is AreaBucketToolSystem) );
            AddUpdateBinding(new GetterValueBinding<bool>(Mod.ToolId, "enabled", () => _bucketToolSystem.ToolEnabled));
            AddUpdateBinding(new GetterValueBinding<float>(Mod.ToolId, "fillRange", () => _bucketToolSystem.FillMaxRange));
            AddBinding(new TriggerBinding(Mod.ToolId, "switch", OnEnableSwitch));
            AddBinding(new TriggerBinding<float>(Mod.ToolId, "setFillRange", OnChangingFillingRange));

            AddUpdateBinding(new GetterValueBinding<bool>(Mod.ToolId, "log4debug", () => _bucketToolSystem.Log4Debug));
            AddBinding(new TriggerBinding(Mod.ToolId, "log4debugSwitch", () => {
                _bucketToolSystem.Log4Debug = !_bucketToolSystem.Log4Debug;
            }));

            AddUpdateBinding(new GetterValueBinding<bool>(Mod.ToolId, "checkIntersection", () => _bucketToolSystem.CheckIntersection));
            AddBinding(new TriggerBinding(Mod.ToolId, "checkIntersectionSwitch", () =>
            {
                _bucketToolSystem.CheckIntersection = !_bucketToolSystem.CheckIntersection;
            }));

            // TODO: lines source entites filter
        }

        /// <summary>
        /// Switch area bucket tool enable state
        /// </summary>
        private void OnEnableSwitch()
        {
            var activePrefab = _toolSystem.activePrefab;
            _bucketToolSystem.ToolEnabled = !_bucketToolSystem.ToolEnabled;
            // TODO: handle vanilla area tool system
            _toolSystem.ActivatePrefabTool(activePrefab); // re-activate tool
        }

        private void OnChangingFillingRange(float number)
        {
            _bucketToolSystem.FillMaxRange = math.clamp(number, 10, 100);
        }
    }
}

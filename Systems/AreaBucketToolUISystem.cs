using Colossal.UI.Binding;
using Game.Input;
using Game.Tools;
using Game.UI;
using System.Reflection;
using System;
using Unity.Mathematics;
using UnityEngine;
using Game.Audio;
using Game.UI.InGame;
using Game.Debug;
using UnityEngine.Rendering;
using System.Collections.Generic;

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
            

            AddUpdateBinding(new GetterValueBinding<bool>(Mod.ToolId, "ToolEnabled", () => _bucketToolSystem.ToolEnabled));

            AddUpdateBinding(new GetterValueBinding<bool>(Mod.ToolId, "Active", () => _bucketToolSystem.Active));
            AddBinding(new TriggerBinding<bool>(Mod.ToolId, "SetActive", (v) =>
            {
                _bucketToolSystem.Active = v;
                ReActivateTool();
            }));


            //Add2WayBinding<float>(_bucketToolSystem, nameof(AreaBucketToolSystem.FillMaxRange));
            Add2WayBinding<float>(
                nameof(AreaBucketToolSystem.FillRange),
                () => _bucketToolSystem.FillRange,
                (v) => _bucketToolSystem.FillRange = Mathf.Clamp(v, 10, _bucketToolSystem.MaxFillingRange)
            );

            AddUpdateBinding(new GetterValueBinding<uint>(Mod.ToolId, nameof(AreaBucketToolSystem.BoundaryMask), () => (uint)_bucketToolSystem.BoundaryMask));
            AddBinding(new TriggerBinding<uint>(Mod.ToolId, "Set" + nameof(AreaBucketToolSystem.BoundaryMask), (v) => _bucketToolSystem.BoundaryMask = (BoundaryMask)v));
            //Add2WayBinding<uint>(_bucketToolSystem, nameof(AreaBucketToolSystem.BoundaryMask));

            // debug options bindings
            Add2WayBinding<bool>(_bucketToolSystem, nameof(AreaBucketToolSystem.ShowDebugOptions));
            Add2WayBinding<bool>(_bucketToolSystem, nameof(AreaBucketToolSystem.Log4Debug));
            Add2WayBinding<bool>(_bucketToolSystem, nameof(AreaBucketToolSystem.CheckIntersection));
            Add2WayBinding<bool>(_bucketToolSystem, nameof(AreaBucketToolSystem.JobImmediate));
            Add2WayBinding<bool>(_bucketToolSystem, nameof(AreaBucketToolSystem.WatchJobTime));
            Add2WayBinding<bool>(_bucketToolSystem, nameof(AreaBucketToolSystem.CheckOcclusion));
            Add2WayBinding<bool>(_bucketToolSystem, nameof(AreaBucketToolSystem.DropOwnedLane));

            // experimental options binding
            AddUpdateBinding(new GetterValueBinding<bool>(Mod.ToolId, "UseExperimentalOptions", () => _bucketToolSystem.UseExperimentalOptions));
            Add2WayBinding<bool>(_bucketToolSystem, nameof(AreaBucketToolSystem.ExtraPoints));

            
        }


        private void OnChangingFillingRange(float number)
        {
            _bucketToolSystem.FillRange = math.clamp(number, 10, 300);
        }

        private void ReActivateTool()
        {
            var activePrefab = _toolSystem.activePrefab;
            _toolSystem.ActivatePrefabTool(activePrefab);
        }


        public void Add2WayBinding<T>(object obj, string propertyName)
        {
            var getterValueBinding = GetterValueBindingHelper<T>(obj, propertyName);
            var triggerBinding = TriggerBindingHelper<T>(obj, propertyName);
            AddUpdateBinding(getterValueBinding);
            AddBinding(triggerBinding);
        }

        private void Add2WayBinding<T>(string name, Func<T> updateUICallback, Action<T> updateSystemCallback)
        {
            var getterValueBinding = new GetterValueBinding<T>(Mod.ToolId, name, updateUICallback);
            var triggerBinding = new TriggerBinding<T>(Mod.ToolId, "Set" + name, updateSystemCallback);
            AddUpdateBinding(getterValueBinding);
            AddBinding(triggerBinding);
        }

        /// <summary>
        /// helper method for binding one property of an object instance.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="obj"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static GetterValueBinding<T> GetterValueBindingHelper<T>(object obj, string propertyName)
        {
            Type type = obj.GetType();
            PropertyInfo propertyInfo = type.GetProperty(propertyName);

            if (propertyInfo == null) throw new ArgumentException($"'{propertyName}' is not a valid property of type '{type.Name}'.");

            return new GetterValueBinding<T>(Mod.ToolId, propertyName, () => (T)propertyInfo.GetValue(obj) );
        }

        public static TriggerBinding<T> TriggerBindingHelper<T>(object obj, string propertyName)
        {
            Type type = obj.GetType();
            PropertyInfo propertyInfo = type.GetProperty(propertyName);

            if (propertyInfo == null) throw new ArgumentException($"'{propertyName}' is not a valid property of type '{type.Name}'.");

            return new TriggerBinding<T>(Mod.ToolId, "Set" + propertyName,  (v) => propertyInfo.SetValue(obj, v) );
        }

    }
}

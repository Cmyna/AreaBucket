using Game.Prefabs;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Jobs;
using UnityEngine;

namespace AreaBucket.Systems
{
    internal partial class BlockAreaToolSystem : ToolBaseSystem
    {
        public override string toolID => "Block Area Tool";

        protected override void OnCreate()
        {
            // ensure Area Tool System is inited and OnCreate method is invoked
            World.GetOrCreateSystemManaged<AreaToolSystem>();
            /*typeof(AreaToolSystem)
                .GetMethod("OnCreate", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(areaToolSystem, null);*/
            base.OnCreate();
            UpdateBeforeAreaToolSystem();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            Mod.Logger.Info($"blocking");
            return base.OnUpdate(inputDeps);
        }
        private void UpdateBeforeAreaToolSystem()
        {
            base.m_ToolSystem.tools.Insert(0, this); // the first one being activated
        }

        public override PrefabBase GetPrefab()
        {
            return default;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            // block any Area Prefab
            return prefab is AreaPrefab;
        }
    }
}

using Game.Common;
using Game.Prefabs;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace AreaBucket.Systems.Jobs
{
    public struct ReplaceAreaEntitiesJob : IJob
    {
        [ReadOnly] public NativeList<Entity> tobeReplacedAreaEntites;

        public ComponentLookup<PrefabRef> cluPrefabRef;

        [ReadOnly] public Entity newAreaPrefab;

        public EntityCommandBuffer ecb;
        public void Execute()
        {
            for (int i = 0; i < tobeReplacedAreaEntites.Length; i++)
            {
                var targetEntity = tobeReplacedAreaEntites[i];
                if (!cluPrefabRef.TryGetComponent(targetEntity, out var prefabRef)) continue;
                prefabRef.m_Prefab = newAreaPrefab;

                ecb.SetComponent(targetEntity, prefabRef);
                ecb.AddComponent<Updated>(targetEntity); // so set updated?
            }
        }
    }
}

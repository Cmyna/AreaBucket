using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using AreaBucket.Utils;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct CollectNetLaneCurves : IJob
    {
        public NativeQuadTree<Entity, QuadTreeBoundsXZ> searchTree;

        public SingletonData singletonData;

        [ReadOnly] public ComponentLookup<Owner> luOwner;

        [ReadOnly] public ComponentLookup<NetLaneGeometryData> luNetLaneGeoData;

        [ReadOnly] public ComponentLookup<Curve> luCurve;

        [ReadOnly] public ComponentLookup<PrefabRef> luPrefabRef;

        [ReadOnly] public ComponentLookup<Game.Tools.EditorContainer> luEditorContainer;

        public CollectNetLaneCurves Init(SingletonData singletonData, NativeQuadTree<Entity, QuadTreeBoundsXZ> searchTree)
        {
            this.singletonData = singletonData;
            this.searchTree = searchTree;
            return this;
        }

        public void Execute()
        {
            var candidateEntites = new NativeList<Entity>(Allocator.Temp);
            var iterator = new In2DHitRangeEntitesIterator<Entity>();
            iterator.items = candidateEntites;
            iterator.hitPos = singletonData.playerHitPos;
            iterator.range = singletonData.fillingRange;
            searchTree.Iterate(ref iterator);

            for (int i = 0; i < iterator.items.Length; i++)
            {
                var netLaneEntity = iterator.items[i];

                // should have curve
                if (!luCurve.TryGetComponent(netLaneEntity, out var curveComp)) continue;

                // should have prefab ref
                if (!luPrefabRef.TryGetComponent(netLaneEntity, out var prefabRef)) continue;
                var prefabEntity = prefabRef.m_Prefab;

                if (!luNetLaneGeoData.HasComponent(prefabEntity)) continue; // should have LaneGeometryData

                if (!luOwner.TryGetComponent(netLaneEntity, out var owner)) continue; // should have owner

                var ownerEntity = owner.m_Owner;

                // owner should be marked as EditorContainer
                // so that will only collect one that is created by dev's panel/Find it/EDT tools
                if (!luEditorContainer.HasComponent(ownerEntity)) continue;


                var curve = curveComp.m_Bezier;
                var bounds = MathUtils.Bounds(curve).xz;
                var distance = MathUtils.Distance(bounds, singletonData.playerHitPos);
                if (distance <= singletonData.fillingRange)
                {
                    singletonData.curves.Add(curve);
                }
            }

            candidateEntites.Dispose();
        }
    }
}

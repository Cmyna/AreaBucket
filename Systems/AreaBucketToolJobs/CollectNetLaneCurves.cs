using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    public struct CollectNetLaneCurves : IJobChunk
    {
        public CommonContext context;

        [ReadOnly] public bool DropLaneOwnedByRoad;

        [ReadOnly] public bool DropLaneOwnedByBuilding;

        [ReadOnly] public ComponentTypeHandle<Curve> thCurve;

        [ReadOnly] public ComponentTypeHandle<PrefabRef> thPrefabRef;

        [ReadOnly] public ComponentTypeHandle<Owner> thOwner;

        [ReadOnly] public ComponentLookup<NetLaneGeometryData> luNetLaneGeoData;

        [ReadOnly] public ComponentLookup<Road> luRoad;

        [ReadOnly] public ComponentLookup<Building> luBuilding;

        [ReadOnly] public ComponentLookup<Game.Tools.EditorContainer> luEditorContainer;

        [ReadOnly] public BufferLookup<Game.Net.SubLane> luSubLane;

        


        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var curves = chunk.GetNativeArray(ref thCurve);
            var prefabRefs = chunk.GetNativeArray(ref thPrefabRef);
            var hasOwner = chunk.Has(ref thOwner);
            var owners = chunk.GetNativeArray(ref thOwner);

            for (int i = 0; i < curves.Length; i++)
            {
                var prefabEntity = prefabRefs[i].m_Prefab;

                if (!luNetLaneGeoData.HasComponent(prefabEntity)) continue; // should have LaneGeometryData
                //if (hasOwner && IsSubLane(owners[i])) continue; // drop net lane that is sub lane
                //if (hasOwner && DropLaneOwnedByRoad && OwnedByRoad(owners[i])) continue;
                //else if (hasOwner && DropLaneOwnedByBuilding && OwnedByBuilding(owners[i])) continue;

                // only collect net lanes created by dev tools / mods (assets from Find it/EDT etc.)
                // the charactor is net lane entities owned by an entity with EditorContianer component
                if (hasOwner && !OwnedByEditorContianer(owners[i])) continue;

                var curve = curves[i].m_Bezier;
                var bounds = MathUtils.Bounds(curve).xz;
                var distance = MathUtils.Distance(bounds, context.hitPos);
                if (distance <= context.filterRange) context.curves.Add(curve);
            }
        }

        private bool OwnedByRoad(Owner owner)
        {
            var ownerEntity = owner.m_Owner;
            return luRoad.HasComponent(ownerEntity);
        }

        private bool OwnedByBuilding(Owner owner)
        {
            return luBuilding.HasComponent(owner.m_Owner);
        }

        /// <summary>
        /// for net lanes drawed by dev tool/ EDT tools, its owner is entity from EditorContainer
        /// </summary>
        /// <param name="owner"></param>
        /// <returns></returns>
        private bool OwnedByEditorContianer(Owner owner)
        {
            return luEditorContainer.HasComponent(owner.m_Owner);
        }

        private bool IsSubLane(Owner owner)
        {
            var ownerEntity = owner.m_Owner;
            return luSubLane.HasBuffer(ownerEntity);
        }

    }
}

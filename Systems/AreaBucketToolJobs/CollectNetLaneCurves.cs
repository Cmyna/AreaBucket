﻿using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    public struct CollectNetLaneCurves : IJobChunk
    {
        public CommonContext context;

        public NativeList<Bezier4x3> curveList;

        [ReadOnly] public ComponentTypeHandle<Curve> thCurve;

        [ReadOnly] public ComponentTypeHandle<PrefabRef> thPrefabRef;

        [ReadOnly] public ComponentTypeHandle<Owner> thOwner;

        [ReadOnly] public ComponentLookup<NetLaneGeometryData> luNetLaneGeoData;

        [ReadOnly] public BufferLookup<Game.Net.SubLane> luSubLane;


        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var curves = chunk.GetNativeArray(ref thCurve);
            var prefabRefs = chunk.GetNativeArray(ref thPrefabRef);
            //var hasOwner = chunk.Has(ref thOwner);
            //var owners = chunk.GetNativeArray(ref thOwner);

            for (int i = 0; i < curves.Length; i++)
            {
                var prefabEntity = prefabRefs[i].m_Prefab;
                if (!luNetLaneGeoData.HasComponent(prefabEntity)) continue; // should have LaneGeometryData
                //if (hasOwner && IsSubLane(owners[i])) continue; // drop net lane that is sub lane

                var curve = curves[i].m_Bezier;
                var bounds = MathUtils.Bounds(curve).xz;
                var distance = MathUtils.Distance(bounds, context.hitPos);
                if (distance <= context.filterRange) curveList.Add(curve);
            }
        }

        private bool IsSubLane(Owner owner)
        {
            var ownerEntity = owner.m_Owner;
            return luSubLane.HasBuffer(ownerEntity);
        }
    }
}

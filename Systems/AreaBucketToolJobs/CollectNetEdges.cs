using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{

    [BurstCompile]
    public struct CollectNetEdges: IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<EdgeGeometry> thEdgeGeo;

        [ReadOnly] public ComponentTypeHandle<StartNodeGeometry> thStartNodeGeometry;

        [ReadOnly] public ComponentTypeHandle<EndNodeGeometry> thEndNodeGeometry;

        [ReadOnly] public ComponentTypeHandle<Composition> thComposition;

        [ReadOnly] public ComponentTypeHandle<Owner> thOwner;

        [ReadOnly] public ComponentLookup<NetCompositionData> luCompositionData;

        public CommonContext context;

        public BoundaryMask mask;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            // check mask has subnet filter or not
            var isSubnet = chunk.Has(ref thOwner);
            var useSubNetAsBounds = (mask & BoundaryMask.SubNet) != 0;
            if (isSubnet && !useSubNetAsBounds) return;

            var geos = chunk.GetNativeArray(ref thEdgeGeo);
            var startNodeGeos = chunk.GetNativeArray(ref thStartNodeGeometry);
            var endNodeGeos = chunk.GetNativeArray(ref thEndNodeGeometry);
            var compositions = chunk.GetNativeArray(ref thComposition);
            for (var i = 0; i < geos.Length; i++)
            {
                if (!IsBounds(luCompositionData[compositions[i].m_Edge])) continue;
                var geo = geos[i];
                
                var distance = MathUtils.Distance(geo.m_Bounds.xz, context.hitPos);
                if (distance > context.filterRange) continue;

                context.curves.Add(geo.m_Start.m_Left);
                context.curves.Add(geo.m_Start.m_Right);

                context.curves.Add(geo.m_End.m_Left);
                context.curves.Add(geo.m_End.m_Right);

                TryAddNodeGeometry(startNodeGeos[i].m_Geometry);
                TryAddNodeGeometry(endNodeGeos[i].m_Geometry);
            }
        }

        /// <summary>
        /// Is net that can be boundaries for area filling
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool IsBounds(NetCompositionData data)
        {
            var hasSurface = (data.m_State & CompositionState.HasSurface) != 0;
            var checker = CompositionFlags.General.Elevated | 
                CompositionFlags.General.Tunnel;
            var flag = data.m_Flags.m_General;
            return (flag & checker) == 0 && hasSurface;
        }

        private void TryAddNodeGeometry(EdgeNodeGeometry node)
        {
            var isValid = IsValid(node);
            if (isValid) 
            {
                // add outside edges of node
                context.curves.Add(node.m_Left.m_Left);
                context.curves.Add(node.m_Right.m_Right);
            }

            // TODO: I am not sure this code should be used or not..
            // guess the inside edge used if the node is part of roundabout
            if (isValid && node.m_MiddleRadius > 0f)
            {
                context.curves.Add(node.m_Left.m_Right);
                context.curves.Add(node.m_Right.m_Left);
            }
        }

        /// <summary>
        /// Copied from NetDebugSystem.NetGizmosJob.IsValid
        /// Check a node geometry is valid or not
        /// </summary>
        /// <param name="nodeGeometry"></param>
        /// <returns></returns>
        private bool IsValid(EdgeNodeGeometry nodeGeometry)
        {
            float3 @float = nodeGeometry.m_Left.m_Left.d - nodeGeometry.m_Left.m_Left.a;
            float3 float2 = nodeGeometry.m_Left.m_Right.d - nodeGeometry.m_Left.m_Right.a;
            float3 float3 = nodeGeometry.m_Right.m_Left.d - nodeGeometry.m_Right.m_Left.a;
            float3 float4 = nodeGeometry.m_Right.m_Right.d - nodeGeometry.m_Right.m_Right.a;
            return math.lengthsq(@float + float2 + float3 + float4) > 1E-06f;
        }


        public void InitHandles(ref SystemState state)
        {
            thComposition.Update(ref state);
            thEdgeGeo.Update(ref state);
            thEndNodeGeometry.Update(ref state);
            thEndNodeGeometry.Update(ref state);
            luCompositionData.Update(ref state);
        }
    }
}

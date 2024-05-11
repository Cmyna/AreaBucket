using Colossal.Mathematics;
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
    public struct FilterEdgesGeos: IJobChunk
    {
        [ReadOnly] public ComponentTypeHandle<EdgeGeometry> thEdgeGeo;

        [ReadOnly] public ComponentTypeHandle<StartNodeGeometry> thStartNodeGeometry;

        [ReadOnly] public ComponentTypeHandle<EndNodeGeometry> thEndNodeGeometry;

        [ReadOnly] public ComponentTypeHandle<Composition> thComposition;

        [ReadOnly] public ComponentLookup<NetCompositionData> luCompositionData;

        [ReadOnly] public float filterRange;

        [ReadOnly] public float2 hitPoint;


        public NativeList<Bezier4x3> filterResults;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var geos = chunk.GetNativeArray(ref thEdgeGeo);
            var startNodeGeos = chunk.GetNativeArray(ref thStartNodeGeometry);
            var endNodeGeos = chunk.GetNativeArray(ref thEndNodeGeometry);
            var compositions = chunk.GetNativeArray(ref thComposition);
            for (var i = 0; i < geos.Length; i++)
            {
                if (!IsValidNet(luCompositionData[compositions[i].m_Edge])) continue;
                var geo = geos[i];
                
                var distance = MathUtils.Distance(geo.m_Bounds.xz, hitPoint);
                if (distance > filterRange) continue;

                filterResults.Add(geo.m_Start.m_Left);
                filterResults.Add(geo.m_Start.m_Right);

                filterResults.Add(geo.m_End.m_Left);
                filterResults.Add(geo.m_End.m_Right);

                TryAddNodeGeometry(startNodeGeos[i].m_Geometry);
                TryAddNodeGeometry(endNodeGeos[i].m_Geometry);
            }
        }

        private bool IsValidNet(NetCompositionData data)
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
                filterResults.Add(node.m_Left.m_Left);
                filterResults.Add(node.m_Right.m_Right);
            }

            // TODO: I am not sure this code should be used or not..
            // guess the inside edge used if the node is part of roundabout
            if (isValid && node.m_MiddleRadius > 0f)
            {
                filterResults.Add(node.m_Left.m_Right);
                filterResults.Add(node.m_Right.m_Left);
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
    }
}

using Colossal.Mathematics;
using Game.Net;
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

        [ReadOnly] public float filterRange;

        [ReadOnly] public float2 hitPoint;

        public NativeList<Bezier4x3> filterResults;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var geos = chunk.GetNativeArray(ref thEdgeGeo);
            var startNodeGeos = chunk.GetNativeArray(ref thStartNodeGeometry);
            var endNodeGeos = chunk.GetNativeArray(ref thEndNodeGeometry);
            for (var i = 0; i < geos.Length; i++)
            {
                var geo = geos[i];
                
                var distance = MathUtils.Distance(geo.m_Bounds.xz, hitPoint);
                if (distance > filterRange) continue;
                var nodeGeo1 = startNodeGeos[i].m_Geometry;
                var nodeGeo2 = endNodeGeos[i].m_Geometry;

                filterResults.Add(geo.m_Start.m_Left);
                filterResults.Add(geo.m_Start.m_Right);

                filterResults.Add(geo.m_End.m_Left);
                filterResults.Add(geo.m_End.m_Right);

                var node1Valid = IsValid(nodeGeo1);
                var node2Valid = IsValid(nodeGeo2);
                if (node1Valid)
                {
                    filterResults.Add(nodeGeo1.m_Left.m_Left);
                    //filterResults.Add(nodeGeo1.m_Left.m_Right);
                    //filterResults.Add(nodeGeo1.m_Right.m_Left);
                    filterResults.Add(nodeGeo1.m_Right.m_Right);
                }

                if (node1Valid && nodeGeo1.m_MiddleRadius > 0f)
                {
                    filterResults.Add(nodeGeo1.m_Left.m_Right);
                    filterResults.Add(nodeGeo1.m_Right.m_Left);
                }



                if (node2Valid)
                {
                    filterResults.Add(nodeGeo2.m_Left.m_Left);
                    //filterResults.Add(nodeGeo2.m_Left.m_Right);
                    // filterResults.Add(nodeGeo2.m_Right.m_Left);
                    filterResults.Add(nodeGeo2.m_Right.m_Right);
                }

                if (node2Valid && nodeGeo2.m_MiddleRadius > 0f)
                {
                    filterResults.Add(nodeGeo2.m_Left.m_Right);
                    filterResults.Add(nodeGeo2.m_Right.m_Left);
                }



            }
        }

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

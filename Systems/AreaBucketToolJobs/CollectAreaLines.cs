﻿using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Game.Areas;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct CollectAreaLines : IJobChunk
    {

        [ReadOnly] public BufferTypeHandle<Node> bthNode;

        [ReadOnly] public ComponentTypeHandle<Area> thArea;

        [ReadOnly] public BufferTypeHandle<Triangle> bthTriangle;

        public CommonContext context;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            if (!chunk.Has(ref bthTriangle)) return;
            var triangleAccessor = chunk.GetBufferAccessor(ref bthTriangle);
            var nodesAccessor = chunk.GetBufferAccessor(ref bthNode);
            var areas = chunk.GetNativeArray(ref thArea);
            for (var i = 0; i < nodesAccessor.Length; i++)
            {
                if ((areas[i].m_Flags & AreaFlags.Complete) == 0) continue;
                var buffer = nodesAccessor[i];
                var triangleBuffer = triangleAccessor[i];
                // if no triangle, means it is an invalid area shape,
                // this kinds of entities is generated from area entity that players insert and drag new point on one edge to another edge
                // the area entity just "invisible" because no more triangle, but still there
                if (triangleBuffer.Length == 0) continue;
                HandleBuffer(buffer);
            }
        }

        private void HandleBuffer(DynamicBuffer<Node> buffer)
        {
            for (var i = 0; i < buffer.Length - 1; i++)
            {
                HandleLine(buffer[i].m_Position.xz, buffer[i + 1].m_Position.xz);
            }
            HandleLine(buffer[buffer.Length - 1].m_Position.xz, buffer[0].m_Position.xz);
        }

        private void HandleLine(float2 p1, float2 p2)
        {
            if (math.distance(p1, p2) < 0.5f) return;
            var line = new Line2(p1, p2);
            if (!InRange(line)) return;
            // CollectedChoppedPoints(line, 50f);
            //UnamangedUtils.CollectDivPoints(line, context.hitPos, context.filterRange, ref context.points);
            //CollectDivPoints(line, ref points);
            context.totalBoundaryLines.Add(line);
        }

        private bool InRange(Line2 line)
        {
            var hitPos = context.hitPos;
            var dist1 = MathUtils.Distance(line, hitPos, out var t);
            var onSegment = t >= 0 && t <= 1;
            if (onSegment) return dist1 <= context.filterRange;
            else
            {
                var dist2 = math.distance(line.a, hitPos);
                var dist3 = math.distance(line.b, hitPos);
                return math.min(dist2, dist3) <= context.filterRange;
            }
        }

        private bool InRange2(Line2 line)
        {
            var hitPos = context.hitPos;
            var dist1 = math.length(line.a - hitPos);
            var dist2 = math.length(line.b - hitPos);
            var minDist = math.min(dist1, dist2);
            return minDist <= context.filterRange;
        }


        private void CollectedChoppedPoints(Line2 line, float maxLength = 4f)
        {
            var count = (int)(math.length(line.a - line.b) / maxLength) + 1;
            float tStep = 1 / (float)count;
            for (var i = 0; i < count; i++)
            {
                float t = tStep * i;
                float t2 = tStep * (i + 1);
                t2 = math.min(t2, 1);
                t = math.max(t, 0);

                var p1 = math.lerp(line.a, line.b, t);
                var p2 = math.lerp(line.a, line.b, t2);

                //checklines.Add(line);
                //lines.Add(line);
                context.points.Add(p1);
                context.points.Add(p2);
            }
        }
    }
}

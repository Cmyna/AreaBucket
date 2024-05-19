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
    public struct Areas2Lines : IJobChunk
    {

        [ReadOnly] public BufferTypeHandle<Node> bthNode;

        [ReadOnly] public ComponentTypeHandle<Area> thArea;

        public CommonContext context;

        public NativeList<Line2> checklines;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var nodesAccessor = chunk.GetBufferAccessor(ref bthNode);
            var areas = chunk.GetNativeArray(ref thArea);
            for (var i = 0; i < nodesAccessor.Length; i++)
            {
                if ((areas[i].m_Flags & AreaFlags.Complete) == 0) continue;
                var buffer = nodesAccessor[i];
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
            UnamangedUtils.CollectDivPoints(line, context.hitPos, context.filterRange, ref context.points);
            //CollectDivPoints(line, ref points);
            checklines.Add(line);
            context.lines.Add(line);
        }

        private bool InRange(Line2 line)
        {
            var hitPos = context.hitPos;
            var dist1 = MathUtils.Distance(line, hitPos, out var t);
            var dist2 = math.distance(line.a, hitPos);
            var dist3 = math.distance(line.b, hitPos);
            var minDist = math.min(dist1, dist2);
            minDist = math.min(minDist, dist3);
            return minDist <= context.filterRange;
            //var onSegment = t >= 0 && t <= 1;
            //return (dist1 <= range) && onSegment;
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

using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct DropObscuredLines : IJob
    {
        public CommonContext context;

        public SingletonData signletonData;

        public DropObscuredLines Init(CommonContext context, SingletonData signletonData)
        {
            this.context = context;
            this.signletonData = signletonData;
            return this;
        }

        public void Execute()
        {
            // clear buffer before
            context.ClearOcclusionBuffer();
            context.usedBoundaryLines.Clear();

            var metasCache = new NativeList<LineMeta>(Allocator.Temp);

            for (int i = 0; i < signletonData.totalBoundaryLines.Length; i++)
            {
                var line = signletonData.totalBoundaryLines[i];
                var v1 = line.a - context.rayStartPoint;
                var v2 = line.b - context.rayStartPoint;

                var minDist = MinDist(line);
                var maxDist = math.max(math.length(v1), math.length(v2));

                var meta = new LineMeta
                {
                    minDist = minDist,
                    maxDist = maxDist,
                    bounds = GetTraveledDegs(v1, v2, out _, out _)
                };
                metasCache.Add(meta);

                // update occlusion buffer
                UpdateOcclusionBuffer(meta);
            }

            // drop obscured lines
            for (int i = 0; i < signletonData.totalBoundaryLines.Length; i++)
            {
                if (IsObscured(metasCache[i])) continue;
                context.usedBoundaryLines.Add(signletonData.totalBoundaryLines[i]);
            }


            metasCache.Dispose();
        }

        private void UpdateOcclusionBuffer(LineMeta meta)
        {
            for (int i = Mathf.CeilToInt(meta.bounds.x); i < Mathf.FloorToInt(meta.bounds.y); i++)
            {
                var idx = i % context.occlusionsBuffer.Length;
                context.occlusionsBuffer[idx] = math.min(context.occlusionsBuffer[idx], meta.maxDist);
            }
        }

        private bool IsObscured(LineMeta meta)
        {
            bool exposed = false;
            for (int i = Mathf.FloorToInt(meta.bounds.x); i < Mathf.CeilToInt(meta.bounds.y); i++)
            {
                var depth = context.occlusionsBuffer[i % context.occlusionsBuffer.Length];
                exposed |= depth >= meta.minDist;
            }
            return !exposed;
        }


        private float2 GetTraveledDegs(float2 v1, float2 v2, out bool crossZeroDeg, out float degreeDiff)
        {
            // assume context.occlusionsBuffer.Length == 360
            float2 xzUp = new float2(0, 1);

            var d1 = Utils.Math.RadianInClock(xzUp, v1) * Mathf.Rad2Deg;
            var d2 = Utils.Math.RadianInClock(xzUp, v2) * Mathf.Rad2Deg;

            var dmin = math.min(d1, d2);
            var dmax = math.max(d1, d2);

            degreeDiff = math.abs(d1 - d2);

            crossZeroDeg = degreeDiff > 180;

            if (crossZeroDeg) return new float2 { x = dmax, y = dmin + 360 };
            else return new float2{ x = dmin, y = dmax };
        }

        private float MinDist(Line2 line)
        {
            var rayStartPos = context.rayStartPoint;
            var dist = MathUtils.Distance(line, rayStartPos, out var t);
            if (t >= 0 && t <= 1) return dist;
            var v1 = line.a - rayStartPos;
            var v2 = line.b - rayStartPos;

            return math.min(math.length(v1), math.length(v2));
        }

    }

    public struct LineMeta
    {
        public float minDist;
        public float maxDist;
        public float2 bounds;
    }
}

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

        public SingletonData singletonData;

        public GeneratedArea generatedAreaData;

        public bool checkOcculsion;

        public DropObscuredLines Init(CommonContext context, SingletonData signletonData, GeneratedArea generatedAreaData, bool checkOcculsion)
        {
            this.context = context;
            this.singletonData = signletonData;
            this.generatedAreaData = generatedAreaData;
            this.checkOcculsion = checkOcculsion;
            return this;
        }

        public void Execute()
        {
            context.usedBoundaryLines.Clear();
            // clear buffer before
            context.ClearOcclusionBuffer();

            if (!checkOcculsion)
            {
                context.usedBoundaryLines.AddRange(singletonData.totalBoundaryLines.AsArray());
                context.usedBoundaryLines.AddRange(generatedAreaData.polyLines.AsArray());
                return;
            }

            var metasCache = new NativeList<LineMeta>(Allocator.Temp);

            for (int i = 0; i < singletonData.totalBoundaryLines.Length; i++)
            {
                /*var line = signletonData.totalBoundaryLines[i];
                var v1 = line.a - context.floodingDefinition.rayStartPoint;
                var v2 = line.b - context.floodingDefinition.rayStartPoint;

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
                UpdateOcclusionBuffer(meta);*/
                HandleBoundary(singletonData.totalBoundaryLines[i], ref metasCache);
            }

            // it seems that occlusion not works well on generated polylines...
            // i guess it is because they are too closed to ray start point
            // hense not use occlusions on these lines
            /*for (int i = 0; i < generatedAreaData.polyLines.Length; i++)
            {
                var line = generatedAreaData.polyLines[i];
                HandleBoundary(line, ref metasCache);
            }*/
            context.usedBoundaryLines.AddRange(generatedAreaData.polyLines.AsArray());

            // drop obscured lines
            for (int i = 0; i < singletonData.totalBoundaryLines.Length; i++)
            {
                if (IsObscured(metasCache[i])) continue;
                context.usedBoundaryLines.Add(singletonData.totalBoundaryLines[i]);
            }


            metasCache.Dispose();
        }

        private void HandleBoundary(Line2 line, ref NativeList<LineMeta> metas)
        {
            var v1 = line.a - context.floodingDefinition.rayStartPoint;
            var v2 = line.b - context.floodingDefinition.rayStartPoint;

            var minDist = MinDist(line);
            var maxDist = math.max(math.length(v1), math.length(v2));

            var meta = new LineMeta
            {
                minDist = minDist,
                maxDist = maxDist,
                bounds = GetTraveledDegs(v1, v2, out _, out _)
            };
            metas.Add(meta);

            UpdateOcclusionBuffer(meta);
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
            var rayStartPos = context.floodingDefinition.rayStartPoint;
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

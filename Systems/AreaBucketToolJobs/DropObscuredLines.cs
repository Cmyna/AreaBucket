using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Game.Prefabs.CharacterGroup;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct DropObscuredLines : IJob
    {
        public CommonContext context;

        public SingletonData singletonData;

        public GeneratedArea generatedAreaData;

        public bool checkOcculsion;

        public bool useOldWay;

        public DropObscuredLines Init(CommonContext context, SingletonData singletonData, GeneratedArea generatedAreaData, bool checkOcculsion)
        {
            this.context = context;
            this.singletonData = singletonData;
            this.generatedAreaData = generatedAreaData;
            this.checkOcculsion = checkOcculsion;
            return this;
        }

        public void Execute()
        {
            context.usedBoundaryLines.Clear();
            AddGeneratedPolylinesAsBoundary();

            if (!useOldWay)
            {
                context.usedBoundaryLines.AddRange(singletonData.totalBoundaryLines.AsArray());
                return;
            }

            context.ClearOcclusionBuffer(); // clear buffer before
            if (!checkOcculsion)
            {
                context.usedBoundaryLines.AddRange(singletonData.totalBoundaryLines.AsArray());
                return;
            }
            
            var projectedSegments = new NativeList<PolarSegment>(Allocator.Temp);

            for (int i = 0; i < singletonData.totalBoundaryLines.Length; i++)
            {
                HandleBoundary(singletonData.totalBoundaryLines[i], projectedSegments);
            }

            // drop obscured lines
            for (int i = 0; i < singletonData.totalBoundaryLines.Length; i++)
            {
                if (IsObscured(projectedSegments[i])) continue;
                context.usedBoundaryLines.Add(singletonData.totalBoundaryLines[i]);
            }
        }

        private void AddGeneratedPolylinesAsBoundary()
        {
            // prevent adding the flooding candidate line 
            var splitter = context.floodingDefinition.newAreaPointInsertStartIndex;
            for (int i = 0; i < splitter; i++) context.usedBoundaryLines.Add(generatedAreaData.polyLines[i]);
            for (int i = splitter + 1; i < generatedAreaData.polyLines.Length; i++) context.usedBoundaryLines.Add(generatedAreaData.polyLines[i]);
        }

        /// <summary>
        /// colllect some related info for each boundary lines
        /// </summary>
        /// <param name="line"></param>
        /// <param name="projectedSegments"></param>
        private void HandleBoundary(Line2 line, NativeList<PolarSegment> projectedSegments)
        {
            var v1 = line.a - context.floodingDefinition.rayStartPoint;
            var v2 = line.b - context.floodingDefinition.rayStartPoint;

            var minDist = MinDist(line);
            var maxDist = math.max(math.length(v1), math.length(v2));

            var projectedSegment = new PolarSegment
            {
                minDist = minDist,
                maxDist = maxDist,
                bounds = GetTraveledDegs(v1, v2, out _, out _)
            };
            projectedSegments.Add(projectedSegment);

            UpdateOcclusionBuffer(projectedSegment);
        }

        private void UpdateOcclusionBuffer(PolarSegment meta)
        {
            for (int i = Mathf.CeilToInt(meta.bounds.x); i < Mathf.FloorToInt(meta.bounds.y); i++)
            {
                var idx = i % context.occlusionsBuffer.Length;
                context.occlusionsBuffer[idx] = math.min(context.occlusionsBuffer[idx], meta.maxDist);
            }
        }

        public bool IsObscured(PolarSegment meta)
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

    [BurstCompile]
    public struct CheckOcclusionJob : IJob
    {
        public NativeList<Line2> usedBoundaries;

        public NativeList<PolarSegment> projectedBoundaries;

        public NativeArray<float> occlusionBuffer;

        public void Execute()
        {
            var boundariesCache = new NativeList<Line2>(Allocator.Temp);
            for (int i = 0; i < occlusionBuffer.Length; i++) occlusionBuffer[i] = float.MaxValue;

            for (int i = 0; i < usedBoundaries.Length; i++)
            {
                UpdateOcclusionBuffer(projectedBoundaries[i], occlusionBuffer);
            }

            for (int i = 0; i < usedBoundaries.Length; i++)
            {
                if (IsObscured(projectedBoundaries[i], occlusionBuffer)) continue;
                boundariesCache.Add(usedBoundaries[i]);
            }
            usedBoundaries.Clear();
            usedBoundaries.AddRange(boundariesCache.AsArray());
        }

        private static void UpdateOcclusionBuffer(PolarSegment segment, NativeArray<float> buffer)
        {
            for (int i = Mathf.FloorToInt(segment.bounds.x); i < Mathf.CeilToInt(segment.bounds.y); i++)
            {
                var idx = i % buffer.Length;
                buffer[idx] = math.min(buffer[idx], segment.maxDist);
            }
        }

        private static bool IsObscured(PolarSegment meta, NativeArray<float> buffer)
        {
            bool exposed = false;
            for (int i = Mathf.FloorToInt(meta.bounds.x); i < Mathf.CeilToInt(meta.bounds.y); i++)
            {
               var depth = buffer[i % buffer.Length];
                exposed |= depth >= meta.minDist;
            }
            return !exposed;
        }
    }

    [BurstCompile]
    public struct PolarProjectionJob : IJob
    {

        public NativeList<Line2> boudaries;

        public NativeList<PolarSegment> projectedBoundaries;

        [ReadOnly] public float2 polarCenter;

        public void Execute()
        {
            for (int i = 0; i < boudaries.Length; i++)
            {
                var line = boudaries[i];
                var projectedSegment = CreateProjection(new Line2.Segment(line.a, line.b), polarCenter);
                projectedBoundaries.Add(projectedSegment);
            }
        }

        public static PolarSegment CreateProjection(Line2.Segment segment, float2 polarCenter)
        {
            var v1 = segment.a - polarCenter;
            var v2 = segment.b - polarCenter;

            var minDist = MinDist(segment, polarCenter);
            var maxDist = math.max(math.length(v1), math.length(v2));

            var projecedSegment = new PolarSegment
            {
                minDist = minDist,
                maxDist = maxDist,
                bounds = GetThetaProjBounds(v1, v2, out _, out _)
            };
            return projecedSegment;
        }

        private static float MinDist(Line2.Segment line, float2 polarCenter)
        {
            var dist = MathUtils.Distance(line, polarCenter, out var t);
            if (t >= 0 && t <= 1) return dist;
            var v1 = line.a - polarCenter;
            var v2 = line.b - polarCenter;

            return math.min(math.length(v1), math.length(v2));
        }


        private static float2 GetThetaProjBounds(float2 v1, float2 v2, out bool crossZeroDeg, out float degreeDiff)
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
            else return new float2 { x = dmin, y = dmax };
        }
    }


    public struct PolarSegment
    {
        /// <summary>
        /// min value projected on rho axis
        /// </summary>
        public float minDist;

        /// <summary>
        /// max value projected on rho axis
        /// </summary>
        public float maxDist;
        /// <summary>
        /// segment bounds projected on theta axis
        /// </summary>
        public float2 bounds;
    }
}

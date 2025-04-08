using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
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

        public DropObscuredLines Init(
            CommonContext context, 
            SingletonData singletonData, 
            GeneratedArea generatedAreaData, 
            bool checkOcculsion)
        {
            this.context = context;
            this.singletonData = singletonData;
            this.generatedAreaData = generatedAreaData;
            this.checkOcculsion = checkOcculsion;
            return this;
        }

        public void Execute()
        {
            context.ClearBoundaries();
            AddGeneratedPolylinesAsBoundary();
            context.AddBoundaries(singletonData.totalBoundaryLines.AsArray());
        }

        private void AddGeneratedPolylinesAsBoundary()
        {
            // prevent adding the flooding candidate line 
            for (int i = 0; i < generatedAreaData.polyLines.Length; i++)
            {
                if (i == context.floodingDefinition.newAreaPointInsertStartIndex) continue;
                context.AddBoundary(generatedAreaData.polyLines[i]);
            }
        }

    }

    [BurstCompile]
    public struct CheckOcclusionJob : IJob
    {


        public NativeList<PolarSegment> projectedBoundaries;


        public CommonContext context;

        public CheckOcclusionJob Init(
            CommonContext context, 
            NativeList<PolarSegment> projectedBoundaries)
        {
            this.context = context;
            this.projectedBoundaries = projectedBoundaries;
            return this;
        }

        public void Execute()
        {
            var boundariesCache = new NativeList<Line2.Segment>(Allocator.Temp);
            for (int i = 0; i < context.occlusionsBuffer.Length; i++) context.occlusionsBuffer[i] = float.MaxValue;

            for (int i = 0; i < projectedBoundaries.Length; i++)
            {
                UpdateOcclusionBuffer(projectedBoundaries[i], context.occlusionsBuffer);
            }

            for (int i = 0; i < projectedBoundaries.Length; i++)
            {
                if (IsObscured(projectedBoundaries[i], context.occlusionsBuffer)) continue;
                boundariesCache.Add(projectedBoundaries[i].originalLine);
            }
            context.ClearBoundaries();
            context.AddBoundaries(boundariesCache.AsArray());
        }

        private static void UpdateOcclusionBuffer(PolarSegment segment, NativeArray<float> buffer)
        {
            for (int i = Mathf.CeilToInt(segment.bounds.x); i < Mathf.FloorToInt(segment.bounds.y); i++)
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
    public struct PolarProjectionJob : IJobParallelFor
    {

        public NativeArray<Line2.Segment>.ReadOnly boudaries;

        public NativeList<PolarSegment>.ParallelWriter projectedBoundaries;

        [ReadOnly] public float2 polarCenter;

        public void Execute(int index)
        {
            var line = boudaries[index];
            var projectedSegment = CreateProjection(line, polarCenter);
            projectedBoundaries.AddNoResize(projectedSegment);
        }

        public static PolarSegment CreateProjection(Line2.Segment segment, float2 polarCenter)
        {
            var v1 = segment.a - polarCenter;
            var v2 = segment.b - polarCenter;

            var minDist = MinDist(new Line2.Segment(segment.a, segment.b), polarCenter);
            var maxDist = math.max(math.length(v1), math.length(v2));

            var projecedSegment = new PolarSegment
            {
                minDist = minDist,
                maxDist = maxDist,
                bounds = GetThetaProjBounds(v1, v2, out _, out _),
                originalLine = segment,
            };
            return projecedSegment;
        }

        private static float MinDist(Line2.Segment segment, float2 polarCenter)
        {
            var dist = MathUtils.Distance(segment, polarCenter, out var t);
            if (t >= 0 && t <= 1) return dist;
            var v1 = segment.a - polarCenter;
            var v2 = segment.b - polarCenter;

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

        public Line2.Segment originalLine;
    }
}

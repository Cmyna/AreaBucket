using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using System.Linq;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    /// <summary>
    /// TODO: refactor the merging algorithm (idea is use a convolution like merging)
    /// 
    /// TODO: sharp angle reduction when merging (still by convolution)
    /// </summary>
    public struct MergePolylines : IJob
    {
        public GeneratedArea generatedAreaData;

        /// <summary>
        /// final polygon edges min length,
        /// for merge rays where their end points too close
        /// </summary>
        [ReadOnly] public float minEdgeLength;

        /// <summary>
        /// a softer threshold, only if over walking distances and over this angle threshold will break merging
        /// </summary>
        [ReadOnly] public float breakMergeAngleThreshold;

        /// <summary>
        /// a larger threshold, the merging should be break whatever
        /// </summary>
        [ReadOnly] public float strictBreakMergeAngleThreshold;

        public void Execute()
        {
            var count = generatedAreaData.points.Length;
            if (count <= 2) return;
            var pointsCache = new NativeList<float2>(Allocator.Temp);
            var linesCache = new NativeList<Line2>(Allocator.Temp);

            // merge points that are too closed (and may affect angle calc)
            for (int i = 0; i < generatedAreaData.points.Length; i++)
            {
                var p1 = generatedAreaData.points[i];
                var p2 = generatedAreaData.points[(i + 1) % count];
                var v = p2 - p1;
                if (math.length(v) <= float.MinValue) continue;
                pointsCache.Add(p1);
            }
            generatedAreaData.points.Clear(); generatedAreaData.points.AddRange(pointsCache.AsArray()); pointsCache.Clear();


            // boundary (special) case, if while loop below ends at input cursor value `points.Length - 1`
            // may cause one point duplicate
            pointsCache.Add(generatedAreaData.points[0]);

            var checkVector = generatedAreaData.points[1] - generatedAreaData.points[0];

            float walkThroughDist = 0;
            for (int i = 1; i < generatedAreaData.points.Length; i++)
            {
                var p1 = generatedAreaData.points[i];
                var p2 = generatedAreaData.points[(i + 1) % count];
                var v = p2 - p1;
                if (math.length(v) <= 0.05f) continue; // 0.05f: it is a magic number prevent tiny vector affects other calcuations
                var angle = Angle(checkVector, v);
                walkThroughDist += math.length(v);

                var overWalkThroughDist = walkThroughDist >= minEdgeLength;
                var overSoftAngleThreshold = angle > breakMergeAngleThreshold;
                var overStrictAngleThreshold = angle > strictBreakMergeAngleThreshold;

                var generateBreak = (overSoftAngleThreshold && overWalkThroughDist) || overStrictAngleThreshold;

                if (!generateBreak) continue;
                pointsCache.Add(p1);
                walkThroughDist = 0f;
                checkVector = v;
            }

            // check (pointsCache[0], pointsCache[1]) and (pointsCache[-1], pointsCache[0]) are collinear or not
            var canMerge = false;
            if (pointsCache.Length >= 3)
            {
                var v1 = pointsCache[1] - pointsCache[0];
                var v2 = pointsCache[0] - pointsCache.ElementAt(pointsCache.Length - 1);
                canMerge = Angle(v1, v2) <= breakMergeAngleThreshold;
            }
            if (canMerge) pointsCache.RemoveAt(0);

            generatedAreaData.points.Clear();
            generatedAreaData.points.AddRange(pointsCache.AsArray());

            UnamangedUtils.BuildPolylines(generatedAreaData.points, generatedAreaData.polyLines);

            pointsCache.Dispose();
            linesCache.Dispose();
        }

        private float Angle(float2 a, float2 b)
        {
            var cosTheta = math.dot(a, b) / math.length(a) / math.length(b);
            return math.acos(math.clamp(cosTheta, 0, 1));
        }
    }
}

using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal;
using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    /// <summary>
    /// merge rays that their end points ALMOST collinear
    /// 
    /// </summary>
    public struct MergeRays : IJob
    {

        public CommonContext context;

        /// <summary>
        /// final polygon edges min length,
        /// for merge rays where their end points too close
        /// </summary>
        public float minEdgeLength;

        /// <summary>
        /// a softer threshold, only if over walking distances and over this angle threshold will break merging
        /// </summary>
        public float breakMergeAngleThreshold;

        /// <summary>
        /// a larger threshold, the merging should be break whatever
        /// </summary>
        internal float strictBreakMergeAngleThreshold;

        public void Execute()
        {
            if (context.rays.Length < 2) return;

            var cache = new NativeList<Ray>(Allocator.Temp);
            var count = context.rays.Length;

            // boundary (special) case, if while loop below ends at input cursor value `rays.Length - 1`
            // may cause one ray duplicate
            cache.Add(context.rays[0]); 

            var checkVector = context.rays[1].vector - context.rays[0].vector;

            float walkThroughDist = 0;
            for (int i = 1; i < count; i++)
            {
                var r1 = context.rays[i];
                var r2 = context.rays[(i + 1) % count];
                var v = r2.vector - r1.vector;
                var angle = Angle(checkVector, v);
                walkThroughDist += math.length(v);

                var overWalkThroughDist = walkThroughDist >= minEdgeLength;
                var overSoftAngleThreshold = angle > breakMergeAngleThreshold;
                var overStrictAngleThreshold = angle > strictBreakMergeAngleThreshold;
                var generateBreak = (overSoftAngleThreshold && overWalkThroughDist) || overStrictAngleThreshold;
                if (!generateBreak) continue;
                walkThroughDist = 0f;
                checkVector = v;
                cache.Add(r1);
            }

            // write back
            context.rays.Clear();
            context.rays.AddRange(cache.AsArray());

            cache.Dispose();
        }

        private float Angle(float2 a, float2 b)
        {
            var cosTheta = math.dot(a, b) / math.length(a) / math.length(b);
            return math.acos(math.clamp(cosTheta, 0, 1));
        }

        /// <summary>
        /// this method make NativeList rays becomes a infinite ring list iterator
        /// </summary>
        /// <param name="cursor"></param>
        /// <param name="nextRay">iterate to next ray</param>
        /// <returns>next index</returns>
        private int NextRay(int cursor, out Ray nextRay)
        {
            int nextIdx = cursor == context.rays.Length - 1 ? 0 : cursor + 1;
            nextRay = context.rays[nextIdx];
            return nextIdx;
        }

        
    }
}

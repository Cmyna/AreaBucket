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
        /// out of conillear angle bound (in radian)
        /// </summary>
        public float angleBound;

        /// <summary>
        /// final polygon edges min length,
        /// for merge rays where their end points too close
        /// </summary>
        public float minEdgeLength;

        public void Execute()
        {
            if (context.rays.Length < 2) return;

            var cache = new NativeList<Ray>(Allocator.Temp);

            // boundary (special) case, if while loop below ends at input cursor value `rays.Length - 1`
            // may cause one ray duplicate
            cache.Add(context.rays[0]); 

            var checkVector = context.rays[1].vector - context.rays[0].vector;
            var cursor = 1;
            while(WalkConillearRays(0, cursor, checkVector, out var nextIdx, out var nextDir))
            {
                var addedIdx = nextIdx - 1;
                if (addedIdx < 0) addedIdx = context.rays.Length - 1;
                cache.Add(context.rays[addedIdx]);
                checkVector = nextDir;
                cursor = nextIdx;
            }

            // write back
            context.rays.Clear();
            for (var i = 0; i < cache.Length; i++) context.rays.Add(cache[i]);

            cache.Dispose();
        }

        /// <summary>
        /// the method will stop just one index after "breakpoint"
        /// </summary>
        /// <param name="startIdx">also the stop index (for walking)</param>
        /// <param name="currentIdx"></param>
        /// <param name="dir"></param>
        /// <param name="nextIdx">the index to next point (denoted as n1) just after the next breakpoint (denoted as n2) since the method started</param>
        /// <param name="nextDir">vector n1n2</param>
        /// <returns></returns>
        public bool WalkConillearRays(int startIdx, int currentIdx, float2 dir, out int nextIdx, out float2 nextDir)
        {
            // assume checking starts at 0
            int cursor = currentIdx;
            nextIdx = startIdx;
            nextDir = dir;
            var startRay = context.rays[cursor];
            while (true)
            {
                if (cursor == startIdx) return false; // break walk through
                var r1 = context.rays[cursor];
                cursor = NextRay(cursor, out var r2);
                var v = r2.vector - r1.vector;
                var angle = Angle(dir, v);

                // if too close, continue walk
                if (math.length(r2.vector - startRay.vector) <= minEdgeLength) continue;

                // over angle turn bound or walk back
                if (angle > angleBound || cursor == currentIdx)
                {
                    nextIdx = cursor;
                    nextDir = v; // modified ref to next dir
                    return true;
                }
            }
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

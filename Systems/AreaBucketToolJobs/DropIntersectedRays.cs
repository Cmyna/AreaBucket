using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    /// <summary>
    /// FIX: performance issue (I guessed) when filling range is too large (and too much lines)
    /// </summary>
    [BurstCompile]
    public struct DropIntersectedRays : IJob
    {
        [ReadOnly] public float2 raysStartPoint;

        [ReadOnly] public NativeList<Line2> checkLines;

        public NativeList<Ray> rays;

        public void Execute()
        {
            var raysCache = new NativeList<Ray>(Allocator.Temp);

            // drop rays has intersection with any check lines
            for (var i = 0; i < rays.Length; i++)
            {
                var rayline = new Line2()
                {
                    a = raysStartPoint,
                    b = raysStartPoint + rays[i].vector
                };
                bool hasIntersction = false;
                for (var j = 0; j < checkLines.Length; j++)
                {
                    hasIntersction = Intersect(checkLines[j], rayline);
                    if (!hasIntersction) continue;
                    else break;
                }
                if (hasIntersction) continue; // drop ray
                raysCache.Add(rays[i]);
            }

            rays.Clear();

            for (var i = 0; i < raysCache.Length; i++)
            {
                rays.Add(raysCache[i]);
            }

            raysCache.Dispose();
        }

        private bool Intersect(Line2 line1, Line2 ray)
        {
            var isParallel = !MathUtils.Intersect(ray, line1, out var t);
            if (isParallel) return false;
            // should t1 and t2 between 0-1 iff intersected: https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect/1201356#1201356
            if (UnderTollerance(0.2f, ray, t.x)) return false;
            return !UnderTollerance(0f, line1, t.y);
        }


        private bool UnderTollerance(float tolleranceDist, Line2 rayLine, float t)
        {
            if (t < 0 || t > 1) return true;
            var middle = math.lerp(rayLine.a, rayLine.b, t);
            var v1 = middle - rayLine.a;
            var v2 = rayLine.b - middle;

            var dist1 = Mathf.Sqrt(math.dot(v1, v1));
            var dist2 = Mathf.Sqrt(math.dot(v2, v2));
            var minDist = math.min(dist1, dist2);
            return minDist <= tolleranceDist;
        }
    }
}

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
    /// 
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
            // check bounds first
            if (!MathUtils.Intersect(GetBounds(line1), GetBounds(ray))) return false;

            var isParallel = !MathUtils.Intersect(ray, line1, out var t);
            if (isParallel) return false;
            // should both t.x and t.y between 0-1 iff intersected: https://stackoverflow.com/questions/563198/how-do-you-detect-where-two-line-segments-intersect/1201356#1201356
            // we allow t.x has weaker intersection check (t for ray line)
            /**
             * denote ray under tollerance as A, check line under tollerance as B
             * truth table: 
             * A, B => Intersect(A, B)
             * T, T => F
             * T, F => F
             * F, T => F
             * F, F => T
             */
            var divPoint1 = math.lerp(ray.a, ray.b, t.x);
            var dist1 = math.length(divPoint1 - ray.a);
            var dist2 = math.length(divPoint1 - ray.b);
            // if ray intersect happens nears to ray's start, the tollerance is stricter,
            // when intersect happens at far side, tollerance is weaker
            var rayUnderTollerance = dist1 < 0.2f || dist2 < 0.4f || t.x < 0 || t.x > 1;
            if (rayUnderTollerance) return false;

            return (t.y > 0 && t.y < 1);
        }


        private Bounds2 GetBounds(Line2 line)
        {
            return new Bounds2()
            {
                min = math.min(line.a, line.b),
                max = math.max(line.a, line.b)
            };
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="tolleranceDist">the tollerance distance (unit: meter)</param>
        /// <param name="rayLine"></param>
        /// <param name="t"></param>
        /// <returns></returns>
        private bool UnderTollerance(float tolleranceDist, Line2 rayLine, float t)
        {
            if (t <= 0 || t >= 1) return true;
            var middle = math.lerp(rayLine.a, rayLine.b, t);
            var v1 = middle - rayLine.a;
            var v2 = rayLine.b - middle;

            var dist1 = math.length(v1);
            var dist2 = math.length(v2);
            var minDist = math.min(dist1, dist2);
            return minDist <= tolleranceDist;
        }
    }
}

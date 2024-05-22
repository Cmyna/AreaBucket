using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Utils
{
    public static class Math
    {
        public static float MinDistance2Bounds(Bounds2 bounds, float2 position)
        {
            // FIX: the function is broken for unknown reason, maybe some invalid float value?
            var dxmin = bounds.min.x - position.x;
            var dxmax = bounds.max.x - position.x;

            var dymin = bounds.min.y - position.y;
            var dymax = bounds.max.y - position.y;

            // determines how to compute distances(min distance to corner or min distance to bounds edges)
            bool minDist2Edge = (dxmin * dxmax * dymin * dymax) >= 0;

            // the game utils can only roughly check the distance between points and bounds
            // and returns 0 if it is inside bounds
            bool insideBounds = MathUtils.Distance(bounds, position) == 0;

            float distance = float.MaxValue;

            var corners = new NativeArray<float2>(4, Allocator.Temp);
            var lines = new NativeArray<Line2>(4, Allocator.Temp);
            if (minDist2Edge)
            {
                Lines(bounds, ref corners, ref lines);
                for (var i = 0; i < lines.Length; i++)
                {
                    var line = lines[i];
                    distance = math.min(distance, MathUtils.Distance(line, position, out var _));
                }
            }
            else
            {
                Corners(bounds, ref corners);
                for (var i = 0; i < corners.Length; i++)
                {
                    var corner = corners[i];
                    distance = math.min(distance, math.distance(corner, position));
                }
            }

            if (insideBounds) distance *= -1;


            corners.Dispose();
            lines.Dispose();
            return distance;

        }

        private static void Corners(Bounds2 bounds, ref NativeArray<float2> res)
        {
            res[0] = new float2(bounds.min.x, bounds.min.y);
            res[1] = new float2(bounds.max.x, bounds.min.y);
            res[2] = new float2(bounds.max.x, bounds.max.y);
            res[3] = new float2(bounds.min.x, bounds.max.y);
        }

        public static void Lines(Bounds2 bounds, ref NativeArray<float2> corners, ref NativeArray<Line2> lines)
        {
            Corners(bounds, ref corners);
            lines[0] = new Line2() { a = corners[0], b = corners[1] };
            lines[1] = new Line2() { a = corners[1], b = corners[2] };
            lines[2] = new Line2() { a = corners[2], b = corners[3] };
            lines[3] = new Line2() { a = corners[3], b = corners[0] };
        }

        /// <summary>
        /// compute radian from a to b
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <param name="clampBounds"></param>
        /// <returns></returns>
        public static float RadianInClock(float2 a, float2 b, float clampBounds = float.MinValue)
        {
            var det = a.x * b.y - a.y * b.x;
            //if (math.abs(det) < clampBounds) return 0;
            bool isCounterClock = det < 0;
            var rad = Mathf.Deg2Rad * Vector2.Angle(a, b);
            if (isCounterClock)
            {
                rad = Mathf.PI * 2 - rad;
            }

            return rad;
        }


        public static bool CheckIntersection(Line2 line1, Line2 line2)
        {
            return _IntersectHelper(line1.a, line1.b, line2.a, line2.b) && _IntersectHelper(line2.a, line2.b, line1.a, line1.b);
        }

        private static bool _IntersectHelper(float2 a, float2 b, float2 c, float2 d)
        {
            var ab = b - a;
            var ac = c - a;
            var ad = d - a;

            var abXac = ab.x * ac.y - ab.y * ac.x;
            var abXad = ab.x * ad.y - ab.y * ad.x;

            return abXac * abXad < 0;
        }

    }
}

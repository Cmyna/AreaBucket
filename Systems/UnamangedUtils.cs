using Colossal.Mathematics;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems
{
    /// <summary>
    /// untilities methods for unmanaged call (Job, System, etc.)
    /// </summary>
    public static class UnamangedUtils
    {

        /// <summary>
        /// collect one or two cutting points of circle and lines, 
        /// which the circle use hitPos at center, range as cirlce range <br/>
        /// if the line only cut one point, use end point of line instead <br/>
        /// assume line is (A, B), hitPos -> O, range -> r, solve this equation: |tAB + OA| = r <br/>
        /// we can get ( -(AB * OA) +- sqrt(term1) ) / AB^2 <br/>
        /// term1 = (AB * OA)^2 - AB^2*OA^2 + AB^2*r^2
        /// </summary>
        /// <param name="line"></param>
        public static bool CollectDivPoints(
            Line2 line, 
            float2 hitPos, 
            float filterRange,
            out float2 p1, 
            out float2 p2
        ) {
            p1 = default; p2 = default;

            var ab = line.b - line.a;
            var oa = line.a - hitPos;
            var abDoa = math.dot(ab, oa);
            var abSqr = math.lengthsq(ab);
            var rSqr = filterRange * filterRange;
            var oaSqr = math.lengthsq(oa);

            // term sqrt(b^2 - 4ac) / 4 in Quadratic formula
            var term1 = abDoa * abDoa - abSqr * oaSqr + abSqr * rSqr;
            if (term1 < 0) return false; // no solution(no cutting points)

            var term2 = math.sqrt(term1);

            var t2 = (-abDoa + term2) / abSqr; // point should near B
            var t1 = (-abDoa - term2) / abSqr; // point should near A

            // check t1, t2 on the line or not
            if (t1 >= 0 && t1 <= 1) p1 = math.lerp(line.a, line.b, t1);
            else p1 = line.a;

            if (t2 >= 0 && t2 <= 1) p2 = math.lerp(line.a, line.b, t2);
            else p2 = line.b;


            /*if (t1 >= 0 && t1 <= 1) pointList.Add(math.lerp(line.a, line.b, t1));
            else pointList.Add(line.a);

            if (t2 >= 0 && t2 <= 1) pointList.Add(math.lerp(line.a, line.b, t2));
            else pointList.Add(line.b);*/

            return true;
        }

        public static void BuildPolylines(
            NativeList<float2> points, 
            NativeList<Line2.Segment> lines
        )
        {
            lines.Clear();
            for (int i = 0; i < points.Length; i++)
            {
                var i1 = i;
                var i2 = (i + 1) % points.Length;
                var p1 = points[i1];
                var p2 = points[i2];
                lines.Add(new Line2.Segment(p1, p2));
            }
        }

        public static bool Between(float a, float min, float max)
        {
            return a >= min && a <= max;
        }

        public static Bounds2 GetBounds(Line2 line)
        {
            return new Bounds2()
            {
                min = math.min(line.a, line.b),
                max = math.max(line.a, line.b)
            };
        }

        public static void FindLargestSector(NativeList<AreaBucketToolJobs.Ray> sortedRays, out float maxSectorRadian, out int maxSectorIndex)
        {
            //var sortedRays = context.rays;
            maxSectorRadian = 0;
            maxSectorIndex = -1;
            for (var i = 0; i < sortedRays.Length; i++)
            {
                float a = sortedRays[i].radian;
                float b;
                float rayDiff;
                if (i == sortedRays.Length - 1)
                {
                    b = sortedRays[0].radian;
                    rayDiff = b + Mathf.PI * 2 - a;
                }
                else
                {
                    b = sortedRays[i + 1].radian;
                    rayDiff = b - a;
                }
                if (rayDiff > maxSectorRadian)
                {
                    maxSectorRadian = rayDiff;
                    maxSectorIndex = i + 1;
                }
            }

            if (maxSectorIndex == sortedRays.Length) maxSectorIndex = 0;

        }

    }

}

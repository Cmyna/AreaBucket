using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Game.Net;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    public struct FilterExposedPolylines : IJob
    {
        public CommonContext context;

        public GeneratedArea generatedAreaData;

        public NativeList<Line2> exposedLines;

        public float collinearTollerance;
        public void Execute()
        {
            for (int i = 0; i < generatedAreaData.polyLines.Length; i++)
            {
                var line = generatedAreaData.polyLines[i];
                var middle = math.lerp(line.a, line.b, 0.5f);
                var vector = line.b - line.a;

                if (math.length(vector) < 0.5f) continue;

                // if both two end points reach filling max range, drop it
                /*var fillingRange = context.filterRange - 1; // -1: prevent twinkling
                var overFillingRange = (math.length(line.a - context.hitPos) > fillingRange) &&
                   (math.length(line.b - context.hitPos) > fillingRange);
                if (overFillingRange) continue;*/

                var exposed = true;
                for (int j = 0; j < context.usedBoundaryLines.Length; j++)
                {
                    var boundaryLine = context.usedBoundaryLines[j];
                    if (!MathUtils.Intersect(GetBounds(boundaryLine), GetBounds(line))) continue;

                    var pVector = Utils.Math.Perpendicular(vector, 0.5f);
                    var p1 = middle + pVector;
                    var p2 = middle - pVector;

                    var line2 = new Line2(p1, p2);

                    // check if line2 intersect with boundaryLine
                    MathUtils.Intersect(line2, boundaryLine, out var t);
                    if (Between(t.x, 0, 1) && Between(t.y, 0, 1))
                    {
                        exposed = false;
                        break;
                    }

                    if (!Collinear(line, boundaryLine)) continue;



                    MathUtils.Distance(line, boundaryLine.a, out var t1);
                    MathUtils.Distance(line, boundaryLine.b, out var t2);
                    MathUtils.Distance(boundaryLine, line.a, out var t3);
                    MathUtils.Distance(boundaryLine, line.a, out var t4);

                    var overlap = Between(t1, 0, 1) || Between(t2, 0, 1) || Between(t3, 0, 1) || Between(t4, 0, 1);
                    exposed &= !overlap;
                    if (!overlap) continue;
                    else break;
                }
                if (exposed) exposedLines.Add(line);
            }
        }


        private bool Between(float a, float from, float to)
        {
            return a >= from && a <= to;
        }


        private bool Collinear(Line2 line1, Line2 line2)
        {
            var a = line1.a;
            var b = line1.b;
            var c = line2.a;
            var d = line2.b;

            var ab = b - a;
            var ac = c - a;
            var ad = d - a;

            // cross products
            var c1 = ab.x * ac.y - ab.y * ac.x;
            var c2 = ab.x * ad.y - ab.y * ad.x;

            // check (ab, ac) collinear, and (ab,ad) collinear (with tollerance)
            return (math.abs(c1) < collinearTollerance) && (math.abs(c2) < collinearTollerance);

        }


        private Bounds2 GetBounds(Line2 line)
        {
            return new Bounds2()
            {
                min = math.min(line.a, line.b),
                max = math.max(line.a, line.b)
            };
        }
    }
}

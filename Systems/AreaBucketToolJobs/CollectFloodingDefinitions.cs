using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Game.Net;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    public struct CollectFloodingDefinitions : IJob
    {
        public CommonContext context;

        public GeneratedArea generatedAreaData;

        public NativeList<Line2> exposedLines;

        public NativeList<FloodingDefinition> floodingDefintions;

        public float collinearTollerance;

        public NativeReference<int2> rfCheckRange;

        public CollectFloodingDefinitions Init(
            CommonContext context,
            GeneratedArea generatedAreaData,
            NativeList<Line2> exposedLines, 
            NativeList<FloodingDefinition> floodingDefinitions,
            float collinearTollerance,
            NativeReference<int2> rfCheckRange
        ) {
            this.context = context;
            this.generatedAreaData = generatedAreaData;
            this.exposedLines = exposedLines;
            this.floodingDefintions = floodingDefinitions;
            this.collinearTollerance = collinearTollerance;
            this.rfCheckRange = rfCheckRange;
            return this;
        }

        public void Execute()
        {
            
            //var usableFloodingDefs = new NativeList<FloodingDefinition>(Allocator.Temp);

            // because the generated poly line may reach another flooding candidate line,
            // which is hard to be excluded in DropInteresectedRays job
            // hense we should check those def here
            /*for (int i = 0; i < floodingDefintions.Length; i++)
            {
                var floodingDef = floodingDefintions[i];
                var line = floodingDef.floodingSourceLine;
                var vector = line.b - line.a;
                var middle = math.lerp(line.a, line.b, 0.5f);

                var exposed = true;
                for (int j = 0; j < generatedAreaData.polyLines.Length; j++)
                {
                    // if satisfy this condition, means it is flooding candidate source line
                    if (j == floodingDef.newAreaPointInsertStartIndex) continue;

                    var boundaryLine = generatedAreaData.polyLines[j];
                    if (FoundIntersection(line, vector, middle, boundaryLine))
                    {
                        exposed = false;
                        break;
                    }
                }
                if (exposed) usableFloodingDefs.Add(floodingDef);
            }*/

            var newPointsIndicesRange = rfCheckRange.Value;
            var lowerBound = math.max(newPointsIndicesRange.x - 1, 0);
            var upperBound = math.min(newPointsIndicesRange.y + 1, generatedAreaData.polyLines.Length);

            for (int i = lowerBound; i < upperBound; i++)
            {
                var line = generatedAreaData.polyLines[i];
                var vector = line.b - line.a;
                if (math.length(vector) < 0.5f) continue;
                if (FoundIntersection(line, context.usedBoundaryLines)) continue;
                floodingDefintions.Add(NewFloodingDef(line, i));
                exposedLines.Add(line);
            }

            

            //floodingDefintions.Clear();
            //floodingDefintions.AddRange(usableFloodingDefs.AsArray());

            //usableFloodingDefs.Dispose();
        }



        private bool FoundIntersection(Line2 line, NativeList<Line2> boundaryLines)
        {
            var vector = line.b - line.a;
            var middle = math.lerp(line.a, line.b, 0.5f);
            for (int j = 0; j < boundaryLines.Length; j++)
            {
                var boundaryLine = boundaryLines[j];
                if (FoundIntersection(line, vector, middle, boundaryLine)) return true;

                /*if (!MathUtils.Intersect(GetBounds(boundaryLine), GetBounds(line))) continue;

                var pVector = Utils.Math.Perpendicular(vector, 0.5f);
                var p1 = middle + pVector;
                var p2 = middle - pVector;

                var line2 = new Line2(p1, p2);

                // check if line2 intersect with boundaryLine
                MathUtils.Intersect(line2, boundaryLine, out var t);
                if (Between(t.x, 0, 1) && Between(t.y, 0, 1))
                {
                    return false;
                }*/
            }
            return false;
        }
        private bool FoundIntersection(Line2 line, float2 vector, float2 middle, Line2 boundaryLine)
        {
            if (!MathUtils.Intersect(UnamangedUtils.GetBounds(boundaryLine), UnamangedUtils.GetBounds(line))) return false;
            var pVector = Utils.Math.Perpendicular(vector, 0.5f);
            var p1 = middle + pVector;
            var p2 = middle - pVector;

            var line2 = new Line2(p1, p2);

            // check if line2 intersect with boundaryLine
            MathUtils.Intersect(line2, boundaryLine, out var t);
            return Between(t.x, 0, 1) && Between(t.y, 0, 1);
        }


        private FloodingDefinition NewFloodingDef(Line2 line, int insertStartIndex)
        {
            var vector = line.b - line.a;
            var middle = math.lerp(line.a, line.b, 0.5f);
            float2 xzUp = new float2(0, 1);
            // FIX: it is better to move "out side of" flooded polygons, it should be left hande side (counter clock wise) of line vector
            // well it seems should be right hand side / clockwise ...
            var v = Utils.Math.PerpendicularClockwise(vector, 0.1f);
            var startPoint = middle + v;

            var r1 = Utils.Math.RadianInClock(xzUp, line.a - startPoint);
            var r2 = Utils.Math.RadianInClock(xzUp, line.b - startPoint);
            var floodingDef = new FloodingDefinition
            {
                rayStartPoint = startPoint,
                floodingSourceLine = line,
                floodRadRange = new float2(r1, r2),
                newAreaPointInsertStartIndex = insertStartIndex,
                floodingDepth = context.floodingDefinition.floodingDepth + 1
            };
            return floodingDef;
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
    }
}

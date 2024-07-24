using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct CollectFloodingDefinitions : IJob
    {
        public CommonContext context;

        public GeneratedArea generatedAreaData;


        public NativeList<FloodingDefinition> floodingDefintions;

        /// <summary>
        /// rf: abbr for NativeReference type
        /// it declares the index range of collecting exposed flooding candidates
        /// </summary>
        public NativeReference<int2> rfCheckRange;

        public CollectFloodingDefinitions Init(
            CommonContext context,
            GeneratedArea generatedAreaData,
            NativeList<FloodingDefinition> floodingDefinitions,
            NativeReference<int2> rfCheckRange
        ) {
            this.context = context;
            this.generatedAreaData = generatedAreaData;
            this.floodingDefintions = floodingDefinitions;
            this.rfCheckRange = rfCheckRange;
            return this;
        }

        public void Execute()
        {

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
            }

        }



        private bool FoundIntersection(Line2 line, NativeList<Line2> boundaryLines)
        {
            var vector = line.b - line.a;
            var middle = math.lerp(line.a, line.b, 0.5f);
            for (int j = 0; j < boundaryLines.Length; j++)
            {
                var boundaryLine = boundaryLines[j];
                if (FoundIntersection(line, vector, middle, boundaryLine)) return true;
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
            return UnamangedUtils.Between(t.x, 0, 1) && UnamangedUtils.Between(t.y, 0, 1);
        }


        private FloodingDefinition NewFloodingDef(Line2 line, int insertStartIndex)
        {
            var vector = line.b - line.a;
            var middle = math.lerp(line.a, line.b, 0.5f);
            float2 xzUp = new float2(0, 1);
            // TODO: while offset 'outside', some consequence is several final generated points almost collinear and break the polygon shape
            // which happens on flooding depth > 1 and flooding start line collinear to other boundaries
            // one way is move flooding inside and disable flooding candidate line's intersection checking
            // FIX: it is better to move "out side of" flooded polygons, it should be left hande side (counter clock wise) of line vector
            // well it seems should be right hand side / clockwise ...
            // var v = Utils.Math.PerpendicularClockwise(vector, 0.1f);
            var v = Utils.Math.PerpendicularCounterClockwise(vector, 2f);
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
    }
}

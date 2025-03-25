using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Collections;
using Colossal.Mathematics;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;


namespace AreaBucket.Systems.AreaBucketToolJobs
{

    /// <summary>
    /// generate new points from lines' intersections
    /// </summary>
    [BurstCompile]
    public struct GenIntersectedPoints : IJob
    {
        public CommonContext context;

        public GenIntersectedPoints Init(CommonContext context)
        {
            this.context = context;
            return this;
        }

        public void Execute()
        {
            
            // TODO: just use brutal intersection check now, should be optimized for performance
            for (int i = 0; i < context.usedBoundaryLines.Length; i++)
            {
                for (int j = i + 1; j < context.usedBoundaryLines.Length; j++)
                {
                    var line1 = context.usedBoundaryLines[i];
                    var line2 = context.usedBoundaryLines[j];

                    if (!MathUtils.Intersect(GetBounds(line1), GetBounds(line2))) continue;

                    var isParallel = !MathUtils.Intersect(line1, line2, out var t);
                    if (isParallel) continue;

                    // if they are not intersected or just touching
                    if (t.x <= 0 || t.x >= 1 || t.y <= 0 || t.y >= 1) continue;

                    // add one point
                    var p = math.lerp(line1.a, line1.b, t.x);
                    context.points.Add(p);
                }
            }
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

using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Net;
using Unity.Burst;
using Unity.Collections;
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


    [BurstCompile]
    public struct GenIntersectedPoints2: IJob
    {
        public CommonContext context;


        private struct IntersectionChecker : IIntersectionChecker
        {
            Bounds2 baseBounds;
            public Line2.Segment segment;

            public IntersectionChecker(Line2.Segment s)
            {
                this.baseBounds = MathUtils.Bounds(s);
                this.segment = s;
            }

            public bool Intersect(Bounds2 bounds)
            {
                return MathUtils.Intersect(bounds, this.baseBounds);
            }

            public bool Intersect(Bounds2 bounds, EquatableSegment segment)
            {
                if (math.all(this.segment.ab == segment.segment.ab)) return false;
                return MathUtils.Intersect(this.segment, segment.segment, out var t);
            }
        }

        public GenIntersectedPoints2 Init(CommonContext context)
        {
            this.context = context;
            return this;
        }


        public void Execute()
        {
            var enumerator1 = this.context.usedBoundaryLines2.GetEnumerator(Allocator.Temp, new AllTrueChecker());
            while (enumerator1.Next(out var s1))
            {
                var enumerator2 = this.context.usedBoundaryLines2.GetEnumerator(Allocator.Temp, new IntersectionChecker(s1));
                while (enumerator2.Next(out var s2))
                {
                    MathUtils.Intersect(s1, s2, out var t);
                    var p = math.lerp(s1.a, s1.b, t.x);
                    context.points.Add(p);
                }
            }
        }
    }
}

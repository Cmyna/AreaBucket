using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Collections;
using Colossal.Mathematics;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


namespace AreaBucket.Systems.AreaBucketToolJobs
{

    /// <summary>
    /// 
    /// </summary>
    [BurstCompile]
    public struct DropIntersectedRays : IJob
    {
        public CommonContext context;

        public DebugContext debugContext;

        public float2 rayTollerance;

        public SingletonData signletonData;

        public DropIntersectedRays Init(
            CommonContext context, 
            DebugContext debugContext, 
            float2 rayTollerance, 
            SingletonData singletonData
        ) {
            this.context = context;
            this.debugContext = debugContext;
            this.rayTollerance = rayTollerance;
            this.signletonData = singletonData;
            return this;
        }

        public void Execute()
        {
            var raysCache = new NativeList<Ray>(Allocator.Temp);
            var rayStartPos = context.floodingDefinition.rayStartPoint;

            // drop rays has intersection with any check lines
            for (var i = 0; i < context.rays.Length; i++)
            {
                var ray = context.rays[i];
                var rayline = new Line2()
                {
                    a = rayStartPos,
                    b = rayStartPos + ray.vector
                };
                bool hasIntersection = false;
                for (var j = 0; j < context.usedBoundaryLines.Length; j++)
                {
                    hasIntersection = RayIntersectionHelper.IsSoftIntersect(rayline, context.usedBoundaryLines[j], rayTollerance);
                    if (hasIntersection)
                    {
                        debugContext.intersectedLines.Add(context.usedBoundaryLines[j]);
                        debugContext.intersectedRays.Add(rayline);
                        break;
                    }
                }
                if (hasIntersection) continue; // drop ray
                raysCache.Add(ray);
            }

            context.rays.Clear();

            for (var i = 0; i < raysCache.Length; i++)
            {
                context.rays.Add(raysCache[i]);
            }

            raysCache.Dispose();
        }

    }

    [BurstCompile]
    public struct DropIntersectedRays2 : IJob
    {

        private struct TreeIterator : INativeQuadTreeIterator<EquatableSegment, Bounds2>
        {
            public Line2.Segment ray;

            public Bounds2 bounds;

            public NativeReference<bool> hasIntersection;

            public NativeReference<Line2.Segment> intersectedLine;

            public float2 tollerances;

            public TreeIterator(
                Line2.Segment ray, 
                NativeReference<bool> hasIntersection, 
                NativeReference<Line2.Segment> intersectedLine,
                float2 tollerances
                )
            {
                this.ray = ray;
                this.bounds = MathUtils.Bounds(ray);
                this.hasIntersection = hasIntersection;
                this.intersectedLine = intersectedLine;
                this.tollerances = tollerances;
            }

            public bool Intersect(Bounds2 b)
            {
                if (hasIntersection.Value) return false;
                return MathUtils.Intersect(b, this.bounds);
            }

            public void Iterate(Bounds2 bounds, EquatableSegment item)
            {
                if (hasIntersection.Value) return;
                hasIntersection.Value = RayIntersectionHelper.IsSoftIntersect(this.ray, item.segment, this.tollerances);
                if (hasIntersection.Value) intersectedLine.Value = item.segment;
            }
        }

        public CommonContext context;

        public DebugContext debugContext;

        public float2 rayTollerance;

        public SingletonData signletonData;

        public DropIntersectedRays2 Init(
            CommonContext context,
            DebugContext debugContext,
            float2 rayTollerance,
            SingletonData singletonData
        )
        {
            this.context = context;
            this.debugContext = debugContext;
            this.rayTollerance = rayTollerance;
            this.signletonData = singletonData;
            return this;
        }

        public void Execute()
        {
            var raysCache = new NativeList<Ray>(Allocator.Temp);
            var rayStartPos = context.floodingDefinition.rayStartPoint;
            var hasIntersection = new NativeReference<bool>(Allocator.Temp);
            var intersectedLine = new NativeReference<Line2.Segment>(Allocator.Temp);
            // drop rays has intersection with any check lines
            for (var i = 0; i < context.rays.Length; i++)
            {
                var ray = context.rays[i];
                var raySegment = new Line2.Segment(rayStartPos, rayStartPos + ray.vector);
                hasIntersection.Value = false;
                var it = new TreeIterator(raySegment, hasIntersection, intersectedLine, this.rayTollerance);
                context.usedBoundaryLines2.Iterate(ref it);
                if (hasIntersection.Value)
                {
                    debugContext.AddRaySegment(raySegment);
                    debugContext.AddIntersectedSegment(intersectedLine.Value);
                    continue; // drop ray
                }
                raysCache.Add(ray);
            }

            context.rays.Clear();
            context.rays.AddRange(raysCache.AsArray());
        }
    }


    public struct RayIntersectionHelper
    {

        public static bool IsSoftIntersect(Line2 ray, Line2 s2, float2 tollerances)
        {
            return IsSoftIntersect(
                new Line2.Segment(ray.a, ray.b),
                new Line2.Segment(s2.a, s2.b),
                tollerances);
        }

        public static bool IsSoftIntersect(Line2.Segment ray, Line2.Segment s2, float2 tollerances)
        {
            // segment intersect checking has wierd bounds check issue, hense use line intersect function
            var isParallel = !MathUtils.Intersect(new Line2(ray.a, ray.b), new Line2(s2.a, s2.b), out var t);
            if (isParallel) return false;
            if (math.any(t < 0 | t > 1)) return false;

            var divPoint1 = math.lerp(ray.a, ray.b, t.x);
            var dist1 = math.length(divPoint1 - ray.a);
            var dist2 = math.length(divPoint1 - ray.b);
            // if intersect under start/end tollerance, the regard it as not intersect
            return !(dist1 < tollerances.x || dist2 < tollerances.y);
        }
    }
}

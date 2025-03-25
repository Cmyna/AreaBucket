using Colossal.Collections;
using Colossal.Mathematics;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


namespace AreaBucket.Systems.AreaBucketToolJobs.JobData
{
    using BoundariesTree = NativeQuadTree<EquatableSegment, Bounds2>;

    /// <summary>
    /// Line2.Segment implements IEquatable
    /// </summary>
    public struct EquatableSegment : IEquatable<EquatableSegment>
    {
        public Line2.Segment segment;

        public EquatableSegment(float2 a, float2 b)
        {
            this.segment = new Line2.Segment(a, b);
        }

        public bool Equals(EquatableSegment other)
        {
            return math.all(segment.ab == segment.ab);
        }

        public override int GetHashCode()
        {
            // should override GetHashCode to unmanaged method
            return segment.ab.GetHashCode();
        }
    }


    public static class NativeQuadTreeHelper
    {
        public static JobHandle Dispose<TI, TB>(this NativeQuadTree<TI, TB> tree, JobHandle inputDeps)
            where TI : unmanaged, IEquatable<TI>
            where TB : unmanaged, IEquatable<TB>, IBounds2<TB>
        {
            if (!tree.IsCreated) return inputDeps;
            NativeQuadTreeDisposeJob<TI, TB> disposeJob = default;
            disposeJob.tree = tree;
            return disposeJob.Schedule(inputDeps);
        }

        public static bool AddSegment(this BoundariesTree tree, float2 a, float2 b)
        {
            var bounds = new Bounds2(math.min(a, b), math.max(a, b));
            return tree.TryAdd(new EquatableSegment(a, b), bounds);
        }

        public static void CollectBoundaries(this BoundariesTree tree, NativeList<Line2.Segment> result)
        {
            var it = new FullBoundariesLinesIterator(result);
            tree.Iterate(ref it);
        }
    }

    public struct FullBoundariesLinesIterator : INativeQuadTreeIterator<EquatableSegment, Bounds2>
    {
        public NativeList<Line2.Segment> results;

        public FullBoundariesLinesIterator(NativeList<Line2.Segment> results)
        {
            this.results = results;
        }

        public bool Intersect(Bounds2 bounds)
        {
            return true;
        }

        public void Iterate(Bounds2 bounds, EquatableSegment item)
        {
            results.Add(item.segment);
        }
    }


    struct NativeQuadTreeDisposeJob<TItem, TBounds> : IJob
        where TItem : unmanaged, IEquatable<TItem>
        where TBounds : unmanaged, IEquatable<TBounds>, IBounds2<TBounds>
    {

        public NativeQuadTree<TItem, TBounds> tree;
        public void Execute()
        {
            tree.Dispose();
        }

    }
}
using Colossal.Collections;
using Colossal.Mathematics;
using System;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


namespace AreaBucket.Systems.AreaBucketToolJobs.JobData
{
    using BoundariesTree = NativeQuadTree<EquatableSegment, Bounds2>;
    using BoundariesIterator = INativeQuadTreeIterator<EquatableSegment, Bounds2>;

    public interface IIntersectionChecker
    {
        bool Intersect(Bounds2 bounds);

        bool Intersect(Bounds2 bounds, EquatableSegment segment);
    }

    public struct AllTrueChecker : IIntersectionChecker
    {
        public bool Intersect(Bounds2 bounds)
        {
            return true;
        }

        public bool Intersect(Bounds2 bounds, EquatableSegment segment)
        {
            return true;
        }
    }

    public struct BoundariesCollections : IDisposable, INativeDisposable
    {
        private struct DisposeJob : IJob
        {
            private BoundariesCollections collections;
            public DisposeJob(BoundariesCollections collections)
            {
                this.collections = collections;
            }
            public void Execute()
            {
                this.collections.Dispose();
            }
        }

        public struct Enumerator<TChecker> : BoundariesIterator where TChecker: IIntersectionChecker
        {
            private NativeQueue<Line2.Segment> collectionQueue;

            private TChecker checker;

            public Enumerator(Allocator allocator, TChecker checker)
            {
                this.collectionQueue = new NativeQueue<Line2.Segment>(allocator);
                this.checker = checker;
            }

            public bool Next(out Line2.Segment segment)
            {
                segment = default;
                return collectionQueue.TryDequeue(out segment);
            }

            public bool HasNext()
            {
                return !collectionQueue.IsEmpty();
            }

            public bool Intersect(Bounds2 bounds)
            {
                return checker.Intersect(bounds);
            }

            public void Iterate(Bounds2 bounds, EquatableSegment item)
            {
                if (!checker.Intersect(bounds, item)) return;
                collectionQueue.Enqueue(item.segment);
            }
        }


        public BoundariesTree tree;

        public NativeList<EquatableSegment> collisionCache;

        public BoundariesCollections(Allocator allocator)
        {
            this.tree = new BoundariesTree(1f, allocator);
            this.collisionCache = new NativeList<EquatableSegment>(allocator);
        }

        public void Add(float2 a, float2 b)
        {
            var item = new EquatableSegment(a, b);
            if (!tree.TryAdd(item, MathUtils.Bounds(item.segment) ) )
            {
                collisionCache.Add(item);
            }
        }

        public void Iterate<TIterator>(ref TIterator iterator) where TIterator: BoundariesIterator
        {
            tree.Iterate(ref iterator);
            // iterate through collision cache
            for (var i = 0; i < this.collisionCache.Length; i++)
            {
                var segment = this.collisionCache[i];
                var bounds = MathUtils.Bounds(segment.segment);
                if (!iterator.Intersect(bounds)) continue;
                iterator.Iterate(bounds, segment);
            }
        }

        public NativeList<Line2.Segment> AllToList(Allocator allocator)
        {
            var result = new NativeList<Line2.Segment>(allocator);
            var enumerator = GetEnumerator(Allocator.Temp, new AllTrueChecker());
            while (enumerator.Next(out var segment)) result.Add(segment);
            return result;
        }

        public Enumerator<TChecker> GetEnumerator<TChecker>(
            Allocator allocator, 
            TChecker checker
        ) where TChecker : IIntersectionChecker
        {
            var enumerator = new Enumerator<TChecker>(allocator, checker);
            Iterate(ref enumerator);
            return enumerator;
        }

        public int CollisionCount()
        {
            return collisionCache.Length;
        }

        public void Clear()
        {
            tree.Clear();
            collisionCache.Clear();
        }

        public void Dispose()
        {
            if (collisionCache.IsCreated) collisionCache.Dispose();
            if (tree.IsCreated) tree.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            return new DisposeJob(this).Schedule(inputDeps);
        }


    }

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
            var s = new EquatableSegment(a, b);
            var bounds = MathUtils.Bounds(s.segment);
            return tree.TryAdd(s, bounds);
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
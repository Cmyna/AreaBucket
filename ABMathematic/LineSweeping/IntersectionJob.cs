using AreaBucket.Mathematics.NativeCollections;
using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Index = System.Int32;

namespace ABMathematics.LineSweeping
{
    public struct IntersectionJob : IJob, IDisposable
    {


        public class Debuger
        {
            public IntersectionJob job;
            public List<SweepEvent> history;
            public Debuger(IntersectionJob job)
            {
                this.job = job;
                history = new List<SweepEvent>();
            }


            public void Record(SweepEvent e)
            {
                history.Add(e);
            }


            public (int, SweepEvent)[] Query(int segmentIndex)
            {
                var result = new List<(int, SweepEvent)>();
                for (var i = 0; i < history.Count; i++)
                {
                    var e = history[i];
                    if (e.segmentPointers.x != segmentIndex && e.segmentPointers.y != segmentIndex) continue;
                    result.Add((i, e));
                }
                return result.ToArray();
            }

            public bool CheckOrder(out string abs)
            {
                var hasDiff = false;
                var result1 = CurrentOrder();
                var result2 = ActualOrder();

                //currentAbs = SegmentIndicesAbstract(result1);
                //actualAbs = SegmentIndicesAbstract(result2);
                var diffStart = int.MaxValue;
                var diffEnd = int.MinValue;

                for (int i = 0; i < result1.Length; i++)
                {
                    hasDiff |= result1[i] != result2[i];
                    if (result1[i] != result2[i])
                    {
                        diffStart = math.min(i, diffStart);
                        diffEnd = math.max(i, diffEnd);
                    }
                }

                // expand range
                var i1 = math.max(0, diffStart - 2);
                var i2 = math.min(result1.Length - 1, diffEnd + 2);

                abs = "order check pass";
                if (i1 < result1.Length && i2 >= 0)
                {
                    var diffData = new Index[i2 - i1 + 1];

                    abs = $"difference between [{diffStart}, {diffEnd}]\n";
                    Array.Copy(result1, i1, diffData, 0, i2 - i1 + 1);
                    abs += $"current order: {SegmentIndicesAbstract(diffData)}\n";
                    Array.Copy(result2, i1, diffData, 0, i2 - i1 + 1);
                    abs += $"actual order: {SegmentIndicesAbstract(diffData)}\n";
                }


                return !hasDiff;
            }

            public (string, string) Order(float x)
            {
                var currentX = job.comparer.X;
                job.comparer.UpdateX(x);

                var result1 = CurrentOrder();
                var result2 = ActualOrder();

                job.comparer.UpdateX(currentX);

                return (
                    SegmentIndicesAbstract(result1),
                    SegmentIndicesAbstract(result2)
                    );
            }


            public int CompareAt(float x, int i1, int i2)
            {
                var currentX = job.comparer.X;
                job.comparer.UpdateX(x);

                var result = job.comparer.Compare(i1, i2);

                job.comparer.UpdateX(currentX);

                return result;
            }


            public float2 GetIntersect(int i1, int i2)
            {
                job.CheckIntersect(job.segments[i1], job.segments[i2], out float2 result);
                return result;
            }


            public int[] CurrentOrder()
            {
                return GetSegmentsIndicesInTree();
            }

            /// <summary>
            /// recalculate all order
            /// </summary>
            /// <returns></returns>
            /// <exception cref="NotImplementedException"></exception>
            public int[] ActualOrder()
            {
                var indices = GetSegmentsIndicesInTree();
                // stable sort
                return indices.OrderBy((i) => i, job.comparer).ToArray();
            }


            private string SegmentIndicesAbstract(int[] indices)
            {
                var result = $"";
                foreach (int i in indices)
                {
                    result += $"{i}, ";
                }
                return result;
            }


            private int[] GetSegmentsIndicesInTree()
            {
                var num = job.tree.Count();
                var result = new int[num];
                for (int k = 1; k <= num; k++)
                {
                    var (_, _, value) = job.tree.AsDebuger().Kth(k);
                    result[k-1] = value;
                }
                return result;
            }

           
        }


        private NativeArray<Line2.Segment> segments;

        private NativeSegmantComparer comparer;

        private NativeHeap<SweepEvent> eventQueue;

        private NativeSplayTree<Index, NativeSegmantComparer> tree;

        public NativeList<float2> result;

        /// <summary>
        /// temp buffer for swapping (re-ordering)
        /// </summary>
        private NativeList<Index> tempBuffer; 

        private NativeReference<Index> eventsCounter;

        private readonly float eps;

        private Index EventsCounter 
        {
            get => eventsCounter.Value;
            set => eventsCounter.Value = value;
        }
   

        public IntersectionJob(
            NativeArray<Line2.Segment> segments, 
            NativeList<float2> result, 
            Allocator allocator = Allocator.Temp,
            float eps = 0.01f
            )
        {
            this.eps = eps;
            this.result = result;
            this.segments = segments;
            comparer = new NativeSegmantComparer(segments, allocator, eps);
            eventQueue = new NativeHeap<SweepEvent>(segments.Length / 10 + 10, allocator);
            tree = new NativeSplayTree<Index, NativeSegmantComparer>(comparer, segments.Length * 2, allocator);
            tempBuffer = new NativeList<Index>(allocator);
            eventsCounter = new NativeReference<Index>(allocator);
        }


        public void Execute2()
        {
            int eventsUpperbound = (segments.Length - 1) * segments.Length / 2 + segments.Length * 2;
            var debuger = new Debuger(this);

            InitStartEnds();

            EventsCounter = 0;
            SweepEvent lastEvent = default;
            while (eventQueue.Pop(out var nextEvent))
            {
                
                if (EventsCounter > eventsUpperbound) throw new Exception("Assertion Error: Over UpperBound!");
                EventsCounter++;

                comparer.UpdateX(nextEvent.posXZ.x); // update comparer x value

                if (SkipEvent(lastEvent, nextEvent))
                {
                    debuger.Record(nextEvent);
                    continue;
                }

                // Reorder(new int2(lastEvent.kRange.x - 500, lastEvent.kRange.y + 500));

                nextEvent = NextEvent(lastEvent, nextEvent);
                debuger.Record(nextEvent);
                lastEvent = nextEvent;
            }
        }


        public void Execute()
        {
            string abs;

            int eventsUpperbound = (segments.Length - 1) * segments.Length / 2 + segments.Length * 2;

            // insert all segments start end event
            for (int i = 0; i < segments.Length; i++)
            {
                InsertStartEndEvent(i, segments[i], ref eventQueue);
            }

            int k;
            int2 kRange;
            SweepEvent lastEvent = default;
            bool hasPre, hasNext;

            var debuger = new Debuger(this);

            while (eventQueue.Pop(out var sweepEvent))
            {
                debuger.Record(sweepEvent);
                
                if (EventsCounter > eventsUpperbound) throw new Exception("Assertion Error: Over UpperBound!");
                EventsCounter++;

                // if duplicate event: skip
                if (SkipEvent(sweepEvent, lastEvent)) continue;

                comparer.UpdateX(sweepEvent.posXZ.x); // update comparer x value
                int i = sweepEvent.segmentPointers.x;

                // DEBUG: check order is not broken
                // FIX: has false negative (maybe float precision issue)
                if (!debuger.CheckOrder(out abs))
                {
                    throw new Exception($"Order Assertion Error: \n{abs}");
                }

                if (sweepEvent.eventType == SweepEventType.PointStart)
                {
                    // insert into splay tree
                    k = tree.InsertNode(i);

                    // check neighbor's intersections
                    if (tree.Kth(k - 1, out var preIndex) ) 
                    {
                        TryCreateNewIntersectionEvent(sweepEvent, preIndex, i);
                    }

                    if (
                        tree.Kth(k + 1, out var nextIndex) )
                    {
                        TryCreateNewIntersectionEvent(sweepEvent, i, nextIndex);
                        
                    }


                }
                else if (
                    sweepEvent.eventType == SweepEventType.PointEnd)
                {
                    // FIX: actual situation is more complex
                    if (!tree.Rank2(sweepEvent.segmentPointers.x, out kRange))
                    {
                        tree.AsDebuger().Search(sweepEvent.segmentPointers.x);
                        throw new Exception("Assertion Error: should found matched k");
                    }
                    
                    k = kRange.x;

                    // check its neighbor's intersection
                    if (
                        tree.Kth(k - 1, out var preIndex) &&
                        tree.Kth(k + 1, out var nextIndex)
                        )
                    {
                        TryCreateNewIntersectionEvent(sweepEvent, preIndex, nextIndex);
                    }

                    

                    tree.DeleteNodeByRank(k, out var v);
                    if (v != i)
                    {
                        throw new Exception();
                    }

                }
                else // is intersection event
                {
                    result.Add(sweepEvent.posXZ);
                    // get intersected k range
                    if (!tree.Rank2(i, out kRange))
                    {
                        tree.AsDebuger().Search(sweepEvent.segmentPointers.x);
                        throw new Exception("Assertion Error: should found match");
                    }
                    if (kRange.x == kRange.y)
                    {
                        throw new Exception("Assertion Error: should at least 2 segments in range");
                    }

                    hasPre = tree.Kth(kRange.x - 1, out var preIndex);
                    hasNext = tree.Kth(kRange.y + 1, out var nextIndex);

                    tree.Kth(kRange.x, out var index2Lowest);
                    tree.Kth(kRange.y, out var index2Highest);

                    // check kRange.x and nextIndex
                    if (hasNext)
                    {
                        TryCreateNewIntersectionEvent(sweepEvent, index2Lowest, nextIndex);
                    }

                    // check kRange.y and preIndex
                    if (hasPre)
                    {
                        TryCreateNewIntersectionEvent(sweepEvent, preIndex, index2Highest);
                    }

                    // reverse order in kRange's nodes (delete and re-insert kRange.x n times)
                    for (int k2 = kRange.y - 1; k2 >= kRange.x; k2--)
                    {
                        tree.DeleteNodeByRank(k2, out var v);
                        tree.InsertNode(v);
                    }

                    // DEBUG: check order is not broken
                    if (!debuger.CheckOrder(out abs))
                    {
                        throw new Exception($"Order Assertion Error: \n{abs}");
                    }
                }


                lastEvent = sweepEvent;
            }

        }


        private void InitStartEnds()
        {
            for (int i = 0; i < segments.Length; i++)
            {
                InsertStartEndEvent(i, segments[i], ref eventQueue);
            }
        }

        private SweepEvent NextEvent(SweepEvent lastEvent, SweepEvent nextEvent)
        {
            int i = nextEvent.segmentPointers.x;

            if (nextEvent.eventType == SweepEventType.PointStart)
            {
                // insert into splay tree
                var k = tree.InsertNode(i);

                // check neighbor's intersections
                if (tree.Kth(k - 1, out var preIndex))
                {
                    TryCreateNewIntersectionEvent(nextEvent, preIndex, i);
                }

                if (
                    tree.Kth(k + 1, out var nextIndex))
                {
                    TryCreateNewIntersectionEvent(nextEvent, i, nextIndex);

                }

                nextEvent.kRange = new int2(k);
            }
            else if (
                nextEvent.eventType == SweepEventType.PointEnd)
            {
                // FIX: actual situation is more complex
                
                if (!tree.Rank2(nextEvent.segmentPointers.x, out var kRange))
                {
                    tree.AsDebuger().Search(nextEvent.segmentPointers.x);
                    throw new Exception("Assertion Error: should found matched k");
                }

                int k;
                var foundK = false;
                for (k = kRange.x; k <= kRange.y; k++)
                {
                    tree.Kth(k, out var v);
                    if (v == i)
                    {
                        foundK = true;
                        break;
                    }
                }
                if (!foundK) throw new Exception();

                // check its neighbor's intersection
                if (
                    tree.Kth(k - 1, out var preIndex) &&
                    tree.Kth(k + 1, out var nextIndex)
                    )
                {
                    TryCreateNewIntersectionEvent(nextEvent, preIndex, nextIndex);
                }

                tree.DeleteNodeByRank(k, out _);

                nextEvent.kRange = new int2(k);
            }
            else // is intersection event
            {
                tree.Rank2(i, out var kRange); // get intersected k range
                nextEvent.kRange = kRange; // update event's kRange

                // handle special case: multiple intersection at one point
                // in these cases, theses intersection events expected be emitted continuously
                // and their k-range is same
                // the position swap should only do once or the final segments order in tree will be broken
                // hense we check lastEvent and nextEvent, both are belongs to same intersection or not
                // (used to check by kRange, but has float presision issue)
                // if it is true, just skip the event (do-nothing), only record its kRange
                if (
                    lastEvent.eventType == SweepEventType.Intersection &&
                    IsSameIntersection(lastEvent.posXZ, nextEvent.posXZ)
                    )
                {
                    return nextEvent;
                }

                result.Add(nextEvent.posXZ); // add intersection point to result

                var hasPre = tree.Kth(kRange.x - 1, out var preIndex);
                var hasNext = tree.Kth(kRange.y + 1, out var nextIndex);

                tree.Kth(kRange.x, out var index2Lowest);
                tree.Kth(kRange.y, out var index2Highest);

                // check kRange.x and nextIndex
                if (hasNext)
                {
                    TryCreateNewIntersectionEvent(nextEvent, index2Lowest, nextIndex);
                }

                // check kRange.y and preIndex
                if (hasPre)
                {
                    TryCreateNewIntersectionEvent(nextEvent, preIndex, index2Highest);
                }

                // reverse order
                for (var _k = kRange.y - 1; _k >= kRange.x; _k--)
                {
                    tree.DeleteNodeByRank(_k, out var v);
                    tree.InsertNode(v);
                }

            }
            return nextEvent;
        }
        

        private void Reorder(int2 kRange)
        {
            tempBuffer.Clear();
            kRange.x = math.max(kRange.x, 1);
            kRange.y = math.min(kRange.y, tree.Count());
            for (var k = kRange.y; k >= kRange.x; k--)
            {
                tree.DeleteNodeByRank(k, out var i);
                tempBuffer.Add(i);
            }

            for (int i = 0; i < tempBuffer.Length; i++)
            {
                tree.InsertNode(tempBuffer[i]);
            }
        }


        private void TryCreateNewIntersectionEvent(
            SweepEvent currentEvent, 
            int i1, 
            int i2
            )
        {
            if (!CheckIntersect(segments[i1], segments[i2], out var pos)) return;
            if (pos.x <= currentEvent.posXZ.x) return;
            eventQueue.Push(new SweepEvent
            {
                eventType = SweepEventType.Intersection,
                posXZ = pos,
                segmentPointers = new int2(i1, i2),
                from = EventsCounter
            });
        }


        private bool IsSameIntersection(float2 pos1, float2 pos2)
        {
            var diffVector = pos1 - pos2;
            var diff = diffVector.x + diffVector.y; // manhattan dist
            return math.abs(diff) < eps;
        }


        private bool SkipEvent(SweepEvent lastEvent, SweepEvent currentEvent)
        {
            if (lastEvent.eventType != currentEvent.eventType) return false;
            // if both are intersection
            if (math.any(lastEvent.segmentPointers != currentEvent.segmentPointers)) return false;
            return (math.abs(lastEvent.posXZ.x - currentEvent.posXZ.x) < eps);
        }


        private bool CheckIntersect(Line2.Segment s1, Line2.Segment s2, out float2 intersectPos)
        {
            intersectPos = default;
            if (!MathUtils.Intersect(s1, s2, out var t)) return false;
            intersectPos = math.lerp(s1.a, s1.b, t.x);
            return true;
        }


        private void InsertStartEndEvent(int i, Line2.Segment segment, ref NativeHeap<SweepEvent> eventQueue)
        {
            float2 startPoint, endPoint;
            if (segment.a.x < segment.b.x)
            {
                startPoint = segment.a;
                endPoint = segment.b;
            }
            else
            {
                startPoint = segment.b;
                endPoint = segment.a;
            }

            eventQueue.Push(new SweepEvent
            {
                eventType = SweepEventType.PointStart,
                posXZ = startPoint,
                segmentPointers = new int2(i, 0)
            });

            eventQueue.Push(new SweepEvent
            {
                eventType = SweepEventType.PointEnd,
                posXZ = endPoint,
                segmentPointers = new int2(i, 0)
            });
        }

        public void Dispose()
        {
            comparer.Dispose();
            eventQueue.Dispose();
            tree.Dispose();
            eventsCounter.Dispose();
        }
    }

    public struct NativeSegmantComparer : IComparer<int>
    {
        private NativeReference<float> x;

        private NativeArray<float2> mbReprs;

        private readonly float eps;

        public float X { get => x.Value; }

        public NativeSegmantComparer(
            NativeArray<Line2.Segment> segments, 
            Allocator allocator = Allocator.Temp,
            float eps = 0.01f
            )
        {
            this.eps = eps;
            x = new NativeReference<float>(allocator);
            mbReprs = new NativeArray<float2>(segments.Length, allocator);
            for (int i = 0; i < segments.Length; i++)
            {
                mbReprs[i] = SweepEvent.AsSweepRepresentation(segments[i].a, segments[i].b);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(Index x, Index y)
        {
            return SweepEvent.CompareSegment(mbReprs[x], mbReprs[y], this.x.Value, eps);
        }

        public void Dispose()
        {
            if (x.IsCreated) x.Dispose();
            if (mbReprs.IsCreated) mbReprs.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateX(float x)
        {
            this.x.Value = x;
        }
    }
}

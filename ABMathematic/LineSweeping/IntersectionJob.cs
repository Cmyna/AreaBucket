using AreaBucket.Mathematics.NativeCollections;
using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using Index = System.Int32;

namespace ABMathematics.LineSweeping
{
    public struct IntersectionJob : IJob
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


            public bool CheckOrder(out string currentAbs, out string actualAbs)
            {
                var hasDiff = false;
                var result1 = CurrentOrder();
                var result2 = ActualOrder();
                currentAbs = SegmentIndicesAbstract(result1);
                actualAbs = SegmentIndicesAbstract(result2);
                for (int i = 0; i < result1.Length; i++)
                {
                    hasDiff |= result1[i] != result2[i];
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
                float2 result = default;
                job.CheckIntersect(job.segments[i1], job.segments[i2], out result);
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
                Array.Sort(indices, job.comparer);
                return indices;
            }


            private string SegmentIndicesAbstract(int[] indices)
            {
                var result = $"(length: {indices.Length})";
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

        public IntersectionJob(NativeArray<Line2.Segment> segments, NativeList<float2> result, Allocator allocator = Allocator.Temp)
        {
            this.result = result;
            this.segments = segments;
            comparer = new NativeSegmantComparer(segments, allocator);
            eventQueue = new NativeHeap<SweepEvent>(segments.Length / 10 + 10, allocator);
            tree = new NativeSplayTree<Index, NativeSegmantComparer>(comparer, segments.Length * 2, allocator);
            // eventQueue = new NativeHeap<SweepEvent>(100, Allocator.TempJob);
        }


        public void Execute()
        {
            string currentOrder, actualOrder;

            // int eventsUpperbound = (segments.Length - 1) * segments.Length / 2 + segments.Length * 2;
            int eventsUpperbound = segments.Length * segments.Length;
            int eventsCounter = 0;

            // insert all segments start end event
            for (int i = 0; i < segments.Length; i++)
            {
                InsertStartEndEvent(i, segments[i], ref eventQueue);
            }

            float2 intersectPos;
            int k;
            int2 kRange;
            SweepEvent lastEvent = default;
            bool hasPre, hasNext;

            var debuger = new Debuger(this);

            while (eventQueue.Pop(out var sweepEvent))
            {
                debuger.Record(sweepEvent);
                
                if (eventsCounter > eventsUpperbound) throw new Exception("Assertion Error: Over UpperBound!");
                eventsCounter++;

                // if duplicate event: skip
                if (EqualEvent(sweepEvent, lastEvent)) continue;

                comparer.UpdateX(sweepEvent.posXZ.x); // update comparer x value
                int i = sweepEvent.segmentPointers.x;

                // DEBUG: check order is not broken
                // FIX: has false negative (maybe float precision issue)
                if (!debuger.CheckOrder(out currentOrder, out actualOrder))
                {
                    throw new Exception($"Order Assertion Error: \ncurrent: {currentOrder}\nactual: {actualOrder}");
                }

                if (sweepEvent.eventType == SweepEventType.PointStart)
                {
                    // insert into splay tree
                    k = tree.InsertNode(i);

                    // check neighbor's intersections
                    if (
                        tree.Kth(k - 1, out var preIndex) &&
                        CheckIntersect(segments[i], segments[preIndex], out intersectPos) &&
                        intersectPos.x > sweepEvent.posXZ.x
                        )
                    {
                        eventQueue.Push(new SweepEvent
                        {
                            eventType = SweepEventType.Intersection,
                            posXZ = intersectPos,
                            segmentPointers = new int2(preIndex, i),
                            from = eventsCounter
                        });
                        
                    }

                    if (
                        tree.Kth(k + 1, out var nextIndex) &&
                        CheckIntersect(segments[i], segments[nextIndex], out intersectPos) &&
                        intersectPos.x > sweepEvent.posXZ.x
                        )
                    {
                        eventQueue.Push(new SweepEvent
                        {
                            eventType = SweepEventType.Intersection,
                            posXZ = intersectPos,
                            segmentPointers = new int2(i, nextIndex),
                            from = eventsCounter
                        });
                        
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
                        tree.Kth(k + 1, out var nextIndex) &&
                        CheckIntersect(segments[preIndex], segments[nextIndex], out intersectPos) &&
                        intersectPos.x > sweepEvent.posXZ.x
                        )
                    {
                        eventQueue.Push(new SweepEvent
                        {
                            eventType = SweepEventType.Intersection,
                            posXZ = intersectPos,
                            segmentPointers = new int2(preIndex, nextIndex),
                            from = eventsCounter
                        });
                        
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
                    if (hasNext && 
                        CheckIntersect(segments[index2Lowest], segments[nextIndex], out intersectPos) &&
                        intersectPos.x > sweepEvent.posXZ.x
                        )
                    {
                        eventQueue.Push(new SweepEvent
                        {
                            eventType = SweepEventType.Intersection,
                            posXZ = intersectPos,
                            segmentPointers = new int2(index2Lowest, nextIndex),
                            from = eventsCounter
                        });
                    }

                    // check kRange.y and preIndex
                    if (
                        hasPre && 
                        CheckIntersect(segments[index2Highest], segments[preIndex], out intersectPos) &&
                        intersectPos.x > sweepEvent.posXZ.x
                        )
                    {
                        eventQueue.Push(new SweepEvent
                        {
                            eventType = SweepEventType.Intersection,
                            posXZ = intersectPos,
                            segmentPointers = new int2(preIndex, index2Highest),
                            from = eventsCounter
                        });
                    }

                    // reverse order in kRange's nodes (delete and re-insert kRange.x n times)
                    for (int k2 = kRange.y - 1; k2 >= kRange.x; k2--)
                    {
                        tree.DeleteNodeByRank(k2, out var v);
                        tree.InsertNode(v);
                    }

                    // DEBUG: check order is not broken
                    if (!debuger.CheckOrder(out currentOrder, out actualOrder))
                    {
                        throw new Exception($"Order Assertion Error: \ncurrent: {currentOrder}\nactual: {actualOrder}");
                    }
                }


                lastEvent = sweepEvent;
            }

        }


        private void TryCreateNewIntersectionEvent(
            SweepEvent currentEvent, 
            int i1, 
            int i2,
            int counter
            )
        {
            if (!CheckIntersect(segments[i1], segments[i2], out var pos)) return;
            if (pos.x <= currentEvent.posXZ.x) return;
            eventQueue.Push(new SweepEvent
            {
                eventType = SweepEventType.Intersection,
                posXZ = pos,
                segmentPointers = new int2(i1, i2),
                from = counter
            });
        }


        private bool EqualEvent(SweepEvent event1, SweepEvent event2)
        {
            if (event1.eventType != event2.eventType) return false;
            if (math.any(event1.segmentPointers != event2.segmentPointers)) return false;
            var eps = 1e-3;
            return (math.abs(event1.posXZ.x - event2.posXZ.x) < eps);
        }


        private bool CheckIntersect(Line2.Segment s1, Line2.Segment s2, out float2 intersectPos)
        {
            intersectPos = default;
            if (!MathUtils.Intersect(s1, s2, out var t)) return false;
            intersectPos = math.lerp(s1.a, s1.b, t);
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

        
    }

    public struct NativeSegmantComparer : IComparer<int>
    {
        private NativeReference<float> x;

        private NativeArray<float2> mbReprs;

        public float X { get => x.Value; }

        public NativeSegmantComparer(NativeArray<Line2.Segment> segments, Allocator allocator = Allocator.Temp)
        {
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
            return SweepEvent.CompareSegment(mbReprs[x], mbReprs[y], this.x.Value);
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

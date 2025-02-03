using AreaBucket.Mathematics.NativeCollections;
using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using static Unity.Collections.AllocatorManager;
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

            /// <summary>
            /// execute with debuger
            /// </summary>
            /// <exception cref="Exception"></exception>
            public void Execute()
            {
                int eventsUpperbound = (job.segments.Length - 1) * job.segments.Length / 2 + job.segments.Length * 2;

                job.InitStartEnds();

                job.EventsCounter = 0;
                while (job.eventQueue.Pop(out var nextEvent))
                {
                    job.NextEventsGroup(nextEvent);
                    job.EventsCounter++;
                    if (job.EventsCounter > eventsUpperbound) throw new Exception("Assertion Error: over event upper bounds");
                }
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

            public void CheckTree()
            {
                var debuger = job.tree.AsDebuger();
                for (int k = 1; k <= job.tree.Count(); k++)
                {
                    var (_, _, v) = debuger.Kth(k);
                    var r = debuger.Search(v);
                    var rMin = math.max(r.x, 1);
                    var rMax = math.min(job.tree.Count(), r.y);

                    if (rMin > rMax)
                    {
                        debuger.Search(v);
                        throw new Exception($"Assertion Error: k-th value(k: {k},v: {v}) search failed");
                    }
                }
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


        public void Execute()
        {
            int eventsUpperbound = (segments.Length - 1) * segments.Length / 2 + segments.Length * 2;

            InitStartEnds();

            EventsCounter = 0;
            while(eventQueue.Pop(out var nextEvent))
            {
                NextEventsGroup(nextEvent);
                EventsCounter++;
                if (EventsCounter > eventsUpperbound) throw new Exception("Assertion Error: over event upper bounds");
            }
        }

        private void InitStartEnds()
        {
            for (int i = 0; i < segments.Length; i++)
            {
                InsertStartEndEvent(i, segments[i], ref eventQueue);
            }
        }


        private bool NextEventsGroup(SweepEvent startEvent)
        {
            // temp buffer create code
            var indices = new NativeHashSet<Index>(10, Allocator.Temp);

            var nextEvent = startEvent;
            SweepEvent lastEvent = nextEvent;

            var hasNextGroup = false;

            comparer.UpdateOffset(-100f); // keep before-swapping order in tree 

            var hasIntersection = false;
            var startEndCount = 0;
            do
            {
                // check is same event group or not
                // expect EQ will continuously pop events in same group
                // only if no event in current group, the EQ will pop next group events
                // in one group, events may not equals (means e.CompareTo(e2) != 0)
                // but adjacent event is equal
                // hense we iterate through and compare nextEvent == lastEvent or not,
                // break if nextEvent != lastEvent, and nextEvent is in next group
                if (!IsAdjacentEvent(lastEvent, nextEvent))
                {
                    hasNextGroup = true;
                    break;
                }
                
                if (nextEvent.eventType == SweepEventType.PointStart)
                {
                    indices.Add(nextEvent.segmentPointers.x);
                    startEndCount++;
                }

                if (nextEvent.eventType == SweepEventType.PointEnd)
                {
                    tree.DeleteNode(nextEvent.segmentPointers.x, out _);
                    startEndCount++;
                }

                if (nextEvent.eventType == SweepEventType.Intersection) 
                {
                    indices.Add(nextEvent.segmentPointers.x);
                    indices.Add(nextEvent.segmentPointers.y);
                }

                // one group add one intersection if have intersection event or two start/end point is touching
                if (!hasIntersection && (nextEvent.eventType == SweepEventType.Intersection || startEndCount >= 2) )
                {
                    hasIntersection = true;
                    result.Add(nextEvent.posXZ);
                }

                comparer.UpdateX(math.max(comparer.X, nextEvent.posXZ.x));

                lastEvent = nextEvent;

            } while (eventQueue.Pop(out nextEvent));

#if UNITYPACKAGE
            new Debuger(this).CheckTree();
#endif

            // pop all exists segments in kRange
            var enumerator = indices.GetEnumerator();
            while(enumerator.MoveNext())
            {
                tree.DeleteNode(enumerator.Current, out _);
            }

            // handle event buffer and do swapping

            comparer.UpdateOffset(100f); // as after-swapping order in tree 

            // re-insert all segments and added segments, compute kRange
            var reinsertNum = indices.Count;
            var minK = int.MaxValue;
            enumerator.Reset();
            while (enumerator.MoveNext())
            {
                var k = tree.InsertNode(enumerator.Current);
                minK = math.min(k, minK);
            }

            // try find two neighbor and check intersect
            Index i1, i2;
            if (tree.Kth(minK, out i2) && tree.Kth(minK - 1, out i1))
            {
                TryCreateNewIntersectionEvent(startEvent, i1, i2);
            }

            if (tree.Kth(minK + reinsertNum - 1, out i1) && tree.Kth(minK + reinsertNum, out i2))
            {
                TryCreateNewIntersectionEvent(startEvent, i1, i2);
            }

            // push back again
            if (hasNextGroup) eventQueue.Push(nextEvent);

            return hasNextGroup;
        }


        private bool IsAdjacentEvent(SweepEvent e1, SweepEvent e2)
        {
            // compare by their positions
            var diff = math.abs(e1.posXZ - e2.posXZ);
            return math.all(diff < eps);
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
            if (tempBuffer.IsCreated) tempBuffer.Dispose();
        }
    }

    public struct NativeSegmantComparer : IComparer<int>
    {
        private NativeReference<float> x;

        /// <summary>
        /// extra value to offset x when current compare result is equal
        /// </summary>
        private NativeReference<float> xOffset;

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
            x = new NativeReference<float>(-999999f, allocator);
            xOffset = new NativeReference<float>(allocator);
            xOffset.Value = 0;
            mbReprs = new NativeArray<float2>(segments.Length, allocator);
            for (int i = 0; i < segments.Length; i++)
            {
                mbReprs[i] = SweepEvent.AsSweepRepresentation(segments[i].a, segments[i].b);
            }
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int Compare(Index x, Index y)
        {
            if (x == y) return 0; // if same segment
            var firstCompare = SweepEvent.CompareSegment(mbReprs[x], mbReprs[y], this.x.Value, eps);
            if (firstCompare != 0 || math.abs(xOffset.Value) < eps) return firstCompare;
            // if first compare result == 0 and |offset| > 0, do second compare with offset
            return SweepEvent.CompareSegment(mbReprs[x], mbReprs[y], this.x.Value + xOffset.Value, eps);
        }

        public void Dispose()
        {
            if (x.IsCreated) x.Dispose();
            if (mbReprs.IsCreated) mbReprs.Dispose();
            if (xOffset.IsCreated) xOffset.Dispose();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateX(float x)
        {
            this.x.Value = x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateOffset(float offset)
        {
            this.xOffset.Value = offset;
        }
    }
}

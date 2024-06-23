using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using System.Linq;
using Unity.Collections;
using Unity.Entities.UniversalDelegates;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    /// <summary>
    /// TODO: refactor the merging algorithm (idea is use a convolution like merging)
    /// 
    /// TODO: sharp angle reduction when merging (still by convolution)
    /// </summary>
    public struct MergePolylines : IJob
    {
        public GeneratedArea generatedAreaData;

        /// <summary>
        /// final polygon edges min length,
        /// for merge rays where their end points too close
        /// </summary>
        [ReadOnly] public float minEdgeLength;

        /// <summary>
        /// a larger threshold, the merging should be break whatever
        /// </summary>
        [ReadOnly] public float breakMergeAngleThreshold;

        public bool mergeUnderDistances;

        public bool mergeUnderAngleThreshold;

        public void Execute()
        {
            var count = generatedAreaData.points.Length;
            if (count <= 2) return;
            var pointsCache = new NativeList<float2>(Allocator.Temp);
            // var linesCache = new NativeList<Line2>(Allocator.Temp);

            // merge points that are too closed (and may affect angle calc)
            if (mergeUnderDistances)
            {
                MergePointsByDist(generatedAreaData.points, pointsCache);
                generatedAreaData.points.Clear(); generatedAreaData.points.AddRange(pointsCache.AsArray()); pointsCache.Clear();
            }

            
            if (true)
            {
                // merge polygon lines if they are nearly collinear

                // boundary (special) case, if while loop below ends at input cursor value `points.Length - 1`
                // may cause one point duplicate
                pointsCache.Add(generatedAreaData.points[0]);

                var checkVector = generatedAreaData.points[1] - generatedAreaData.points[0];

                //float walkThroughDist = 0;
                count = generatedAreaData.points.Length; // update count of points after merging those points under dist
                for (int i = 1; i < generatedAreaData.points.Length; i++)
                {
                    var p1 = generatedAreaData.points[i];
                    var p2 = generatedAreaData.points[(i + 1) % count];
                    var v = p2 - p1;

                    var angle = Angle(checkVector, v) * Mathf.Rad2Deg;
                    //walkThroughDist += math.length(v);

                    //var overWalkThroughDist = walkThroughDist >= minEdgeLength;
                    //var overSoftAngleThreshold = angle > breakMergeAngleThreshold;
                    // NaN check: fail safe check
                    var overStrictAngleThreshold = float.IsNaN(angle) || angle > breakMergeAngleThreshold;

                    //var generateBreak = (overSoftAngleThreshold && overWalkThroughDist) || overStrictAngleThreshold;
                    var generateBreak = overStrictAngleThreshold || (!mergeUnderAngleThreshold);

                    if (!generateBreak) continue;
                    pointsCache.Add(p1);
                    //walkThroughDist = 0f;
                    checkVector = v;
                }

                // check (pointsCache[0], pointsCache[1]) and (pointsCache[-1], pointsCache[0]) are collinear or not
                var canMerge = false;
                if (pointsCache.Length >= 3)
                {
                    var v1 = pointsCache[1] - pointsCache[0];
                    var v2 = pointsCache[0] - pointsCache.ElementAt(pointsCache.Length - 1);
                    canMerge = Angle(v1, v2) <= breakMergeAngleThreshold;
                }
                if (canMerge) pointsCache.RemoveAt(0);

                generatedAreaData.points.Clear(); generatedAreaData.points.AddRange(pointsCache.AsArray());
            }



            UnamangedUtils.BuildPolylines(generatedAreaData.points, generatedAreaData.polyLines);

            pointsCache.Dispose();
            // linesCache.Dispose();
        }

        private void MergePointsByDist(NativeList<float2> points, NativeList<float2> newPoints)
        {
            // collect a local cluster that the points are too closed
            var cursor = 0;
            var mergingPoints = false;
            while (cursor < points.Length)
            {
                if (!mergingPoints)
                {
                    mergingPoints = true;
                    var overdistEdgePointStartIdx = cursor;
                    cursor = NextPolylineUnderMergeDist(points, cursor); // cursor, also over distance edge point end index (exclude)
                    for (int i = overdistEdgePointStartIdx; i < cursor; i++) newPoints.Add(points[i]);
                } else
                {
                    mergingPoints = false;
                    var mergeStartIndex = cursor;
                    // this index is point between the last under dist line and next overdist line
                    var mergeEndIndex = NextPolylineOverDist(points, cursor);
                    cursor = mergeEndIndex + 1; // so the cursor should move forward
                    if (mergeEndIndex == points.Length) mergeEndIndex--; // NextPolylineOverDist may return points.Length as return

                    if (mergeEndIndex <= mergeStartIndex) continue;

                    var startPoint = points[mergeStartIndex];
                    var endPoint = points[mergeEndIndex];
                    if (math.length(startPoint - endPoint) < minEdgeLength)
                    {
                        newPoints.Add(math.lerp(startPoint, endPoint, 0.5f));
                        continue;
                    }

                    // merge points
                    newPoints.Add(points[mergeStartIndex]);

                    var lastAddedPointIndex = mergeStartIndex;
                    for (int i = mergeStartIndex + 1; i < mergeEndIndex; i++)
                    {
                        // add points[i] iff both dist(points[i]-points[i - 1]) and dist(points[i]-points[range.y]) larger than min edge distances
                        var d1 = math.length(points[i] - points[lastAddedPointIndex]);
                        var d2 = math.length(points[i] - points[mergeEndIndex]);
                        if (d1 < minEdgeLength || d2 < minEdgeLength) continue;
                        newPoints.Add(points[i]);
                        lastAddedPointIndex = i;
                    }

                    newPoints.Add(points[mergeEndIndex]); // add end point
                }
            }

            // check points[0] and points[points.Length - 1]
            //var l = math.length(points[0] - points[points.Length - 1]);
            //if (l <= minEdgeLength) newPoints.RemoveAt(newPoints.Length - 1);



        }

        private int NextPolylineUnderMergeDist(NativeList<float2> points, int cursor)
        {
            for (int i = cursor; i < points.Length; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Length];
                var v = p2 - p1;
                var length = math.length(v);
                var underMinEdgeLength = length < minEdgeLength;
                if (underMinEdgeLength) return i;
            }
            return points.Length;
        }


        /// <summary>
        /// find the range of adjacent polygon points that their distances smaller than min edge distance
        /// </summary>
        /// <param name="points"></param>
        /// <param name="cursor"></param>
        /// <param name="range">(startIndex, endIndex) of those adjacent points</param>
        /// <returns></returns>
        private int NextPolylineOverDist(NativeList<float2> points, int cursor)
        {
            for (int i = cursor; i < points.Length; i++)
            {
                var p1 = points[i];
                var p2 = points[(i + 1) % points.Length];
                var v = p2 - p1;
                var length = math.length(v);
                var underMinEdgeLength = length < minEdgeLength;
                if (!underMinEdgeLength) return i;
            }
            return points.Length;
        }

        /**
         * return the angle between two vector (unit is radian)
         */
        private float Angle(float2 a, float2 b)
        {
            var cosTheta = math.dot(a, b) / math.length(a) / math.length(b);
            return math.acos(math.clamp(cosTheta, 0, 1));
        }
    }
}

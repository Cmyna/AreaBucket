
using System.Runtime.InteropServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct FilterPoints : IJob
    {
        public NativeList<float2> points;

        [ReadOnly] public float overlayDist;

        [ReadOnly] public float2 hitPos;

        [ReadOnly] public float range;
        public void Execute()
        {
            var cellsMap = new NativeParallelMultiHashMap<int2, float2>(points.Capacity, Allocator.Temp);

            // add points to cells Map
            for (var i = 0; i < points.Length; i++)
            {
                TryAddPoint(cellsMap, points[i]);
            }

            // all points back to 
            points.Clear();

            var enumerator = cellsMap.GetEnumerator();
            while(enumerator.MoveNext())
            {
                var pair = enumerator.Current;
                points.Add(pair.Value);
            }

            cellsMap.Dispose();

            /**
            var pointsCache = new NativeList<float2>(Allocator.Temp);

            for (var i = 0; i < points.Length; i++)
            {
                var p1 = points[i];

                // check point in range
                if (!InRange(p1)) continue;

                var hasOverlay = false;
                for (var j = i + 1; j < points.Length; j++)
                {
                    var p2 = points[j];
                    var vector = p2 - p1;
                    var distance = math.dot(vector, vector);

                    hasOverlay = distance <= overlayedDistSquare;
                    if (hasOverlay) break;
                }

                if (hasOverlay) continue; // drop point
                pointsCache.Add(p1);
            }

            points.Clear();

            for (var i = 0; i < pointsCache.Length; i++)
            {
                points.Add(pointsCache[i]);
            }

            pointsCache.Dispose();*/

        }


        private void TryAddPoint(NativeParallelMultiHashMap<int2, float2> cellsMap, float2 point)
        {
            if (!InRange(point)) return;
            // TODO: check nearby 8 cells

            var key = AsCellKey(point);
            if (!cellsMap.ContainsKey(key))
            {
                cellsMap.Add(key, point);
                return;
            }

            var enumerator = cellsMap.GetValuesForKey(key);
            while (enumerator.MoveNext())
            {
                var checkPoint = enumerator.Current;
                if (Overlay(checkPoint, point)) return;
            }
            cellsMap.Add(key, point);
        }


        private bool InRange(float2 point)
        {
            var vector = point - hitPos;
            // tollerance required from Area2Lines.CollectDivPoints
            // that the length from hit pos to cutted point could really closed to range length
            // which will cause unstable filtering here
            var tollerance = 0.1f; // 0.1m tollerance
            return math.length(vector) <= (range + tollerance); 
        }

        private bool Overlay(float2 p1, float2 p2)
        {
            return math.length(p1 - p2) <= overlayDist;
        }

        private int2 AsCellKey(float2 point)
        {
            var scale = 2;
            return new int2(math.floor(point) * scale);
        }

    }

}

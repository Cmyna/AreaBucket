
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

        [ReadOnly] public float overlayedDistSquare;

        [ReadOnly] public float2 hitPos;

        [ReadOnly] public float range;
        public void Execute()
        {
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

            pointsCache.Dispose();
        }


        private bool InRange(float2 point)
        {
            var vector = point - hitPos;
            var distance = math.dot(vector, vector);
            distance = math.sqrt(distance);
            return distance <= range;
        }
    }
}

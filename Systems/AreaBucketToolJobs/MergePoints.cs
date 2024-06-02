
using System.Runtime.InteropServices;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct MergePoints : IJob
    {
        public CommonContext context;

        [ReadOnly] public float overlayDist;

        public MergePoints Init(CommonContext contextIn, float overlayDistIn)
        {
            context = contextIn;
            overlayDist = overlayDistIn;
            return this;
        }

        public void Execute()
        {
            var cellsMap = new NativeParallelMultiHashMap<int2, TracedPoint>(context.points.Capacity, Allocator.Temp);

            // a mapping from new merged points list index to old points list indices
            var p2pIndexMap = new NativeParallelMultiHashMap<int, int>(context.points.Capacity, Allocator.Temp);

            var mergedPoints = new NativeList<float2>(Allocator.Temp);
            for (int i = 0; i < context.points.Length; i++)
            {
                var point = context.points[i];
                if (!InRange(point)) continue; // just drop it

                var key = AsCellKey(point);

                
                bool hasOverlay = CheckOverlayInCellMap(cellsMap, key, point, out var mergeIndex);
                if (!hasOverlay)
                {
                    cellsMap.Add(key, new TracedPoint { pos = point, mergedListIndex = mergedPoints.Length });
                    mergedPoints.Add(point);
                }

                // add p2p index mapping
                if (hasOverlay && mergeIndex >= 0) p2pIndexMap.Add(mergeIndex, i);
                else p2pIndexMap.Add(mergedPoints.Length - 1, i);
            }

            // remap points index to lines index

            // clear and write back
            context.points.Clear();
            context.points.AddRange(mergedPoints.AsArray());


            cellsMap.Dispose();
            mergedPoints.Dispose();
        }


        private bool CheckOverlayInCellMap(
            NativeParallelMultiHashMap<int2, TracedPoint> cellsMap,
            int2 key,
            float2 point,
            out int overlayPointMergeListIdx
        ) {
            overlayPointMergeListIdx = -1;
            if (!cellsMap.ContainsKey(key)) return false;
            var enumerator = cellsMap.GetValuesForKey(key);
            while (enumerator.MoveNext())
            {
                var checkPoint = enumerator.Current;
                var hasOverlay = Overlay(checkPoint.pos, point);
                if (hasOverlay)
                {
                    overlayPointMergeListIdx = checkPoint.mergedListIndex;
                    return true;
                }
            }
            return false;
        }

        private bool InRange(float2 point)
        {
            var vector = point - context.hitPos;
            // tollerance required from Area2Lines.CollectDivPoints
            // that the length from hit pos to cutted point could really closed to range length
            // which will cause unstable filtering here
            var tollerance = 0.1f; // 0.1m tollerance
            return math.length(vector) <= (context.filterRange + tollerance); 
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

        private struct TracedPoint
        {
            public float2 pos;
            /// <summary>
            /// the index in merged points list
            /// </summary>
            public int mergedListIndex;
        }

    }

}

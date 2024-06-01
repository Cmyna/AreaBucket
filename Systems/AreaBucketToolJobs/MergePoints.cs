
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

        public RayHitPointsRelations relations;

        [ReadOnly] public float overlayDist;
        public void Execute()
        {
            var cellsMap = new NativeParallelMultiHashMap<int2, TracedPoint>(context.points.Capacity, Allocator.Temp);

            // a mapping from new merged points list index to old points list indices
            var p2pIndexMap = new NativeParallelMultiHashMap<int, int>(context.points.Capacity, Allocator.Temp);

            var p2lIndexMapNew = new NativeParallelMultiHashMap<int, int>(relations.lineSourcesMap.Capacity, Allocator.Temp);

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
            RemapLineSources(p2pIndexMap, relations.lineSourcesMap, p2lIndexMapNew);

            // clear and write back
            relations.lineSourcesMap.Clear();
            var p2lEnumeratorNew = p2lIndexMapNew.GetEnumerator();
            while(p2lEnumeratorNew.MoveNext())
            {
                p2lEnumeratorNew.Current.GetKeyValue(out var key, out var value);
                relations.lineSourcesMap.Add(key, value);
            }
            context.points.Clear();
            context.points.AddRange(mergedPoints.AsArray());


            p2lIndexMapNew.Dispose();
            cellsMap.Dispose();
            mergedPoints.Dispose();
        }


        private void RemapLineSources(
            NativeParallelMultiHashMap<int, int> p2pIndexMap,
            NativeParallelMultiHashMap<int, int> p2lIndexMapOld,
            NativeParallelMultiHashMap<int, int> p2lIndexMapNew
        )
        {
            var p2pEnumerator = p2pIndexMap.GetEnumerator();
            while(p2pEnumerator.MoveNext())
            {
                var entry = p2pEnumerator.Current;
                entry.GetKeyValue(out int newPointIndex, out int oldPointIndex);
                if (!p2lIndexMapOld.ContainsKey(oldPointIndex)) continue;
                var p2lEnumeratorOld = p2lIndexMapOld.GetValuesForKey(oldPointIndex);
                while (p2lEnumeratorOld.MoveNext())
                {
                    var lineIndex = p2lEnumeratorOld.Current;
                    p2lIndexMapNew.Add(newPointIndex, lineIndex);
                }
            }
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

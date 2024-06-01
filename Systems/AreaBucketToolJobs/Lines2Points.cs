using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Unity.Burst;
using Unity.Jobs;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    /// <summary>
    /// Convert boundary lines to points
    /// </summary>
    [BurstCompile]
    public struct Lines2Points : IJob
    {
        public CommonContext context;

        public Relations relations;
        public void Execute()
        {
            context.points.Clear();
            var cursor = context.points.Length;
            for (int i = 0; i < context.usedBoundaryLines.Length; i++)
            {
                var line = context.usedBoundaryLines[i];
                if (UnamangedUtils.CollectDivPoints(line, context.hitPos, context.filterRange, out var p1, out var p2))
                {
                    context.points.Add(p1);
                    relations.points2linesMap.Add(cursor, i);
                    cursor++;

                    context.points.Add(p2);
                    relations.points2linesMap.Add(cursor, i);
                    cursor++;
                }
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Jobs;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct Lines2Points : IJob
    {
        public CommonContext context;
        public void Execute()
        {
            context.points.Clear();
            for (int i = 0; i < context.lines.Length; i++)
            {
                var line = context.lines[i];
                UnamangedUtils.CollectDivPoints(line, context.hitPos, context.filterRange, ref context.points);
            }
        }
    }
}

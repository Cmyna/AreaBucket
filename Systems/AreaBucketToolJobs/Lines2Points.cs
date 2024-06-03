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
    [BurstCompile]
    public struct Lines2Points : IJob
    {
        public CommonContext context;

        public SingletonData singletonData;

        public Lines2Points Init(CommonContext context, SingletonData singletonData)
        {
            this.context = context;
            this.singletonData = singletonData;
            return this;
        }

        public void Execute()
        {
            context.points.Clear();
            for (int i = 0; i < context.usedBoundaryLines.Length; i++)
            {
                var line = context.usedBoundaryLines[i];
                var rayStartPos = context.rayStartPoint;
                if (UnamangedUtils.CollectDivPoints(line, rayStartPos, singletonData.fillingRange, out var p1, out var p2))
                {
                    context.points.Add(p1);

                    context.points.Add(p2);
                }
            }
        }
    }
}

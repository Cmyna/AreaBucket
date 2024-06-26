﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;

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
                //var hitPos = context.floodingDefinition.rayStartPoint;
                var hitPos = singletonData.playerHitPos; // collect points under filling circle range distance from player hit pos
                UnamangedUtils.CollectDivPoints(line, hitPos, singletonData.fillingRange, out var p1, out var p2);
                context.points.Add(p1);
                context.points.Add(p2);
            }
        }
    }
}

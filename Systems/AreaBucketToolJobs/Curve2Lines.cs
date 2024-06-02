﻿using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct Curve2Lines : IJob
    {

        [ReadOnly] public int chopCount;

        public CommonContext context;

        public SingletonData staticData;

        public Curve2Lines Init(CommonContext context, SingletonData signletonData, int chopCount)
        {
            this.context = context;
            this.chopCount = chopCount;
            this.staticData = signletonData;
            return this;
        }

        public void Execute()
        {
            for (var i = 0; i < staticData.curves.Length; i++)
            {
                Chop(staticData.curves[i], chopCount);
            }
        }

        private void Chop(Bezier4x3 curve, int chopCount)
        {
            float tStep = 1f / chopCount;
            var estimatedDist = math.length(curve.a.xz - curve.d.xz);
            /*if (estimatedDist < 2) chopCount = 1; // if length is too short, reduce chop count
            else if (estimatedDist < 4) chopCount = 2;
            else if (estimatedDist < 8) chopCount = 4;*/
            for (var i = 0; i < chopCount; i++)
            {
                var t = i * tStep;
                var bounds = new Bounds1(t, t + tStep);
                bounds.max = math.min(bounds.max, 1);
                var cutted = MathUtils.Cut(curve, bounds);
                var line = new Line2() { a = cutted.a.xz, b = cutted.d.xz };

                //context.points.Add(MathUtils.Position(curve, t).xz);
                // context.totalBoundaryLines.Add(line);
                staticData.totalBoundaryLines.Add(line);
            }
            //context.points.Add(curve.d.xz);
        }
    }
}

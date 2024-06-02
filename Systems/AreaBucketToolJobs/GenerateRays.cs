using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct GenerateRays : IJob
    {
        public CommonContext context;

        public SingletonData singletonData;

        public static readonly float2 xzUp = new float2(0, 1);

        public GenerateRays Init(CommonContext context, SingletonData singletonData)
        {
            this.context = context;
            this.singletonData = singletonData;
            return this;
        }

        public void Execute()
        {
            GenerateData(ref context.rays);

            context.rays.Sort(new RayComparer());
        }

        private void GenerateData(ref NativeList<Ray> raysCache)
        {
            for (var i = 0; i < context.points.Length; i++)
            {
                var vector = context.points[i] - singletonData.playerHitPos;
                var radian = Utils.Math.RadianInClock(xzUp, vector);
                var ray = new Ray() { vector = vector, radian = radian };
                raysCache.Add(ray);
            }
        }
    }

    public struct Ray
    {
        public float2 vector;
        /// <summary>
        /// the rays signed radian angle (swept clockwise from vector xz(0, 1), 0-2PI)
        /// </summary>
        public float radian;
    }

    public struct RayComparer : IComparer<Ray>
    {
        public int Compare(Ray x, Ray y)
        {
            if (x.radian - y.radian > 0) return 1;
            else if (x.radian - y.radian == 0) return 0;
            else return -1;
        }
    }
}

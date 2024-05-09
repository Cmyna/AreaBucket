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

        [ReadOnly] public NativeList<float2> points;

        [ReadOnly] public NativeList<Line2> lines;

        [ReadOnly] public float2 rayStartPoint;

        public NativeList<Ray> rays;

        public static readonly float2 xzUp = new float2(0, 1);

        public void Execute()
        {
            //NativeList<Ray> raysCache1 = new NativeList<Ray>(points.Length, Allocator.Temp);
            GenerateData(ref rays);

            rays.Sort(new RayComparer());

            // TODO check intersections

            //raysCache1.Dispose();
        }

        private void GenerateData(ref NativeList<Ray> raysCache)
        {
            for (var i = 0; i < points.Length; i++)
            {
                var vector = points[i] - rayStartPoint;
                var radian = AreaBucket.Utils.Math.RadianInClock(xzUp, vector);
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

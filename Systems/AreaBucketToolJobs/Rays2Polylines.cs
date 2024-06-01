using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Game.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{

    public struct Rays2Polylines : IJob
    {
        public CommonContext context;

        public GeneratedArea generatedArea;

        private NativeParallelHashMap<int, int> p2rMap;

        public void Execute()
        {
            p2rMap = new NativeParallelHashMap<int, int>(1000, Allocator.Temp);
            FindLargestSector(out var maxSectorRadian, out var maxSectorRayIndex);
            BuildPoints(maxSectorRadian, maxSectorRayIndex);
            BuildPolylines();
            p2rMap.Dispose();
        }


        private void BuildPoints(float maxSectorRadian, int startIndex)
        {
            generatedArea.points.Clear();

            bool addHitPoint = maxSectorRadian > Mathf.PI;
            if (addHitPoint) generatedArea.points.Add(context.hitPos);
            var rays = context.rays;
            var raysCount = rays.Length;
            for (int i = startIndex; i < startIndex + raysCount; i++)
            {
                var ray = rays[i % raysCount];
                var p = context.hitPos + ray.vector;
                generatedArea.points.Add(p);
                p2rMap.Add(generatedArea.points.Length - 1, i % raysCount);
            }
        }

        private void BuildPolylines()
        {
            generatedArea.polyLines.Clear();
            for (int i = 0; i < generatedArea.points.Length; i++)
            {
                var i1 = i;
                var i2 = (i + 1) % generatedArea.points.Length;
                var p1 = generatedArea.points[i1];
                var p2 = generatedArea.points[i2];
                generatedArea.polyLines.Add(new Line2 { a = p1, b = p2 });

                var plIndex = generatedArea.polyLines.Length - 1;
                int2 rIndices = new int2(-1, -1);
                if (p2rMap.TryGetValue(i1, out var rIndex)) rIndices.x = rIndex;
                if (p2rMap.TryGetValue(i2, out var rIndex2)) rIndices.y = rIndex2;
            }
        }


        private void FindLargestSector(out float maxSectorRadian, out int maxSectorIndex)
        {
            var sortedRays = context.rays;
            maxSectorRadian = 0;
            maxSectorIndex = -1;
            for (var i = 0; i < sortedRays.Length; i++)
            {
                float a = sortedRays[i].radian;
                float b;
                float rayDiff;
                if (i == sortedRays.Length - 1)
                {
                    b = sortedRays[0].radian;
                    rayDiff = b + Mathf.PI * 2 - a;
                }
                else
                {
                    b = sortedRays[i + 1].radian;
                    rayDiff = b - a;
                }
                if (rayDiff > maxSectorRadian)
                {
                    maxSectorRadian = rayDiff;
                    maxSectorIndex = i + 1;
                }
            }

            if (maxSectorIndex == sortedRays.Length) maxSectorIndex = 0;

        }
    }
}
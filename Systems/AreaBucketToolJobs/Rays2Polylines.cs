using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Unity.Jobs;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{

    public struct Rays2Polylines : IJob
    {
        public CommonContext context;

        public GeneratedArea generatedArea;

        public SingletonData singletonData;

        public Rays2Polylines Init(CommonContext context, SingletonData singletonData, GeneratedArea generatedAreaData)
        {
            this.context = context;
            this.generatedArea = generatedAreaData;
            this.singletonData = singletonData;
            return this;
        }

        public void Execute()
        {
            FindLargestSector(out var maxSectorRadian, out var maxSectorRayIndex);
            BuildPoints(maxSectorRadian, maxSectorRayIndex);
            UnamangedUtils.BuildPolylines(ref generatedArea.points, ref generatedArea.polyLines);
        }


        private void BuildPoints(float maxSectorRadian, int startIndex)
        {
            generatedArea.points.Clear();
            var rayStartPos = context.rayStartPoint;
            // if not flooding a circle, then not add ray start point
            bool addHitPoint = (maxSectorRadian > Mathf.PI) && context.FloodingCirle();
            if (addHitPoint) generatedArea.points.Add(singletonData.playerHitPos);
            var rays = context.rays;
            var raysCount = rays.Length;
            for (int i = startIndex; i < startIndex + raysCount; i++)
            {
                var ray = rays[i % raysCount];
                var p = rayStartPos + ray.vector;
                generatedArea.points.Add(p);
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
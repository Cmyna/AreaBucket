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


        public void Execute()
        {
            FindLargestSector(out var maxSectorRadian, out var maxSectorRayIndex);
            BuildPoints(maxSectorRadian, maxSectorRayIndex);
            UnamangedUtils.BuildPolylines(ref generatedArea.points, ref generatedArea.polyLines);
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
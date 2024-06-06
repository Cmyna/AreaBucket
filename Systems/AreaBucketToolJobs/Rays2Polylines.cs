using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{

    public struct Rays2Polylines : IJob
    {
        public CommonContext context;

        public GeneratedArea generatedArea;

        public SingletonData singletonData;

        public NativeList<FloodingDefinition> remainFloodingDefs;

        public NativeReference<int2> newPointsRange;

        public Rays2Polylines Init(
            CommonContext context, 
            SingletonData singletonData, 
            GeneratedArea generatedAreaData,
            NativeList<FloodingDefinition> floodingDefs,
            NativeReference<int2> newPointsRange
        ) {
            this.context = context;
            this.generatedArea = generatedAreaData;
            this.singletonData = singletonData;
            this.remainFloodingDefs = floodingDefs;
            this.newPointsRange = newPointsRange;
            return this;
        }

        public void Execute()
        {
            FindLargestSector(context.rays, out var maxSectorRadian, out var maxSectorRayIndex);
            var cache = new NativeList<float2>(Allocator.Temp);
            BuildPoints(maxSectorRadian, maxSectorRayIndex, cache);
            var insertIndex = context.floodingDefinition.newAreaPointInsertStartIndex;
            // update other flooding defs
            for (int i = 0; i < remainFloodingDefs.Length; i++)
            {
                var otherDef = remainFloodingDefs[i];
                if (otherDef.newAreaPointInsertStartIndex >= insertIndex)
                {
                    otherDef.newAreaPointInsertStartIndex += cache.Length;
                    remainFloodingDefs[i] = otherDef;
                }
            }
            newPointsRange.Value = new int2(insertIndex + 1, insertIndex + 1 + cache.Length);

            // insert points
            var cache2 = new NativeList<float2>(Allocator.Temp);
            if (insertIndex < generatedArea.points.Length) for (int i = 0; i < insertIndex + 1; i++) cache2.Add(generatedArea.points[i]);
            cache2.AddRange(cache.AsArray());
            for (int i = insertIndex + 1; i < generatedArea.points.Length; i++) cache2.Add(generatedArea.points[i]);
            generatedArea.points.Clear();
            generatedArea.points.AddRange(cache2.AsArray());

            UnamangedUtils.BuildPolylines(generatedArea.points, generatedArea.polyLines);

            cache.Dispose();
            cache2.Dispose();
        }


        private void BuildPoints(float maxSectorRadian, int startIndex, NativeList<float2> pointsList)
        {
            var rays = context.rays;
            var raysCount = rays.Length;

            pointsList.Clear();
            var rayStartPos = context.floodingDefinition.rayStartPoint;
            // if not flooding a circle, then not add ray start point
            bool addHitPoint = (maxSectorRadian > Mathf.PI) && context.floodingDefinition.FloodingCirle();

            var start = startIndex;
            var end = startIndex + raysCount;
            if (addHitPoint) pointsList.Add(singletonData.playerHitPos);
            if (!context.floodingDefinition.FloodingCirle())
            {
                // it would contains extra 2 rays (at start and end) hit poly line points to be intersected
                // so ignore these two rays
                start++; end--;
            }
            
            for (int i = start; i < end; i++)
            {
                var ray = rays[i % raysCount];
                var p = rayStartPos + ray.vector;
                pointsList.Add(p);
            }
        }

        private void FindLargestSector(NativeList<Ray> sortedRays, out float maxSectorRadian, out int maxSectorIndex)
        {
            //var sortedRays = context.rays;
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
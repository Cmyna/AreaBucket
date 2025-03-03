using Colossal.Collections;
using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs.JobData
{
    /// <summary>
    /// this struct is for sharing common members in Area Bucket Jobs
    /// to reduce redundant code
    /// </summary>
    public struct CommonContext : IDisposable
    {

        public NativeList<float2> points;

        public NativeList<Line2> usedBoundaryLines;

        public BoundariesCollections usedBoundaryLines2;

        public NativeList<Ray> rays;

        /// <summary>
        /// something like Z-buffer in CG, for checking lines occlusions
        /// </summary>
        public NativeArray<float> occlusionsBuffer;


        public FloodingDefinition floodingDefinition;

        public bool useQuadTree;

        public CommonContext Init(FloodingDefinition floodingDefinition, Allocator allocator = Allocator.TempJob)
        {
            this.floodingDefinition = floodingDefinition;
            points = new NativeList<float2>(allocator);
            usedBoundaryLines = new NativeList<Line2>(allocator);
            usedBoundaryLines2 = new BoundariesCollections(allocator);
            rays = new NativeList<Ray>(allocator);
            occlusionsBuffer = new NativeArray<float>(360, allocator); // 1 degree per unit
            // floodRadRange = new float2(0, Mathf.PI * 2);
            ClearOcclusionBuffer();
            return this;

        }

        public void ClearOcclusionBuffer()
        {
            for (int i = 0; i < occlusionsBuffer.Length; i++) occlusionsBuffer[i] = float.MaxValue;
        }


        public void ClearBoundaries()
        {
            usedBoundaryLines.Clear();
            usedBoundaryLines2.Clear();
        }

        public void AddBoundaries(NativeArray<Line2> boundaries)
        {
            usedBoundaryLines.AddRange(boundaries);
            if (!useQuadTree) return;
            for (int i = 0; i < boundaries.Length; i++)
            {
                var l = boundaries[i];
                usedBoundaryLines2.Add(l.a, l.b);
                
            }
        }

        public void AddBoundary(Line2 boundary)
        {
            usedBoundaryLines.Add(boundary);
            if (!useQuadTree) return;
            usedBoundaryLines2.Add(boundary.a, boundary.b);
            
        }
        

        public void Dispose()
        {
            points.Dispose();
            rays.Dispose();
            occlusionsBuffer.Dispose();
            usedBoundaryLines.Dispose();
            usedBoundaryLines2.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = inputDeps;
            jobHandle = points.Dispose(jobHandle);
            jobHandle = rays.Dispose(jobHandle);
            jobHandle = occlusionsBuffer.Dispose(jobHandle);
            jobHandle = usedBoundaryLines.Dispose(jobHandle);
            jobHandle = usedBoundaryLines2.Dispose(jobHandle);
            return jobHandle;
        }
    }


}

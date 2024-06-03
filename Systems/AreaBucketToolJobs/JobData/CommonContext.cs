using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
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

        public NativeList<Ray> rays;

        /// <summary>
        /// something like Z-buffer in CG, for checking lines occlusions
        /// </summary>
        public NativeArray<float> occlusionsBuffer;

        public float2 rayStartPoint;

        /// <summary>
        /// set the filling sector radian range, the algorithms should generate area filling polygons between range.
        /// two float are signed radian (between 0-2PI), radian range from floodRadRange.x to floodRadRange.y
        /// if floodRadRange.x > floodRadRange.y, means range crossing zero radian,
        /// if want to filling the whole circle range, set it as (0, 2 * PI)
        /// </summary>
        public float2 floodRadRange;

        /// <summary>
        /// where to insert new polygon points to GeneratedArea.points list
        /// </summary>
        public int newAreaPointInsertStartIndex;

        public CommonContext Init(Allocator allocator = Allocator.TempJob)
        {
            points = new NativeList<float2>(allocator);
            usedBoundaryLines = new NativeList<Line2>(allocator);
            rays = new NativeList<Ray>(allocator);
            occlusionsBuffer = new NativeArray<float>(360, allocator); // 1 degree per unit
            floodRadRange = new float2(0, Mathf.PI * 2);
            ClearOcclusionBuffer();
            return this;
        }

        public void ClearOcclusionBuffer()
        {
            for (int i = 0; i < occlusionsBuffer.Length; i++) occlusionsBuffer[i] = float.MaxValue;
        }

        public bool FloodingCirle()
        {
            return floodRadRange.x == 0 && floodRadRange.y == Mathf.PI * 2;
        }

        public bool InFloodingRange(float radian)
        {
            if (floodRadRange.x > floodRadRange.y)
            {
                return UnamangedUtils.Between(radian, 0, floodRadRange.y) || 
                    UnamangedUtils.Between(radian, floodRadRange.x, Mathf.PI * 2);
            } else
            {
                return UnamangedUtils.Between(radian, floodRadRange.x, floodRadRange.y);
            }
        }

        public void Dispose()
        {
            points.Dispose();
            rays.Dispose();
            occlusionsBuffer.Dispose();
            usedBoundaryLines.Dispose();
        }


    }
}

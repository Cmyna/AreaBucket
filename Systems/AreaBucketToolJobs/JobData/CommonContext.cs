using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;

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

        public CommonContext Init(Allocator allocator = Allocator.TempJob)
        {
            points = new NativeList<float2>(allocator);
            usedBoundaryLines = new NativeList<Line2>(allocator);
            rays = new NativeList<Ray>(allocator);
            occlusionsBuffer = new NativeArray<float>(360, allocator); // 1 degree per unit
            for (int i = 0; i < occlusionsBuffer.Length; i++) occlusionsBuffer[i] = float.MaxValue;
            return this;
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

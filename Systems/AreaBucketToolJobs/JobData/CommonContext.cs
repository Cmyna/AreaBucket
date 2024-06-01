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

        public NativeList<Line2> totalBoundaryLines;

        public NativeList<Ray> rays;

        public NativeList<Bezier4x3> curves;

        /// <summary>
        /// something like Z-buffer in CG, for checking lines occlusions
        /// </summary>
        public NativeArray<float> occlusionsBuffer;

        public float2 hitPos;

        public float filterRange;

        public CommonContext Init(float2 hitPos, float filterRange, Allocator allocator = Allocator.TempJob)
        {
            points = new NativeList<float2>(allocator);
            totalBoundaryLines = new NativeList<Line2>(allocator);
            usedBoundaryLines = new NativeList<Line2>(allocator);
            rays = new NativeList<Ray>(allocator);
            curves = new NativeList<Bezier4x3>(allocator);
            occlusionsBuffer = new NativeArray<float>(360, allocator); // 1 degree per unit
            for (int i = 0; i < occlusionsBuffer.Length; i++) occlusionsBuffer[i] = float.MaxValue;

            this.hitPos = hitPos;
            this.filterRange = filterRange;
            return this;
        }


        public void Dispose()
        {
            points.Dispose();
            totalBoundaryLines.Dispose();
            rays.Dispose();
            occlusionsBuffer.Dispose();
            curves.Dispose();
            usedBoundaryLines.Dispose();
        }


    }
}

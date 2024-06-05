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
    /// some implicit assumptions: 
    /// * All generated points and lines are sorted clockwise (from raystart point's perspective) in the list.
    /// * polyLines[i] are generated from points[i] -> points[(i + 1) % points.Length]
    /// </summary>
    public struct GeneratedArea : IDisposable
    {
        public NativeList<Line2> polyLines;

        public NativeList<float2> points;


        public GeneratedArea Init(Allocator allocator = Allocator.TempJob)
        {
            polyLines = new NativeList<Line2>(allocator);
            points = new NativeList<float2>(allocator);
            return this;
        }

        public void Dispose()
        {
            polyLines.Dispose();
            points.Dispose();
        }
    }
}
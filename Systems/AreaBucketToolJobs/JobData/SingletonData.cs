using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;

namespace AreaBucket.Systems.AreaBucketToolJobs.JobData
{
    public struct SingletonData : IDisposable
    {
        public NativeList<Line2> totalBoundaryLines;

        public NativeList<Bezier4x3> curves;

        public SingletonData Init(Allocator allocator = Allocator.TempJob)
        {
            curves = new NativeList<Bezier4x3>(allocator);
            totalBoundaryLines = new NativeList<Line2>(allocator);
            return this;
        }

        public void Dispose()
        {
            curves.Dispose();
            totalBoundaryLines.Dispose();
        }
    }
}

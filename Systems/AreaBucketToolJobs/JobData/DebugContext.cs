using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;

namespace AreaBucket.Systems.AreaBucketToolJobs.JobData
{
    public struct DebugContext : IDisposable
    {
        public NativeList<Line2> intersectedLines;

        public NativeList<Line2> intersectedRays;

        public DebugContext Init(Allocator allocator = Allocator.TempJob)
        {
            intersectedLines = new NativeList<Line2>(allocator);
            intersectedRays = new NativeList<Line2>(allocator);
            return this;
        }

        public void Dispose()
        {
            intersectedLines.Dispose();
            intersectedRays.Dispose();
        }
    }
}

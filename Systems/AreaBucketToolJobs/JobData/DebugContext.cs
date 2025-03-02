using Colossal;
using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;

namespace AreaBucket.Systems.AreaBucketToolJobs.JobData
{
    public struct DebugContext : IDisposable
    {
        public NativeList<Line2> intersectedLines;

        public NativeList<Line2> intersectedRays;

        public GizmoBatcher gizmoBatcher;

        public DebugContext Init(GizmoBatcher gizmoBatcher, Allocator allocator = Allocator.TempJob)
        {
            intersectedLines = new NativeList<Line2>(allocator);
            intersectedRays = new NativeList<Line2>(allocator);
            this.gizmoBatcher = gizmoBatcher;
            return this;
        }

        public void AddIntersectedSegment(Line2.Segment s)
        {
            intersectedLines.Add(new Line2.Segment(s.a, s.b));
        }

        public void AddRaySegment(Line2.Segment rs)
        {
            intersectedRays.Add(new Line2.Segment(rs.a, rs.b));
        }

        public void Dispose()
        {
            intersectedLines.Dispose();
            intersectedRays.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = inputDeps;
            jobHandle = intersectedLines.Dispose(jobHandle);
            jobHandle = intersectedRays.Dispose(jobHandle);
            return jobHandle;
        }
    }
}

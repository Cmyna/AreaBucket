


using Colossal.Mathematics;
using System;
using Unity.Collections;

namespace AreaBucket.Data
{

    public struct BoundariesData : IDisposable
    {

        public NativeList<Line2> lines;

        public NativeList<Bezier4x3> curves;

        public BoundariesData Init(Allocator allocator = Allocator.TempJob)
        {
            this.lines = new NativeList<Line2>(allocator);
            this.curves = new NativeList<Bezier4x3>(allocator);
            return this;
        }

        public void Dispose()
        {
            if (lines.IsCreated) lines.Dispose();
            if (curves.IsCreated) curves.Dispose();
        }
    }
}
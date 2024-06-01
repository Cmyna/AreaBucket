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
    /// struct storing candidate points for new area polygons (or its predecessor, the generated rays)
    /// </summary>
    public struct Relations : IDisposable
    {

        /// <summary>
        /// the map stores where the points comes from (which lines sources).
        /// the key is the index of cadidates ray end points (fields of CommonContext?), 
        /// the value is the index of a NativeList<Line2> lines (now fields of CommonContext?).
        /// one point may have multiple line sources (because the point may from touching point of two/three/more lines, or intersection point from two lines)
        /// </summary>
        public NativeParallelMultiHashMap<int, int> points2linesMap;

        public NativeParallelHashMap<int, int> rays2pointsMap;

        public NativeParallelHashMap<int, int2> genAreaLine2raysMap;

        public Relations Init(Allocator allocator = Allocator.TempJob)
        {
            points2linesMap = new NativeParallelMultiHashMap<int, int>(10000, allocator);
            rays2pointsMap = new NativeParallelHashMap<int, int>(10000, allocator);
            genAreaLine2raysMap = new NativeParallelHashMap<int, int2>(10000, allocator);
            return this;
        }

        public void Dispose()
        {
            points2linesMap.Dispose();
            rays2pointsMap.Dispose();
            genAreaLine2raysMap.Dispose();
        }
    }
}

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
    public struct RayHitPointsRelations : IDisposable
    {

        /// <summary>
        /// the map stores where the points comes from (which lines sources).
        /// the key is the index of cadidates ray end points (fields of CommonContext?), 
        /// the value is the index of a NativeList<Line2> lines (now fields of CommonContext?).
        /// one point may have multiple line sources (because the point may from touching point of two/three/more lines, or intersection point from two lines)
        /// </summary>
        public NativeParallelMultiHashMap<int, int> lineSourcesMap;

        public RayHitPointsRelations Init(Allocator allocator = Allocator.TempJob)
        {
            lineSourcesMap = new NativeParallelMultiHashMap<int, int>(10000, allocator);
            return this;
        }

        public void Dispose()
        {
            lineSourcesMap.Dispose();
        }
    }
}

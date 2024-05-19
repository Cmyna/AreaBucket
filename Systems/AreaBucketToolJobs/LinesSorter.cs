using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AreaBucket.Systems.AreaBucketToolJobs
{

    public struct CenterAroundComparer : IComparer<Line2>
    {
        public float2 hitPos;
        public int Compare(Line2 x, Line2 y)
        {
            var dist1 = MathUtils.Distance(x, hitPos, out _);
            var dist2 = MathUtils.Distance(y, hitPos, out _);
            return (int)(dist1 - dist2);
        }
    }
}

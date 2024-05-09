using Colossal.Mathematics;
using Game.Net;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    /// <summary>
    /// The job filters nets in hit range
    /// </summary>
    public struct FilterNets : IJobChunk
    {

        [ReadOnly] public ComponentTypeHandle<Curve> curveTypehandle;

        //[ReadOnly] public EntityTypeHandle entityHandle;

        [ReadOnly] public float filterRange;

        [ReadOnly] public float2 hitPoint;

        public NativeList<Bezier4x3> filterResults;

        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            var curveCompArray = chunk.GetNativeArray(ref curveTypehandle);
            //var entites = chunk.GetNativeArray(entityHandle);

            for (var i = 0; i < curveCompArray.Length; i++)
            {
                var curve = curveCompArray[i];
                Bounds2 bounds = MathUtils.TightBounds(curve.m_Bezier.xz);
                // var distance = AreaBucket.Utils.Math.MinDistance2Bounds(bounds, hitPoint);
                var distance = MathUtils.Distance(bounds, hitPoint);
                if (distance > filterRange) continue;
                filterResults.Add(curve.m_Bezier);
            }
        }
    }


}

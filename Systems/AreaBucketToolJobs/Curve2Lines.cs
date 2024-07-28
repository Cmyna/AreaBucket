using AreaBucket.Systems.AreaBucketToolJobs.JobData;
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
    [BurstCompile]
    public struct Curve2Lines : IJob
    {

        public SingletonData staticData;

        public Curve2Lines Init(SingletonData signletonData)
        {
            this.staticData = signletonData;
            return this;
        }

        public void Execute()
        {
            for (var i = 0; i < staticData.curves.Length; i++)
            {
                Chop(staticData.curves[i], 0, 1);
            }
        }


        private void Chop(Bezier4x3 curve, int depth, int depthLimit)
        {
            if (depth > depthLimit)
            {
                staticData.totalBoundaryLines.Add(new Line2 { a = curve.a.xz, b = curve.d.xz });
                return;
            }
            var bounds = GetDistanceBound(curve);
            if (bounds.max < 0.5f)
            {
                staticData.totalBoundaryLines.Add(new Line2 { a = curve.a.xz, b = curve.d.xz });
                return;
            }
            var leftCurve = MathUtils.Cut(curve, new float2(0, 0.5f));
            var rightCurve = MathUtils.Cut(curve, new float2(0.5f, 1f));
            Chop(leftCurve, depth + 1, depthLimit);
            Chop(rightCurve, depth + 1, depthLimit);
        }

        private Bounds1 GetDistanceBound(Bezier4x3 curve)
        {
            var min = math.length(curve.d.xz - curve.a.xz);
            var max = math.length(curve.b.xz - curve.a.xz);
            max += math.length(curve.c.xz - curve.b.xz);
            max += math.length(curve.d.xz - curve.c.xz);
            return new Bounds1 { min = min, max = max };
        }
    }
}

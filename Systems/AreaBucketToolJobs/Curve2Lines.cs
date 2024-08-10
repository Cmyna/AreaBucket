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
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct Curve2Lines : IJob
    {

        public SingletonData staticData;

        [ReadOnly] public float angleLimit;

        public Curve2Lines Init(SingletonData signletonData, float angleLimit)
        {
            this.staticData = signletonData;
            this.angleLimit = angleLimit;
            return this;
        }

        public void Execute()
        {
            for (var i = 0; i < staticData.curves.Length; i++)
            {
                Chop(staticData.curves[i], 0, 2);
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
            var angleMeasure = ControlPointAngleMeasure(curve);
            if (bounds.max < 0.5f || angleMeasure <= angleLimit)
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

        private float ControlPointAngleMeasure(Bezier4x3 curve)
        {
            var ab = curve.b.xz - curve.a.xz; ab /= math.length(ab);
            var bc = curve.c.xz - curve.b.xz; bc /= math.length(bc);
            var cd = curve.d.xz - curve.c.xz; cd /= math.length(cd);

            var angle1 = MathUtils.RotationAngle(ab, bc) * Mathf.Rad2Deg; //angle1 = math.abs(angle1);
            var angle2 = MathUtils.RotationAngle(bc, cd) * Mathf.Rad2Deg; //angle2 = math.abs(angle2);

            return angle1 + angle2;
        }
    }
}

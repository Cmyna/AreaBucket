using Colossal.Mathematics;
using Game.Creatures;
using Game.Net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;

namespace AreaBucket.Utils
{
    public static class BezierMath
    {

        public static float Travel(Bezier4x3 curve, float startT, float travelDistance, float tollerance = 0.1f)
        {
            startT = math.clamp(startT, 0, 1);

            var curveLength = CubicBezierArcLengthXZGauss04(curve, 0, 1);

            // first step: move t factor based on travelDistances to arc length ratio
            var ratio = travelDistance / curveLength; // signed ratio
            var currentT = startT + ratio;

            // Newton's approximation
            var stepLimit = 12;
            for (var i = 0; i < stepLimit; i++)
            {
                if (currentT > 1 || currentT < 0) break;
                var signedCurrentLength = CubicBezierArcLengthXZGauss04(curve, startT, currentT);
                var signedDiff = travelDistance - signedCurrentLength;
                if (math.abs(signedDiff) < tollerance) break;
                currentT += signedDiff / CubicSpeedXZ(curve, currentT);
            }

            return math.clamp(currentT, 0, 1);
        }

        /// <summary>
        /// Reference From Algernon's LinesTool-CS2 SimpleCurve.cs
        /// From Alterann's PropLineTool (CubicBezierArcLengthXZGauss04, Utilities/PLTMath.cs).
        /// Returns the XZ arclength of a cubic Bezier curve between two t factors.
        /// Uses Gauss–Legendre Quadrature with n = 4.
        /// </summary>
        /// <param name="t1">Starting t factor.</param>
        /// <param name="t2">Ending t factor.</param>
        /// <returns>XZ arc length.</returns>
        public static float CubicBezierArcLengthXZGauss04(Bezier4x3 curve, float t1, float t2)
        {

            float linearAdj = (t2 - t1) / 2f;

            // Constants are from Gauss-Lengendre quadrature rules for n = 4.
            float p1 = CubicSpeedXZGaussPoint(curve, 0.3399810435848563f, 0.6521451548625461f, t1, t2);
            float p2 = CubicSpeedXZGaussPoint(curve, -0.3399810435848563f, 0.6521451548625461f, t1, t2);
            float p3 = CubicSpeedXZGaussPoint(curve, 0.8611363115940526f, 0.3478548451374538f, t1, t2);
            float p4 = CubicSpeedXZGaussPoint(curve, -0.8611363115940526f, 0.3478548451374538f, t1, t2);

            return linearAdj * (p1 + p2 + p3 + p4);
        }


        /// <summary>
        /// Reference From Algernon's LinesTool-CS2 SimpleCurve.cs
        /// From Alterann's PropLineTool (CubicSpeedXZ, Utilities/PLTMath.cs).
        /// Returns the integrand of the arc length function for a cubic Bezier curve, constrained to the XZ-plane at a specific t.
        /// </summary>
        /// <param name="bezier">the bezier curve</oaram>
        /// <param name="t"> t factor.</param>
        /// <returns>Integrand of arc length.</returns>
        public static float CubicSpeedXZ(Bezier4x3 bezier, float t)
        {
            // Pythagorean theorem.
            float3 tangent = MathUtils.Tangent(bezier, t);
            float derivXsqr = tangent.x * tangent.x;
            float derivZsqr = tangent.z * tangent.z;

            return math.sqrt(derivXsqr + derivZsqr);
        }


        /// <summary>
        /// Reference From Algernon's LinesTool-CS2 SimpleCurve.cs
        /// From Alterann's PropLineTool (CubicSpeedXZGaussPoint, Utilities/PLTMath.cs).
        /// </summary>
        /// <param name="x_i">X i.</param>
        /// <param name="w_i">W i.</param>
        /// <param name="a">a.</param>
        /// <param name="b">b.</param>
        /// <returns>Cubic speed.</returns>
        public static float CubicSpeedXZGaussPoint(Bezier4x3 curve, float x_i, float w_i, float a, float b)
        {
            float linearAdj = (b - a) / 2f;
            float constantAdj = (a + b) / 2f;
            return w_i * CubicSpeedXZ(curve, (linearAdj * x_i) + constantAdj);
        }
    }
}

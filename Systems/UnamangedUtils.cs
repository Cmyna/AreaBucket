using Colossal.Mathematics;
using Game.Routes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems
{
    /// <summary>
    /// untilities methods for unmanaged call (Job, System, etc.)
    /// </summary>
    public static class UnamangedUtils
    {

        /// <summary>
        /// collect one or two cutting points of circle and lines, 
        /// which the circle use hitPos at center, range as cirlce range <br/>
        /// if the line only cut one point, use end point of line instead <br/>
        /// assume line is (A, B), hitPos -> O, range -> r, solve this equation: |tAB + OA| = r <br/>
        /// we can get ( -(AB * OA) +- sqrt(term1) ) / AB^2 <br/>
        /// term1 = (AB * OA)^2 - AB^2*OA^2 + AB^2*r^2
        /// </summary>
        /// <param name="line"></param>
        public static void CollectDivPoints(
            Line2 line, 
            float2 hitPos, 
            float filterRange,
            ref NativeList<float2> pointList
        ) {
            var ab = line.b - line.a;
            var oa = line.a - hitPos;
            var abDoa = math.dot(ab, oa);
            var abSqr = math.lengthsq(ab);
            var rSqr = filterRange * filterRange;
            var oaSqr = math.lengthsq(oa);

            // term sqrt(b^2 - 4ac) / 4 in Quadratic formula
            var term1 = abDoa * abDoa - abSqr * oaSqr + abSqr * rSqr;
            if (term1 < 0) return; // no solution(no cutting points)

            var term2 = math.sqrt(term1);

            var t2 = (-abDoa + term2) / abSqr; // point should near B
            var t1 = (-abDoa - term2) / abSqr; // point should near A

            // check t1, t2 on the line or not

            if (t1 >= 0 && t1 <= 1) pointList.Add(math.lerp(line.a, line.b, t1));
            else pointList.Add(line.a);

            if (t2 >= 0 && t2 <= 1) pointList.Add(math.lerp(line.a, line.b, t2));
            else pointList.Add(line.b);
        }


        
    }

}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace ABMathematics.LineSweeping
{

    public enum SweepEventType
    {
        PointStart = 1,
        PointEnd = 2,
        Intersection = 4
    }


    public struct SweepEvent : IComparable<SweepEvent>
    {

        public SweepEventType eventType;

        public float2 posXZ;

        /// <summary>
        /// points to a Line2.Segment[] array
        /// if eventType is start or end, only .x is used, 
        /// else both .x and .y used to represent two intersected segments
        /// (also .x's segment's kth is smaller than .y's)
        /// </summary>
        public int2 segmentPointers;

        /// <summary>
        /// from which event (only for interesection events)
        /// </summary>
        public int from;

        /// <summary>
        /// records event's k range in segments tree (at that moment)
        /// </summary>
        public int2 kRange;

        public int CompareTo(SweepEvent other)
        {
            // (both x and z) smaller value has higher priority
            var eps = 1e-3f;
            var xDiff = posXZ.x - other.posXZ.x;
            if (math.abs(xDiff) > eps) return xDiff > 0 ? -1 : 1;
            // check y(z) diff
            var yDiff = posXZ.y - other.posXZ.y;
            if (math.abs(yDiff) > eps) return yDiff > 0 ? -1 : 1;

            return 0;
        }

        public static float2 AsSweepRepresentation(float2 a, float2 b)
        {
            float2 diff = a - b;
            float m = diff.y / diff.x;
            float _b = a.y - m * a.x;
            return new float2(m, _b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int CompareSegment(float2 s1, float2 s2, float x, float eps)
        {
            float z1 = s1.x * x + s1.y;
            float z2 = s2.x * x + s2.y;
            float diff = math.abs(z1 - z2);
            if (diff <= eps) return 0;
            else if (z1 > z2) return 1;
            else return -1;
        }
    }
}

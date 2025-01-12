using System;
using System.Collections.Generic;
using System.Linq;
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

        public int CompareTo(SweepEvent other)
        {
            // smaller value has higher priority
            return (posXZ.x - other.posXZ.x) > 0 ? -1 : 1;
        }

        public static float2 AsSweepRepresentation(float2 a, float2 b)
        {
            float2 diff = a - b;
            float m = diff.y / diff.x;
            float _b = a.y - m * a.x;
            return new float2(m, _b);
        }

        public static int CompareSegment(float2 s1, float2 s2, float x)
        {
            float z1 = s1.x * x + s1.y;
            float z2 = s2.x * x + s2.y;
            float diff = math.abs(z1 - z2);
            float eps = 0.001f;
            if (diff <= eps) return 0;
            else if (z1 > z2) return 1;
            else return -1;
        }
    }
}

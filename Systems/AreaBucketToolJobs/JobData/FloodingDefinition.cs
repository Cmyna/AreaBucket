using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs.JobData
{
    public struct FloodingDefinition
    {
        public int floodingDepth;

        public float2 rayStartPoint;

        public Line2.Segment floodingSourceLine;

        /// <summary>
        /// set the filling sector radian range, the algorithms should generate area filling polygons between range.
        /// two float are signed radian (between 0-2PI), radian range from floodRadRange.x to floodRadRange.y
        /// if floodRadRange.x > floodRadRange.y, means range crossing zero radian,
        /// if want to filling the whole circle range, set it as (0, 2 * PI)
        /// </summary>
        public float2 floodRadRange;

        /// <summary>
        /// where to insert new polygon points to GeneratedArea.points list
        /// </summary>
        public int newAreaPointInsertStartIndex;

        public FloodingDefinition Init(float2 rayStartPoint, int depth, int newAreaPointInsertStartIndex = -1)
        {
            this.rayStartPoint = rayStartPoint;
            this.floodRadRange = new float2(0, Mathf.PI * 2);
            this.floodingDepth = depth;
            this.newAreaPointInsertStartIndex = newAreaPointInsertStartIndex;
            return this;
        }

        public bool FloodingCirle()
        {
            return floodRadRange.x == 0 && floodRadRange.y == Mathf.PI * 2;
        }

        public bool InFloodingRange(float radian)
        {
            if (floodRadRange.x > floodRadRange.y)
            {
                return UnamangedUtils.Between(radian, 0, floodRadRange.y) ||
                    UnamangedUtils.Between(radian, floodRadRange.x, Mathf.PI * 2);
            }
            else
            {
                return UnamangedUtils.Between(radian, floodRadRange.x, floodRadRange.y);
            }
        }
    }
}

﻿using Colossal.Collections;
using Colossal.Mathematics;
using Game.Simulation;
using System;
using System.Collections.Generic;
using System.Drawing.Design;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;

namespace AreaBucket.Systems.AreaBucketToolJobs.JobData
{
    public struct SingletonData : IDisposable
    {
        public NativeList<Line2.Segment> totalBoundaryLines;

        public NativeList<Bezier4x3> curves;

        public float2 playerHitPos;

        public float fillingRange;

        public TerrainHeightData terrainHeightData;

        public SingletonData Init(float2 playerHitPos, float fillingRange, TerrainHeightData terrainHeightData, Allocator allocator = Allocator.TempJob)
        {
            curves = new NativeList<Bezier4x3>(allocator);
            totalBoundaryLines = new NativeList<Line2.Segment>(allocator);
            this.playerHitPos = playerHitPos;
            this.fillingRange = fillingRange;
            this.terrainHeightData = terrainHeightData;
            return this;
        }

        public void Dispose()
        {
            curves.Dispose();
            totalBoundaryLines.Dispose();
        }

        public JobHandle Dispose(JobHandle inputDeps)
        {
            var jobHandle = inputDeps;
            jobHandle = curves.Dispose(jobHandle);
            jobHandle = totalBoundaryLines.Dispose(jobHandle);
            return jobHandle;
        }

        public void AddLine(Line2 line)
        {
            AddLine(line.a, line.b);
        }

        public void AddLine(float2 a, float2 b)
        {
            this.totalBoundaryLines.Add(new Line2.Segment(a, b));
        }
    }

    

    
}

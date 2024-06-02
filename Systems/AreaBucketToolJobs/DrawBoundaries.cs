using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal;
using Game.Simulation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct DrawBoundaries : IJob
    {
        public CommonContext context;

        public GizmoBatcher gizmoBatcher;

        public TerrainHeightData heightData;

        public SingletonData signletonData;

        public DrawBoundaries Init(
            CommonContext context, 
            GizmoBatcher batcher, 
            TerrainHeightData heightData, 
            SingletonData signletonData
        ) {
            this.context = context;
            this.gizmoBatcher = batcher;
            this.heightData = heightData;
            this.signletonData = signletonData;
            return this;
        }
        public void Execute()
        {
            var color = Color.red;
            // draw lines collected by areas and curves collect from net lanes/road edges
            for (int i = 0; i < signletonData.curves.Length; i++)
            {
                gizmoBatcher.DrawBezier(signletonData.curves[i], color);
            }
            for (int i = 0;i < signletonData.totalBoundaryLines.Length; i++)
            {
                var line = signletonData.totalBoundaryLines[i];
                gizmoBatcher.DrawLine(GetWorldPos(line.a), GetWorldPos(line.b), color);
            }
        }

        private float3 GetWorldPos(float2 pos)
        {
            var worldPos = new float3 { x = pos.x, y = 0, z = pos.y };
            var height = TerrainUtils.SampleHeight(ref heightData, worldPos);
            worldPos.y = height;
            return worldPos;
        }
    }
}

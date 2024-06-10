using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal;
using Game.Simulation;
using Unity.Burst;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.DebugHelperJobs
{
    [BurstCompile]
    public struct DrawBoundaries : IJob
    {

        public GizmoBatcher gizmoBatcher;

        public SingletonData signletonData;

        public DrawBoundaries Init(
            GizmoBatcher batcher, 
            SingletonData signletonData
        ) {
            this.gizmoBatcher = batcher;
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
            var height = TerrainUtils.SampleHeight(ref signletonData.terrainHeightData, worldPos);
            worldPos.y = height;
            return worldPos;
        }
    }
}

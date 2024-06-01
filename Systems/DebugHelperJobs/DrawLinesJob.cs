using Colossal;
using Colossal.Mathematics;
using Game.Simulation;
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

namespace AreaBucket.Systems.DebugHelperJobs
{
    [BurstCompile]
    public struct DrawLinesJob : IJob
    {
        public NativeList<Line2> lines;

        public GizmoBatcher gizmoBatcher;

        public TerrainHeightData heightData;

        public Color color;

        public void Execute()
        {
            for (int i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
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

        public DrawLinesJob AsNewJobData(Color colorIn, NativeList<Line2> linesIn)
        {
            return new DrawLinesJob
            {
                lines = linesIn,
                color = colorIn,
                gizmoBatcher = gizmoBatcher,
                heightData = heightData
            };
        }
    }
}

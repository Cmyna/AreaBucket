using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Game.Areas;
using Game.Common;
using Game.Simulation;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    public struct Polylines2AreaDefinition : IJob
    {

        public GeneratedArea generatedAreaData;

        [ReadOnly] public Entity prefab;

        [ReadOnly] public TerrainHeightData terrianHeightData;

        public EntityCommandBuffer commandBuffer;

        public int generateNodesCount;
        public void Execute()
        {
            if (generatedAreaData.points.Length <= 0) return;

            var defEntity = commandBuffer.CreateEntity();
            var definition = default(CreationDefinition);
            definition.m_Prefab = prefab;

            var nodeBuffer = commandBuffer.AddBuffer<Node>(defEntity);
            nodeBuffer.ResizeUninitialized(generatedAreaData.points.Length + 1);

            // append nodes
            int cursor = 0;
            for (int i = 0; i < generatedAreaData.points.Length; i++)
            {
                var point2D = generatedAreaData.points[i];
                var point3D = new float3()
                {
                    x = point2D.x,
                    y = 0,
                    z = point2D.y
                };
                point3D.y = TerrainUtils.SampleHeight(ref terrianHeightData, point3D);
                nodeBuffer[cursor] = new Node(point3D, float.MinValue);
                cursor++;
            }

            nodeBuffer[cursor] = nodeBuffer[0]; // last point is the first point

            generateNodesCount = cursor + 1;

            commandBuffer.AddComponent(defEntity, definition);
            commandBuffer.AddComponent(defEntity, default(Updated));
        }
    }
}

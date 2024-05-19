using Game.Areas;
using Game.Common;
using Game.Simulation;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct Rays2AreaDefinition : IJob
    {
        public CommonContext context;

        [ReadOnly] public Entity prefab;

        [ReadOnly] public ControlPoint gameRaycastPoint;

        [ReadOnly] public TerrainHeightData terrianHeightData;

        public EntityCommandBuffer commandBuffer;

        public int generateNodesCount;

        public void Execute()
        {
            if (context.rays.Length <= 0) return;

            FindLargestSector(out var maxSectorRadian, out var maxSectorRayIndex);
            // float maxSectorRadian = Mathf.PI + 0.1f;
            // int maxSectorRayIndex = 0;

            var defEntity = commandBuffer.CreateEntity();
            var definition = default(CreationDefinition);
            definition.m_Prefab = prefab;
            var nodeBuffer = commandBuffer.AddBuffer<Node>(defEntity);

            BuildArea(maxSectorRadian, maxSectorRayIndex, ref nodeBuffer);
            generateNodesCount = nodeBuffer.Length;

            commandBuffer.AddComponent(defEntity, definition);
            commandBuffer.AddComponent(defEntity, default(Updated));
        }


        private void BuildArea(float maxSectorRadian, int startIndex, ref DynamicBuffer<Node> buffer)
        {
            var sortedRays = context.rays;
            bool addRaycastPoint = maxSectorRadian > Mathf.PI;
            var nodeNum = sortedRays.Length + 1;
            if (addRaycastPoint) nodeNum += 1;
            buffer.ResizeUninitialized(nodeNum);

            int cursor = 0;

            // two for loops: logic like sortedRays.Slice(startIndex).Concat(sortedRays.Slice(0, startIndex)).ForEach(...)
            for (var i = startIndex; i < sortedRays.Length; i++)
            {
                var point3D = new float3() { 
                    x = sortedRays[i].vector.x + gameRaycastPoint.m_HitPosition.x, 
                    y = 0, 
                    z = sortedRays[i].vector.y + gameRaycastPoint.m_HitPosition.z
                };
                point3D.y = TerrainUtils.SampleHeight(ref terrianHeightData, point3D);
                buffer[cursor] = new Node(point3D, float.MinValue);
                cursor++;
            }
            for (var i = 0; i < startIndex; i++)
            {
                var point3D = new float3()
                {
                    x = sortedRays[i].vector.x + gameRaycastPoint.m_HitPosition.x,
                    y = 0,
                    z = sortedRays[i].vector.y + gameRaycastPoint.m_HitPosition.z
                };
                point3D.y = TerrainUtils.SampleHeight(ref terrianHeightData, point3D);
                buffer[cursor] = new Node(point3D, float.MinValue);
                cursor++;
            }

            if (addRaycastPoint)
            {
                // buffer.Add(new Node(gameRaycastPoint.m_HitPosition, float.MinValue));
                buffer[cursor] = new Node(gameRaycastPoint.m_HitPosition, float.MinValue);
                cursor++;
            }
            buffer[cursor] = buffer[0];
            //buffer.Add(buffer[0]);
        }

        private void FindLargestSector(out float maxSectorRadian, out int maxSectorIndex)
        {
            var sortedRays = context.rays;
            maxSectorRadian = 0;
            maxSectorIndex = -1;
            for (var i = 0; i < sortedRays.Length; i++)
            {
                float a = sortedRays[i].radian;
                float b;
                float rayDiff;
                if (i == sortedRays.Length - 1)
                {
                    b = sortedRays[0].radian;
                    rayDiff = b + Mathf.PI * 2 - a;
                } else
                {
                    b = sortedRays[i + 1].radian;
                    rayDiff = b - a;
                }
                if (rayDiff > maxSectorRadian)
                {
                    maxSectorRadian = rayDiff;
                    maxSectorIndex = i + 1;
                }
            }

            if (maxSectorIndex == sortedRays.Length) maxSectorIndex = 0;

        }
    }
}

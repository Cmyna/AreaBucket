using Game.Areas;
using Game.Common;
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

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct SimpleAreaDefinition : IJob
    {
        [ReadOnly] public NativeList<ControlPoint> m_ControlPoints;
        [ReadOnly] public Entity m_Prefab;

        public EntityCommandBuffer m_CommandBuffer;
        public void Execute()
        {
            var defEntity = m_CommandBuffer.CreateEntity();

            var definition = default(CreationDefinition);
            definition.m_Prefab = m_Prefab;

            // add nodes
            var nodeNum = m_ControlPoints.Length;
            var nodeBuffer = m_CommandBuffer.AddBuffer<Node>(defEntity);
            nodeBuffer.ResizeUninitialized(nodeNum + 1);
            for (var i = 0; i < nodeNum; i++)
            {
                nodeBuffer[i] = new Node(m_ControlPoints[i].m_Position, float.MinValue);
            }
            nodeBuffer[nodeNum] = nodeBuffer[0];

            // add components
            m_CommandBuffer.AddComponent(defEntity, definition);
            m_CommandBuffer.AddComponent(defEntity, default(Updated));
        }


    }
}

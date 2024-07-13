using Game.Areas;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine.Rendering;


namespace AreaBucket.Utils
{
    public static class AreaDefinitionCreation
    {
        public static DynamicBuffer<Node> AsDynmaicBufferNodes
        (
            EntityCommandBuffer ecb, 
            Entity entity, 
            NativeList<float2> points, 
            bool isADrawingDefinition
        )
        {
            var bufferSize = isADrawingDefinition ? points.Length + 1 : points.Length;
            var nodeBuffer = ecb.AddBuffer<Node>(entity);
            nodeBuffer.ResizeUninitialized(bufferSize);

            for (int i = 0; i < points.Length; i++)
            {
                var point2D = points[i];
                var point3D = new float3()
                {
                    x = point2D.x,
                    y = 0,
                    z = point2D.y
                };
                nodeBuffer[i] = new Node(point3D, float.MinValue);
            }
            if (isADrawingDefinition)
            {
                nodeBuffer[points.Length] = nodeBuffer[0];
            }

            return nodeBuffer;
        }

        public static CreationDefinition WithCreationDefinition(EntityCommandBuffer ecb, Entity entity, Entity prefab)
        {
            var definition = default(CreationDefinition);
            definition.m_Prefab = prefab;
            ecb.AddComponent(entity, definition);
            ecb.AddComponent(entity, default(Updated));
            return definition;
        }
    }
}

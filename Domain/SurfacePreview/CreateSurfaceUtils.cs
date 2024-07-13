using Game.Areas;
using Game.Common;
using Game.Simulation;
using Game.Tools;
using System;
using Unity.Entities;


namespace AreaBucket.Systems.SurfacePreviewSystem
{
    public static class CreateSurfaceUtils
    {

        /// <summary>
        /// append/remove Owner component based on CreationDefinition
        /// </summary>
        /// <param name="newAreaEntity"></param>
        /// <param name="creationDefinition"></param>
        /// <param name="ecb"></param>
        /// <returns></returns>
        public static bool WithOwner(Entity newAreaEntity, CreationDefinition creationDefinition, EntityCommandBuffer ecb)
        {
            ecb.RemoveComponent<OwnerDefinition>(newAreaEntity);
            if (creationDefinition.m_Owner != Entity.Null)
            {
                ecb.AddComponent(newAreaEntity, new Owner(creationDefinition.m_Owner));
                return true;
            }
            ecb.RemoveComponent<Owner>(newAreaEntity);
            return false;
        }

        /// <summary>
        /// initialize owner based on OwnerDefinition, (speculation) it may means the owner entity will be created later
        /// </summary>
        /// <param name="newAreaEntity"></param>
        /// <param name="ownerDefinition"></param>
        /// <param name="ecb"></param>
        /// <returns></returns>
        public static bool WithDefinedOwner(Entity newAreaEntity, OwnerDefinition ownerDefinition, EntityCommandBuffer ecb)
        {
            if (ownerDefinition.m_Prefab != Entity.Null)
            {
                ecb.AddComponent(newAreaEntity, default(Owner));
                ecb.AddComponent(newAreaEntity, ownerDefinition);
                return true;
            }
            return false;
        }


        public static void AdjustPosition
        (
            DynamicBuffer<Node> nodeBuffer, 
            ref TerrainHeightData terrainHeightData,
            ref WaterSurfaceData waterSurfaceData,
            bool onWaterSurface
        )
        {
            for (int i = 0; i < nodeBuffer.Length; i++)
            {
                var node = nodeBuffer[i];
                if (!onWaterSurface)
                {
                    node = AreaUtils.AdjustPosition(node, ref terrainHeightData);
                }
                else
                {
                    node = AreaUtils.AdjustPosition(node, ref terrainHeightData, ref waterSurfaceData);
                }
                nodeBuffer[i] = node;
            }
        }

        /// <summary>
        /// check the polygon shape declared ('drawed') by nodes is complete or not.
        /// complete: last node equals first node, at least has 4 nodes
        /// </summary>
        /// <param name="nodeBuffer"></param>
        /// <returns></returns>
        public static bool IsCompletePolygonDrawn(DynamicBuffer<Node> nodeBuffer)
        {
            if (nodeBuffer.Length < 4) return false;
            var firstNode = nodeBuffer[0];
            var lastNode = nodeBuffer[nodeBuffer.Length - 1];
            return firstNode.m_Position.Equals(lastNode.m_Position);
        }

        /// <summary>
        /// copy a node buffer to another, but source is a representation of drawing an area, 
        /// while target is an actual (to be created) node buffer representation in scene.
        /// The difference is: if source is a complete polygon drawn, then the last node equals the first node (a duplication), 
        /// while in scene polygon nodes buffer doesn't have that duplication
        /// </summary>
        /// <param name="source"></param>
        /// <param name="target"></param>
        public static void Drawing2Scene(DynamicBuffer<Node> source, DynamicBuffer<Node> target)
        {
            var isCompleteDraw = IsCompletePolygonDrawn(source);
            var copyUpperBound = isCompleteDraw ? source.Length - 1 : source.Length;
            target.ResizeUninitialized(copyUpperBound);
            for (int i = 0; i < copyUpperBound; i++) target[i] = source[i];
        }

        public static void Scene2Drawing(DynamicBuffer<Node> source, DynamicBuffer<Node> target)
        {
            target.ResizeUninitialized(source.Length + 1);
            for (int i = 0; i < source.Length; i++) target[i] = source[i];
            target[source.Length] = target[0];
        }
    }
}

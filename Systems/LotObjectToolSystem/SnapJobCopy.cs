// Substantial portions derived from decompiled game code.
// Copyright (c) Colossal Order and Paradox Interactive.
// Not for reuse or distribution except in accordance with the Paradox Interactive End User License Agreement.


using Colossal.Collections;
using Colossal.Mathematics;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Game.Zones;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using MathF = UnityEngine.Mathf;
using Mode = Game.Tools.ObjectToolSystem.Mode;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    public struct Rotation
    {
        public quaternion m_Rotation;

        public quaternion m_ParentRotation;

        public bool m_IsAligned;
    }

    [BurstCompile]
    public struct SnapJob : IJob
    {
        private struct ZoneBlockIterator : INativeQuadTreeIterator<Entity, Bounds2>, IUnsafeQuadTreeIterator<Entity, Bounds2>
        {
            public ControlPoint m_ControlPoint;

            public ControlPoint m_BestSnapPosition;

            public float m_BestDistance;

            public int2 m_LotSize;

            public Bounds2 m_Bounds;

            public float2 m_Direction;

            public Entity m_IgnoreOwner;

            public ComponentLookup<Owner> m_OwnerData;

            public ComponentLookup<Block> m_BlockData;

            public bool hasSnapping;

            public bool Intersect(Bounds2 bounds)
            {
                return MathUtils.Intersect(bounds, m_Bounds);
            }

            public void Iterate(Bounds2 bounds, Entity blockEntity)
            {
                if (!MathUtils.Intersect(bounds, m_Bounds))
                {
                    return;
                }
                if (m_IgnoreOwner != Entity.Null)
                {
                    Entity entity = blockEntity;
                    Owner componentData;
                    while (m_OwnerData.TryGetComponent(entity, out componentData))
                    {
                        if (componentData.m_Owner == m_IgnoreOwner)
                        {
                            return;
                        }
                        entity = componentData.m_Owner;
                    }
                }
                
                Block block = m_BlockData[blockEntity];
                Quad2 quad = ZoneUtils.CalculateCorners(block);
                Line2.Segment line = new Line2.Segment(quad.a, quad.b);
                Line2.Segment line2 = new Line2.Segment(m_ControlPoint.m_HitPosition.xz, m_ControlPoint.m_HitPosition.xz);
                float2 @float = m_Direction * (math.max(0f, m_LotSize.y - m_LotSize.x) * 4f);
                line2.a -= @float;
                line2.b += @float;
                float2 t;
                float num = MathUtils.Distance(line, line2, out t);
                if (num == 0f)
                {
                    num -= 0.5f - math.abs(t.y - 0.5f);
                }
                if (!(num >= m_BestDistance))
                {
                    // it seems that this if scope is for applying snapping and all snapping pre-condition is satisfied
                    hasSnapping = true;
                    m_BestDistance = num;
                    float2 y = m_ControlPoint.m_HitPosition.xz - block.m_Position.xz;
                    float2 float2 = MathUtils.Left(block.m_Direction);
                    float num2 = (float)block.m_Size.y * 4f;
                    float num3 = (float)m_LotSize.y * 4f;
                    float num4 = math.dot(block.m_Direction, y);
                    float num5 = math.dot(float2, y);
                    float num6 = math.select(0f, 0.5f, ((block.m_Size.x ^ m_LotSize.x) & 1) != 0);
                    num5 -= (math.round(num5 / 8f - num6) + num6) * 8f;
                    m_BestSnapPosition = m_ControlPoint;
                    m_BestSnapPosition.m_Position = m_ControlPoint.m_HitPosition;
                    m_BestSnapPosition.m_Position.xz += block.m_Direction * (num2 - num3 - num4);
                    m_BestSnapPosition.m_Position.xz -= float2 * num5;
                    m_BestSnapPosition.m_Direction = block.m_Direction;
                    m_BestSnapPosition.m_Rotation = ToolUtils.CalculateRotation(m_BestSnapPosition.m_Direction);
                    m_BestSnapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, m_ControlPoint.m_HitPosition.xz * 0.5f, m_BestSnapPosition.m_Position.xz * 0.5f, m_BestSnapPosition.m_Direction);
                    m_BestSnapPosition.m_OriginalEntity = blockEntity;
                }
            }
        }

        [ReadOnly]
        public Snap m_Snap;

        [ReadOnly]
        public Mode m_Mode;

        [ReadOnly]
        public Entity m_Prefab;

        [ReadOnly]
        public Entity m_Selected;

        [ReadOnly]
        public ComponentLookup<Owner> m_OwnerData;

        [ReadOnly]
        public ComponentLookup<Terrain> m_TerrainData;

        [ReadOnly]
        public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingData;

        [ReadOnly]
        public ComponentLookup<PlaceableObjectData> m_PlaceableObjectData;

        [ReadOnly]
        public ComponentLookup<OutsideConnectionData> m_OutsideConnectionData;

        [ReadOnly]
        public ComponentLookup<StackData> m_StackData;

        [ReadOnly]
        public ComponentLookup<Block> m_BlockData;

        [ReadOnly]
        public BufferLookup<Game.Objects.SubObject> m_SubObjects;


        [ReadOnly]
        public NativeQuadTree<Entity, Bounds2> m_ZoneSearchTree;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        public NativeList<ControlPoint> m_ControlPoints;

        public NativeValue<Rotation> m_Rotation;

        public NativeReference<bool> hasSnapping;

        public void Execute()
        {
            ControlPoint controlPoint = m_ControlPoints[0];
            ControlPoint bestSnapPosition = controlPoint;
            bestSnapPosition.m_OriginalEntity = Entity.Null;
            if (m_OutsideConnectionData.HasComponent(m_Prefab))
            {
                HandleWorldSize(ref bestSnapPosition, controlPoint);
            }
            float waterSurfaceHeight = float.MinValue;
            if ((m_Snap & Snap.NetSide) != 0)
            {
                BuildingData buildingData = m_BuildingData[m_Prefab];
                float num = (float)buildingData.m_LotSize.y * 4f + 16f;
                float bestDistance = (float)math.cmin(buildingData.m_LotSize) * 4f + 16f;
                ZoneBlockIterator zoneBlockIterator = default(ZoneBlockIterator);
                zoneBlockIterator.m_ControlPoint = controlPoint;
                zoneBlockIterator.m_BestSnapPosition = bestSnapPosition;
                zoneBlockIterator.m_BestDistance = bestDistance;
                zoneBlockIterator.m_LotSize = buildingData.m_LotSize;
                zoneBlockIterator.m_Bounds = new Bounds2(controlPoint.m_Position.xz - num, controlPoint.m_Position.xz + num);
                zoneBlockIterator.m_Direction = math.forward(m_Rotation.value.m_Rotation).xz;
                zoneBlockIterator.m_IgnoreOwner = ((m_Mode == Mode.Move) ? m_Selected : Entity.Null);
                zoneBlockIterator.m_OwnerData = m_OwnerData;
                zoneBlockIterator.m_BlockData = m_BlockData;
                zoneBlockIterator.hasSnapping = false;
                ZoneBlockIterator iterator = zoneBlockIterator;
                m_ZoneSearchTree.Iterate(ref iterator);
                bestSnapPosition = iterator.m_BestSnapPosition;

                hasSnapping.Value |= iterator.hasSnapping;
            }
            CalculateHeight(ref bestSnapPosition, waterSurfaceHeight);
            Rotation value = m_Rotation.value;
            value.m_IsAligned &= value.m_Rotation.Equals(bestSnapPosition.m_Rotation);
            value.m_Rotation = bestSnapPosition.m_Rotation;
            m_Rotation.value = value;
            if (m_StackData.TryGetComponent(m_Prefab, out var componentData7) && componentData7.m_Direction == StackDirection.Up)
            {
                float num5 = componentData7.m_FirstBounds.max + MathUtils.Size(componentData7.m_MiddleBounds) * 2f - componentData7.m_LastBounds.min;
                bestSnapPosition.m_Elevation += num5;
                bestSnapPosition.m_Position.y += num5;
            }
            m_ControlPoints[0] = bestSnapPosition;
        }

        private void HandleWorldSize(ref ControlPoint bestSnapPosition, ControlPoint controlPoint)
        {
            Bounds3 bounds = TerrainUtils.GetBounds(ref m_TerrainHeightData);
            bool2 @bool = false;
            float2 @float = 0f;
            Bounds3 bounds2 = new Bounds3(controlPoint.m_HitPosition, controlPoint.m_HitPosition);
            if (m_ObjectGeometryData.TryGetComponent(m_Prefab, out var componentData))
            {
                bounds2 = ObjectUtils.CalculateBounds(controlPoint.m_HitPosition, controlPoint.m_Rotation, componentData);
            }
            if (bounds2.min.x < bounds.min.x)
            {
                @bool.x = true;
                @float.x = bounds.min.x;
            }
            else if (bounds2.max.x > bounds.max.x)
            {
                @bool.x = true;
                @float.x = bounds.max.x;
            }
            if (bounds2.min.z < bounds.min.z)
            {
                @bool.y = true;
                @float.y = bounds.min.z;
            }
            else if (bounds2.max.z > bounds.max.z)
            {
                @bool.y = true;
                @float.y = bounds.max.z;
            }
            if (math.any(@bool))
            {
                ControlPoint snapPosition = controlPoint;
                snapPosition.m_OriginalEntity = Entity.Null;
                snapPosition.m_Direction = new float2(0f, 1f);
                snapPosition.m_Position.xz = math.select(controlPoint.m_HitPosition.xz, @float, @bool);
                snapPosition.m_Position.y = controlPoint.m_HitPosition.y;
                snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(2f, 1f, controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz, snapPosition.m_Direction);
                float3 forward = default(float3);
                forward.xz = math.sign(@float);
                snapPosition.m_Rotation = quaternion.LookRotationSafe(forward, math.up());
                AddSnapPosition(ref bestSnapPosition, snapPosition);
            }
        }

        private void CalculateHeight(ref ControlPoint controlPoint, float waterSurfaceHeight)
        {
            if (!m_PlaceableObjectData.HasComponent(m_Prefab))
            {
                return;
            }
            PlaceableObjectData placeableObjectData = m_PlaceableObjectData[m_Prefab];
            if (m_SubObjects.HasBuffer(controlPoint.m_OriginalEntity))
            {
                controlPoint.m_Position.y += placeableObjectData.m_PlacementOffset.y;
                return;
            }
            float num;
            if ((placeableObjectData.m_Flags & Game.Objects.PlacementFlags.RoadSide) != 0 && m_BuildingData.HasComponent(m_Prefab))
            {
                BuildingData buildingData = m_BuildingData[m_Prefab];
                float3 worldPosition = BuildingUtils.CalculateFrontPosition(new Transform(controlPoint.m_Position, controlPoint.m_Rotation), buildingData.m_LotSize.y);
                num = TerrainUtils.SampleHeight(ref m_TerrainHeightData, worldPosition);
            }
            else
            {
                num = TerrainUtils.SampleHeight(ref m_TerrainHeightData, controlPoint.m_Position);
            }
            if ((placeableObjectData.m_Flags & Game.Objects.PlacementFlags.Hovering) != 0)
            {
                float num2 = WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, controlPoint.m_Position);
                num2 += placeableObjectData.m_PlacementOffset.y;
                controlPoint.m_Elevation = math.max(0f, num2 - num);
                num = math.max(num, num2);
            }
            else if ((placeableObjectData.m_Flags & (Game.Objects.PlacementFlags.Shoreline | Game.Objects.PlacementFlags.Floating)) == 0)
            {
                num += placeableObjectData.m_PlacementOffset.y;
            }
            else
            {
                float num3 = WaterUtils.SampleHeight(ref m_WaterSurfaceData, ref m_TerrainHeightData, controlPoint.m_Position, out var waterDepth);
                if (waterDepth >= 0.2f)
                {
                    num3 += placeableObjectData.m_PlacementOffset.y;
                    if ((placeableObjectData.m_Flags & Game.Objects.PlacementFlags.Floating) != 0)
                    {
                        controlPoint.m_Elevation = math.max(0f, num3 - num);
                    }
                    num = math.max(num, num3);
                }
            }
            if ((m_Snap & Snap.Shoreline) != 0)
            {
                num = math.max(num, waterSurfaceHeight + placeableObjectData.m_PlacementOffset.y);
            }
            controlPoint.m_Position.y = num;
        }

        private static void AddSnapPosition(ref ControlPoint bestSnapPosition, ControlPoint snapPosition)
        {
            if (ToolUtils.CompareSnapPriority(snapPosition.m_SnapPriority, bestSnapPosition.m_SnapPriority))
            {
                bestSnapPosition = snapPosition;
            }
        }
    }

}
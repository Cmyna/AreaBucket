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
        private struct LoweredParentIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public ControlPoint m_Result;

            public float3 m_Position;

            public ComponentLookup<Edge> m_EdgeData;

            public ComponentLookup<Node> m_NodeData;

            public ComponentLookup<Orphan> m_OrphanData;

            public ComponentLookup<Curve> m_CurveData;

            public ComponentLookup<Composition> m_CompositionData;

            public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

            public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryData;

            public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryData;

            public ComponentLookup<NetCompositionData> m_PrefabCompositionData;

            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                return MathUtils.Intersect(bounds.m_Bounds.xz, m_Position.xz);
            }

            public void Iterate(QuadTreeBoundsXZ bounds, Entity entity)
            {
                if (MathUtils.Intersect(bounds.m_Bounds.xz, m_Position.xz))
                {
                    if (m_EdgeGeometryData.HasComponent(entity))
                    {
                        CheckEdge(entity);
                    }
                    else if (m_OrphanData.HasComponent(entity))
                    {
                        CheckNode(entity);
                    }
                }
            }

            private void CheckNode(Entity entity)
            {
                Node node = m_NodeData[entity];
                Orphan orphan = m_OrphanData[entity];
                NetCompositionData netCompositionData = m_PrefabCompositionData[orphan.m_Composition];
                if ((netCompositionData.m_State & CompositionState.Marker) == 0 && ((netCompositionData.m_Flags.m_Left | netCompositionData.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0)
                {
                    float3 position = node.m_Position;
                    position.y += netCompositionData.m_SurfaceHeight.max;
                    if (math.distance(m_Position.xz, position.xz) <= netCompositionData.m_Width * 0.5f)
                    {
                        m_Result.m_OriginalEntity = entity;
                        m_Result.m_Position = node.m_Position;
                        m_Result.m_HitPosition = m_Position;
                        m_Result.m_HitPosition.y = position.y;
                        m_Result.m_HitDirection = default(float3);
                    }
                }
            }

            private void CheckEdge(Entity entity)
            {
                EdgeGeometry edgeGeometry = m_EdgeGeometryData[entity];
                EdgeNodeGeometry geometry = m_StartNodeGeometryData[entity].m_Geometry;
                EdgeNodeGeometry geometry2 = m_EndNodeGeometryData[entity].m_Geometry;
                bool3 x = default(bool3);
                x.x = MathUtils.Intersect(edgeGeometry.m_Bounds.xz, m_Position.xz);
                x.y = MathUtils.Intersect(geometry.m_Bounds.xz, m_Position.xz);
                x.z = MathUtils.Intersect(geometry2.m_Bounds.xz, m_Position.xz);
                if (!math.any(x))
                {
                    return;
                }
                Composition composition = m_CompositionData[entity];
                Edge edge = m_EdgeData[entity];
                Curve curve = m_CurveData[entity];
                if (x.x)
                {
                    NetCompositionData prefabCompositionData = m_PrefabCompositionData[composition.m_Edge];
                    if ((prefabCompositionData.m_State & CompositionState.Marker) == 0 && ((prefabCompositionData.m_Flags.m_Left | prefabCompositionData.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0)
                    {
                        CheckSegment(entity, edgeGeometry.m_Start, curve.m_Bezier, prefabCompositionData);
                        CheckSegment(entity, edgeGeometry.m_End, curve.m_Bezier, prefabCompositionData);
                    }
                }
                if (x.y)
                {
                    NetCompositionData prefabCompositionData2 = m_PrefabCompositionData[composition.m_StartNode];
                    if ((prefabCompositionData2.m_State & CompositionState.Marker) == 0 && ((prefabCompositionData2.m_Flags.m_Left | prefabCompositionData2.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0)
                    {
                        if (geometry.m_MiddleRadius > 0f)
                        {
                            CheckSegment(edge.m_Start, geometry.m_Left, curve.m_Bezier, prefabCompositionData2);
                            Segment right = geometry.m_Right;
                            Segment right2 = geometry.m_Right;
                            right.m_Right = MathUtils.Lerp(geometry.m_Right.m_Left, geometry.m_Right.m_Right, 0.5f);
                            right2.m_Left = MathUtils.Lerp(geometry.m_Right.m_Left, geometry.m_Right.m_Right, 0.5f);
                            right.m_Right.d = geometry.m_Middle.d;
                            right2.m_Left.d = geometry.m_Middle.d;
                            CheckSegment(edge.m_Start, right, curve.m_Bezier, prefabCompositionData2);
                            CheckSegment(edge.m_Start, right2, curve.m_Bezier, prefabCompositionData2);
                        }
                        else
                        {
                            Segment left = geometry.m_Left;
                            Segment right3 = geometry.m_Right;
                            CheckSegment(edge.m_Start, left, curve.m_Bezier, prefabCompositionData2);
                            CheckSegment(edge.m_Start, right3, curve.m_Bezier, prefabCompositionData2);
                            left.m_Right = geometry.m_Middle;
                            right3.m_Left = geometry.m_Middle;
                            CheckSegment(edge.m_Start, left, curve.m_Bezier, prefabCompositionData2);
                            CheckSegment(edge.m_Start, right3, curve.m_Bezier, prefabCompositionData2);
                        }
                    }
                }
                if (!x.z)
                {
                    return;
                }
                NetCompositionData prefabCompositionData3 = m_PrefabCompositionData[composition.m_EndNode];
                if ((prefabCompositionData3.m_State & CompositionState.Marker) == 0 && ((prefabCompositionData3.m_Flags.m_Left | prefabCompositionData3.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0)
                {
                    if (geometry2.m_MiddleRadius > 0f)
                    {
                        CheckSegment(edge.m_End, geometry2.m_Left, curve.m_Bezier, prefabCompositionData3);
                        Segment right4 = geometry2.m_Right;
                        Segment right5 = geometry2.m_Right;
                        right4.m_Right = MathUtils.Lerp(geometry2.m_Right.m_Left, geometry2.m_Right.m_Right, 0.5f);
                        right4.m_Right.d = geometry2.m_Middle.d;
                        right5.m_Left = right4.m_Right;
                        CheckSegment(edge.m_End, right4, curve.m_Bezier, prefabCompositionData3);
                        CheckSegment(edge.m_End, right5, curve.m_Bezier, prefabCompositionData3);
                    }
                    else
                    {
                        Segment left2 = geometry2.m_Left;
                        Segment right6 = geometry2.m_Right;
                        CheckSegment(edge.m_End, left2, curve.m_Bezier, prefabCompositionData3);
                        CheckSegment(edge.m_End, right6, curve.m_Bezier, prefabCompositionData3);
                        left2.m_Right = geometry2.m_Middle;
                        right6.m_Left = geometry2.m_Middle;
                        CheckSegment(edge.m_End, left2, curve.m_Bezier, prefabCompositionData3);
                        CheckSegment(edge.m_End, right6, curve.m_Bezier, prefabCompositionData3);
                    }
                }
            }

            private void CheckSegment(Entity entity, Segment segment, Bezier4x3 curve, NetCompositionData prefabCompositionData)
            {
                float3 a = segment.m_Left.a;
                float3 @float = segment.m_Right.a;
                for (int i = 1; i <= 8; i++)
                {
                    float t = (float)i / 8f;
                    float3 float2 = MathUtils.Position(segment.m_Left, t);
                    float3 float3 = MathUtils.Position(segment.m_Right, t);
                    Triangle3 triangle = new Triangle3(a, @float, float2);
                    Triangle3 triangle2 = new Triangle3(float3, float2, @float);
                    if (MathUtils.Intersect(triangle.xz, m_Position.xz, out var t2))
                    {
                        float3 position = m_Position;
                        position.y = MathUtils.Position(triangle.y, t2) + prefabCompositionData.m_SurfaceHeight.max;
                        MathUtils.Distance(curve.xz, position.xz, out var t3);
                        m_Result.m_OriginalEntity = entity;
                        m_Result.m_Position = MathUtils.Position(curve, t3);
                        m_Result.m_HitPosition = position;
                        m_Result.m_HitDirection = default(float3);
                        m_Result.m_CurvePosition = t3;
                    }
                    else if (MathUtils.Intersect(triangle2.xz, m_Position.xz, out t2))
                    {
                        float3 position2 = m_Position;
                        position2.y = MathUtils.Position(triangle2.y, t2) + prefabCompositionData.m_SurfaceHeight.max;
                        MathUtils.Distance(curve.xz, position2.xz, out var t4);
                        m_Result.m_OriginalEntity = entity;
                        m_Result.m_Position = MathUtils.Position(curve, t4);
                        m_Result.m_HitPosition = position2;
                        m_Result.m_HitDirection = default(float3);
                        m_Result.m_CurvePosition = t4;
                    }
                    a = float2;
                    @float = float3;
                }
            }
        }

        private struct OriginalObjectIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public Entity m_Parent;

            public Entity m_Result;

            public Bounds3 m_Bounds;

            public float m_BestDistance;

            public bool m_EditorMode;

            public TransportStopData m_TransportStopData1;

            public ComponentLookup<Owner> m_OwnerData;

            public ComponentLookup<Attached> m_AttachedData;

            public ComponentLookup<PrefabRef> m_PrefabRefData;

            public ComponentLookup<NetObjectData> m_NetObjectData;

            public ComponentLookup<TransportStopData> m_TransportStopData;

            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                return MathUtils.Intersect(bounds.m_Bounds, m_Bounds);
            }

            public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
            {
                if (!MathUtils.Intersect(bounds.m_Bounds, m_Bounds) || !m_AttachedData.HasComponent(item) || (!m_EditorMode && m_OwnerData.HasComponent(item)) || m_AttachedData[item].m_Parent != m_Parent)
                {
                    return;
                }
                PrefabRef prefabRef = m_PrefabRefData[item];
                if (!m_NetObjectData.HasComponent(prefabRef.m_Prefab))
                {
                    return;
                }
                TransportStopData transportStopData = default(TransportStopData);
                if (m_TransportStopData.HasComponent(prefabRef.m_Prefab))
                {
                    transportStopData = m_TransportStopData[prefabRef.m_Prefab];
                }
                if (m_TransportStopData1.m_TransportType == transportStopData.m_TransportType)
                {
                    float num = math.distance(MathUtils.Center(m_Bounds), MathUtils.Center(bounds.m_Bounds));
                    if (num < m_BestDistance)
                    {
                        m_Result = item;
                        m_BestDistance = num;
                    }
                }
            }
        }

        private struct ParentObjectIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
        {
            public ControlPoint m_ControlPoint;

            public ControlPoint m_BestSnapPosition;

            public Bounds3 m_Bounds;

            public float m_BestOverlap;

            public bool m_IsBuilding;

            public ObjectGeometryData m_PrefabObjectGeometryData1;

            public ComponentLookup<Transform> m_TransformData;

            public ComponentLookup<PrefabRef> m_PrefabRefData;

            public ComponentLookup<BuildingData> m_BuildingData;

            public ComponentLookup<AssetStampData> m_AssetStampData;

            public ComponentLookup<ObjectGeometryData> m_PrefabObjectGeometryData;

            public bool Intersect(QuadTreeBoundsXZ bounds)
            {
                return MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz);
            }

            public void Iterate(QuadTreeBoundsXZ bounds, Entity item)
            {
                if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Bounds.xz))
                {
                    return;
                }
                PrefabRef prefabRef = m_PrefabRefData[item];
                bool flag = m_BuildingData.HasComponent(prefabRef.m_Prefab);
                bool flag2 = m_AssetStampData.HasComponent(prefabRef.m_Prefab);
                if (m_IsBuilding && !flag2)
                {
                    return;
                }
                float num = m_BestOverlap;
                if (flag || flag2)
                {
                    Transform transform = m_TransformData[item];
                    ObjectGeometryData objectGeometryData = m_PrefabObjectGeometryData[prefabRef.m_Prefab];
                    float3 @float = MathUtils.Center(bounds.m_Bounds);
                    if ((m_PrefabObjectGeometryData1.m_Flags & Game.Objects.GeometryFlags.Circular) != 0)
                    {
                        Circle2 circle = new Circle2(m_PrefabObjectGeometryData1.m_Size.x * 0.5f - 0.01f, (m_ControlPoint.m_Position - @float).xz);
                        Bounds2 intersection;
                        if ((objectGeometryData.m_Flags & Game.Objects.GeometryFlags.Circular) != 0)
                        {
                            Circle2 circle2 = new Circle2(objectGeometryData.m_Size.x * 0.5f - 0.01f, (transform.m_Position - @float).xz);
                            if (MathUtils.Intersect(circle, circle2))
                            {
                                float3 x = default(float3);
                                x.xz = @float.xz + MathUtils.Center(MathUtils.Bounds(circle) & MathUtils.Bounds(circle2));
                                x.y = MathUtils.Center(bounds.m_Bounds.y & m_Bounds.y);
                                num = math.distance(x, m_ControlPoint.m_Position);
                            }
                        }
                        else if (MathUtils.Intersect(ObjectUtils.CalculateBaseCorners(transform.m_Position - @float, transform.m_Rotation, MathUtils.Expand(objectGeometryData.m_Bounds, -0.01f)).xz, circle, out intersection))
                        {
                            float3 x2 = default(float3);
                            x2.xz = @float.xz + MathUtils.Center(intersection);
                            x2.y = MathUtils.Center(bounds.m_Bounds.y & m_Bounds.y);
                            num = math.distance(x2, m_ControlPoint.m_Position);
                        }
                    }
                    else
                    {
                        Quad2 xz = ObjectUtils.CalculateBaseCorners(m_ControlPoint.m_Position - @float, m_ControlPoint.m_Rotation, MathUtils.Expand(m_PrefabObjectGeometryData1.m_Bounds, -0.01f)).xz;
                        if ((objectGeometryData.m_Flags & Game.Objects.GeometryFlags.Circular) != 0)
                        {
                            Circle2 circle3 = new Circle2(objectGeometryData.m_Size.x * 0.5f - 0.01f, (transform.m_Position - @float).xz);
                            if (MathUtils.Intersect(xz, circle3, out var intersection2))
                            {
                                float3 x3 = default(float3);
                                x3.xz = @float.xz + MathUtils.Center(intersection2);
                                x3.y = MathUtils.Center(bounds.m_Bounds.y & m_Bounds.y);
                                num = math.distance(x3, m_ControlPoint.m_Position);
                            }
                        }
                        else
                        {
                            Quad2 xz2 = ObjectUtils.CalculateBaseCorners(transform.m_Position - @float, transform.m_Rotation, MathUtils.Expand(objectGeometryData.m_Bounds, -0.01f)).xz;
                            if (MathUtils.Intersect(xz, xz2, out var intersection3))
                            {
                                float3 x4 = default(float3);
                                x4.xz = @float.xz + MathUtils.Center(intersection3);
                                x4.y = MathUtils.Center(bounds.m_Bounds.y & m_Bounds.y);
                                num = math.distance(x4, m_ControlPoint.m_Position);
                            }
                        }
                    }
                }
                else
                {
                    if (!MathUtils.Intersect(bounds.m_Bounds, m_Bounds) || !m_PrefabObjectGeometryData.HasComponent(prefabRef.m_Prefab))
                    {
                        return;
                    }
                    Transform transform2 = m_TransformData[item];
                    ObjectGeometryData objectGeometryData2 = m_PrefabObjectGeometryData[prefabRef.m_Prefab];
                    float3 float2 = MathUtils.Center(bounds.m_Bounds);
                    quaternion q = math.inverse(m_ControlPoint.m_Rotation);
                    quaternion q2 = math.inverse(transform2.m_Rotation);
                    float3 float3 = math.mul(q, m_ControlPoint.m_Position - float2);
                    float3 float4 = math.mul(q2, transform2.m_Position - float2);
                    if ((m_PrefabObjectGeometryData1.m_Flags & Game.Objects.GeometryFlags.Circular) != 0)
                    {
                        Cylinder3 cylinder = default(Cylinder3);
                        cylinder.circle = new Circle2(m_PrefabObjectGeometryData1.m_Size.x * 0.5f - 0.01f, float3.xz);
                        cylinder.height = new Bounds1(0.01f, m_PrefabObjectGeometryData1.m_Size.y - 0.01f) + float3.y;
                        cylinder.rotation = m_ControlPoint.m_Rotation;
                        if ((objectGeometryData2.m_Flags & Game.Objects.GeometryFlags.Circular) != 0)
                        {
                            Cylinder3 cylinder2 = default(Cylinder3);
                            cylinder2.circle = new Circle2(objectGeometryData2.m_Size.x * 0.5f - 0.01f, float4.xz);
                            cylinder2.height = new Bounds1(0.01f, objectGeometryData2.m_Size.y - 0.01f) + float4.y;
                            cylinder2.rotation = transform2.m_Rotation;
                            float3 pos = default(float3);
                            if (Game.Objects.ValidationHelpers.Intersect(cylinder, cylinder2, ref pos))
                            {
                                num = math.distance(pos, m_ControlPoint.m_Position);
                            }
                        }
                        else
                        {
                            Box3 box = default(Box3);
                            box.bounds = objectGeometryData2.m_Bounds + float4;
                            box.bounds = MathUtils.Expand(box.bounds, -0.01f);
                            box.rotation = transform2.m_Rotation;
                            if (MathUtils.Intersect(cylinder, box, out var cylinderIntersection, out var boxIntersection))
                            {
                                float3 x5 = math.mul(cylinder.rotation, MathUtils.Center(cylinderIntersection));
                                float3 y = math.mul(box.rotation, MathUtils.Center(boxIntersection));
                                num = math.distance(float2 + math.lerp(x5, y, 0.5f), m_ControlPoint.m_Position);
                            }
                        }
                    }
                    else
                    {
                        Box3 box2 = default(Box3);
                        box2.bounds = m_PrefabObjectGeometryData1.m_Bounds + float3;
                        box2.bounds = MathUtils.Expand(box2.bounds, -0.01f);
                        box2.rotation = m_ControlPoint.m_Rotation;
                        if ((objectGeometryData2.m_Flags & Game.Objects.GeometryFlags.Circular) != 0)
                        {
                            Cylinder3 cylinder3 = default(Cylinder3);
                            cylinder3.circle = new Circle2(objectGeometryData2.m_Size.x * 0.5f - 0.01f, float4.xz);
                            cylinder3.height = new Bounds1(0.01f, objectGeometryData2.m_Size.y - 0.01f) + float4.y;
                            cylinder3.rotation = transform2.m_Rotation;
                            if (MathUtils.Intersect(cylinder3, box2, out var cylinderIntersection2, out var boxIntersection2))
                            {
                                float3 x6 = math.mul(box2.rotation, MathUtils.Center(boxIntersection2));
                                float3 y2 = math.mul(cylinder3.rotation, MathUtils.Center(cylinderIntersection2));
                                num = math.distance(float2 + math.lerp(x6, y2, 0.5f), m_ControlPoint.m_Position);
                            }
                        }
                        else
                        {
                            Box3 box3 = default(Box3);
                            box3.bounds = objectGeometryData2.m_Bounds + float4;
                            box3.bounds = MathUtils.Expand(box3.bounds, -0.01f);
                            box3.rotation = transform2.m_Rotation;
                            if (MathUtils.Intersect(box2, box3, out var intersection4, out var intersection5))
                            {
                                float3 x7 = math.mul(box2.rotation, MathUtils.Center(intersection4));
                                float3 y3 = math.mul(box3.rotation, MathUtils.Center(intersection5));
                                num = math.distance(float2 + math.lerp(x7, y3, 0.5f), m_ControlPoint.m_Position);
                            }
                        }
                    }
                }
                if (num < m_BestOverlap)
                {
                    m_BestSnapPosition = m_ControlPoint;
                    m_BestSnapPosition.m_OriginalEntity = item;
                    m_BestSnapPosition.m_ElementIndex = new int2(-1, -1);
                    m_BestOverlap = num;
                }
            }
        }

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
        public bool m_EditorMode;

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
        public ComponentLookup<Transform> m_TransformData;

        [ReadOnly]
        public ComponentLookup<Attached> m_AttachedData;

        [ReadOnly]
        public ComponentLookup<Terrain> m_TerrainData;

        [ReadOnly]
        public ComponentLookup<LocalTransformCache> m_LocalTransformCacheData;

        [ReadOnly]
        public ComponentLookup<Edge> m_EdgeData;

        [ReadOnly]
        public ComponentLookup<Node> m_NodeData;

        [ReadOnly]
        public ComponentLookup<Orphan> m_OrphanData;

        [ReadOnly]
        public ComponentLookup<Curve> m_CurveData;

        [ReadOnly]
        public ComponentLookup<Composition> m_CompositionData;

        [ReadOnly]
        public ComponentLookup<EdgeGeometry> m_EdgeGeometryData;

        [ReadOnly]
        public ComponentLookup<StartNodeGeometry> m_StartNodeGeometryData;

        [ReadOnly]
        public ComponentLookup<EndNodeGeometry> m_EndNodeGeometryData;

        [ReadOnly]
        public ComponentLookup<PrefabRef> m_PrefabRefData;

        [ReadOnly]
        public ComponentLookup<ObjectGeometryData> m_ObjectGeometryData;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_BuildingData;

        [ReadOnly]
        public ComponentLookup<BuildingExtensionData> m_BuildingExtensionData;

        [ReadOnly]
        public ComponentLookup<NetCompositionData> m_PrefabCompositionData;

        [ReadOnly]
        public ComponentLookup<PlaceableObjectData> m_PlaceableObjectData;

        [ReadOnly]
        public ComponentLookup<AssetStampData> m_AssetStampData;

        [ReadOnly]
        public ComponentLookup<OutsideConnectionData> m_OutsideConnectionData;

        [ReadOnly]
        public ComponentLookup<NetObjectData> m_NetObjectData;

        [ReadOnly]
        public ComponentLookup<TransportStopData> m_TransportStopData;

        [ReadOnly]
        public ComponentLookup<StackData> m_StackData;

        [ReadOnly]
        public ComponentLookup<ServiceUpgradeData> m_ServiceUpgradeData;

        [ReadOnly]
        public ComponentLookup<Block> m_BlockData;

        [ReadOnly]
        public BufferLookup<Game.Objects.SubObject> m_SubObjects;

        [ReadOnly]
        public BufferLookup<ConnectedEdge> m_ConnectedEdges;

        [ReadOnly]
        public BufferLookup<NetCompositionArea> m_PrefabCompositionAreas;

        [ReadOnly]
        public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_ObjectSearchTree;

        [ReadOnly]
        public NativeQuadTree<Entity, QuadTreeBoundsXZ> m_NetSearchTree;

        [ReadOnly]
        public NativeQuadTree<Entity, Bounds2> m_ZoneSearchTree;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        public NativeList<ControlPoint> m_ControlPoints;

        public NativeValue<Rotation> m_Rotation;

        public void Execute()
        {
            ControlPoint controlPoint = m_ControlPoints[0];
            if ((m_Snap & (Snap.NetArea | Snap.NetNode)) != 0 && m_TerrainData.HasComponent(controlPoint.m_OriginalEntity) && !m_BuildingData.HasComponent(m_Prefab))
            {
                FindLoweredParent(ref controlPoint);
            }
            ControlPoint bestSnapPosition = controlPoint;
            bestSnapPosition.m_OriginalEntity = Entity.Null;
            if (m_OutsideConnectionData.HasComponent(m_Prefab))
            {
                HandleWorldSize(ref bestSnapPosition, controlPoint);
            }
            float waterSurfaceHeight = float.MinValue;
            if ((m_Snap & Snap.Shoreline) != 0)
            {
                float radius = 1f;
                float3 offset = 0f;
                BuildingExtensionData componentData2;
                if (m_BuildingData.TryGetComponent(m_Prefab, out var componentData))
                {
                    radius = math.length(componentData.m_LotSize) * 4f;
                }
                else if (m_BuildingExtensionData.TryGetComponent(m_Prefab, out componentData2))
                {
                    radius = math.length(componentData2.m_LotSize) * 4f;
                }
                if (m_PlaceableObjectData.TryGetComponent(m_Prefab, out var componentData3))
                {
                    offset = componentData3.m_PlacementOffset;
                }
                SnapShoreline(controlPoint, ref bestSnapPosition, ref waterSurfaceHeight, radius, offset);
            }
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
                ZoneBlockIterator iterator = zoneBlockIterator;
                m_ZoneSearchTree.Iterate(ref iterator);
                bestSnapPosition = iterator.m_BestSnapPosition;
            }
            if ((m_Snap & Snap.OwnerSide) != 0)
            {
                Entity entity = Entity.Null;
                Owner componentData4;
                if (m_Mode == Mode.Upgrade)
                {
                    entity = m_Selected;
                }
                else if (m_Mode == Mode.Move && m_OwnerData.TryGetComponent(m_Selected, out componentData4))
                {
                    entity = componentData4.m_Owner;
                }
                if (entity != Entity.Null)
                {
                    BuildingData buildingData2 = m_BuildingData[m_Prefab];
                    PrefabRef prefabRef = m_PrefabRefData[entity];
                    Transform transform = m_TransformData[entity];
                    BuildingData buildingData3 = m_BuildingData[prefabRef.m_Prefab];
                    int2 lotSize = buildingData3.m_LotSize + buildingData2.m_LotSize.y;
                    Quad2 xz = BuildingUtils.CalculateCorners(transform, lotSize).xz;
                    int num2 = buildingData2.m_LotSize.x - 1;
                    bool flag = false;
                    if (m_ServiceUpgradeData.TryGetComponent(m_Prefab, out var componentData5))
                    {
                        num2 = math.select(num2, componentData5.m_MaxPlacementOffset, componentData5.m_MaxPlacementOffset >= 0);
                        flag |= componentData5.m_MaxPlacementDistance == 0f;
                    }
                    if (!flag)
                    {
                        float2 halfLotSize = (float2)buildingData2.m_LotSize * 4f - 0.4f;
                        Quad2 xz2 = BuildingUtils.CalculateCorners(transform, buildingData3.m_LotSize).xz;
                        Quad2 xz3 = BuildingUtils.CalculateCorners(controlPoint.m_HitPosition, m_Rotation.value.m_Rotation, halfLotSize).xz;
                        flag = MathUtils.Intersect(xz2, xz3) && MathUtils.Intersect(xz, controlPoint.m_HitPosition.xz);
                    }
                    CheckSnapLine(buildingData2, transform, controlPoint, ref bestSnapPosition, new Line2(xz.a, xz.b), num2, 0f, flag);
                    CheckSnapLine(buildingData2, transform, controlPoint, ref bestSnapPosition, new Line2(xz.b, xz.c), num2, MathF.PI / 2f, flag);
                    CheckSnapLine(buildingData2, transform, controlPoint, ref bestSnapPosition, new Line2(xz.c, xz.d), num2, MathF.PI, flag);
                    CheckSnapLine(buildingData2, transform, controlPoint, ref bestSnapPosition, new Line2(xz.d, xz.a), num2, 4.712389f, flag);
                }
            }
            if ((m_Snap & Snap.NetArea) != 0)
            {
                if (m_BuildingData.HasComponent(m_Prefab))
                {
                    if (m_CurveData.TryGetComponent(controlPoint.m_OriginalEntity, out var componentData6))
                    {
                        ControlPoint snapPosition = controlPoint;
                        snapPosition.m_OriginalEntity = Entity.Null;
                        snapPosition.m_Position = MathUtils.Position(componentData6.m_Bezier, controlPoint.m_CurvePosition);
                        snapPosition.m_Direction = math.normalizesafe(MathUtils.Tangent(componentData6.m_Bezier, controlPoint.m_CurvePosition).xz);
                        snapPosition.m_Direction = MathUtils.Left(snapPosition.m_Direction);
                        if (math.dot(math.forward(m_Rotation.value.m_Rotation).xz, snapPosition.m_Direction) < 0f)
                        {
                            snapPosition.m_Direction = -snapPosition.m_Direction;
                        }
                        snapPosition.m_Rotation = ToolUtils.CalculateRotation(snapPosition.m_Direction);
                        snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(1f, 1f, controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz, snapPosition.m_Direction);
                        AddSnapPosition(ref bestSnapPosition, snapPosition);
                    }
                }
                else if (m_EdgeGeometryData.HasComponent(controlPoint.m_OriginalEntity))
                {
                    EdgeGeometry edgeGeometry = m_EdgeGeometryData[controlPoint.m_OriginalEntity];
                    Composition composition = m_CompositionData[controlPoint.m_OriginalEntity];
                    NetCompositionData prefabCompositionData = m_PrefabCompositionData[composition.m_Edge];
                    DynamicBuffer<NetCompositionArea> areas = m_PrefabCompositionAreas[composition.m_Edge];
                    float num3 = 0f;
                    if (m_ObjectGeometryData.HasComponent(m_Prefab))
                    {
                        ObjectGeometryData objectGeometryData = m_ObjectGeometryData[m_Prefab];
                        if ((objectGeometryData.m_Flags & Game.Objects.GeometryFlags.Standing) != 0)
                        {
                            num3 = objectGeometryData.m_LegSize.z * 0.5f;
                            if (objectGeometryData.m_LegSize.y <= prefabCompositionData.m_HeightRange.max)
                            {
                                num3 = math.max(num3, objectGeometryData.m_Size.z * 0.5f);
                            }
                        }
                        else
                        {
                            num3 = objectGeometryData.m_Size.z * 0.5f;
                        }
                    }
                    SnapSegmentAreas(controlPoint, ref bestSnapPosition, num3, controlPoint.m_OriginalEntity, edgeGeometry.m_Start, prefabCompositionData, areas);
                    SnapSegmentAreas(controlPoint, ref bestSnapPosition, num3, controlPoint.m_OriginalEntity, edgeGeometry.m_End, prefabCompositionData, areas);
                }
                else if (m_ConnectedEdges.HasBuffer(controlPoint.m_OriginalEntity))
                {
                    DynamicBuffer<ConnectedEdge> dynamicBuffer = m_ConnectedEdges[controlPoint.m_OriginalEntity];
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        Entity edge = dynamicBuffer[i].m_Edge;
                        Edge edge2 = m_EdgeData[edge];
                        if ((edge2.m_Start != controlPoint.m_OriginalEntity && edge2.m_End != controlPoint.m_OriginalEntity) || !m_EdgeGeometryData.HasComponent(edge))
                        {
                            continue;
                        }
                        EdgeGeometry edgeGeometry2 = m_EdgeGeometryData[edge];
                        Composition composition2 = m_CompositionData[edge];
                        NetCompositionData prefabCompositionData2 = m_PrefabCompositionData[composition2.m_Edge];
                        DynamicBuffer<NetCompositionArea> areas2 = m_PrefabCompositionAreas[composition2.m_Edge];
                        float num4 = 0f;
                        if (m_ObjectGeometryData.HasComponent(m_Prefab))
                        {
                            ObjectGeometryData objectGeometryData2 = m_ObjectGeometryData[m_Prefab];
                            if ((objectGeometryData2.m_Flags & Game.Objects.GeometryFlags.Standing) != 0)
                            {
                                num4 = objectGeometryData2.m_LegSize.z * 0.5f;
                                if (objectGeometryData2.m_LegSize.y <= prefabCompositionData2.m_HeightRange.max)
                                {
                                    num4 = math.max(num4, objectGeometryData2.m_Size.z * 0.5f);
                                }
                            }
                            else
                            {
                                num4 = objectGeometryData2.m_Size.z * 0.5f;
                            }
                        }
                        SnapSegmentAreas(controlPoint, ref bestSnapPosition, num4, edge, edgeGeometry2.m_Start, prefabCompositionData2, areas2);
                        SnapSegmentAreas(controlPoint, ref bestSnapPosition, num4, edge, edgeGeometry2.m_End, prefabCompositionData2, areas2);
                    }
                }
            }
            if ((m_Snap & Snap.NetNode) != 0)
            {
                if (m_NodeData.HasComponent(controlPoint.m_OriginalEntity))
                {
                    Node node = m_NodeData[controlPoint.m_OriginalEntity];
                    SnapNode(controlPoint, ref bestSnapPosition, controlPoint.m_OriginalEntity, node);
                }
                else if (m_EdgeData.HasComponent(controlPoint.m_OriginalEntity))
                {
                    Edge edge3 = m_EdgeData[controlPoint.m_OriginalEntity];
                    SnapNode(controlPoint, ref bestSnapPosition, edge3.m_Start, m_NodeData[edge3.m_Start]);
                    SnapNode(controlPoint, ref bestSnapPosition, edge3.m_End, m_NodeData[edge3.m_End]);
                }
            }
            if ((m_Snap & Snap.ObjectSurface) != 0 && m_TransformData.HasComponent(controlPoint.m_OriginalEntity))
            {
                int parentMesh = controlPoint.m_ElementIndex.x;
                Entity entity2 = controlPoint.m_OriginalEntity;
                while (m_OwnerData.HasComponent(entity2))
                {
                    if (m_LocalTransformCacheData.HasComponent(entity2))
                    {
                        parentMesh = m_LocalTransformCacheData[entity2].m_ParentMesh;
                        parentMesh += math.select(1000, -1000, parentMesh < 0);
                    }
                    entity2 = m_OwnerData[entity2].m_Owner;
                }
                if (m_TransformData.HasComponent(entity2) && m_SubObjects.HasBuffer(entity2))
                {
                    SnapSurface(controlPoint, ref bestSnapPosition, entity2, parentMesh);
                }
            }
            CalculateHeight(ref bestSnapPosition, waterSurfaceHeight);
            if (m_EditorMode)
            {
                if ((m_Snap & Snap.AutoParent) == 0)
                {
                    if ((m_Snap & (Snap.NetArea | Snap.NetNode)) == 0 || m_TransformData.HasComponent(bestSnapPosition.m_OriginalEntity) || m_BuildingData.HasComponent(m_Prefab))
                    {
                        bestSnapPosition.m_OriginalEntity = Entity.Null;
                    }
                }
                else if (bestSnapPosition.m_OriginalEntity == Entity.Null)
                {
                    ObjectGeometryData objectGeometryData3 = default(ObjectGeometryData);
                    if (m_ObjectGeometryData.HasComponent(m_Prefab))
                    {
                        objectGeometryData3 = m_ObjectGeometryData[m_Prefab];
                    }
                    ParentObjectIterator parentObjectIterator = default(ParentObjectIterator);
                    parentObjectIterator.m_ControlPoint = bestSnapPosition;
                    parentObjectIterator.m_BestSnapPosition = bestSnapPosition;
                    parentObjectIterator.m_Bounds = ObjectUtils.CalculateBounds(bestSnapPosition.m_Position, bestSnapPosition.m_Rotation, objectGeometryData3);
                    parentObjectIterator.m_BestOverlap = float.MaxValue;
                    parentObjectIterator.m_IsBuilding = m_BuildingData.HasComponent(m_Prefab);
                    parentObjectIterator.m_PrefabObjectGeometryData1 = objectGeometryData3;
                    parentObjectIterator.m_TransformData = m_TransformData;
                    parentObjectIterator.m_BuildingData = m_BuildingData;
                    parentObjectIterator.m_AssetStampData = m_AssetStampData;
                    parentObjectIterator.m_PrefabRefData = m_PrefabRefData;
                    parentObjectIterator.m_PrefabObjectGeometryData = m_ObjectGeometryData;
                    ParentObjectIterator iterator2 = parentObjectIterator;
                    m_ObjectSearchTree.Iterate(ref iterator2);
                    bestSnapPosition = iterator2.m_BestSnapPosition;
                }
            }
            if (m_Mode == Mode.Create && m_NetObjectData.HasComponent(m_Prefab) && (m_NodeData.HasComponent(bestSnapPosition.m_OriginalEntity) || m_EdgeData.HasComponent(bestSnapPosition.m_OriginalEntity)))
            {
                FindOriginalObject(ref bestSnapPosition, controlPoint);
            }
            Rotation value = m_Rotation.value;
            value.m_IsAligned &= value.m_Rotation.Equals(bestSnapPosition.m_Rotation);
            AlignObject(ref bestSnapPosition, ref value.m_ParentRotation, value.m_IsAligned);
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

        private void FindLoweredParent(ref ControlPoint controlPoint)
        {
            LoweredParentIterator loweredParentIterator = default(LoweredParentIterator);
            loweredParentIterator.m_Result = controlPoint;
            loweredParentIterator.m_Position = controlPoint.m_HitPosition;
            loweredParentIterator.m_EdgeData = m_EdgeData;
            loweredParentIterator.m_NodeData = m_NodeData;
            loweredParentIterator.m_OrphanData = m_OrphanData;
            loweredParentIterator.m_CurveData = m_CurveData;
            loweredParentIterator.m_CompositionData = m_CompositionData;
            loweredParentIterator.m_EdgeGeometryData = m_EdgeGeometryData;
            loweredParentIterator.m_StartNodeGeometryData = m_StartNodeGeometryData;
            loweredParentIterator.m_EndNodeGeometryData = m_EndNodeGeometryData;
            loweredParentIterator.m_PrefabCompositionData = m_PrefabCompositionData;
            LoweredParentIterator iterator = loweredParentIterator;
            m_NetSearchTree.Iterate(ref iterator);
            controlPoint = iterator.m_Result;
        }

        private void FindOriginalObject(ref ControlPoint bestSnapPosition, ControlPoint controlPoint)
        {
            OriginalObjectIterator originalObjectIterator = default(OriginalObjectIterator);
            originalObjectIterator.m_Parent = bestSnapPosition.m_OriginalEntity;
            originalObjectIterator.m_BestDistance = float.MaxValue;
            originalObjectIterator.m_EditorMode = m_EditorMode;
            originalObjectIterator.m_OwnerData = m_OwnerData;
            originalObjectIterator.m_AttachedData = m_AttachedData;
            originalObjectIterator.m_PrefabRefData = m_PrefabRefData;
            originalObjectIterator.m_NetObjectData = m_NetObjectData;
            originalObjectIterator.m_TransportStopData = m_TransportStopData;
            OriginalObjectIterator iterator = originalObjectIterator;
            if (m_ObjectGeometryData.TryGetComponent(m_Prefab, out var componentData))
            {
                iterator.m_Bounds = ObjectUtils.CalculateBounds(bestSnapPosition.m_Position, bestSnapPosition.m_Rotation, componentData);
            }
            else
            {
                iterator.m_Bounds = new Bounds3(bestSnapPosition.m_Position - 1f, bestSnapPosition.m_Position + 1f);
            }
            if (m_TransportStopData.TryGetComponent(m_Prefab, out var componentData2))
            {
                iterator.m_TransportStopData1 = componentData2;
            }
            m_ObjectSearchTree.Iterate(ref iterator);
            if (iterator.m_Result != Entity.Null)
            {
                bestSnapPosition.m_OriginalEntity = iterator.m_Result;
            }
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

        public static void AlignRotation(ref quaternion rotation, quaternion parentRotation, bool zAxis)
        {
            if (zAxis)
            {
                float3 forward = math.rotate(rotation, new float3(0f, 0f, 1f));
                float3 up = math.rotate(parentRotation, new float3(0f, 1f, 0f));
                quaternion a = quaternion.LookRotationSafe(forward, up);
                quaternion q = rotation;
                float num = float.MaxValue;
                for (int i = 0; i < 8; i++)
                {
                    quaternion quaternion = math.mul(a, Unity.Mathematics.quaternion.RotateZ((float)i * (MathF.PI / 4f)));
                    float num2 = MathUtils.RotationAngle(rotation, quaternion);
                    if (num2 < num)
                    {
                        q = quaternion;
                        num = num2;
                    }
                }
                rotation = math.normalizesafe(q, quaternion.identity);
                return;
            }
            float3 forward2 = math.rotate(rotation, new float3(0f, 1f, 0f));
            float3 up2 = math.rotate(parentRotation, new float3(1f, 0f, 0f));
            quaternion a2 = math.mul(quaternion.LookRotationSafe(forward2, up2), quaternion.RotateX(MathF.PI / 2f));
            quaternion q2 = rotation;
            float num3 = float.MaxValue;
            for (int j = 0; j < 8; j++)
            {
                quaternion quaternion2 = math.mul(a2, quaternion.RotateY((float)j * (MathF.PI / 4f)));
                float num4 = MathUtils.RotationAngle(rotation, quaternion2);
                if (num4 < num3)
                {
                    q2 = quaternion2;
                    num3 = num4;
                }
            }
            rotation = math.normalizesafe(q2, quaternion.identity);
        }

        private void AlignObject(ref ControlPoint controlPoint, ref quaternion parentRotation, bool alignRotation)
        {
            PlaceableObjectData placeableObjectData = default(PlaceableObjectData);
            if (m_PlaceableObjectData.HasComponent(m_Prefab))
            {
                placeableObjectData = m_PlaceableObjectData[m_Prefab];
            }
            if ((placeableObjectData.m_Flags & Game.Objects.PlacementFlags.Hanging) != 0)
            {
                ObjectGeometryData objectGeometryData = m_ObjectGeometryData[m_Prefab];
                controlPoint.m_Position.y -= objectGeometryData.m_Bounds.max.y;
            }
            parentRotation = quaternion.identity;
            if (m_TransformData.HasComponent(controlPoint.m_OriginalEntity))
            {
                Entity entity = controlPoint.m_OriginalEntity;
                PrefabRef prefabRef = m_PrefabRefData[entity];
                parentRotation = m_TransformData[entity].m_Rotation;
                while (m_OwnerData.HasComponent(entity) && !m_BuildingData.HasComponent(prefabRef.m_Prefab))
                {
                    entity = m_OwnerData[entity].m_Owner;
                    prefabRef = m_PrefabRefData[entity];
                    if (m_TransformData.HasComponent(entity))
                    {
                        parentRotation = m_TransformData[entity].m_Rotation;
                    }
                }
            }
            if ((placeableObjectData.m_Flags & Game.Objects.PlacementFlags.Wall) != 0)
            {
                float3 @float = math.forward(controlPoint.m_Rotation);
                float3 value = controlPoint.m_HitDirection;
                value.y = math.select(value.y, 0f, (m_Snap & Snap.Upright) != 0);
                if (!MathUtils.TryNormalize(ref value))
                {
                    value = @float;
                    value.y = math.select(value.y, 0f, (m_Snap & Snap.Upright) != 0);
                    if (!MathUtils.TryNormalize(ref value))
                    {
                        value = new float3(0f, 0f, 1f);
                    }
                }
                float3 value2 = math.cross(@float, value);
                if (MathUtils.TryNormalize(ref value2))
                {
                    float angle = math.acos(math.clamp(math.dot(@float, value), -1f, 1f));
                    controlPoint.m_Rotation = math.normalizesafe(math.mul(quaternion.AxisAngle(value2, angle), controlPoint.m_Rotation), quaternion.identity);
                    if (alignRotation)
                    {
                        AlignRotation(ref controlPoint.m_Rotation, parentRotation, zAxis: true);
                    }
                }
                controlPoint.m_Position += math.forward(controlPoint.m_Rotation) * placeableObjectData.m_PlacementOffset.z;
                return;
            }
            float3 float2 = math.rotate(controlPoint.m_Rotation, new float3(0f, 1f, 0f));
            float3 hitDirection = controlPoint.m_HitDirection;
            hitDirection = math.select(hitDirection, new float3(0f, 1f, 0f), (m_Snap & Snap.Upright) != 0);
            if (!MathUtils.TryNormalize(ref hitDirection))
            {
                hitDirection = float2;
            }
            float3 value3 = math.cross(float2, hitDirection);
            if (MathUtils.TryNormalize(ref value3))
            {
                float angle2 = math.acos(math.clamp(math.dot(float2, hitDirection), -1f, 1f));
                controlPoint.m_Rotation = math.normalizesafe(math.mul(quaternion.AxisAngle(value3, angle2), controlPoint.m_Rotation), quaternion.identity);
                if (alignRotation)
                {
                    AlignRotation(ref controlPoint.m_Rotation, parentRotation, zAxis: false);
                }
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

        private void SnapSurface(ControlPoint controlPoint, ref ControlPoint bestPosition, Entity entity, int parentMesh)
        {
            Transform transform = m_TransformData[entity];
            ControlPoint snapPosition = controlPoint;
            snapPosition.m_OriginalEntity = entity;
            snapPosition.m_ElementIndex.x = parentMesh;
            snapPosition.m_Position = controlPoint.m_HitPosition;
            snapPosition.m_Direction = math.forward(transform.m_Rotation).xz;
            snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz, snapPosition.m_Direction);
            AddSnapPosition(ref bestPosition, snapPosition);
        }

        private void SnapNode(ControlPoint controlPoint, ref ControlPoint bestPosition, Entity entity, Node node)
        {
            Bounds1 bounds = new Bounds1(float.MaxValue, float.MinValue);
            DynamicBuffer<ConnectedEdge> dynamicBuffer = m_ConnectedEdges[entity];
            for (int i = 0; i < dynamicBuffer.Length; i++)
            {
                Entity edge = dynamicBuffer[i].m_Edge;
                Edge edge2 = m_EdgeData[edge];
                if (edge2.m_Start == entity)
                {
                    Composition composition = m_CompositionData[edge];
                    bounds |= m_PrefabCompositionData[composition.m_StartNode].m_SurfaceHeight;
                }
                else if (edge2.m_End == entity)
                {
                    Composition composition2 = m_CompositionData[edge];
                    bounds |= m_PrefabCompositionData[composition2.m_EndNode].m_SurfaceHeight;
                }
            }
            ControlPoint snapPosition = controlPoint;
            snapPosition.m_OriginalEntity = entity;
            snapPosition.m_Position = node.m_Position;
            if (bounds.min < float.MaxValue)
            {
                snapPosition.m_Position.y += bounds.min;
            }
            snapPosition.m_Direction = math.normalizesafe(math.forward(node.m_Rotation)).xz;
            snapPosition.m_Rotation = node.m_Rotation;
            snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(1f, 1f, controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz, snapPosition.m_Direction);
            AddSnapPosition(ref bestPosition, snapPosition);
        }

        private void SnapShoreline(ControlPoint controlPoint, ref ControlPoint bestPosition, ref float waterSurfaceHeight, float radius, float3 offset)
        {
            int2 x = (int2)math.floor(WaterUtils.ToSurfaceSpace(ref m_WaterSurfaceData, controlPoint.m_HitPosition - radius).xz);
            int2 x2 = (int2)math.ceil(WaterUtils.ToSurfaceSpace(ref m_WaterSurfaceData, controlPoint.m_HitPosition + radius).xz);
            x = math.max(x, default(int2));
            x2 = math.min(x2, m_WaterSurfaceData.resolution.xz - 1);
            float3 @float = default(float3);
            float3 float2 = default(float3);
            float2 float3 = default(float2);
            for (int i = x.y; i <= x2.y; i++)
            {
                for (int j = x.x; j <= x2.x; j++)
                {
                    float3 worldPosition = WaterUtils.GetWorldPosition(ref m_WaterSurfaceData, new int2(j, i));
                    if (worldPosition.y > 0.2f)
                    {
                        float num = TerrainUtils.SampleHeight(ref m_TerrainHeightData, worldPosition) + worldPosition.y;
                        float num2 = math.max(0f, radius * radius - math.distancesq(worldPosition.xz, controlPoint.m_HitPosition.xz));
                        worldPosition.y = (worldPosition.y - 0.2f) * num2;
                        worldPosition.xz *= worldPosition.y;
                        float2 += worldPosition;
                        num *= num2;
                        float3 += new float2(num, num2);
                    }
                    else if (worldPosition.y < 0.2f)
                    {
                        float num3 = math.max(0f, radius * radius - math.distancesq(worldPosition.xz, controlPoint.m_HitPosition.xz));
                        worldPosition.y = (0.2f - worldPosition.y) * num3;
                        worldPosition.xz *= worldPosition.y;
                        @float += worldPosition;
                    }
                }
            }
            if (@float.y != 0f && float2.y != 0f && float3.y != 0f)
            {
                @float /= @float.y;
                float2 /= float2.y;
                float3 value = default(float3);
                value.xz = @float.xz - float2.xz;
                if (MathUtils.TryNormalize(ref value))
                {
                    waterSurfaceHeight = float3.x / float3.y;
                    bestPosition = controlPoint;
                    bestPosition.m_Position.xz = math.lerp(float2.xz, @float.xz, 0.5f);
                    bestPosition.m_Position.y = waterSurfaceHeight + offset.y;
                    bestPosition.m_Position += value * offset.z;
                    bestPosition.m_Direction = value.xz;
                    bestPosition.m_Rotation = ToolUtils.CalculateRotation(bestPosition.m_Direction);
                    bestPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(0f, 1f, controlPoint.m_HitPosition.xz, bestPosition.m_Position.xz, bestPosition.m_Direction);
                    bestPosition.m_OriginalEntity = Entity.Null;
                }
            }
        }

        private void SnapSegmentAreas(ControlPoint controlPoint, ref ControlPoint bestPosition, float radius, Entity entity, Segment segment1, NetCompositionData prefabCompositionData1, DynamicBuffer<NetCompositionArea> areas1)
        {
            for (int i = 0; i < areas1.Length; i++)
            {
                NetCompositionArea netCompositionArea = areas1[i];
                if ((netCompositionArea.m_Flags & NetAreaFlags.Buildable) == 0)
                {
                    continue;
                }
                float num = netCompositionArea.m_Width * 0.51f;
                if (!(radius >= num))
                {
                    Bezier4x3 curve = MathUtils.Lerp(segment1.m_Left, segment1.m_Right, netCompositionArea.m_Position.x / prefabCompositionData1.m_Width + 0.5f);
                    MathUtils.Distance(curve.xz, controlPoint.m_HitPosition.xz, out var t);
                    ControlPoint snapPosition = controlPoint;
                    snapPosition.m_OriginalEntity = entity;
                    snapPosition.m_Position = MathUtils.Position(curve, t);
                    snapPosition.m_Direction = math.normalizesafe(MathUtils.Tangent(curve, t).xz);
                    if ((netCompositionArea.m_Flags & NetAreaFlags.Invert) != 0)
                    {
                        snapPosition.m_Direction = MathUtils.Right(snapPosition.m_Direction);
                    }
                    else
                    {
                        snapPosition.m_Direction = MathUtils.Left(snapPosition.m_Direction);
                    }
                    float3 @float = MathUtils.Position(MathUtils.Lerp(segment1.m_Left, segment1.m_Right, netCompositionArea.m_SnapPosition.x / prefabCompositionData1.m_Width + 0.5f), t);
                    float maxLength = math.max(0f, math.min(netCompositionArea.m_Width * 0.5f, math.abs(netCompositionArea.m_SnapPosition.x - netCompositionArea.m_Position.x) + netCompositionArea.m_SnapWidth * 0.5f) - radius);
                    float maxLength2 = math.max(0f, netCompositionArea.m_SnapWidth * 0.5f - radius);
                    snapPosition.m_Position.xz += MathUtils.ClampLength(@float.xz - snapPosition.m_Position.xz, maxLength);
                    snapPosition.m_Position.xz += MathUtils.ClampLength(controlPoint.m_HitPosition.xz - snapPosition.m_Position.xz, maxLength2);
                    snapPosition.m_Position.y += netCompositionArea.m_Position.y;
                    snapPosition.m_Rotation = ToolUtils.CalculateRotation(snapPosition.m_Direction);
                    snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(1f, 1f, controlPoint.m_HitPosition.xz, snapPosition.m_Position.xz, snapPosition.m_Direction);
                    AddSnapPosition(ref bestPosition, snapPosition);
                }
            }
        }

        private static Bounds3 SetHeightRange(Bounds3 bounds, Bounds1 heightRange)
        {
            bounds.min.y += heightRange.min;
            bounds.max.y += heightRange.max;
            return bounds;
        }

        private static void CheckSnapLine(BuildingData buildingData, Transform ownerTransformData, ControlPoint controlPoint, ref ControlPoint bestPosition, Line2 line, int maxOffset, float angle, bool forceSnap)
        {
            MathUtils.Distance(line, controlPoint.m_Position.xz, out var t);
            float num = math.select(0f, 4f, ((buildingData.m_LotSize.x - buildingData.m_LotSize.y) & 1) != 0);
            float num2 = (float)math.min(2 * maxOffset - buildingData.m_LotSize.y - buildingData.m_LotSize.x, buildingData.m_LotSize.y - buildingData.m_LotSize.x) * 4f;
            float num3 = math.distance(line.a, line.b);
            t *= num3;
            t = MathUtils.Snap(t + num, 8f) - num;
            t = math.clamp(t, 0f - num2, num3 + num2);
            ControlPoint snapPosition = controlPoint;
            snapPosition.m_OriginalEntity = Entity.Null;
            snapPosition.m_Position.y = ownerTransformData.m_Position.y;
            snapPosition.m_Position.xz = MathUtils.Position(line, t / num3);
            snapPosition.m_Direction = math.mul(math.mul(ownerTransformData.m_Rotation, quaternion.RotateY(angle)), new float3(0f, 0f, 1f)).xz;
            snapPosition.m_Rotation = ToolUtils.CalculateRotation(snapPosition.m_Direction);
            float level = math.select(0f, 1f, forceSnap);
            snapPosition.m_SnapPriority = ToolUtils.CalculateSnapPriority(level, 1f, controlPoint.m_HitPosition.xz * 0.5f, snapPosition.m_Position.xz * 0.5f, snapPosition.m_Direction);
            AddSnapPosition(ref bestPosition, snapPosition);
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
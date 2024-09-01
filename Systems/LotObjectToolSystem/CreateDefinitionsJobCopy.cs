



using Colossal.Collections;
using Colossal.Mathematics;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using static Game.Tools.ObjectToolBaseSystem;
using AgeMask = Game.Tools.AgeMask;
using EditorContainer = Game.Tools.EditorContainer;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct CreateDefinitionsJobCopy : IJob
    {
        /// <summary>
        /// is it used for specifing probabilities of picking prefabs for those random prefab generation?
        /// </summary>
        private struct VariationData
        {
            public Entity m_Prefab;

            public int m_Probability;
        }


        [ReadOnly]
        public bool m_LefthandTraffic;

        [ReadOnly]
        public Entity m_ObjectPrefab;

        [ReadOnly]
        public Entity m_Theme;

        [ReadOnly]
        public RandomSeed m_RandomSeed;

        [ReadOnly]
        public NativeList<ControlPoint> m_ControlPoints;

        [NativeDisableContainerSafetyRestriction]
        [ReadOnly]
        public NativeReference<AttachmentData> m_AttachmentPrefab;

        [ReadOnly]
        public ComponentLookup<Edge> m_EdgeData;

        [ReadOnly]
        public ComponentLookup<Game.Net.Node> m_NodeData;

        [ReadOnly]
        public ComponentLookup<Curve> m_CurveData;

        [ReadOnly]
        public ComponentLookup<Game.Net.Elevation> m_NetElevationData;

        [ReadOnly]
        public ComponentLookup<Orphan> m_OrphanData;

        [ReadOnly]
        public ComponentLookup<Composition> m_CompositionData;

        [ReadOnly]
        public ComponentLookup<NetObjectData> m_PrefabNetObjectData;

        [ReadOnly]
        public ComponentLookup<BuildingData> m_PrefabBuildingData;

        [ReadOnly]
        public ComponentLookup<AssetStampData> m_PrefabAssetStampData;

        [ReadOnly]
        public ComponentLookup<SpawnableObjectData> m_PrefabSpawnableObjectData;

        [ReadOnly]
        public ComponentLookup<PlaceableObjectData> m_PrefabPlaceableObjectData;

        [ReadOnly]
        public ComponentLookup<AreaGeometryData> m_PrefabAreaGeometryData;

        [ReadOnly]
        public ComponentLookup<PlaceholderBuildingData> m_PlaceholderBuildingData;

        [ReadOnly]
        public ComponentLookup<CreatureSpawnData> m_PrefabCreatureSpawnData;

        [ReadOnly]
        public BufferLookup<Game.Objects.SubObject> m_SubObjects;

        [ReadOnly]
        public ComponentLookup<NetGeometryData> m_PrefabNetGeometryData;

        [ReadOnly]
        public ComponentLookup<NetCompositionData> m_PrefabCompositionData;

        [ReadOnly]
        public BufferLookup<ConnectedEdge> m_ConnectedEdges;

        [ReadOnly]
        public BufferLookup<Game.Prefabs.SubObject> m_PrefabSubObjects;

        [ReadOnly]
        public BufferLookup<Game.Prefabs.SubNet> m_PrefabSubNets;

        [ReadOnly]
        public BufferLookup<Game.Prefabs.SubArea> m_PrefabSubAreas;

        [ReadOnly]
        public BufferLookup<SubAreaNode> m_PrefabSubAreaNodes;

        [ReadOnly]
        public BufferLookup<PlaceholderObjectElement> m_PrefabPlaceholderElements;

        [ReadOnly]
        public BufferLookup<ObjectRequirementElement> m_PrefabRequirementElements;

        [ReadOnly]
        public WaterSurfaceData m_WaterSurfaceData;

        [ReadOnly]
        public TerrainHeightData m_TerrainHeightData;

        public EntityCommandBuffer m_CommandBuffer;
        

        public void Execute()
        {
            // is it the point player's mouse hits?
            ControlPoint startPoint = m_ControlPoints[0];

            OwnerDefinition ownerDefinition = default(OwnerDefinition);
            int parentMesh = -1;



            NativeList<ClearAreaData> clearAreas = default(NativeList<ClearAreaData>);



            // 
            if (m_ObjectPrefab != Entity.Null)
            {
                // so here is creating a single object entity (with its owner/sub entity, etc. ?)?
                {
                    Entity objectPrefabEntity = m_ObjectPrefab;

                    // looks like the random prefab picking a actual prefab object by their probability
                    // I guess this one affects lots entities creations, maybe net entities (which has lots of placeholders) also depends on it
                    if (
                        // originalEntity == Entity.Null && 
                        // ownerDefinition.m_Prefab == Entity.Null && 
                        m_PrefabPlaceholderElements.TryGetBuffer(m_ObjectPrefab, out var placeHolders) && 
                        !m_PrefabCreatureSpawnData.HasComponent(m_ObjectPrefab))
                    {
                        Unity.Mathematics.Random random = m_RandomSeed.GetRandom(1000000);
                        int num2 = 0;
                        for (int j = 0; j < placeHolders.Length; j++)
                        {
                            if (GetVariationData(placeHolders[j], out var variation))
                            {
                                num2 += variation.m_Probability;
                                if (random.NextInt(num2) < variation.m_Probability)
                                {
                                    objectPrefabEntity = variation.m_Prefab;
                                }
                            }
                        }
                    }


                    UpdateObject(
                        objectPrefabEntity, 
                        startPoint.m_OriginalEntity, 
                        new Game.Objects.Transform(startPoint.m_Position, startPoint.m_Rotation), 
                        startPoint.m_Elevation, 
                        ownerDefinition, 
                        clearAreas, 
                        topLevel: true, 
                        parentMesh, 
                        0);
                    
                    // creating attachments
                    if (m_AttachmentPrefab.IsCreated && m_AttachmentPrefab.Value.m_Entity != Entity.Null)
                    {
                        Game.Objects.Transform transform3 = new Game.Objects.Transform(startPoint.m_Position, startPoint.m_Rotation);
                        transform3.m_Position += math.rotate(transform3.m_Rotation, m_AttachmentPrefab.Value.m_Offset);
                        UpdateObject(
                            m_AttachmentPrefab.Value.m_Entity, 
                            objectPrefabEntity,
                            transform3, 
                            startPoint.m_Elevation, 
                            ownerDefinition, 
                            clearAreas, 
                            topLevel: true, 
                            parentMesh, 0
                            );
                    }
                }
            }


            if (clearAreas.IsCreated)
            {
                clearAreas.Dispose();
            }
        }

        private bool GetVariationData(PlaceholderObjectElement placeholder, out VariationData variation)
        {
            variation = new VariationData
            {
                m_Prefab = placeholder.m_Object,
                m_Probability = 100
            };
            if (m_PrefabRequirementElements.TryGetBuffer(variation.m_Prefab, out var bufferData))
            {
                int num = -1;
                bool flag = true;
                for (int i = 0; i < bufferData.Length; i++)
                {
                    ObjectRequirementElement objectRequirementElement = bufferData[i];
                    if (objectRequirementElement.m_Group != num)
                    {
                        if (!flag)
                        {
                            break;
                        }
                        num = objectRequirementElement.m_Group;
                        flag = false;
                    }
                    flag |= m_Theme == objectRequirementElement.m_Requirement;
                }
                if (!flag)
                {
                    return false;
                }
            }
            if (m_PrefabSpawnableObjectData.TryGetComponent(variation.m_Prefab, out var componentData))
            {
                variation.m_Probability = componentData.m_Probability;
            }
            return true;
        }

        private bool CheckParentPrefab(Entity parentPrefab, Entity objectPrefab)
        {
            if (parentPrefab == objectPrefab)
            {
                return false;
            }
            if (m_PrefabSubObjects.HasBuffer(objectPrefab))
            {
                DynamicBuffer<Game.Prefabs.SubObject> dynamicBuffer = m_PrefabSubObjects[objectPrefab];
                for (int i = 0; i < dynamicBuffer.Length; i++)
                {
                    if (!CheckParentPrefab(parentPrefab, dynamicBuffer[i].m_Prefab))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// this method is recursive method, guess it is for handling recursive sub element creations
        /// </summary>
        /// <param name="objectPrefab"></param>
        /// <param name="parent"></param>
        /// <param name="transform"></param>
        /// <param name="elevation"></param>
        /// <param name="ownerDefinition"></param>
        /// <param name="clearAreas"></param>
        /// <param name="topLevel"></param>
        /// <param name="parentMesh"></param>
        /// <param name="randomIndex"></param>
        private void UpdateObject(
            Entity objectPrefab, 
            Entity parent, 
            Game.Objects.Transform transform, 
            float elevation, 
            OwnerDefinition ownerDefinition, 
            NativeList<ClearAreaData> clearAreas, 
            bool topLevel,
            int parentMesh, 
            int randomIndex
            )
        {

            OwnerDefinition ownerDefinition2 = ownerDefinition;
            Unity.Mathematics.Random random = m_RandomSeed.GetRandom(randomIndex);
            if (!m_PrefabAssetStampData.HasComponent(objectPrefab) || (ownerDefinition.m_Prefab == Entity.Null))
            {
                Entity e = m_CommandBuffer.CreateEntity();
                CreationDefinition component = default(CreationDefinition);
                component.m_Prefab = objectPrefab;
                component.m_SubPrefab = Entity.Null;
                component.m_Owner = Entity.Null; //owner;
                component.m_Original = Entity.Null;
                component.m_RandomSeed = random.NextInt();


                if (m_PrefabBuildingData.HasComponent(objectPrefab))
                {
                    parentMesh = -1;
                }
                ObjectDefinition component2 = default(ObjectDefinition);
                component2.m_ParentMesh = parentMesh;
                component2.m_Position = transform.m_Position;
                component2.m_Rotation = transform.m_Rotation;
                component2.m_Probability = 100;
                component2.m_PrefabSubIndex = -1;
                component2.m_Scale = 1f;
                component2.m_Intensity = 1f;


                if (m_PrefabPlaceableObjectData.HasComponent(objectPrefab))
                {
                    PlaceableObjectData placeableObjectData = m_PrefabPlaceableObjectData[objectPrefab];
                    if ((placeableObjectData.m_Flags & Game.Objects.PlacementFlags.HasProbability) != 0)
                    {
                        component2.m_Probability = placeableObjectData.m_DefaultProbability;
                    }
                }


                if (parentMesh != -1)
                {
                    component2.m_Elevation = transform.m_Position.y - ownerDefinition.m_Position.y;
                }
                else
                {
                    component2.m_Elevation = elevation;
                }

                if (ownerDefinition.m_Prefab != Entity.Null)
                {
                    m_CommandBuffer.AddComponent(e, ownerDefinition);
                    Game.Objects.Transform transform2 = ObjectUtils.WorldToLocal(ObjectUtils.InverseTransform(new Game.Objects.Transform(ownerDefinition.m_Position, ownerDefinition.m_Rotation)), transform);
                    component2.m_LocalPosition = transform2.m_Position;
                    component2.m_LocalRotation = transform2.m_Rotation;
                }
                else
                {
                    component2.m_LocalPosition = transform.m_Position;
                    component2.m_LocalRotation = transform.m_Rotation;
                }

                if (m_SubObjects.HasBuffer(parent))
                {
                    component.m_Flags |= CreationFlags.Attach;
                    if (parentMesh == -1 && m_NetElevationData.HasComponent(parent))
                    {
                        component2.m_ParentMesh = 0;
                        component2.m_Elevation = math.csum(m_NetElevationData[parent].m_Elevation) * 0.5f;
                        if (IsLoweredParent(parent))
                        {
                            component.m_Flags |= CreationFlags.Lowered;
                        }
                    }
                    if (m_PrefabNetObjectData.HasComponent(objectPrefab))
                    {
                        UpdateAttachedParent(parent);
                    }
                    else
                    {
                        component.m_Attached = parent;
                    }
                }
                else if (m_PlaceholderBuildingData.HasComponent(parent))
                {
                    component.m_Flags |= CreationFlags.Attach;
                    component.m_Attached = parent;
                }


                ownerDefinition2.m_Prefab = objectPrefab;
                ownerDefinition2.m_Position = component2.m_Position;
                ownerDefinition2.m_Rotation = component2.m_Rotation;
                m_CommandBuffer.AddComponent(e, component);
                m_CommandBuffer.AddComponent(e, component2);
                m_CommandBuffer.AddComponent(e, default(Updated));
            }
            else
            {
                if (m_PrefabSubObjects.HasBuffer(objectPrefab))
                {
                    DynamicBuffer<Game.Prefabs.SubObject> dynamicBuffer = m_PrefabSubObjects[objectPrefab];
                    for (int i = 0; i < dynamicBuffer.Length; i++)
                    {
                        Game.Prefabs.SubObject subObject = dynamicBuffer[i];
                        Game.Objects.Transform transform4 = ObjectUtils.LocalToWorld(transform, subObject.m_Position, subObject.m_Rotation);
                        UpdateObject(
                            subObject.m_Prefab, 
                            parent, 
                            transform4, elevation, 
                            ownerDefinition, 
                            default(NativeList<ClearAreaData>), 
                            topLevel: false, 
                            parentMesh, 
                            i);
                    }
                }
                topLevel = true;
            }
            NativeParallelHashMap<Entity, int> selectedSpawnables = default(NativeParallelHashMap<Entity, int>);

            UpdateSubNets(
                transform, 
                objectPrefab,
                topLevel, 
                ownerDefinition2, 
                clearAreas, 
                ref random
                );

            UpdateSubAreas(
                transform, 
                objectPrefab, 
                topLevel, 
                ownerDefinition2, 
                clearAreas, 
                ref random, 
                ref selectedSpawnables
                );

            if (selectedSpawnables.IsCreated)
            {
                selectedSpawnables.Dispose();
            }
        }

        private void UpdateAttachedParent(Entity parent)
        {


            if (m_EdgeData.HasComponent(parent))
            {
                Edge edge = m_EdgeData[parent];
                Entity e = m_CommandBuffer.CreateEntity();
                CreationDefinition component = default(CreationDefinition);
                component.m_Original = parent;
                component.m_Flags |= CreationFlags.Align;
                m_CommandBuffer.AddComponent(e, component);
                m_CommandBuffer.AddComponent(e, default(Updated));
                NetCourse component2 = default(NetCourse);
                component2.m_Curve = m_CurveData[parent].m_Bezier;
                component2.m_Length = MathUtils.Length(component2.m_Curve);
                component2.m_FixedIndex = -1;
                component2.m_StartPosition.m_Entity = edge.m_Start;
                component2.m_StartPosition.m_Position = component2.m_Curve.a;
                component2.m_StartPosition.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(component2.m_Curve));
                component2.m_StartPosition.m_CourseDelta = 0f;
                component2.m_EndPosition.m_Entity = edge.m_End;
                component2.m_EndPosition.m_Position = component2.m_Curve.d;
                component2.m_EndPosition.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(component2.m_Curve));
                component2.m_EndPosition.m_CourseDelta = 1f;
                m_CommandBuffer.AddComponent(e, component2);
            }
            else if (m_NodeData.HasComponent(parent))
            {
                Game.Net.Node node = m_NodeData[parent];
                Entity e2 = m_CommandBuffer.CreateEntity();
                CreationDefinition component3 = default(CreationDefinition);
                component3.m_Original = parent;
                m_CommandBuffer.AddComponent(e2, component3);
                m_CommandBuffer.AddComponent(e2, default(Updated));
                NetCourse component4 = default(NetCourse);
                component4.m_Curve = new Bezier4x3(node.m_Position, node.m_Position, node.m_Position, node.m_Position);
                component4.m_Length = 0f;
                component4.m_FixedIndex = -1;
                component4.m_StartPosition.m_Entity = parent;
                component4.m_StartPosition.m_Position = node.m_Position;
                component4.m_StartPosition.m_Rotation = node.m_Rotation;
                component4.m_StartPosition.m_CourseDelta = 0f;
                component4.m_EndPosition.m_Entity = parent;
                component4.m_EndPosition.m_Position = node.m_Position;
                component4.m_EndPosition.m_Rotation = node.m_Rotation;
                component4.m_EndPosition.m_CourseDelta = 1f;
                m_CommandBuffer.AddComponent(e2, component4);
            }
        }

        private bool IsLoweredParent(Entity entity)
        {
            if (m_CompositionData.TryGetComponent(entity, out var componentData) && m_PrefabCompositionData.TryGetComponent(componentData.m_Edge, out var componentData2) && ((componentData2.m_Flags.m_Left | componentData2.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0)
            {
                return true;
            }
            if (m_OrphanData.TryGetComponent(entity, out var componentData3) && m_PrefabCompositionData.TryGetComponent(componentData3.m_Composition, out componentData2) && ((componentData2.m_Flags.m_Left | componentData2.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0)
            {
                return true;
            }
            if (m_ConnectedEdges.TryGetBuffer(entity, out var bufferData))
            {
                for (int i = 0; i < bufferData.Length; i++)
                {
                    ConnectedEdge connectedEdge = bufferData[i];
                    Edge edge = m_EdgeData[connectedEdge.m_Edge];
                    if (edge.m_Start == entity)
                    {
                        if (m_CompositionData.TryGetComponent(connectedEdge.m_Edge, out componentData) && m_PrefabCompositionData.TryGetComponent(componentData.m_StartNode, out componentData2) && ((componentData2.m_Flags.m_Left | componentData2.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0)
                        {
                            return true;
                        }
                    }
                    else if (edge.m_End == entity && m_CompositionData.TryGetComponent(connectedEdge.m_Edge, out componentData) && m_PrefabCompositionData.TryGetComponent(componentData.m_EndNode, out componentData2) && ((componentData2.m_Flags.m_Left | componentData2.m_Flags.m_Right) & CompositionFlags.Side.Lowered) != 0)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        private void CreateSubNet(
            Entity netPrefab, 
            Entity lanePrefab, // Entity.Null
            Bezier4x3 curve, 
            int2 nodeIndex, 
            int2 parentMesh, 
            CompositionFlags upgrades, 
            NativeList<float4> nodePositions, 
            Game.Objects.Transform parentTransform, 
            OwnerDefinition ownerDefinition, 
            NativeList<ClearAreaData> clearAreas, 
            ref Unity.Mathematics.Random random)
        {
            m_PrefabNetGeometryData.TryGetComponent(netPrefab, out var componentData);
            CreationDefinition component = default(CreationDefinition);
            component.m_Prefab = netPrefab;
            component.m_SubPrefab = lanePrefab;
            component.m_RandomSeed = random.NextInt();
            bool flag = parentMesh.x >= 0 && parentMesh.y >= 0;
            NetCourse component2 = default(NetCourse);
            if ((componentData.m_Flags & Game.Net.GeometryFlags.OnWater) != 0)
            {
                curve.y = default(Bezier4x1);
                Curve curve2 = default(Curve);
                curve2.m_Bezier = ObjectUtils.LocalToWorld(parentTransform.m_Position, parentTransform.m_Rotation, curve);
                Curve curve3 = curve2;
                component2.m_Curve = NetUtils.AdjustPosition(curve3, fixedStart: false, linearMiddle: false, fixedEnd: false, ref m_TerrainHeightData, ref m_WaterSurfaceData).m_Bezier;
            }
            else if (!flag)
            {
                Curve curve2 = default(Curve);
                curve2.m_Bezier = ObjectUtils.LocalToWorld(parentTransform.m_Position, parentTransform.m_Rotation, curve);
                Curve curve4 = curve2;
                bool flag2 = parentMesh.x >= 0;
                bool flag3 = parentMesh.y >= 0;
                flag = flag2 || flag3;
                if ((componentData.m_Flags & Game.Net.GeometryFlags.FlattenTerrain) != 0)
                {
                    component2.m_Curve = curve4.m_Bezier;
                }
                else
                {
                    component2.m_Curve = NetUtils.AdjustPosition(curve4, flag2, flag, flag3, ref m_TerrainHeightData).m_Bezier;
                    component2.m_Curve.a.y += curve.a.y;
                    component2.m_Curve.b.y += curve.b.y;
                    component2.m_Curve.c.y += curve.c.y;
                    component2.m_Curve.d.y += curve.d.y;
                }
            }
            else
            {
                component2.m_Curve = ObjectUtils.LocalToWorld(parentTransform.m_Position, parentTransform.m_Rotation, curve);
            }
            bool onGround = !flag || math.cmin(math.abs(curve.y.abcd)) < 2f;
            if (ClearAreaHelpers.ShouldClear(clearAreas, component2.m_Curve, onGround))
            {
                return;
            }
            Entity e = m_CommandBuffer.CreateEntity();
            m_CommandBuffer.AddComponent(e, component);
            m_CommandBuffer.AddComponent(e, default(Updated));
            if (ownerDefinition.m_Prefab != Entity.Null)
            {
                m_CommandBuffer.AddComponent(e, ownerDefinition);
            }
            component2.m_StartPosition.m_Position = component2.m_Curve.a;
            component2.m_StartPosition.m_Rotation = NetUtils.GetNodeRotation(MathUtils.StartTangent(component2.m_Curve), parentTransform.m_Rotation);
            component2.m_StartPosition.m_CourseDelta = 0f;
            component2.m_StartPosition.m_Elevation = curve.a.y;
            component2.m_StartPosition.m_ParentMesh = parentMesh.x;
            if (nodeIndex.x >= 0)
            {
                if ((componentData.m_Flags & Game.Net.GeometryFlags.OnWater) != 0)
                {
                    component2.m_StartPosition.m_Position.xz = ObjectUtils.LocalToWorld(parentTransform, nodePositions[nodeIndex.x].xyz).xz;
                }
                else
                {
                    component2.m_StartPosition.m_Position = ObjectUtils.LocalToWorld(parentTransform, nodePositions[nodeIndex.x].xyz);
                }
            }
            component2.m_EndPosition.m_Position = component2.m_Curve.d;
            component2.m_EndPosition.m_Rotation = NetUtils.GetNodeRotation(MathUtils.EndTangent(component2.m_Curve), parentTransform.m_Rotation);
            component2.m_EndPosition.m_CourseDelta = 1f;
            component2.m_EndPosition.m_Elevation = curve.d.y;
            component2.m_EndPosition.m_ParentMesh = parentMesh.y;
            if (nodeIndex.y >= 0)
            {
                if ((componentData.m_Flags & Game.Net.GeometryFlags.OnWater) != 0)
                {
                    component2.m_EndPosition.m_Position.xz = ObjectUtils.LocalToWorld(parentTransform, nodePositions[nodeIndex.y].xyz).xz;
                }
                else
                {
                    component2.m_EndPosition.m_Position = ObjectUtils.LocalToWorld(parentTransform, nodePositions[nodeIndex.y].xyz);
                }
            }
            component2.m_Length = MathUtils.Length(component2.m_Curve);
            component2.m_FixedIndex = -1;
            component2.m_StartPosition.m_Flags |= CoursePosFlags.IsFirst;
            component2.m_EndPosition.m_Flags |= CoursePosFlags.IsLast;
            if (component2.m_StartPosition.m_Position.Equals(component2.m_EndPosition.m_Position))
            {
                component2.m_StartPosition.m_Flags |= CoursePosFlags.IsLast;
                component2.m_EndPosition.m_Flags |= CoursePosFlags.IsFirst;
            }
            if (ownerDefinition.m_Prefab == Entity.Null)
            {
                component2.m_StartPosition.m_Flags |= CoursePosFlags.FreeHeight;
                component2.m_EndPosition.m_Flags |= CoursePosFlags.FreeHeight;
            }
            m_CommandBuffer.AddComponent(e, component2);
            if (upgrades != default(CompositionFlags))
            {
                Upgraded upgraded = default(Upgraded);
                upgraded.m_Flags = upgrades;
                Upgraded component3 = upgraded;
                m_CommandBuffer.AddComponent(e, component3);
            }
        }

        private void UpdateSubNets(
            Game.Objects.Transform transform, 
            Entity prefab, 
            bool topLevel, 
            OwnerDefinition ownerDefinition, 
            NativeList<ClearAreaData> clearAreas, 
            ref Unity.Mathematics.Random random
            )
        {
            bool flag = true;
            if (flag && topLevel && m_PrefabSubNets.HasBuffer(prefab))
            {
                DynamicBuffer<Game.Prefabs.SubNet> subNets = m_PrefabSubNets[prefab];
                NativeList<float4> nodePositions = new NativeList<float4>(subNets.Length * 2, Allocator.Temp);

                for (int i = 0; i < subNets.Length; i++)
                {
                    Game.Prefabs.SubNet subNet = subNets[i];
                    if (subNet.m_NodeIndex.x >= 0)
                    {
                        while (nodePositions.Length <= subNet.m_NodeIndex.x)
                        {
                            float4 value = default(float4);
                            nodePositions.Add(in value);
                        }
                        nodePositions[subNet.m_NodeIndex.x] += new float4(subNet.m_Curve.a, 1f);
                    }
                    if (subNet.m_NodeIndex.y >= 0)
                    {
                        while (nodePositions.Length <= subNet.m_NodeIndex.y)
                        {
                            float4 value = default(float4);
                            nodePositions.Add(in value);
                        }
                        nodePositions[subNet.m_NodeIndex.y] += new float4(subNet.m_Curve.d, 1f);
                    }
                }
                for (int j = 0; j < nodePositions.Length; j++)
                {
                    nodePositions[j] /= math.max(1f, nodePositions[j].w);
                }
                for (int k = 0; k < subNets.Length; k++)
                {
                    Game.Prefabs.SubNet subNet2 = NetUtils.GetSubNet(subNets, k, m_LefthandTraffic, ref m_PrefabNetGeometryData);
                    CreateSubNet(
                        subNet2.m_Prefab, 
                        Entity.Null, 
                        subNet2.m_Curve, 
                        subNet2.m_NodeIndex, 
                        subNet2.m_ParentMesh, 
                        subNet2.m_Upgrades, 
                        nodePositions, 
                        transform, 
                        ownerDefinition, 
                        clearAreas, 
                        ref random
                        );
                }
                nodePositions.Dispose();
            }
        }

        private void UpdateSubAreas(
            Game.Objects.Transform transform, 
            Entity prefab, 
            bool topLevel, 
            OwnerDefinition ownerDefinition, 
            NativeList<ClearAreaData> clearAreas, 
            ref Unity.Mathematics.Random random, 
            ref NativeParallelHashMap<Entity, int> selectedSpawnables
            )
        {
            bool flag = true;
            if (flag && topLevel && m_PrefabSubAreas.HasBuffer(prefab))
            {
                DynamicBuffer<Game.Prefabs.SubArea> dynamicBuffer = m_PrefabSubAreas[prefab];
                DynamicBuffer<SubAreaNode> dynamicBuffer2 = m_PrefabSubAreaNodes[prefab];
                for (int i = 0; i < dynamicBuffer.Length; i++)
                {
                    Game.Prefabs.SubArea subArea = dynamicBuffer[i];
                    int seed;
                    if (m_PrefabPlaceholderElements.HasBuffer(subArea.m_Prefab))
                    {
                        DynamicBuffer<PlaceholderObjectElement> placeholderElements = m_PrefabPlaceholderElements[subArea.m_Prefab];
                        if (!selectedSpawnables.IsCreated)
                        {
                            selectedSpawnables = new NativeParallelHashMap<Entity, int>(10, Allocator.Temp);
                        }
                        if (!AreaUtils.SelectAreaPrefab(placeholderElements, m_PrefabSpawnableObjectData, selectedSpawnables, ref random, out subArea.m_Prefab, out seed))
                        {
                            continue;
                        }
                    }
                    else
                    {
                        seed = random.NextInt();
                    }
                    AreaGeometryData areaGeometryData = m_PrefabAreaGeometryData[subArea.m_Prefab];
                    if (areaGeometryData.m_Type == AreaType.Space)
                    {
                        if (ClearAreaHelpers.ShouldClear(clearAreas, dynamicBuffer2, subArea.m_NodeRange, transform))
                        {
                            continue;
                        }
                    }


                    Entity e = m_CommandBuffer.CreateEntity();
                    CreationDefinition component = default(CreationDefinition);
                    component.m_Prefab = subArea.m_Prefab;
                    component.m_RandomSeed = seed;
                    if (areaGeometryData.m_Type != 0)
                    {
                        component.m_Flags |= CreationFlags.Hidden;
                    }
                    m_CommandBuffer.AddComponent(e, component);
                    m_CommandBuffer.AddComponent(e, default(Updated));
                    if (ownerDefinition.m_Prefab != Entity.Null)
                    {
                        m_CommandBuffer.AddComponent(e, ownerDefinition);
                    }
                    DynamicBuffer<Game.Areas.Node> dynamicBuffer3 = m_CommandBuffer.AddBuffer<Game.Areas.Node>(e);
                    dynamicBuffer3.ResizeUninitialized(subArea.m_NodeRange.y - subArea.m_NodeRange.x + 1);


                    int num = ObjectToolBaseSystem.GetFirstNodeIndex(dynamicBuffer2, subArea.m_NodeRange);
                    int num2 = 0;
                    for (int j = subArea.m_NodeRange.x; j <= subArea.m_NodeRange.y; j++)
                    {
                        float3 position = dynamicBuffer2[num].m_Position;
                        float3 position2 = ObjectUtils.LocalToWorld(transform, position);
                        int parentMesh = dynamicBuffer2[num].m_ParentMesh;
                        float elevation = math.select(float.MinValue, position.y, parentMesh >= 0);
                        dynamicBuffer3[num2] = new Game.Areas.Node(position2, elevation);


                        num2++;
                        if (++num == subArea.m_NodeRange.y)
                        {
                            num = subArea.m_NodeRange.x;
                        }
                    }
                }
            }
        }
    }

}
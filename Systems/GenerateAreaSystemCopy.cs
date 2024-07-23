using System;
using System.Runtime.CompilerServices;
using Colossal.Entities;
using Game;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Scripting;

namespace AreaBucket.Systems
{
    [CompilerGenerated]
    public partial class GenerateAreasSystem : GameSystemBase
    {
        private struct OldAreaData : IEquatable<OldAreaData>
        {
            public Entity m_Prefab;

            public Entity m_Original;

            public Entity m_Owner;

            public bool Equals(OldAreaData other)
            {
                if (m_Prefab.Equals(other.m_Prefab) && m_Original.Equals(other.m_Original))
                {
                    return m_Owner.Equals(other.m_Owner);
                }
                return false;
            }

            public override int GetHashCode()
            {
                return ((17 * 31 + m_Prefab.GetHashCode()) * 31 + m_Original.GetHashCode()) * 31 + m_Owner.GetHashCode();
            }
        }

        [BurstCompile]
        private struct CreateAreasJob : IJob
        {
            [ReadOnly]
            public EntityTypeHandle m_EntityType;

            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> m_PrefabRefType;

            [ReadOnly]
            public ComponentTypeHandle<CreationDefinition> m_CreationDefinitionType;

            [ReadOnly]
            public ComponentTypeHandle<OwnerDefinition> m_OwnerDefinitionType;

            [ReadOnly]
            public ComponentTypeHandle<Temp> m_TempType;

            [ReadOnly]
            public ComponentTypeHandle<Owner> m_OwnerType;

            [ReadOnly]
            public BufferTypeHandle<Node> m_NodeType;

            [ReadOnly]
            public BufferTypeHandle<LocalNodeCache> m_LocalNodeCacheType;

            [ReadOnly]
            public ComponentLookup<Storage> m_StorageData;

            [ReadOnly]
            public ComponentLookup<Native> m_NativeData;

            [ReadOnly]
            public ComponentLookup<Deleted> m_DeletedData;

            [ReadOnly]
            public ComponentLookup<PseudoRandomSeed> m_PseudoRandomSeedData;

            [ReadOnly]
            public ComponentLookup<PrefabRef> m_PrefabRefData;

            [ReadOnly]
            public ComponentLookup<AreaData> m_AreaData;

            [ReadOnly]
            public ComponentLookup<AreaGeometryData> m_AreaGeometryData;

            [ReadOnly]
            public BufferLookup<Game.Areas.SubArea> m_SubAreas;

            [ReadOnly]
            public BufferLookup<LocalNodeCache> m_LocalNodeCache;

            [ReadOnly]
            public BufferLookup<Game.Prefabs.SubArea> m_PrefabSubAreas;

            [ReadOnly]
            public NativeList<ArchetypeChunk> m_DefinitionChunks;

            [ReadOnly]
            public NativeList<ArchetypeChunk> m_DeletedChunks;

            [ReadOnly]
            public TerrainHeightData m_TerrainHeightData;

            [ReadOnly]
            public WaterSurfaceData m_WaterSurfaceData;

            public EntityCommandBuffer m_CommandBuffer;

            public void Execute()
            {
                NativeParallelMultiHashMap<OldAreaData, Entity> deletedAreas = new NativeParallelMultiHashMap<OldAreaData, Entity>(16, Allocator.Temp);
                for (int i = 0; i < m_DeletedChunks.Length; i++)
                {
                    FillDeletedAreas(m_DeletedChunks[i], deletedAreas);
                }
                for (int j = 0; j < m_DefinitionChunks.Length; j++)
                {
                    CreateAreas(m_DefinitionChunks[j], deletedAreas);
                }
                deletedAreas.Dispose();
            }

            /// <summary>
            /// this method maps all area entity that is marked as Deleted
            /// </summary>
            /// <param name="chunk"></param>
            /// <param name="deletedAreas"></param>
            private void FillDeletedAreas(ArchetypeChunk chunk, NativeParallelMultiHashMap<OldAreaData, Entity> deletedAreas)
            {
                NativeArray<Entity> deletedEntities = chunk.GetNativeArray(m_EntityType);
                NativeArray<Temp> temps = chunk.GetNativeArray(ref m_TempType);
                NativeArray<Owner> owners = chunk.GetNativeArray(ref m_OwnerType);
                NativeArray<PrefabRef> prefabRefs = chunk.GetNativeArray(ref m_PrefabRefType);
                for (int i = 0; i < deletedEntities.Length; i++)
                {
                    Entity item = deletedEntities[i];
                    OldAreaData oldAreaData = default(OldAreaData);
                    oldAreaData.m_Prefab = prefabRefs[i].m_Prefab;
                    oldAreaData.m_Original = temps[i].m_Original;
                    OldAreaData key = oldAreaData;
                    if (owners.Length != 0)
                    {
                        key.m_Owner = owners[i].m_Owner;
                    }
                    deletedAreas.Add(key, item);
                }
            }

            private void CreateAreas(ArchetypeChunk chunk, NativeParallelMultiHashMap<OldAreaData, Entity> deletedAreas)
            {
                NativeArray<CreationDefinition> creationDefinitions = chunk.GetNativeArray(ref m_CreationDefinitionType);
                NativeArray<OwnerDefinition> ownerDefinitions = chunk.GetNativeArray(ref m_OwnerDefinitionType);
                BufferAccessor<Node> nodesBufferAccessor = chunk.GetBufferAccessor(ref m_NodeType);
                BufferAccessor<LocalNodeCache> localNodeCacheBufferAccessor = chunk.GetBufferAccessor(ref m_LocalNodeCacheType);
                for (int i = 0; i < creationDefinitions.Length; i++)
                {
                    CreationDefinition creationDefinition = creationDefinitions[i];
                    // ignore all entites that its owner has been marked as Deleted
                    if (m_DeletedData.HasComponent(creationDefinition.m_Owner))
                    {
                        continue;
                    }

                    OwnerDefinition ownerDefinition = default(OwnerDefinition);
                    if (ownerDefinitions.Length != 0)
                    {
                        ownerDefinition = ownerDefinitions[i];
                    }
                    DynamicBuffer<Node> nodesBuffer = nodesBufferAccessor[i];
                    AreaFlags areaFlags = (AreaFlags)0;
                    TempFlags tempFlags = (TempFlags)0u;

                    // if creationDefinition has original entity
                    if (creationDefinition.m_Original != Entity.Null)
                    {
                        // set it as hidden
                        m_CommandBuffer.AddComponent(creationDefinition.m_Original, default(Hidden));
                        // update creation definition's prefab to original's prefab
                        creationDefinition.m_Prefab = m_PrefabRefData[creationDefinition.m_Original].m_Prefab;

                        // update tempflags / area flags
                        if ((creationDefinition.m_Flags & CreationFlags.Recreate) != 0)
                        {
                            tempFlags |= TempFlags.Modify;
                        }
                        else
                        {
                            areaFlags |= AreaFlags.Complete;
                            if ((creationDefinition.m_Flags & CreationFlags.Delete) != 0)
                            {
                                tempFlags |= TempFlags.Delete;
                            }
                            else if ((creationDefinition.m_Flags & CreationFlags.Select) != 0)
                            {
                                tempFlags |= TempFlags.Select;
                            }
                            else if ((creationDefinition.m_Flags & CreationFlags.Relocate) != 0)
                            {
                                tempFlags |= TempFlags.Modify;
                            }
                        }
                    }
                    else // else it is a new creation
                    {
                        tempFlags |= TempFlags.Create;
                    }


                    if (ownerDefinition.m_Prefab == Entity.Null)
                    {
                        tempFlags |= TempFlags.Essential;
                    }

                    // set it as hidden
                    if ((creationDefinition.m_Flags & CreationFlags.Hidden) != 0)
                    {
                        tempFlags |= TempFlags.Hidden;
                    }


                    bool keepLocalNodeCache = false; // what flag?

                    // get key to old (deleted) area's
                    OldAreaData oldAreaData = default(OldAreaData);
                    oldAreaData.m_Prefab = creationDefinition.m_Prefab;
                    oldAreaData.m_Original = creationDefinition.m_Original;
                    oldAreaData.m_Owner = creationDefinition.m_Owner;
                    OldAreaData key = oldAreaData;

                    // if the creation definition is not marked as Permanent and find old area data
                    if ((creationDefinition.m_Flags & CreationFlags.Permanent) == 0 && deletedAreas.TryGetFirstValue(key, out var newAreaEntity, out var it))
                    {
                        // remove its Deleted mark and set it as Updated
                        deletedAreas.Remove(it); 
                        m_CommandBuffer.SetComponent(newAreaEntity, new Temp(creationDefinition.m_Original, tempFlags));
                        m_CommandBuffer.AddComponent(newAreaEntity, default(Updated));
                        m_CommandBuffer.RemoveComponent<Deleted>(newAreaEntity);


                        if (ownerDefinition.m_Prefab != Entity.Null)
                        {
                            m_CommandBuffer.AddComponent(newAreaEntity, default(Owner));
                            m_CommandBuffer.AddComponent(newAreaEntity, ownerDefinition);
                        }
                        else
                        {
                            // add Owner component if definition specifies owner
                            if (creationDefinition.m_Owner != Entity.Null)
                            {
                                m_CommandBuffer.AddComponent(newAreaEntity, new Owner(creationDefinition.m_Owner));
                            }
                            else
                            {
                                m_CommandBuffer.RemoveComponent<Owner>(newAreaEntity);
                            }
                            m_CommandBuffer.RemoveComponent<OwnerDefinition>(newAreaEntity);
                        }

                        // I dont know what Native tag means
                        if ((creationDefinition.m_Flags & CreationFlags.Native) != 0 || m_NativeData.HasComponent(creationDefinition.m_Original))
                        {
                            m_CommandBuffer.AddComponent(newAreaEntity, default(Native));
                        }
                        else
                        {
                            m_CommandBuffer.RemoveComponent<Native>(newAreaEntity);
                        }
                    }
                    else
                    {
                        AreaData areaData = m_AreaData[creationDefinition.m_Prefab];
                        
                        // for surface asset, arche type is: (Node, Triangle, Surface, Geometry, Expand, Created, Updated)
                        newAreaEntity = m_CommandBuffer.CreateEntity(areaData.m_Archetype);
                        m_CommandBuffer.SetComponent(newAreaEntity, new PrefabRef(creationDefinition.m_Prefab));
                        if ((creationDefinition.m_Flags & CreationFlags.Permanent) == 0)
                        {
                            m_CommandBuffer.AddComponent(newAreaEntity, new Temp(creationDefinition.m_Original, tempFlags));
                        }

                        // handle owner if the new created entity has owner definition or creation def has specified owner
                        if (ownerDefinition.m_Prefab != Entity.Null)
                        {
                            m_CommandBuffer.AddComponent(newAreaEntity, default(Owner));
                            m_CommandBuffer.AddComponent(newAreaEntity, ownerDefinition);
                        }
                        else if (creationDefinition.m_Owner != Entity.Null)
                        {
                            m_CommandBuffer.AddComponent(newAreaEntity, new Owner(creationDefinition.m_Owner));
                        }


                        if ((creationDefinition.m_Flags & CreationFlags.Native) != 0 || m_NativeData.HasComponent(creationDefinition.m_Original))
                        {
                            m_CommandBuffer.AddComponent(newAreaEntity, default(Native));
                        }


                        keepLocalNodeCache = true;
                    }


                    DynamicBuffer<Node> newEntityNodesBuffer = m_CommandBuffer.SetBuffer<Node>(newAreaEntity);


                    // guess here is check the area polygon outline is closed or not (complete or not)
                    bool isAreaComplete = false;
                    if 
                    (
                        (areaFlags & AreaFlags.Complete) == 0 && 
                        nodesBuffer.Length >= 4 && 
                        nodesBuffer[0].m_Position.Equals(nodesBuffer[nodesBuffer.Length - 1].m_Position)
                    )
                    {
                        // here if it is complete, we actual not add last point (that equals to first point)
                        newEntityNodesBuffer.ResizeUninitialized(nodesBuffer.Length - 1);
                        for (int j = 0; j < nodesBuffer.Length - 1; j++)
                        {
                            newEntityNodesBuffer[j] = nodesBuffer[j];
                        }
                        areaFlags |= AreaFlags.Complete;
                        isAreaComplete = true;
                    }
                    else
                    {
                        // node not complete case 
                        newEntityNodesBuffer.ResizeUninitialized(nodesBuffer.Length);
                        for (int k = 0; k < nodesBuffer.Length; k++)
                        {
                            newEntityNodesBuffer[k] = nodesBuffer[k];
                        }
                    }


                    bool onWaterSurface = false;
                    bool hasRandom = false;
                    if (m_AreaGeometryData.TryGetComponent(creationDefinition.m_Prefab, out var areaGeometryData))
                    {
                        onWaterSurface = (areaGeometryData.m_Flags & GeometryFlags.OnWaterSurface) != 0;
                        hasRandom = (areaGeometryData.m_Flags & GeometryFlags.PseudoRandom) != 0;
                    }

                    // adjust node position by terrian/water surface
                    for (int l = 0; l < newEntityNodesBuffer.Length; l++)
                    {
                        ref Node nodeRef = ref newEntityNodesBuffer.ElementAt(l);
                        if (nodeRef.m_Elevation == float.MinValue)
                        {
                            Node node = 
                            (
                                (!onWaterSurface) ? 
                                AreaUtils.AdjustPosition(nodeRef, ref m_TerrainHeightData) : 
                                AreaUtils.AdjustPosition(nodeRef, ref m_TerrainHeightData, ref m_WaterSurfaceData)
                            );
                            bool c = math.abs(node.m_Position.y - nodeRef.m_Position.y) >= 0.01f;
                            nodeRef.m_Position = math.select(nodeRef.m_Position, node.m_Position, c);
                        }
                    }

                    // it seems copy the local node cache to created area entity
                    if (localNodeCacheBufferAccessor.Length != 0)
                    {
                        DynamicBuffer<LocalNodeCache> localNodeCacheBufferFromDefinition = localNodeCacheBufferAccessor[i];

                        DynamicBuffer<LocalNodeCache> dynamicBuffer4 = 
                        (
                            (!keepLocalNodeCache && m_LocalNodeCache.HasBuffer(newAreaEntity)) ? 
                            m_CommandBuffer.SetBuffer<LocalNodeCache>(newAreaEntity) : 
                            m_CommandBuffer.AddBuffer<LocalNodeCache>(newAreaEntity)
                        );

                        if (isAreaComplete)
                        {
                            dynamicBuffer4.ResizeUninitialized(localNodeCacheBufferFromDefinition.Length - 1);
                            for (int m = 0; m < localNodeCacheBufferFromDefinition.Length - 1; m++)
                            {
                                dynamicBuffer4[m] = localNodeCacheBufferFromDefinition[m];
                            }
                        }
                        else
                        {
                            dynamicBuffer4.ResizeUninitialized(localNodeCacheBufferFromDefinition.Length);
                            for (int n = 0; n < localNodeCacheBufferFromDefinition.Length; n++)
                            {
                                dynamicBuffer4[n] = localNodeCacheBufferFromDefinition[n];
                            }
                        }
                    }
                    else if (!keepLocalNodeCache && m_LocalNodeCache.HasBuffer(newAreaEntity))
                    {
                        m_CommandBuffer.RemoveComponent<LocalNodeCache>(newAreaEntity);
                    }

                    // set Area component, the component is used for specified its nature of geometry
                    m_CommandBuffer.SetComponent(newAreaEntity, new Area(areaFlags));

                    // set storage
                    if (m_StorageData.HasComponent(creationDefinition.m_Original))
                    {
                        m_CommandBuffer.SetComponent(newAreaEntity, m_StorageData[creationDefinition.m_Original]);
                    }

                    // handle random seed
                    PseudoRandomSeed pseudoRandomSeed = default(PseudoRandomSeed);
                    if (hasRandom)
                    {
                        if (!m_PseudoRandomSeedData.TryGetComponent(creationDefinition.m_Original, out pseudoRandomSeed))
                        {
                            pseudoRandomSeed = new PseudoRandomSeed((ushort)creationDefinition.m_RandomSeed);
                        }
                        m_CommandBuffer.SetComponent(newAreaEntity, pseudoRandomSeed);
                    }

                    // handle sub areas
                    if (!m_PrefabSubAreas.TryGetBuffer(creationDefinition.m_Prefab, out var subAreas))
                    {
                        continue;
                    }
                    NativeParallelMultiHashMap<Entity, Entity> nativeParallelMultiHashMap = default(NativeParallelMultiHashMap<Entity, Entity>);
                    tempFlags &= ~TempFlags.Essential;
                    areaFlags |= AreaFlags.Slave;
                    if (m_SubAreas.TryGetBuffer(creationDefinition.m_Original, out var bufferData2) && bufferData2.Length != 0)
                    {
                        nativeParallelMultiHashMap = new NativeParallelMultiHashMap<Entity, Entity>(16, Allocator.Temp);
                        for (int num = 0; num < bufferData2.Length; num++)
                        {
                            Game.Areas.SubArea subArea = bufferData2[num];
                            nativeParallelMultiHashMap.Add(m_PrefabRefData[subArea.m_Area].m_Prefab, subArea.m_Area);
                        }
                    }
                    for (int num2 = 0; num2 < subAreas.Length; num2++)
                    {
                        Game.Prefabs.SubArea subArea2 = subAreas[num2];
                        oldAreaData = default(OldAreaData);
                        oldAreaData.m_Prefab = subArea2.m_Prefab;
                        oldAreaData.m_Owner = newAreaEntity;
                        key = oldAreaData;
                        if (nativeParallelMultiHashMap.IsCreated && nativeParallelMultiHashMap.TryGetFirstValue(subArea2.m_Prefab, out key.m_Original, out var it2))
                        {
                            nativeParallelMultiHashMap.Remove(it2);
                        }
                        if ((creationDefinition.m_Flags & CreationFlags.Permanent) == 0 && deletedAreas.TryGetFirstValue(key, out var item2, out it))
                        {
                            deletedAreas.Remove(it);
                            m_CommandBuffer.SetComponent(item2, new Temp(key.m_Original, tempFlags));
                            m_CommandBuffer.AddComponent(item2, default(Updated));
                            m_CommandBuffer.RemoveComponent<Deleted>(item2);
                            m_CommandBuffer.AddComponent(item2, new Owner(newAreaEntity));
                            if ((creationDefinition.m_Flags & CreationFlags.Native) != 0 || m_NativeData.HasComponent(key.m_Original))
                            {
                                m_CommandBuffer.AddComponent(item2, default(Native));
                            }
                            else
                            {
                                m_CommandBuffer.RemoveComponent<Native>(item2);
                            }
                        }
                        else
                        {
                            AreaData areaData2 = m_AreaData[subArea2.m_Prefab];
                            item2 = m_CommandBuffer.CreateEntity(areaData2.m_Archetype);
                            m_CommandBuffer.SetComponent(item2, new PrefabRef(subArea2.m_Prefab));
                            if ((creationDefinition.m_Flags & CreationFlags.Permanent) == 0)
                            {
                                m_CommandBuffer.AddComponent(item2, new Temp(key.m_Original, tempFlags));
                            }
                            m_CommandBuffer.AddComponent(item2, new Owner(newAreaEntity));
                            if ((creationDefinition.m_Flags & CreationFlags.Native) != 0 || m_NativeData.HasComponent(key.m_Original))
                            {
                                m_CommandBuffer.AddComponent(newAreaEntity, default(Native));
                            }
                        }
                        m_CommandBuffer.SetComponent(item2, new Area(areaFlags));
                        if (m_StorageData.HasComponent(key.m_Original))
                        {
                            m_CommandBuffer.SetComponent(item2, m_StorageData[key.m_Original]);
                        }
                        if (hasRandom)
                        {
                            m_CommandBuffer.SetComponent(item2, pseudoRandomSeed);
                        }
                    }
                    if (nativeParallelMultiHashMap.IsCreated)
                    {
                        nativeParallelMultiHashMap.Dispose();
                    }
                }
            }
        }

        private struct TypeHandle
        {
            [ReadOnly]
            public EntityTypeHandle __Unity_Entities_Entity_TypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<CreationDefinition> __Game_Tools_CreationDefinition_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<OwnerDefinition> __Game_Tools_OwnerDefinition_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<Temp> __Game_Tools_Temp_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<Owner> __Game_Common_Owner_RO_ComponentTypeHandle;

            [ReadOnly]
            public BufferTypeHandle<Node> __Game_Areas_Node_RO_BufferTypeHandle;

            [ReadOnly]
            public BufferTypeHandle<LocalNodeCache> __Game_Tools_LocalNodeCache_RO_BufferTypeHandle;

            [ReadOnly]
            public ComponentLookup<Storage> __Game_Areas_Storage_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Native> __Game_Common_Native_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<Deleted> __Game_Common_Deleted_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<PseudoRandomSeed> __Game_Common_PseudoRandomSeed_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<PrefabRef> __Game_Prefabs_PrefabRef_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<AreaData> __Game_Prefabs_AreaData_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<AreaGeometryData> __Game_Prefabs_AreaGeometryData_RO_ComponentLookup;

            [ReadOnly]
            public BufferLookup<Game.Areas.SubArea> __Game_Areas_SubArea_RO_BufferLookup;

            [ReadOnly]
            public BufferLookup<LocalNodeCache> __Game_Tools_LocalNodeCache_RO_BufferLookup;

            [ReadOnly]
            public BufferLookup<Game.Prefabs.SubArea> __Game_Prefabs_SubArea_RO_BufferLookup;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void __AssignHandles(ref SystemState state)
            {
                __Unity_Entities_Entity_TypeHandle = state.GetEntityTypeHandle();
                __Game_Prefabs_PrefabRef_RO_ComponentTypeHandle = state.GetComponentTypeHandle<PrefabRef>(isReadOnly: true);
                __Game_Tools_CreationDefinition_RO_ComponentTypeHandle = state.GetComponentTypeHandle<CreationDefinition>(isReadOnly: true);
                __Game_Tools_OwnerDefinition_RO_ComponentTypeHandle = state.GetComponentTypeHandle<OwnerDefinition>(isReadOnly: true);
                __Game_Tools_Temp_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Temp>(isReadOnly: true);
                __Game_Common_Owner_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Owner>(isReadOnly: true);
                __Game_Areas_Node_RO_BufferTypeHandle = state.GetBufferTypeHandle<Node>(isReadOnly: true);
                __Game_Tools_LocalNodeCache_RO_BufferTypeHandle = state.GetBufferTypeHandle<LocalNodeCache>(isReadOnly: true);
                __Game_Areas_Storage_RO_ComponentLookup = state.GetComponentLookup<Storage>(isReadOnly: true);
                __Game_Common_Native_RO_ComponentLookup = state.GetComponentLookup<Native>(isReadOnly: true);
                __Game_Common_Deleted_RO_ComponentLookup = state.GetComponentLookup<Deleted>(isReadOnly: true);
                __Game_Common_PseudoRandomSeed_RO_ComponentLookup = state.GetComponentLookup<PseudoRandomSeed>(isReadOnly: true);
                __Game_Prefabs_PrefabRef_RO_ComponentLookup = state.GetComponentLookup<PrefabRef>(isReadOnly: true);
                __Game_Prefabs_AreaData_RO_ComponentLookup = state.GetComponentLookup<AreaData>(isReadOnly: true);
                __Game_Prefabs_AreaGeometryData_RO_ComponentLookup = state.GetComponentLookup<AreaGeometryData>(isReadOnly: true);
                __Game_Areas_SubArea_RO_BufferLookup = state.GetBufferLookup<Game.Areas.SubArea>(isReadOnly: true);
                __Game_Tools_LocalNodeCache_RO_BufferLookup = state.GetBufferLookup<LocalNodeCache>(isReadOnly: true);
                __Game_Prefabs_SubArea_RO_BufferLookup = state.GetBufferLookup<Game.Prefabs.SubArea>(isReadOnly: true);
            }
        }

        private TerrainSystem m_TerrainSystem;

        private WaterSystem m_WaterSystem;

        private ModificationBarrier1 m_ModificationBarrier;

        private EntityQuery m_DefinitionQuery;

        private EntityQuery m_DeletedQuery;

        private TypeHandle __TypeHandle;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = base.World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem = base.World.GetOrCreateSystemManaged<WaterSystem>();
            m_ModificationBarrier = base.World.GetOrCreateSystemManaged<ModificationBarrier1>();
            m_DefinitionQuery = GetEntityQuery(ComponentType.ReadOnly<CreationDefinition>(), ComponentType.ReadOnly<Node>(), ComponentType.ReadOnly<Updated>());
            m_DeletedQuery = GetEntityQuery(ComponentType.ReadOnly<Area>(), ComponentType.ReadOnly<Temp>(), ComponentType.ReadOnly<Deleted>());
            RequireForUpdate(m_DefinitionQuery);
        }

        [Preserve]
        protected override void OnUpdate()
        {
            JobHandle outJobHandle;
            NativeList<ArchetypeChunk> definitionChunks = m_DefinitionQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out outJobHandle);
            JobHandle outJobHandle2;
            NativeList<ArchetypeChunk> deletedChunks = m_DeletedQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out outJobHandle2);
            __TypeHandle.__Game_Prefabs_SubArea_RO_BufferLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Tools_LocalNodeCache_RO_BufferLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Areas_SubArea_RO_BufferLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Prefabs_AreaGeometryData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Prefabs_AreaData_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Common_PseudoRandomSeed_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Common_Deleted_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Common_Native_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Areas_Storage_RO_ComponentLookup.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Tools_LocalNodeCache_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Areas_Node_RO_BufferTypeHandle.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Common_Owner_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Tools_OwnerDefinition_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Tools_CreationDefinition_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            __TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle.Update(ref base.CheckedStateRef);
            __TypeHandle.__Unity_Entities_Entity_TypeHandle.Update(ref base.CheckedStateRef);
            CreateAreasJob jobData = default(CreateAreasJob);
            jobData.m_EntityType = __TypeHandle.__Unity_Entities_Entity_TypeHandle;
            jobData.m_PrefabRefType = __TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentTypeHandle;
            jobData.m_CreationDefinitionType = __TypeHandle.__Game_Tools_CreationDefinition_RO_ComponentTypeHandle;
            jobData.m_OwnerDefinitionType = __TypeHandle.__Game_Tools_OwnerDefinition_RO_ComponentTypeHandle;
            jobData.m_TempType = __TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle;
            jobData.m_OwnerType = __TypeHandle.__Game_Common_Owner_RO_ComponentTypeHandle;
            jobData.m_NodeType = __TypeHandle.__Game_Areas_Node_RO_BufferTypeHandle;
            jobData.m_LocalNodeCacheType = __TypeHandle.__Game_Tools_LocalNodeCache_RO_BufferTypeHandle;
            jobData.m_StorageData = __TypeHandle.__Game_Areas_Storage_RO_ComponentLookup;
            jobData.m_NativeData = __TypeHandle.__Game_Common_Native_RO_ComponentLookup;
            jobData.m_DeletedData = __TypeHandle.__Game_Common_Deleted_RO_ComponentLookup;
            jobData.m_PseudoRandomSeedData = __TypeHandle.__Game_Common_PseudoRandomSeed_RO_ComponentLookup;
            jobData.m_PrefabRefData = __TypeHandle.__Game_Prefabs_PrefabRef_RO_ComponentLookup;
            jobData.m_AreaData = __TypeHandle.__Game_Prefabs_AreaData_RO_ComponentLookup;
            jobData.m_AreaGeometryData = __TypeHandle.__Game_Prefabs_AreaGeometryData_RO_ComponentLookup;
            jobData.m_SubAreas = __TypeHandle.__Game_Areas_SubArea_RO_BufferLookup;
            jobData.m_LocalNodeCache = __TypeHandle.__Game_Tools_LocalNodeCache_RO_BufferLookup;
            jobData.m_PrefabSubAreas = __TypeHandle.__Game_Prefabs_SubArea_RO_BufferLookup;
            jobData.m_DefinitionChunks = definitionChunks;
            jobData.m_DeletedChunks = deletedChunks;
            jobData.m_TerrainHeightData = m_TerrainSystem.GetHeightData();
            jobData.m_WaterSurfaceData = m_WaterSystem.GetSurfaceData(out var deps);
            jobData.m_CommandBuffer = m_ModificationBarrier.CreateCommandBuffer();
            JobHandle jobHandle = IJobExtensions.Schedule(jobData, JobUtils.CombineDependencies(base.Dependency, outJobHandle, outJobHandle2, deps));
            definitionChunks.Dispose(jobHandle);
            deletedChunks.Dispose(jobHandle);
            m_TerrainSystem.AddCPUHeightReader(jobHandle);
            m_WaterSystem.AddSurfaceReader(jobHandle);
            m_ModificationBarrier.AddJobHandleForProducer(jobHandle);
            base.Dependency = jobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void __AssignQueries(ref SystemState state)
        {
        }

        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            __AssignQueries(ref base.CheckedStateRef);
            __TypeHandle.__AssignHandles(ref base.CheckedStateRef);
        }

        [Preserve]
        public GenerateAreasSystem()
        {
        }
    }
}
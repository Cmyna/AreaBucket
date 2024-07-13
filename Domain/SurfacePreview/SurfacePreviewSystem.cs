using AreaBucket.Components;
using AreaBucket.Utils;
using Game;
using Game.Areas;
using Game.Common;
using Game.Debug;
using Game.Prefabs;
using Game.Rendering;
using Game.Simulation;
using Game.Tools;
using System.Collections.Generic;
using System.Security.Cryptography;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Rendering;

namespace AreaBucket.Systems.SurfacePreviewSystem
{
    public partial class SurfacePreviewSystem : GameSystemBase
    {
        private struct PreviewedEntity
        {
            public Entity m_Entity;
            public SurfacePreviewMarker m_Marker;
        }


        private struct GeneratePreviewedSurfaceJob : IJobChunk
        {
            public BufferLookup<Node> bluNodes;

            public ComponentLookup<AreaData> cluAreaData;

            public ComponentLookup<AreaGeometryData> cluAreaGeometryData;

            public ComponentTypeHandle<SurfacePreviewDefinition> cthSurfacePreviewDefinition;

            public EntityTypeHandle thEntity;

            public EntityCommandBuffer ecb;

            public TerrainHeightData terrainHeightData;

            public WaterSurfaceData waterSurfaceData;


            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                var entities = chunk.GetNativeArray(thEntity);
                var surfacePreviewDefinitions = chunk.GetNativeArray(ref cthSurfacePreviewDefinition);

                for (int i = 0; i < entities.Length; i++)
                {
                    CreateInSceneEntity(entities[i], surfacePreviewDefinitions[i], false);
                    if (!surfacePreviewDefinitions[i].applyPreview)
                    {
                        CreateInSceneEntity(entities[i], surfacePreviewDefinitions[i], true);
                    }

                    // mark old preview definition entity as Deleted
                    ecb.AddComponent(entities[i], default(Deleted));
                }
            }

            public void CreateInSceneEntity(Entity surfacePreivewDefEntity, SurfacePreviewDefinition surfacePreviewDefinition, bool isOutlineDummy)
            {
                var defNodesBuffer = bluNodes[surfacePreivewDefEntity];

                AreaFlags areaFlag = AreaFlags.Complete;

                var onWaterSurface = cluAreaGeometryData.TryGetComponent(surfacePreviewDefinition.prefab, out var areaGeometryData) &&
                     (areaGeometryData.m_Flags & GeometryFlags.OnWaterSurface) != 0;

                // add new entity
                var prefabData = cluAreaData[surfacePreviewDefinition.prefab];
                var newAreaEntity = ecb.CreateEntity(prefabData.m_Archetype);
                ecb.SetComponent(newAreaEntity, new PrefabRef(surfacePreviewDefinition.prefab));
                var nodeBuffer = ecb.AddBuffer<Node>(newAreaEntity);
                CreateSurfaceUtils.Drawing2Scene(defNodesBuffer, nodeBuffer);
                CreateSurfaceUtils.AdjustPosition(nodeBuffer, ref terrainHeightData, ref waterSurfaceData, onWaterSurface);
                ecb.AddComponent(newAreaEntity, new Area(areaFlag));

                if (!surfacePreviewDefinition.applyPreview)
                {
                    ecb.AddComponent(newAreaEntity, new SurfacePreviewMarker { key = surfacePreviewDefinition.key });
                    // ecb.AddComponent(newAreaEntity, new Temp { m_Flags = TempFlags.Select });
                }
                if (isOutlineDummy) ecb.AddComponent(newAreaEntity, new Temp { m_Flags = TempFlags.Select });
            }
        }


        private EntityQuery _previewDefintionQuery;

        private EntityQuery _previewEntitiesQuery;

        private ModificationBarrier1 _modificationBarrier;

        private TerrainSystem _terrainSystem;

        private WaterSystem _waterSystem;

        private int lastDefsCount { get; set; } = -1;

        private HashSet<int> m_trackedEntities;

        protected override void OnCreate()
        {
            base.OnCreate();
            _previewDefintionQuery = GetEntityQuery
            (
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<SurfacePreviewDefinition>()
            );

            _previewEntitiesQuery = GetEntityQuery
            (
                ComponentType.ReadOnly<Area>(),
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<SurfacePreviewMarker>()
            );

            _modificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier1>();
            _terrainSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            _waterSystem = World.GetOrCreateSystemManaged<WaterSystem>();

            m_trackedEntities = new HashSet<int>();

            CreateDebugUI();
        }


        protected override void OnUpdate()
        {
            var jobHandle = base.Dependency;

            /*var bluNodes = SystemAPI.GetBufferLookup<Node>(isReadOnly: false);
            var cluAreaData = SystemAPI.GetComponentLookup<AreaData>();
            var cluAreaGeometryData = SystemAPI.GetComponentLookup<AreaGeometryData>();*/

            var ecb = _modificationBarrier.CreateCommandBuffer();

            MarkOldEntitiesDeleted(ecb);

            var terrainHeightData = _terrainSystem.GetHeightData();
            var waterSystemLoaded = _waterSystem.Loaded;
            
            if (!waterSystemLoaded) return;

            WaterSurfaceData waterSurfaceData = _waterSystem.GetSurfaceData(out var deps);
            JobHandle.CombineDependencies(jobHandle, deps);


            //var entites = _previewDefintionQuery.ToEntityArray(Allocator.Temp);
            //var previewDefs = _previewDefintionQuery.ToComponentDataArray<SurfacePreviewDefinition>(Allocator.Temp);
            //lastDefsCount = entites.Length;

            var genPreviewedSurfaceJob = default(GeneratePreviewedSurfaceJob);

            genPreviewedSurfaceJob.bluNodes = SystemAPI.GetBufferLookup<Node>(isReadOnly: false);
            genPreviewedSurfaceJob.cluAreaData = SystemAPI.GetComponentLookup<AreaData>();
            genPreviewedSurfaceJob.cluAreaGeometryData = SystemAPI.GetComponentLookup<AreaGeometryData>();
            genPreviewedSurfaceJob.cthSurfacePreviewDefinition = SystemAPI.GetComponentTypeHandle<SurfacePreviewDefinition>();
            genPreviewedSurfaceJob.thEntity = SystemAPI.GetEntityTypeHandle();

            genPreviewedSurfaceJob.ecb = ecb;
            genPreviewedSurfaceJob.waterSurfaceData = waterSurfaceData;
            genPreviewedSurfaceJob.terrainHeightData = terrainHeightData;

            jobHandle = genPreviewedSurfaceJob.Schedule(_previewDefintionQuery, jobHandle);


            /*jobHandle = Job.WithCode(() => 
            {
                for (int i = 0; i < entites.Length; i++)
                {
                    var previewDefEntity = entites[i];
                    var previewDef = previewDefs[i];
                    var defNodesBuffer = bluNodes[previewDefEntity];

                    AreaFlags areaFlag = AreaFlags.Complete;

                    var onWaterSurface = false;
                    if (cluAreaGeometryData.TryGetComponent(previewDef.prefab, out var areaGeometryData))
                    {
                        onWaterSurface = (areaGeometryData.m_Flags & GeometryFlags.OnWaterSurface) != 0;
                    }

                    // add new entity
                    var prefabData = cluAreaData[previewDef.prefab];
                    var newAreaEntity = ecb.CreateEntity(prefabData.m_Archetype);
                    ecb.SetComponent(newAreaEntity, new PrefabRef(previewDef.prefab));
                    var nodeBuffer = ecb.AddBuffer<Node>(newAreaEntity);
                    CreateSurfaceUtils.Drawing2Scene(defNodesBuffer, nodeBuffer);
                    CreateSurfaceUtils.AdjustPosition(nodeBuffer, ref terrainHeightData, ref waterSurfaceData, false);
                    ecb.AddComponent(newAreaEntity, new Area(areaFlag));

                    if (!previewDef.applyPreview)
                    {
                        ecb.AddComponent(newAreaEntity, new SurfacePreviewMarker { key = previewDef.key });
                        // ecb.AddComponent(newAreaEntity, new Temp { m_Flags = TempFlags.Select });
                    }


                    // mark old preview definition entity as Deleted
                    ecb.AddComponent(previewDefEntity, default(Deleted));
                }
            }).Schedule(jobHandle);*/

            _terrainSystem.AddCPUHeightReader(jobHandle);
            _waterSystem.AddSurfaceReader(jobHandle);
            _modificationBarrier.AddJobHandleForProducer(jobHandle);

            //entites.Dispose(jobHandle);
            //previewDefs.Dispose(jobHandle);
        }



        private void MarkOldEntitiesDeleted(EntityCommandBuffer ecb)
        {
            var oldEntites = _previewEntitiesQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < oldEntites.Length; i++)
            {
                ecb.AddComponent(oldEntites[i], default(Deleted));
            }
            oldEntites.Dispose();
        }


        private void CollectTrackedEntites(Dictionary<SurfacePreviewMarker, Entity> result)
        {
            var entities = _previewEntitiesQuery.ToEntityArray(Allocator.Temp);
            var markers = _previewEntitiesQuery.ToComponentDataArray<SurfacePreviewMarker>(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                result[markers[i]] = entities[i];
            }

            entities.Dispose();
            markers.Dispose();
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            _previewDefintionQuery.Dispose();
        }

        private void CreateDebugUI()
        {
            var panel = DebugManager.instance.GetPanel("Surface Preview System", createIfNull: true, groupIndex: 0, overrideIfExist: true);
            List<DebugUI.Widget> list = new List<DebugUI.Widget>
            {
                new DebugUI.BoolField { displayName = nameof(Enabled), getter = () => Enabled, setter = (v) => Enabled = v },
                new DebugUI.Value { displayName = nameof(lastDefsCount), getter = () => lastDefsCount },
            };
            panel.children.Clear();
            panel.children.Add(list);
        }

        
    }

    
}

using AreaBucket.Components;
using AreaBucket.Utils;
using Game;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.Rendering;

namespace AreaBucket.Systems.SurfacePreviewSystem
{
    public partial class SurfacePreviewSystem : GameSystemBase
    {

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
                    var createdEntity = CreateInSceneEntity(entities[i], surfacePreviewDefinitions[i], false);
                    // create a dummy area entity that only shows highlighted borders
                    if (!surfacePreviewDefinitions[i].applyPreview)
                    {
                        CreateInSceneEntity(entities[i], surfacePreviewDefinitions[i], true);
                    }

                    // mark old preview definition entity as Deleted
                    ecb.AddComponent(entities[i], default(Deleted));
                }
            }

            public Entity CreateInSceneEntity(Entity surfacePreivewDefEntity, SurfacePreviewDefinition surfacePreviewDefinition, bool isOutlineDummy)
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

                // if not apply preview, then add a marker represent that the entity is been previewed
                if (!surfacePreviewDefinition.applyPreview)
                {
                    ecb.AddComponent(newAreaEntity, new SurfacePreviewMarker { key = surfacePreviewDefinition.key });
                    // ecb.AddComponent(newAreaEntity, new Temp { m_Flags = TempFlags.Select });
                }
                if (isOutlineDummy) ecb.AddComponent(newAreaEntity, new Temp { m_Flags = TempFlags.Select });
                return newAreaEntity;
            }
        }


        private EntityQuery _previewDefintionQuery;

        private EntityQuery _previewEntitiesQuery;

        private ModificationBarrier1 _modificationBarrier;

        private TerrainSystem _terrainSystem;

        private WaterSystem _waterSystem;

        private int lastDefsCount { get; set; } = -1;

        private DebugUI.Container _debugUIContainer;

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

            CreateDebugUI();
        }


        protected override void OnUpdate()
        {
            var jobHandle = base.Dependency;

            var ecb = _modificationBarrier.CreateCommandBuffer();

            MarkOldEntitiesDeleted(ecb);

            var terrainHeightData = _terrainSystem.GetHeightData();
            var waterSystemLoaded = _waterSystem.Loaded;
            
            if (!waterSystemLoaded) return;

            GetComponentTypeHandle<Area>();

            WaterSurfaceData waterSurfaceData = _waterSystem.GetSurfaceData(out var deps);
            JobHandle.CombineDependencies(jobHandle, deps);

            var a = CheckedStateRef;

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


            _terrainSystem.AddCPUHeightReader(jobHandle);
            _waterSystem.AddSurfaceReader(jobHandle);
            _modificationBarrier.AddJobHandleForProducer(jobHandle);

            base.Dependency = jobHandle;
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

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _previewDefintionQuery.Dispose();
        }

        private void CreateDebugUI()
        {
            _debugUIContainer = new DebugUI.Container(
                "Surface Preview System",
                new ObservableList<DebugUI.Widget>
                {
                    new DebugUI.BoolField { displayName = nameof(Enabled), getter = () => Enabled, setter = (v) => Enabled = v },
                    new DebugUI.Value { displayName = nameof(lastDefsCount), getter = () => lastDefsCount },
                }
                );
            Mod.AreaBucketDebugUI.children.Add(_debugUIContainer);
        }

        
    }

    
}

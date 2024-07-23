using System.Runtime.CompilerServices;
using AreaBucket.Components;
using AreaBucket.Systems.Jobs;
using AreaBucket.Utils;
using Colossal.Entities;
using Colossal.Mathematics;
using Colossal.Serialization.Entities;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;
using UnityEngine.Scripting;


namespace AreaBucket.Systems
{
    public partial class GeometrySystemCopy : GameSystemBase
    {
        
        private struct Index
        {
            public int m_NodeIndex;

            public int m_PrevIndex;

            public int m_NextIndex;

            public int m_SkipIndex;
        }

        public bool AlterVanillaSystem { get; set; } = false;

        private TerrainSystem m_TerrainSystem;

        private WaterSystem m_WaterSystem;

        private ModificationBarrier2B _modificationBarrier;

        private EntityQuery m_UpdatedAreasQuery;

        private EntityQuery m_AllAreasQuery;

        private EntityQuery m_CreatedBuildingsQuery;

        private EntityQuery _hidingAreasQuery;

        private bool m_Loaded;

        private DebugUI.Container _debugUIContainer;

        private GeometrySystem _vanillaGeometrySystem;

        private int _hiddenAreaCount = -1;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_TerrainSystem = base.World.GetOrCreateSystemManaged<TerrainSystem>();
            m_WaterSystem = base.World.GetOrCreateSystemManaged<WaterSystem>();
            _vanillaGeometrySystem = World.GetOrCreateSystemManaged<GeometrySystem>();
            _modificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier2B>();
            m_UpdatedAreasQuery = GetEntityQuery(
                new EntityQueryDesc
                {
                    All = new ComponentType[]
                    {
                        ComponentType.ReadOnly<Area>(),
                        ComponentType.ReadOnly<Updated>(),
                        ComponentType.ReadOnly<Node>(),
                        ComponentType.ReadWrite<Triangle>()
                    },
                    None = new ComponentType[]
                    {
                        ComponentType.ReadOnly<AreaHiddenMarker>()
                    }
                }
            );




            _hidingAreasQuery = GetEntityQuery(
                ComponentType.ReadWrite<Batch>(),
                ComponentType.ReadOnly<Area>(),
                ComponentType.ReadOnly<AreaHiddenMarker>()
                // ComponentType.ReadOnly<Updated>()
                );


            m_AllAreasQuery = GetEntityQuery(
                ComponentType.ReadOnly<Area>(), 
                ComponentType.ReadOnly<Node>(), 
                ComponentType.ReadWrite<Triangle>()
            );
            
            m_CreatedBuildingsQuery = GetEntityQuery(
                ComponentType.ReadOnly<Created>(),
                ComponentType.ReadOnly<Building>(), 
                ComponentType.ReadOnly<Owner>(), 
                ComponentType.Exclude<Temp>()
            );

            AddDebugUI();
        }

        protected override void OnGameLoaded(Context serializationContext)
        {
            m_Loaded = true;
        }

        private bool GetLoaded()
        {
            if (m_Loaded)
            {
                m_Loaded = false;
                return true;
            }
            return false;
        }

        protected override void OnUpdate()
        {
            var alterSystem = Mod.modSetting?.AlterVanillaGeometrySystem ?? false;
            ToggleSystem(alterSystem);
            if (!alterSystem) return;

            var ecb = _modificationBarrier.CreateCommandBuffer();

            EntityQuery entityQuery = (GetLoaded() ? m_AllAreasQuery : m_UpdatedAreasQuery);
            if (!entityQuery.IsEmptyIgnoreFilter)
            {
                NativeList<Entity> list = entityQuery.ToEntityListAsync(Allocator.TempJob, out JobHandle jhEntitesQuery);
                NativeList<Entity> buildings = m_CreatedBuildingsQuery.ToEntityListAsync(Allocator.TempJob, out JobHandle jhCreatedBuildingsQuery);
                TriangulateAreasJob jobData = default;
                jobData.m_Entities = list.AsDeferredJobArray();
                jobData.m_Buildings = buildings;
                jobData.m_SpaceData = SystemAPI.GetComponentLookup<Space>();
                jobData.m_OwnerData = SystemAPI.GetComponentLookup<Owner>(isReadOnly: true);
                jobData.m_UpdatedData = SystemAPI.GetComponentLookup<Updated>(isReadOnly: true);
                jobData.m_DeletedData = SystemAPI.GetComponentLookup<Deleted>(isReadOnly: true);
                jobData.m_TransformData = SystemAPI.GetComponentLookup<Transform>(isReadOnly: true);
                jobData.m_BuildingData = SystemAPI.GetComponentLookup<Building>(isReadOnly: true);
                jobData.m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: true);
                jobData.m_PrefabTerrainAreaData = SystemAPI.GetComponentLookup<TerrainAreaData>(isReadOnly: true);
                jobData.m_PrefabAreaGeometryData = SystemAPI.GetComponentLookup<AreaGeometryData>(isReadOnly: true);
                jobData.m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>(isReadOnly: true);
                jobData.m_SubObjects = SystemAPI.GetBufferLookup<Game.Objects.SubObject>(isReadOnly: true);
                jobData.m_TerrainHeightData = m_TerrainSystem.GetHeightData(waitForPending: true);
                jobData.m_WaterSurfaceData = m_WaterSystem.GetSurfaceData(out var deps);
                jobData.m_AreaData = SystemAPI.GetComponentLookup<Area>(isReadOnly: false);
                jobData.m_GeometryData = SystemAPI.GetComponentLookup<Geometry>(isReadOnly: false);
                jobData.m_Nodes = SystemAPI.GetBufferLookup<Node>(isReadOnly: false);
                jobData.m_Triangles = SystemAPI.GetBufferLookup<Triangle>(isReadOnly: false);

                JobHandle jobHandle = jobData.Schedule(
                    list, 
                    1, 
                    JobUtils.CombineDependencies(
                        base.Dependency, 
                        jhCreatedBuildingsQuery, 
                        jhEntitesQuery, 
                        deps
                    )
                );

                list.Dispose(jobHandle);
                buildings.Dispose(jobHandle);
                m_TerrainSystem.AddCPUHeightReader(jobHandle);
                m_WaterSystem.AddSurfaceReader(jobHandle);
                base.Dependency = jobHandle;
            }

            HideAreaGeometry(ecb);
        }


        private void HideAreaGeometry(EntityCommandBuffer ecb)
        {
            if (_hidingAreasQuery.IsEmpty) return;
            var entities = _hidingAreasQuery.ToEntityArray(Allocator.Temp);
            var cluBatch = SystemAPI.GetComponentLookup<Batch>(isReadOnly: false);
            _hiddenAreaCount = entities.Length;
            for (int i = 0; i < entities.Length; i++)
            {
                ecb.RemoveComponent<Batch>(entities[i]);
                ecb.AddComponent(entities[i], default(Updated));
            }

            entities.Dispose();

        }

        private void ToggleSystem(bool alterVanilla)
        {
            if (_vanillaGeometrySystem == null) return;
            _vanillaGeometrySystem.Enabled = !alterVanilla;
        }

        private void AddDebugUI()
        {
            _debugUIContainer = new DebugUI.Container(
                "Geometry System Alter",
                new ObservableList<DebugUI.Widget>
                {
                    new DebugUI.BoolField
                    {
                        displayName = "alter Game.Areas.GeometrySystem",
                        getter = () => Mod.modSetting.AlterVanillaGeometrySystem,
                        setter = (v) => Mod.modSetting.AlterVanillaGeometrySystem = v,
                    },
                    new DebugUI.Value { displayName = "Vanilla Geometry System Enabled", getter = () => _vanillaGeometrySystem.Enabled },
                    new DebugUI.Value { displayName = nameof(_hiddenAreaCount), getter = () => _hiddenAreaCount },
                }
                );
            Mod.AreaBucketDebugUI.children.Add(_debugUIContainer);
        }
    }

}

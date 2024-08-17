using AreaBucket.Systems.AreaBucketToolJobs;
using AreaBucket.Utils;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Citizens;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Routes;
using Game.Simulation;
using Game.Tools;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


namespace AreaBucket.Systems
{
    public partial class LotObjectToolSystem : ObjectToolBaseSystem
    {
        public override string toolID => "Object Tool";

        public bool ToolEnabled { get; set; } = false;

        private LotPrefab _selectedLotPrefab;

        private ObjectPrefab _selectedPrefab;

        private DebugUI.Container _debugUIContainer;

        private ProxyAction _applyAction;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem.tools.Remove(this); // rollback added self in base.OnCreate 
            m_ToolSystem.tools.Insert(0, this); // applied before vanilla systems

            _applyAction = Mod.modSetting.GetAction(Mod.kModAreaToolApply);
            BindingUtils.MimicBuiltinBinding(_applyAction, InputManager.kToolMap, "Apply", nameof(Mouse));

            CreateDebugUI();
        }

        protected override void OnStartRunning()
        {
            _applyAction.shouldBeEnabled = true;
            base.OnStartRunning();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!ToolEnabled || _selectedPrefab == null) return inputDeps;

            // clear tool apply state
            applyMode = ApplyMode.Clear;

            if (!GetRaycastResult(out var raycastResult)) return inputDeps;

            if (_applyAction.WasPressedThisFrame())
            {
                applyMode = ApplyMode.Apply;
                return inputDeps;
            }

            var objectPrefabEntity = m_PrefabSystem.GetEntity(_selectedPrefab);
            return CreateDefinition(objectPrefabEntity, raycastResult, inputDeps);
        }

        protected override void OnStopRunning()
        {
            _applyAction.shouldBeEnabled = false;
            base.OnStopRunning();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        public override PrefabBase GetPrefab()
        {
            return _selectedPrefab;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            if (!ToolEnabled) return false;
            if (!AreaPrefabUtils.TryGetLotPrefab(prefab, m_PrefabSystem, out _selectedLotPrefab)) return false;
            _selectedPrefab = (ObjectPrefab) prefab;
            return true;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();

            // Set raycast mask.
            m_ToolRaycastSystem.typeMask = TypeMask.Terrain;
        }


        private JobHandle CreateDefinition(Entity objectPrefabEntity, ControlPoint controlPoint, JobHandle deps)
        {
            CreateDefinitionsJobCopy jobData = default;
            jobData.m_EditorMode = m_ToolSystem.actionMode.IsEditor();
            jobData.m_LefthandTraffic = false;
            jobData.m_Removing = false; // guess is true when object is rotating
            jobData.m_Stamping = false;
            jobData.m_BrushSize = brushSize;
            jobData.m_BrushAngle = brushAngle;
            jobData.m_BrushStrength = brushStrength;
            jobData.m_DeltaTime = UnityEngine.Time.deltaTime; // from ObjectToolSystem.UpdateDefinitions, use unity's delta time if not using brush
            jobData.m_ObjectPrefab = objectPrefabEntity;
            jobData.m_TransformPrefab = Entity.Null;
            jobData.m_BrushPrefab = Entity.Null;
            jobData.m_Owner = Entity.Null;
            jobData.m_Original = Entity.Null;
            jobData.m_LaneEditor = Entity.Null;
            jobData.m_Theme = Entity.Null;
            jobData.m_RandomSeed = RandomSeed.Next(); // from ObjectToolSystem.Randomize
            jobData.m_Snap = GetActualSnap();
            // dont know why but ObjectToolSystem.actualAgeMask seems use this enum as default if not 'allowAgeMask'
            jobData.m_AgeMask = Game.Tools.AgeMask.Sapling; 
            jobData.m_ControlPoints = new NativeList<ControlPoint>(Allocator.TempJob);
            jobData.m_ControlPoints.Add(controlPoint);
            jobData.m_AttachmentPrefab = default; // from ObjectToolSystem.UpdateDefinitions, seems doesn't require initializations
            jobData.m_OwnerData = SystemAPI.GetComponentLookup<Owner>();
            jobData.m_TransformData = SystemAPI.GetComponentLookup<Game.Objects.Transform>();
            jobData.m_AttachedData = SystemAPI.GetComponentLookup<Attached>();
            jobData.m_LocalTransformCacheData = SystemAPI.GetComponentLookup<LocalTransformCache>();
            jobData.m_ElevationData = SystemAPI.GetComponentLookup<Game.Objects.Elevation>();
            jobData.m_BuildingData = SystemAPI.GetComponentLookup<Building>();
            jobData.m_LotData = SystemAPI.GetComponentLookup<Game.Buildings.Lot>();
            jobData.m_EdgeData = SystemAPI.GetComponentLookup<Edge>();
            jobData.m_NodeData = SystemAPI.GetComponentLookup<Game.Net.Node>();
            jobData.m_CurveData = SystemAPI.GetComponentLookup<Curve>();
            jobData.m_NetElevationData = SystemAPI.GetComponentLookup<Game.Net.Elevation>();
            jobData.m_OrphanData = SystemAPI.GetComponentLookup<Orphan>();
            jobData.m_UpgradedData = SystemAPI.GetComponentLookup<Upgraded>();
            jobData.m_CompositionData = SystemAPI.GetComponentLookup<Composition>();
            jobData.m_AreaClearData = SystemAPI.GetComponentLookup<Clear>();
            jobData.m_AreaSpaceData = SystemAPI.GetComponentLookup<Game.Areas.Space>();
            jobData.m_AreaLotData = SystemAPI.GetComponentLookup<Game.Areas.Lot>();
            jobData.m_EditorContainerData = SystemAPI.GetComponentLookup<Game.Tools.EditorContainer>();
            jobData.m_PrefabRefData = SystemAPI.GetComponentLookup<PrefabRef>();
            jobData.m_PrefabNetObjectData = SystemAPI.GetComponentLookup<NetObjectData>();
            jobData.m_PrefabBuildingData = SystemAPI.GetComponentLookup<BuildingData>();
            jobData.m_PrefabAssetStampData = SystemAPI.GetComponentLookup<AssetStampData>();
            jobData.m_PrefabBuildingExtensionData = SystemAPI.GetComponentLookup<BuildingExtensionData>();
            jobData.m_PrefabSpawnableObjectData = SystemAPI.GetComponentLookup<SpawnableObjectData>();
            jobData.m_PrefabObjectGeometryData = SystemAPI.GetComponentLookup<ObjectGeometryData>();
            jobData.m_PrefabPlaceableObjectData = SystemAPI.GetComponentLookup<PlaceableObjectData>();
            jobData.m_PrefabAreaGeometryData = SystemAPI.GetComponentLookup<AreaGeometryData>();
            jobData.m_PrefabBrushData = SystemAPI.GetComponentLookup<BrushData>();
            jobData.m_PrefabBuildingTerraformData = SystemAPI.GetComponentLookup<BuildingTerraformData>();
            jobData.m_PrefabCreatureSpawnData = SystemAPI.GetComponentLookup<CreatureSpawnData>();
            jobData.m_PlaceholderBuildingData = SystemAPI.GetComponentLookup<PlaceholderBuildingData>();
            jobData.m_PrefabNetGeometryData = SystemAPI.GetComponentLookup<NetGeometryData>();
            jobData.m_PrefabCompositionData = SystemAPI.GetComponentLookup<NetCompositionData>();
            jobData.m_SubObjects = SystemAPI.GetBufferLookup<Game.Objects.SubObject>();
            jobData.m_CachedNodes = SystemAPI.GetBufferLookup<LocalNodeCache>();
            jobData.m_InstalledUpgrades = SystemAPI.GetBufferLookup<InstalledUpgrade>();
            jobData.m_SubNets = SystemAPI.GetBufferLookup<Game.Net.SubNet>();
            jobData.m_ConnectedEdges = SystemAPI.GetBufferLookup<ConnectedEdge>();
            jobData.m_SubAreas = SystemAPI.GetBufferLookup<Game.Areas.SubArea>();
            jobData.m_AreaNodes = SystemAPI.GetBufferLookup<Game.Areas.Node>();
            jobData.m_AreaTriangles = SystemAPI.GetBufferLookup<Triangle>();
            jobData.m_PrefabSubObjects = SystemAPI.GetBufferLookup<Game.Prefabs.SubObject>();
            jobData.m_PrefabSubNets = SystemAPI.GetBufferLookup<Game.Prefabs.SubNet>();
            jobData.m_PrefabSubLanes = SystemAPI.GetBufferLookup<Game.Prefabs.SubLane>();
            jobData.m_PrefabSubAreas = SystemAPI.GetBufferLookup<Game.Prefabs.SubArea>();
            jobData.m_PrefabSubAreaNodes = SystemAPI.GetBufferLookup<Game.Prefabs.SubAreaNode>();
            jobData.m_PrefabPlaceholderElements = SystemAPI.GetBufferLookup<PlaceholderObjectElement>();
            jobData.m_PrefabRequirementElements = SystemAPI.GetBufferLookup<ObjectRequirementElement>();
            jobData.m_PrefabServiceUpgradeBuilding = SystemAPI.GetBufferLookup<ServiceUpgradeBuilding>();
            jobData.m_PrefabBrushCells = SystemAPI.GetBufferLookup<BrushCell>();
            jobData.m_ObjectSearchTree = m_ObjectSearchSystem.GetStaticSearchTree(readOnly: true, out var objSearchTreeDeps);
            jobData.m_WaterSurfaceData = m_WaterSystem.GetSurfaceData(out var waterSurfaceDataDeps);
            jobData.m_TerrainHeightData = m_TerrainSystem.GetHeightData();
            jobData.m_CommandBuffer = m_ToolOutputBarrier.CreateCommandBuffer();
            JobHandle jobHandle = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(deps, objSearchTreeDeps, waterSurfaceDataDeps));
            m_ObjectSearchSystem.AddStaticSearchTreeReader(jobHandle);
            m_WaterSystem.AddSurfaceReader(jobHandle);
            m_TerrainSystem.AddCPUHeightReader(jobHandle);
            m_ToolOutputBarrier.AddJobHandleForProducer(jobHandle);

            jobHandle = jobData.m_ControlPoints.Dispose(jobHandle);
            return jobHandle;
        }

        private void CreateDebugUI()
        {
            _debugUIContainer = new DebugUI.Container(
                "Lot Object Tool System",
                new ObservableList<DebugUI.Widget>
                {
                    new DebugUI.BoolField
                    {
                        displayName = nameof(ToolEnabled),
                        getter = () => ToolEnabled,
                        setter = (v) => ToolEnabled = v,
                    },

                    new DebugUI.Value
                    {
                        displayName = nameof(_selectedLotPrefab),
                        getter = () => _selectedLotPrefab?.prefab?.ToString() ?? "null",
                    }
                }
                );
            Mod.AreaBucketDebugUI.children.Add(_debugUIContainer);
        }
    }
}

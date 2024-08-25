using AreaBucket.Controllers;
using AreaBucket.Systems.AreaBucketToolJobs;
using AreaBucket.Utils;
using Colossal.Collections;
using Colossal.Entities;
using Game;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Objects;
using Game.Prefabs;
using Game.Tools;
using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using static Colossal.AssetPipeline.Diagnostic.Report;
using NetSearchSystem = Game.Net.SearchSystem;
using ZoneSearchSystem = Game.Zones.SearchSystem;

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

        private ProxyAction _secondaryApplyAction;

        private EntityQuery _buildingQuery;

        private RandomSeed _randomSeed;

        private quaternion _snapedRotation;

        private VanillaLikeRotationController _rotationController;

        private NetSearchSystem _netSearchSystem;

        private ZoneSearchSystem _zoneSearchSystem;

        private bool _hasSnapping = false;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem.tools.Remove(this); // rollback added self in base.OnCreate 
            m_ToolSystem.tools.Insert(0, this); // applied before vanilla systems

            _applyAction = Mod.modSetting.GetAction(Mod.kModAreaToolApply);
            BindingUtils.MimicBuiltinBinding(_applyAction, InputManager.kToolMap, "Apply", nameof(Mouse));
            _secondaryApplyAction = Mod.modSetting.GetAction(Mod.kModAreaToolSecondaryApply);
            BindingUtils.MimicBuiltinBinding(_secondaryApplyAction, InputManager.kToolMap, "Secondary Apply", nameof(Mouse));

            _rotationController = new VanillaLikeRotationController();

            _buildingQuery = GetEntityQuery(
                ComponentType.ReadOnly<BuildingData>(), 
                ComponentType.ReadOnly<SpawnableBuildingData>(), 
                ComponentType.ReadOnly<BuildingSpawnGroupData>(), 
                ComponentType.ReadOnly<PrefabData>()
                );

            _netSearchSystem = World.GetOrCreateSystemManaged<NetSearchSystem>();
            _zoneSearchSystem = World.GetOrCreateSystemManaged<ZoneSearchSystem>();

            CreateDebugUI();
        }

        protected override void OnStartRunning()
        {
            _applyAction.shouldBeEnabled = true;
            _secondaryApplyAction.shouldBeEnabled = true;
            base.OnStartRunning();
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (!ToolEnabled || _selectedPrefab == null) return inputDeps;

            // clear tool apply state
            applyMode = ApplyMode.Clear;

            if (!GetRaycastResult(out var raycastPoint)) return inputDeps;

            if (_applyAction.WasPressedThisFrame())
            {
                applyMode = ApplyMode.Apply; 
                _randomSeed = RandomSeed.Next(); // change random seed only after applied
                return inputDeps;
            }

            GetAvailableSnapMask(out m_SnapOnMask, out m_SnapOffMask);
            UpdateRotationState(raycastPoint.m_Position);

            var jobHandle = inputDeps;

            var objectPrefabEntity = m_PrefabSystem.GetEntity(_selectedPrefab);
            NativeReference<AttachmentData> attachmentPrefab = default;

            var isInEditorScene = m_ToolSystem.actionMode.IsEditor();
            var hasPlaceHolderInBuildingPrefab = EntityManager.TryGetComponent<PlaceholderBuildingData>(
                objectPrefabEntity, 
                out var placeholderBuildingData
                );

            if (!isInEditorScene && hasPlaceHolderInBuildingPrefab)
            {
                attachmentPrefab = new NativeReference<AttachmentData>(Allocator.TempJob);
                jobHandle = FindAttachments(objectPrefabEntity, placeholderBuildingData, attachmentPrefab, jobHandle);
            }

            var targetControlPoint = raycastPoint;
            Rotation targetRotation = default;
            targetRotation.m_Rotation = _rotationController.Rotation;

            jobHandle = SnapControlPoint(objectPrefabEntity, raycastPoint, jobHandle, out targetControlPoint, out targetRotation);

            jobHandle = CreateDefinition(objectPrefabEntity, GetPlaceControlPoint(targetControlPoint, targetRotation), attachmentPrefab, jobHandle);
            if (attachmentPrefab.IsCreated)
            {
                attachmentPrefab.Dispose(jobHandle);
            }
            return jobHandle;
        }

        protected override void OnStopRunning()
        {
            _applyAction.shouldBeEnabled = false;
            _secondaryApplyAction.shouldBeEnabled = false;
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

        private ControlPoint GetPlaceControlPoint(ControlPoint raycastPoint, Rotation rotation)
        {
            return new ControlPoint 
            { 
                m_Position = raycastPoint.m_Position, 
                m_Rotation = _hasSnapping ? raycastPoint.m_Rotation : _rotationController.Rotation,
            };
        }

        private void UpdateRotationState(float3 raycastWorldPosM)
        {
            var controllerIsRotating = _rotationController.State == VanillaLikeRotationController.ControllerState.Rotating;
            if (!controllerIsRotating && _secondaryApplyAction.WasPressedThisFrame())
            {
                _rotationController.StartRotation(raycastWorldPosM);
            }
            else if (controllerIsRotating && _secondaryApplyAction.WasReleasedThisFrame())
            {
                _rotationController.StopRotation();
            } 
            else
            {
                _rotationController.UpdateRotation(raycastWorldPosM);
            }
        }


        private JobHandle FindAttachments(
            Entity objectPrefabEntity, 
            PlaceholderBuildingData component, 
            NativeReference<AttachmentData> attachmentPrefab, 
            JobHandle deps
            )
        {
            ZoneData componentData = base.EntityManager.GetComponentData<ZoneData>(component.m_ZonePrefab);
            BuildingData componentData2 = base.EntityManager.GetComponentData<BuildingData>(objectPrefabEntity);
            _buildingQuery.ResetFilter();
            _buildingQuery.SetSharedComponentFilter(new BuildingSpawnGroupData(componentData.m_ZoneType));
            NativeList<ArchetypeChunk> chunks = _buildingQuery.ToArchetypeChunkListAsync(Allocator.TempJob, out var buildingQueryJobHandle);

            FindAttachmentBuildingJobCopy jobData = default;
            jobData.m_EntityType = SystemAPI.GetEntityTypeHandle();
            jobData.m_BuildingDataType = SystemAPI.GetComponentTypeHandle<BuildingData>(isReadOnly: true);
            jobData.m_SpawnableBuildingType = SystemAPI.GetComponentTypeHandle<SpawnableBuildingData>(isReadOnly: true);
            jobData.m_BuildingData = componentData2;
            jobData.m_RandomSeed = _randomSeed;
            jobData.m_Chunks = chunks;
            jobData.m_AttachmentPrefab = attachmentPrefab;
            var jobHandle = IJobExtensions.Schedule(jobData, JobHandle.CombineDependencies(deps, buildingQueryJobHandle));
            chunks.Dispose(jobHandle);
            return jobHandle;
        }

        private JobHandle CreateDefinition(
            Entity objectPrefabEntity, 
            ControlPoint controlPoint, 
            NativeReference<AttachmentData> attachmentPrefab,
            JobHandle deps
            )
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
            jobData.m_RandomSeed = _randomSeed; // from ObjectToolSystem.Randomize
            jobData.m_Snap = GetActualSnap();
            // dont know why but ObjectToolSystem.actualAgeMask seems use this enum as default if not 'allowAgeMask'
            jobData.m_AgeMask = Game.Tools.AgeMask.Sapling; 
            jobData.m_ControlPoints = new NativeList<ControlPoint>(Allocator.TempJob);
            jobData.m_ControlPoints.Add(controlPoint);
            jobData.m_AttachmentPrefab = attachmentPrefab; // from ObjectToolSystem.UpdateDefinitions, seems doesn't require initializations
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


        private JobHandle SnapControlPoint(
            Entity objectPrefabEntity, 
            ControlPoint sourceControlPoint, 
            JobHandle inputDeps,
            out ControlPoint snappedControlPoint,
            out Rotation snappedRotation
            )
        {
            // Entity selected = ((actualMode == Mode.Move) ? m_MovingObject : m_ToolSystem.selected);
            SnapJob jobData = default;
            jobData.m_Snap = GetActualSnap(); // it seems that current mask for lot building is NetSite | ContourLine | Upright
            // jobData.m_Snap = Snap.NetSide;
            jobData.m_Mode = ObjectToolSystem.Mode.Create;
            jobData.m_Prefab = objectPrefabEntity;

            // components lookup handle
            jobData.m_OwnerData = SystemAPI.GetComponentLookup<Owner>(isReadOnly: true);
            jobData.m_TerrainData = SystemAPI.GetComponentLookup<Game.Common.Terrain>(isReadOnly: true);
            jobData.m_BuildingData = SystemAPI.GetComponentLookup<BuildingData>(isReadOnly: true);
            jobData.m_PlaceableObjectData = SystemAPI.GetComponentLookup<PlaceableObjectData>(isReadOnly: true);
            jobData.m_BlockData = SystemAPI.GetComponentLookup<Game.Zones.Block>(isReadOnly: true);
            // buffers
            jobData.m_SubObjects = SystemAPI.GetBufferLookup<Game.Objects.SubObject>(isReadOnly: true);
            // search trees
            jobData.m_ZoneSearchTree = _zoneSearchSystem.GetSearchTree(readOnly: true, out var zoneSearchDeps);
            jobData.m_WaterSurfaceData = m_WaterSystem.GetSurfaceData(out var waterDataDeps);
            jobData.m_TerrainHeightData = m_TerrainSystem.GetHeightData();

            // jobData.m_ControlPoints = m_ControlPoints;
            jobData.m_ControlPoints = new NativeList<ControlPoint>(Allocator.TempJob);
            jobData.m_ControlPoints.Add(sourceControlPoint);

            jobData.hasSnapping = new NativeReference<bool>(Allocator.TempJob);

            var rotationData = default(Rotation);
            rotationData.m_Rotation = _rotationController.Rotation;
            rotationData.m_ParentRotation = quaternion.identity;
            rotationData.m_IsAligned = false;
            jobData.m_Rotation = new NativeValue<Rotation>(rotationData, Allocator.TempJob);


            var depsFinal = JobHandle.CombineDependencies(inputDeps, zoneSearchDeps, waterDataDeps);
            JobHandle jobHandle = IJobExtensions.Schedule(jobData, depsFinal);

            m_ObjectSearchSystem.AddStaticSearchTreeReader(jobHandle);
            _netSearchSystem.AddNetSearchTreeReader(jobHandle);
            _zoneSearchSystem.AddSearchTreeReader(jobHandle);
            m_WaterSystem.AddSurfaceReader(jobHandle);

            // to get snapped rotation data
            jobHandle.Complete();

            snappedRotation = jobData.m_Rotation.value;
            snappedControlPoint = jobData.m_ControlPoints[0];
            _hasSnapping = jobData.hasSnapping.Value;

            jobData.m_ControlPoints.Dispose(jobHandle);
            jobData.m_Rotation.Dispose();
            jobData.hasSnapping.Dispose(jobHandle);

            return jobHandle;
        }


        public override void GetAvailableSnapMask(out Snap onMask, out Snap offMask)
        {
            if (_selectedPrefab != null)
            {
                bool flag = m_PrefabSystem.HasComponent<BuildingData>(_selectedPrefab);
                bool isAssetStamp = false;
                bool flag2 = false;
                bool stamping = false;
                if (m_PrefabSystem.HasComponent<PlaceableObjectData>(_selectedPrefab))
                {
                    GetAvailableSnapMask(
                        m_PrefabSystem.GetComponentData<PlaceableObjectData>(_selectedPrefab), 
                        m_ToolSystem.actionMode.IsEditor(), 
                        flag, 
                        isAssetStamp, 
                        flag2, 
                        stamping, 
                        out onMask, 
                        out offMask
                        );
                }
                else
                {
                    GetAvailableSnapMask(
                        default(PlaceableObjectData), 
                        m_ToolSystem.actionMode.IsEditor(),
                        flag, 
                        isAssetStamp, 
                        flag2, 
                        stamping, 
                        out onMask, 
                        out offMask
                        );
                }
            }
            else
            {
                base.GetAvailableSnapMask(out onMask, out offMask);
            }
        }

        private static void GetAvailableSnapMask(
            PlaceableObjectData prefabPlaceableData, 
            bool editorMode, 
            bool isBuilding, 
            bool isAssetStamp, 
            bool brushing, 
            bool stamping, 
            out Snap onMask, 
            out Snap offMask
            )
        {
            onMask = Snap.Upright;
            offMask = Snap.None;
            
            if ((prefabPlaceableData.m_Flags & (Game.Objects.PlacementFlags.RoadSide | Game.Objects.PlacementFlags.OwnerSide)) == Game.Objects.PlacementFlags.OwnerSide)
            { // if placeable flag have OwnerSide and dont have RoadSide
                onMask |= Snap.OwnerSide;
            }
            else if ((prefabPlaceableData.m_Flags & (Game.Objects.PlacementFlags.RoadSide | Game.Objects.PlacementFlags.Shoreline | Game.Objects.PlacementFlags.Floating | Game.Objects.PlacementFlags.Hovering)) != 0)
            { // if flag has any (RoadSide, Shoreline, Floating, Hovering)
                if ((prefabPlaceableData.m_Flags & Game.Objects.PlacementFlags.OwnerSide) != 0)
                { 
                    onMask |= Snap.OwnerSide;
                    offMask |= Snap.OwnerSide;
                }
                if ((prefabPlaceableData.m_Flags & Game.Objects.PlacementFlags.RoadSide) != 0)
                {
                    onMask |= Snap.NetSide;
                    offMask |= Snap.NetSide;
                }
                if ((prefabPlaceableData.m_Flags & Game.Objects.PlacementFlags.RoadEdge) != 0)
                {
                    onMask |= Snap.NetArea;
                    offMask |= Snap.NetArea;
                }
                if ((prefabPlaceableData.m_Flags & Game.Objects.PlacementFlags.Shoreline) != 0)
                {
                    onMask |= Snap.Shoreline;
                    offMask |= Snap.Shoreline;
                }
                if ((prefabPlaceableData.m_Flags & Game.Objects.PlacementFlags.Hovering) != 0)
                {
                    onMask |= Snap.ObjectSurface;
                    offMask |= Snap.ObjectSurface;
                }
            }
            else if ((prefabPlaceableData.m_Flags & (Game.Objects.PlacementFlags.RoadNode | Game.Objects.PlacementFlags.RoadEdge)) != 0)
            { // if flag has any (RoadNode, RoadEdge)
                if ((prefabPlaceableData.m_Flags & Game.Objects.PlacementFlags.RoadNode) != 0)
                {
                    onMask |= Snap.NetNode;
                }
                if ((prefabPlaceableData.m_Flags & Game.Objects.PlacementFlags.RoadEdge) != 0)
                {
                    onMask |= Snap.NetArea;
                }
            }
            else if (editorMode && !isBuilding)
            { // if it is not building and it is in editorMode
                onMask |= Snap.ObjectSurface;
                offMask |= Snap.ObjectSurface;
                offMask |= Snap.Upright;
            }
            if (editorMode && (!isAssetStamp || stamping))
            {
                onMask |= Snap.AutoParent;
                offMask |= Snap.AutoParent;
            }
            if (brushing)
            {
                onMask &= Snap.Upright;
                offMask &= Snap.Upright;
                onMask |= Snap.PrefabType;
                offMask |= Snap.PrefabType;
            }
            if (isBuilding || isAssetStamp)
            {
                onMask |= Snap.ContourLines;
                offMask |= Snap.ContourLines;
            }
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
                    },

                    new DebugUI.Value
                    {
                        displayName = "Actual Snap",
                        getter = () =>
                        {
                            var snapMask = GetActualSnap();
                            if (snapMask == Snap.All) return "All";
                            if (snapMask == Snap.None) return "None";

                            string res = "";
                            
                            if ((snapMask & Snap.ExistingGeometry) != 0) res += "| ExistingGeometry";
                            if ((snapMask & Snap.CellLength) != 0) res += "| CellLength";
                            if ((snapMask & Snap.StraightDirection) != 0) res += "| StraightDirection";
                            if ((snapMask & Snap.NetSide) != 0) res += "| NetSide";
                            if ((snapMask & Snap.NetArea) != 0) res += "| NetArea";
                            if ((snapMask & Snap.OwnerSide) != 0) res += "| OwnerSide";
                            if ((snapMask & Snap.ObjectSide) != 0) res += "| ObjectSide";
                            if ((snapMask & Snap.NetMiddle) != 0) res += "| NetMiddle";
                            if ((snapMask & Snap.Shoreline) != 0) res += "| Shoreline";
                            if ((snapMask & Snap.NearbyGeometry) != 0) res += "| NearbyGeometry";
                            if ((snapMask & Snap.GuideLines) != 0) res += "| GuideLines";
                            if ((snapMask & Snap.ZoneGrid) != 0) res += "| ZoneGrid";
                            if ((snapMask & Snap.ObjectSurface) != 0) res += "| ObjectSurface";
                            if ((snapMask & Snap.Upright) != 0) res += "| Upright";
                            if ((snapMask & Snap.LotGrid) != 0) res += "| LotGrid";
                            if ((snapMask & Snap.AutoParent) != 0) res += "| AutoParent";
                            if ((snapMask & Snap.PrefabType) != 0) res += "| PrefabType";
                            if ((snapMask & Snap.ContourLines) != 0) res += "| ContourLines";

                            return res;
                        }
                    },

                    new DebugUI.Value
                    {
                        displayName = "Rotation State",
                        getter = () =>
                        {
                            if (_rotationController == null) return "Error: Rotation Controller is null";
                            if (_rotationController.State == VanillaLikeRotationController.ControllerState.Stop) return "Stop";
                            else if (_rotationController.State == VanillaLikeRotationController.ControllerState.Rotating) return "Rotating";
                            else return "Unknown";
                        }
                    },

                    new DebugUI.Value
                    {
                        displayName = "Rotation Angle",
                        getter = () =>
                        {
                            if (_rotationController == null) return "Error: Rotation Controller is null";
                            var id = UnityEngine.Quaternion.identity;
                            var rotation = new UnityEngine.Quaternion(
                                _rotationController.Rotation.value.x,
                                _rotationController.Rotation.value.y,
                                _rotationController.Rotation.value.z,
                                _rotationController.Rotation.value.w
                                );

                            var angleDegree = UnityEngine.Quaternion.Angle(id, rotation);
                            return angleDegree;
                        }
                    }
                }
                );
            Mod.AreaBucketDebugUI.children.Add(_debugUIContainer);
        }
    }
}

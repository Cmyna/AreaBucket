using AreaBucket.Components;
using AreaBucket.Systems.SurfacePreviewSystem;
using AreaBucket.Utils;
using Game.Areas;
using Game.Audio;
using Game.Common;
using Game.Debug;
using Game.Input;
using Game.Prefabs;
using Game.Tools;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AreaBucket.Systems
{
    public partial class AreaReplacementToolSystem : ToolBaseSystem
    {
        public override string toolID => "Area Tool";

        public bool ToolEnabled = false;

        /// <summary>
        /// if it is true, the tool is active and changing the game/scene
        /// </summary>
        public bool Active {
            get => _active;
            set
            {
                _active = value;
                ReActivateTool();
            }
        }

        private bool _active = false;

        private AreaPrefab _selectedPrefab;

        private ToolOutputBarrier _toolOutputBarrier;

        private ProxyAction _applyAction;

        /// <summary>
        /// a cache provides relations to native (unmanaged, burst compiled code) between prefabs and RenderedArea component,
        /// the index of the list is the index of prefab entity instances.
        /// </summary>
        private NativeList<NativeRenderedArea> _nativeRenderedAreas;

        private PrefabSystem _prefabSystem;

        private HashSet<Entity> _selectedEntities = new HashSet<Entity>();

        private Entity lastTargetEntity;

        private ControlPoint lastControlPoint;

        private bool _originalHasNodes;

        private bool _originalHasTriangles;

        private bool _originalHasOwner;

        private bool _originalHasLocalNodeCache;

        private bool _originalHasPrefabRef;

        private int _originalNodesCount;

        private AudioManager _audioManager;

        private EntityQuery _soundQuery;

        private int key = new Random().Next();

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem.tools.Remove(this); // rollback added self in base.OnCreate 
            m_ToolSystem.tools.Insert(0, this); // applied before vanilla systems

            _audioManager = World.GetOrCreateSystemManaged<AudioManager>();
            _soundQuery = GetEntityQuery(ComponentType.ReadOnly<ToolUXSoundSettingsData>());

            _toolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();

            _applyAction = Mod.modSetting.GetAction(Mod.kModAreaToolApply);
            BindingUtils.MimicBuiltinBinding(_applyAction, InputManager.kToolMap, "Apply", nameof(Mouse));

            _nativeRenderedAreas = new NativeList<NativeRenderedArea>(Allocator.Persistent);
            CreateDebugUI();
        }


        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _applyAction.shouldBeEnabled = true;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (_selectedPrefab == null || !Active || !ToolEnabled) return inputDeps;
            if (!_prefabSystem.TryGetEntity(_selectedPrefab, out var prefabEntity)) return inputDeps;
            if (!GetRaycastResult(out var controlPoint)) return inputDeps;

            applyMode = ApplyMode.Clear;

            if (_applyAction.WasPressedThisFrame())
            {
                _audioManager.PlayUISound(_soundQuery.GetSingleton<ToolUXSoundSettingsData>().m_PlacePropSound);
                applyMode = ApplyMode.Apply;
                return inputDeps;
            }

            lastControlPoint = controlPoint;
            var targetAreaEntity = controlPoint.m_OriginalEntity;

            if (targetAreaEntity == Entity.Null) return inputDeps;
            lastTargetEntity = targetAreaEntity;


            var ecb = _toolOutputBarrier.CreateCommandBuffer();


            var bluNodes = SystemAPI.GetBufferLookup<Node>(isReadOnly: true);
            var bluLocalNodeCache = SystemAPI.GetBufferLookup<LocalNodeCache>(isReadOnly: true);
            var bluTriangles = SystemAPI.GetBufferLookup<Triangle>(isReadOnly: true);
            var cluOwner = SystemAPI.GetComponentLookup<Owner>(isReadOnly: true);
            var cluPrefabRef = SystemAPI.GetComponentLookup<PrefabRef>(isReadOnly: false);

            var jobHandle = inputDeps;

            _originalHasNodes = bluNodes.TryGetBuffer(targetAreaEntity, out var originalNodes);
            _originalHasTriangles = bluTriangles.HasBuffer(targetAreaEntity);
            _originalHasOwner = cluOwner.TryGetComponent(targetAreaEntity, out var owner);
            _originalHasLocalNodeCache = bluLocalNodeCache.TryGetBuffer(targetAreaEntity, out var originalLocalNodeCache);
            _originalHasPrefabRef = cluPrefabRef.TryGetComponent(targetAreaEntity, out var prefabRef);


            // create a new one
            var newReplacedEntity = ecb.CreateEntity();
            var creationDefinition = default(CreationDefinition);
            creationDefinition.m_Prefab = prefabEntity;
            ecb.AddComponent(newReplacedEntity, creationDefinition);
            if (_originalHasOwner) creationDefinition.m_Owner = owner.m_Owner;
            if (_originalHasNodes)
            {
                var newNodesBuffer = ecb.AddBuffer<Node>(newReplacedEntity);
                newNodesBuffer.ResizeUninitialized(originalNodes.Length + 1);
                for (int i = 0; i < originalNodes.Length; i++) newNodesBuffer[i] = originalNodes[i];
                newNodesBuffer[originalNodes.Length] = newNodesBuffer[0];
            }
            if (_originalHasLocalNodeCache)
            {
                var newCacheBuffer = ecb.AddBuffer<LocalNodeCache>(newReplacedEntity);
                newCacheBuffer.ResizeUninitialized(originalLocalNodeCache.Length + 1);
                for (int i = 0; i < originalNodes.Length; i++) newCacheBuffer[i] = originalLocalNodeCache[i];
                newCacheBuffer[originalLocalNodeCache.Length] = newCacheBuffer[0];
            }
            ecb.AddComponent(newReplacedEntity, default(Updated));


            // send to Modifications phases that tend to delete old one
            var deleteRequestEntity = ecb.CreateEntity();
            var deleteDefition = default(CreationDefinition);
            deleteDefition.m_Original = targetAreaEntity;
            deleteDefition.m_Flags = CreationFlags.Delete;
            if (_originalHasOwner) deleteDefition.m_Owner = owner.m_Owner;
            // for general surface entities, it is necessary to append Nodes buffer in delete request
            // (well it is really counterintuitive that why we need nodes info for a to be deleted entity?)
            if (_originalHasNodes)
            {
                var nodeBuffer = ecb.AddBuffer<Node>(deleteRequestEntity);
                nodeBuffer.ResizeUninitialized(originalNodes.Length + 1);
                for (int i = 0; i < originalNodes.Length; i++) nodeBuffer[i] = originalNodes[i];
                nodeBuffer[originalNodes.Length] = nodeBuffer[0];
            }

            ecb.AddComponent(deleteRequestEntity, deleteDefition);
            ecb.AddComponent(deleteRequestEntity, default(Updated));


            return jobHandle;
        }

        public static void RequestHideArea(Entity targetAreaEntity, EntityCommandBuffer ecb)
        {
            var definitionEntity = ecb.CreateEntity();
            var areaHiddenDefinition = new AreaHiddenDefinition { target = targetAreaEntity };
            ecb.AddComponent(definitionEntity, areaHiddenDefinition);
        }


        public static void RequestRestoreArea(Entity targetAreaEntity, EntityCommandBuffer ecb)
        {
            // just simple notify the are is updated
            ecb.AddComponent(targetAreaEntity, default(Updated));
        }
        



        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            _applyAction.shouldBeEnabled = false;
        }


        protected override void OnDestroy()
        {
            base.OnDestroy();
            _nativeRenderedAreas.Dispose();
        }

        public override PrefabBase GetPrefab()
        {
            return _selectedPrefab;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            ToolEnabled = CanEnable(prefab); // set ComponentSystemBase.Enabled to true so it starts running OnUpdate method
            if (ToolEnabled) _selectedPrefab = prefab as AreaPrefab;
            return ToolEnabled && Active;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Areas | TypeMask.Terrain;
            m_ToolRaycastSystem.raycastFlags |= RaycastFlags.SubElements;
            m_ToolRaycastSystem.areaTypeMask = AreaTypeMask.Surfaces;
        }

        /// <summary>
        /// this one determines that the tool is enable or not 
        /// </summary>
        /// <param name="prefab"></param>
        /// <returns></returns>
        private bool CanEnable(PrefabBase prefab)
        {
            if (!(prefab is AreaPrefab)) return false; // if selected prefab is not area prefab, it will not be enabled
            // if prefab is District or Lot prefab, not enabled
            if (prefab is DistrictPrefab) return false;
            if (prefab is LotPrefab) return false;
            return true;
        }


        private void CreateDebugUI()
        {
            var panel = DebugManager.instance.GetPanel("Area Replacement Tool", createIfNull: true, groupIndex: 0, overrideIfExist: true);
            List<DebugUI.Widget> list = new List<DebugUI.Widget>
            {
                new DebugUI.BoolField
                {
                    displayName = nameof(Active),
                    getter = () => Active,
                    setter = (v) =>
                    {
                        Active = v;
                        ReActivateTool();
                    }
                },
                new DebugUI.Value
                {
                    displayName = nameof(ToolEnabled),
                    getter = () => ToolEnabled,
                    
                },
                new DebugUI.Value
                {
                    displayName = nameof(_selectedPrefab),
                    getter = () =>
                    {
                        if (_selectedPrefab == null) return "null";
                        else return _selectedPrefab.name;
                    },
                },
                new DebugUI.Value
                {
                    displayName = nameof(lastTargetEntity),
                    getter = () => lastTargetEntity.Index,
                },
                new DebugUI.Value
                {
                    displayName = nameof(lastControlPoint),
                    getter = () =>
                    {
                        return $"({lastControlPoint.m_HitPosition.x}, {lastControlPoint.m_HitPosition.z})";
                    },
                },
                new DebugUI.Value { displayName = nameof(_originalHasNodes),  getter = () => _originalHasNodes, },
                new DebugUI.Value { displayName = nameof(_originalHasTriangles),  getter = () => _originalHasTriangles, },
                new DebugUI.Value { displayName = nameof(_originalHasOwner),  getter = () => _originalHasOwner, },
                new DebugUI.Value { displayName = nameof(_originalHasLocalNodeCache),  getter = () => _originalHasLocalNodeCache, },
                new DebugUI.Value { displayName = nameof(_originalHasPrefabRef),  getter = () => _originalHasPrefabRef, },

                new DebugUI.Value { displayName = nameof(_originalNodesCount),  getter = () => _originalNodesCount, },

            }; 
            panel.children.Clear();
            panel.children.Add(list);
        }

        private void ReActivateTool()
        {
            var activePrefab = m_ToolSystem.activePrefab;
            m_ToolSystem.ActivatePrefabTool(activePrefab);
        }


    }
}

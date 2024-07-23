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

            //_originalNodesCount = originalNodes.Length;

            // var originalNodesCount = new NativeReference<int>();


            /*jobHandle = Job.WithCode(() => 
            {*/
            /*originalHasNodes.Value = bluNodes.TryGetBuffer(targetAreaEntity, out var originalNodes);
            originalHasTriangles.Value = bluTriangles.HasBuffer(targetAreaEntity);
            originalHasOwner.Value = cluOwner.TryGetComponent(targetAreaEntity, out var owner);
            originalHasLocalNodeCache.Value = bluLocalNodeCache.TryGetBuffer(targetAreaEntity, out var originalLocalNodeCache);*/
            

            if (!_originalHasNodes || !_originalHasTriangles || !_originalHasPrefabRef) return jobHandle;

            _originalNodesCount = originalNodes.Length;


            /* var creationDefinition = default(CreationDefinition);
            // creationDefinition.m_Original = targetAreaEntity;
            creationDefinition.m_Prefab = prefabEntity;
            // creationDefinition.m_Flags = CreationFlags.C;
            if (_originalHasOwner)
            {
                creationDefinition.m_Owner = owner.m_Owner;
            }

            var updateDefEntity = ecb.CreateEntity();
            ecb.AddComponent(updateDefEntity, creationDefinition);
            ecb.AddComponent(updateDefEntity, default(Updated));

            DynamicBuffer<Node> nodes = ecb.AddBuffer<Node>(updateDefEntity);
            nodes.ResizeUninitialized(originalNodes.Length + 1);
            for (int i = 0; i < originalNodes.Length; i++) nodes[i] = originalNodes[i];
            nodes[originalNodes.Length] = nodes[0];*/

            // Game.Tools.GenerateAreasSystem.CreateAreasJob CreateAreas methods:
            // it will overwrite the creationDefinition.m_Prefab to fit original entity's prefab if found m_Original is not Entity.Null
            // hence here we should add an new creation defintion just for original entity (to hide and to be deleted)


            var originalUpdateDefEntity = ecb.CreateEntity();

            ecb.AddComponent(originalUpdateDefEntity, new AreaHiddenDefinition
            {
                target = targetAreaEntity
            });
            ecb.AddComponent(originalUpdateDefEntity, default(Updated));

            /*ecb.AddComponent(originalUpdateDefEntity, new CreationDefinition
            {
                m_Original = targetAreaEntity,
                //m_Flags = CreationFlags.Select | CreationFlags.Delete,
                m_Flags = CreationFlags.Recreate | CreationFlags.Select,
                m_Prefab = prefabEntity,
            });*/
            //ecb.AddComponent(originalUpdateDefEntity, default(Updated));
            //var updatedNodeBuffer = ecb.AddBuffer<Node>(originalUpdateDefEntity);
            //CreateSurfaceUtils.Scene2Drawing(originalNodes, updatedNodeBuffer);

            /*}).Schedule(jobHandle);


            jobHandle.Complete();
            /*_originalHasNodes = originalHasNodes.Value;
            _originalHasOwner = originalHasOwner.Value;
            _originalHasTriangles = originalHasTriangles.Value;
            _originalHasLocalNodeCache = originalHasLocalNodeCache.Value;
            _originalNodesCount = originalNodesCount.Value;

            originalHasOwner.Dispose(jobHandle);
            originalHasNodes.Dispose(jobHandle);
            originalHasTriangles.Dispose(jobHandle);
            originalHasLocalNodeCache.Dispose(jobHandle);
            originalNodesCount.Dispose(jobHandle);*/


            /*if (hasLocalNodeCache)
            {
                var localNodes = ecb.AddBuffer<LocalNodeCache>(updateDefEntity);
                localNodes.ResizeUninitialized(originalLocalNodeCache.Length);
                for (int i = 0; i < originalLocalNodeCache.Length; i++) localNodes[i] = originalLocalNodeCache[i];
            }*/


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
                    },
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

        private void NewKey()
        {
            key = new Random().Next();
        }
    }
}

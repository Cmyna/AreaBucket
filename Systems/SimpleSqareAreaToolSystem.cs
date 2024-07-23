using AreaBucket.Components;
using AreaBucket.Utils;
using Game.Areas;
using Game.Audio;
using Game.Common;
using Game.Debug;
using Game.Input;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace AreaBucket.Systems
{
    public partial class SimpleSqareAreaToolSystem : ToolBaseSystem
    {
        public override string toolID => "Area Tool";

        public bool Active { get; set; } = false;

        public bool toolEnabled = false;

        private AudioManager _audioManager;

        private EntityQuery _soundQuery;

        private ToolOutputBarrier _toolOutputBarrier;

        private ProxyAction _applyAction;

        private AreaPrefab _selectedPrefab;

        private PrefabSystem _prefabSystem;

        private TerrainSystem _terrainSystem;

        private int _trackingId;

        protected override void OnCreate()
        {
            base.OnCreate();

            m_ToolSystem.tools.Remove(this); // rollback added self in base.OnCreate 
            m_ToolSystem.tools.Insert(0, this); // applied before vanilla systems

            _audioManager = World.GetOrCreateSystemManaged<AudioManager>();
            _soundQuery = GetEntityQuery(ComponentType.ReadOnly<ToolUXSoundSettingsData>());

            _toolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            _prefabSystem = World.GetOrCreateSystemManaged<PrefabSystem>();
            _terrainSystem = World.GetExistingSystemManaged<TerrainSystem>();

            _applyAction = Mod.modSetting.GetAction(Mod.kModAreaToolApply);
            BindingUtils.MimicBuiltinBinding(_applyAction, InputManager.kToolMap, "Apply", nameof(Mouse));

            _trackingId = new Random().NextInt();

            CreateDebugUI();


        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
            _applyAction.shouldBeEnabled = true;
        }


        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            if (_selectedPrefab == null || !Active) return inputDeps;
            if (!_prefabSystem.TryGetEntity(_selectedPrefab, out var prefabEntity)) return inputDeps;
            if (!GetRaycastResult(out var controlPoint)) return inputDeps;

            applyMode = ApplyMode.Clear;

            var ecb = _toolOutputBarrier.CreateCommandBuffer();
            var terrainHeightData = _terrainSystem.GetHeightData();

            var points = new NativeList<float3>(Allocator.Temp);

            SimpleSquare(controlPoint.m_HitPosition, points);

            var surfacePreviewEntity = ecb.CreateEntity();
            ecb.AddComponent(surfacePreviewEntity, new SurfacePreviewDefinition
            {
                prefab = prefabEntity,
                key = _trackingId,
                applyPreview = false
            });
            var nodeBuffer = ecb.AddBuffer<Node>(surfacePreviewEntity);
            nodeBuffer.ResizeUninitialized(points.Length + 1);
            for (int i = 0; i < points.Length; i++)
            {
                var node = default(Node);
                node.m_Elevation = float.MinValue;
                var point = points[i];
                point.y = TerrainUtils.SampleHeight(ref terrainHeightData, point);
                node.m_Position = point;
                nodeBuffer[i] = node;
            }
            nodeBuffer[points.Length] = nodeBuffer[0];


            points.Dispose();

            return inputDeps;
        }

        protected override void OnStopRunning()
        {
            base.OnStopRunning();
            _applyAction.shouldBeEnabled = false;
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
            toolEnabled = CanEnable(prefab); // set ComponentSystemBase.Enabled to true so it starts running OnUpdate method
            if (toolEnabled) _selectedPrefab = prefab as AreaPrefab;
            return toolEnabled && Active;
        }

        private bool CanEnable(PrefabBase prefab)
        {
            if (!(prefab is AreaPrefab)) return false; // if selected prefab is not area prefab, it will not be enabled
            // if prefab is District or Lot prefab, not enabled
            if (prefab is DistrictPrefab) return false;
            if (prefab is LotPrefab) return false;
            return true;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Terrain;
            //m_ToolRaycastSystem.raycastFlags |= RaycastFlags.SubElements;
            //m_ToolRaycastSystem.areaTypeMask = AreaTypeMask.Surfaces;
        }


        private void SimpleSquare(float3 centerPos, NativeList<float3> points, float length = 30)
        {
            points.Add(new float3(centerPos.x - length, centerPos.y, centerPos.z - length) );
            points.Add(new float3(centerPos.x + length, centerPos.y, centerPos.z - length) );
            points.Add(new float3(centerPos.x + length, centerPos.y, centerPos.z + length) );
            points.Add(new float3(centerPos.x - length, centerPos.y, centerPos.z + length) );

        }

        private void ReActivateTool()
        {
            var activePrefab = m_ToolSystem.activePrefab;
            m_ToolSystem.ActivatePrefabTool(activePrefab);
        }

        private void CreateDebugUI()
        {
            var panel = DebugManager.instance.GetPanel("Simple Square Area Tool", createIfNull: true, groupIndex: 0, overrideIfExist: true);
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
                    displayName = nameof(Enabled),
                    getter = () => Enabled,
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
                /*new DebugUI.Value { displayName = nameof(_originalHasNodes),  getter = () => _originalHasNodes, },
                new DebugUI.Value { displayName = nameof(_originalHasTriangles),  getter = () => _originalHasTriangles, },
                new DebugUI.Value { displayName = nameof(_originalHasOwner),  getter = () => _originalHasOwner, },
                new DebugUI.Value { displayName = nameof(_originalHasLocalNodeCache),  getter = () => _originalHasLocalNodeCache, },
                new DebugUI.Value { displayName = nameof(_originalHasPrefabRef),  getter = () => _originalHasPrefabRef, },

                new DebugUI.Value { displayName = nameof(_originalNodesCount),  getter = () => _originalNodesCount, },*/

            };
            panel.children.Clear();
            panel.children.Add(list);
        }


    }
}

using AreaBucket.Components;
using Game;
using Game.Areas;
using Game.Common;
using Game.Debug;
using Game.Prefabs;
using Game.Rendering;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.Rendering;

namespace AreaBucket.Systems
{
    public partial class SimpleHideAreaSystem : GameSystemBase
    {
        private SafeCommandBufferSystem _modificationBarrier;

        private EntityQuery _hideAreaDefintionQuery;

        private EntityQuery _markedHiddenAreasQuery;

        private int _lastHandledDefs;

        private int _trianglesCount = -1;

        private NativeHashSet<Entity> _entitesReceivedHiddenDefinitions;

        private DebugUI.Container _debugUIContainer;

        private bool _removeMarkers;

        protected override void OnCreate()
        {
            base.OnCreate();
            _modificationBarrier = World.GetOrCreateSystemManaged<ModificationBarrier1>();
            _hideAreaDefintionQuery = GetEntityQuery
            (
                ComponentType.ReadOnly<AreaHiddenDefinition>(),
                ComponentType.ReadOnly<Updated>()
            );

            _markedHiddenAreasQuery = GetEntityQuery(ComponentType.ReadOnly<AreaHiddenMarker>());

            _entitesReceivedHiddenDefinitions = new NativeHashSet<Entity>(100, Allocator.Persistent);
            CreateDebugPanel();
        }
        protected override void OnUpdate()
        {
            
            var ecb = _modificationBarrier.CreateCommandBuffer();

            UpdateHiddingStates();
            AddNewHiddenEntities(ecb);
            if (_removeMarkers) RemoveUnHiddenMarkers(_entitesReceivedHiddenDefinitions, ecb);

            ecb.DestroyEntity(_hideAreaDefintionQuery, EntityQueryCaptureMode.AtPlayback);
        }
        

        protected override void OnDestroy()
        {
            base.OnDestroy();
            _hideAreaDefintionQuery.Dispose();
            _markedHiddenAreasQuery.Dispose();
            _entitesReceivedHiddenDefinitions.Dispose();
        }

        private void UpdateHiddingStates()
        {
            _entitesReceivedHiddenDefinitions.Clear();
            if (_hideAreaDefintionQuery.IsEmpty) return;

            var definitions = _hideAreaDefintionQuery.ToComponentDataArray<AreaHiddenDefinition>(Allocator.Temp);
            for (int i = 0; i < definitions.Length; i++)
            {
                var targetEntity = definitions[i].target;
                _entitesReceivedHiddenDefinitions.Add(targetEntity);
            }
            definitions.Dispose();
        }


        private void AddNewHiddenEntities(EntityCommandBuffer ecb)
        {
            if (_hideAreaDefintionQuery.IsEmpty) return;
            var definitions = _hideAreaDefintionQuery.ToComponentDataArray<AreaHiddenDefinition>(Allocator.Temp);

            var cluHiddenMarker = SystemAPI.GetComponentLookup<AreaHiddenMarker>();

            for (int i = 0; i < definitions.Length; i++)
            {
                var targetEntity = definitions[i].target;
                if (cluHiddenMarker.HasComponent(targetEntity)) continue;

                ecb.AddComponent(targetEntity, default(AreaHiddenMarker));
                ecb.AddComponent(targetEntity, default(Updated));
            }

            definitions.Dispose();
        }


        private void RemoveUnHiddenMarkers(NativeHashSet<Entity> definedHiddenEntites, EntityCommandBuffer ecb)
        {
            if (_markedHiddenAreasQuery.IsEmpty) return;
            var entities = _markedHiddenAreasQuery.ToEntityArray(Allocator.Temp);


            for (int i = 0; i < entities.Length; i++)
            {
                if (definedHiddenEntites.Contains(entities[i])) continue;
                ecb.RemoveComponent<AreaHiddenMarker>(entities[i]);
                ecb.AddComponent(entities[i], default(Updated));
            }


            entities.Dispose();
        }


        private void CreateDebugPanel()
        {
            _debugUIContainer = new DebugUI.Container(
                "Hide Geometry System",
                new ObservableList<DebugUI.Widget>
                {
                    new DebugUI.Value { displayName=nameof(_lastHandledDefs), getter = () => _lastHandledDefs},
                    new DebugUI.Value { displayName=nameof(_trianglesCount), getter = () => _trianglesCount},
                    new DebugUI.BoolField
                    {
                        displayName = "enabled remove markers",
                        getter = () => _removeMarkers,
                        setter = (v) => _removeMarkers = v,
                    }
                }
                );
            Mod.AreaBucketDebugUI.children.Add(_debugUIContainer);
        }
    }
}

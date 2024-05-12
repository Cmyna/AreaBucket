using AreaBucket.Systems.AreaBucketToolJobs;
using Colossal.Mathematics;
using Game.Areas;
using Game.Common;
using Game.Input;
using Game.Net;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems
{
    /// <summary>
    /// FIX: performance issue, after testing I believe too much rays is the main reason
    /// After profiling, main performance cost comes from FilterPoints job(fixed), secondary comes from drop rays job
    /// </summary>
    public partial class AreaBucketToolSystem : ToolBaseSystem
    {
        public bool ToolEnabled = true;

        /// <summary>
        /// bucket max filling range
        /// </summary>
        public float FillMaxRange = 30;

        public float MinEdgeLength = 2f;

        public bool Log4Debug = false;

        public bool CheckIntersection = true;

        public bool FillWithArea = true;

        public bool FillWithNet = true;

        public bool JobImmediate = true;

        public bool WatchJobTime = true;




        public override string toolID => Mod.ToolId;

        private NativeList<ControlPoint> _controlPoints;

        private AreaPrefab _selectedPrefab;

        private TerrainSystem _terrianSystem;

        private ToolOutputBarrier _toolOutputBarrier;

        private ProxyAction _applyAction;

        private ProxyAction _secondaryApplyAction;

        private EntityQuery netEntityQuery;

        private EntityQuery areaEntityQuery;

        private EntityQuery edgeGeoEntityQuery;

        private int frameCount = 0;

        private System.Diagnostics.Stopwatch timer;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ToolSystem.tools.Remove(this); // rollback added self in base.OnCreate 
            m_ToolSystem.tools.Insert(0, this); // applied before vanilla systems

            _terrianSystem = base.World.GetOrCreateSystemManaged<TerrainSystem>();
            _toolOutputBarrier = base.World.GetOrCreateSystemManaged<ToolOutputBarrier>();

            _controlPoints = new NativeList<ControlPoint>(Allocator.Persistent);

            _applyAction = InputManager.instance.FindAction("Tool", "Apply");
            _secondaryApplyAction = InputManager.instance.FindAction("Tool", "Secondary Apply");

            timer = new System.Diagnostics.Stopwatch();

            netEntityQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAll<Curve>()
                .WithAll<Edge>()
                .Build(EntityManager);
            areaEntityQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAll<Area>()
                .WithAll<Game.Areas.Node>()
                .WithNone<Updated>()
                .WithNone<Temp>()
                .WithNone<CreationDefinition>()
                .WithNone<Deleted>()
                .WithNone<MapTile>()
                .Build(EntityManager);
            edgeGeoEntityQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAny<EdgeGeometry>()
                .WithAny<Game.Net.Node>()
                .WithNone<Hidden>().WithNone<Deleted>()
                .Build(EntityManager);
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();

            _applyAction.shouldBeEnabled = true;
            _secondaryApplyAction.shouldBeEnabled = true;

            applyMode = ApplyMode.Clear;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            frameCount = frameCount + 1;
            if (frameCount >= 10) frameCount = 0;
            // if not active, do nothing
            if (_selectedPrefab == null || !ToolEnabled) return inputDeps;

            if (_applyAction.WasPressedThisFrame())
            {
                applyMode = ApplyMode.Apply;
                return inputDeps;
            }
            applyMode = ApplyMode.Clear;

            if (!GetRaycastResult(out var raycastPoint))
            {
                return inputDeps;
            }

            return ApplyBucket(inputDeps, raycastPoint);

            // return SquareAreaTest2(inputDeps, raycastPoint);
            // return SquareAreaTest(inputDeps, raycastPoint);
        }


        protected override void OnStopRunning()
        {
            base.OnStopRunning();

            _applyAction.shouldBeEnabled = false;
            _secondaryApplyAction.shouldBeEnabled = false;
        }

        protected override void OnDestroy()
        {
            _controlPoints.Dispose();
            netEntityQuery.Dispose();
            areaEntityQuery.Dispose();
            edgeGeoEntityQuery.Dispose();
            base.OnDestroy();
        }

        public override PrefabBase GetPrefab()
        {
            return _selectedPrefab;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            if (ToolEnabled && prefab is AreaPrefab)
            {
                _selectedPrefab = prefab as AreaPrefab;
                //LogAreaPrefab(prefab as AreaPrefab);
                return true;
            }
            return false;
        }

        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Terrain;
        }

        private JobHandle ApplyBucket(JobHandle inputDeps, ControlPoint raycastPoint)
        {
            var jobHandle = inputDeps;
            // prepare jobs data

            var pointsCache = new NativeList<float2>(Allocator.TempJob);
            var linesCache = new NativeList<Line2>(Allocator.TempJob);
            var checkLinesCache = new NativeList<Line2>(Allocator.TempJob);
            var raysCache = new NativeList<AreaBucketToolJobs.Ray>(Allocator.TempJob);
            var bezierCurvesCache = new NativeList<Bezier4x3>(Allocator.TempJob);

            var filterEdgesJob = default(FilterEdgesGeos);
            filterEdgesJob.thEdgeGeo = SystemAPI.GetComponentTypeHandle<EdgeGeometry>();
            filterEdgesJob.thStartNodeGeometry = SystemAPI.GetComponentTypeHandle<StartNodeGeometry>();
            filterEdgesJob.thEndNodeGeometry = SystemAPI.GetComponentTypeHandle<EndNodeGeometry>();
            filterEdgesJob.thComposition = SystemAPI.GetComponentTypeHandle<Composition>();
            filterEdgesJob.luCompositionData = SystemAPI.GetComponentLookup<NetCompositionData>();
            filterEdgesJob.filterRange = FillMaxRange;
            filterEdgesJob.hitPoint = raycastPoint.m_HitPosition.xz;
            filterEdgesJob.filterResults = bezierCurvesCache;

            var curve2LinesJob = default(Curve2Lines);
            curve2LinesJob.chopCount = 8;
            curve2LinesJob.curves = bezierCurvesCache;
            curve2LinesJob.linesCache = linesCache;
            curve2LinesJob.pointsCache = pointsCache;

            var area2LinesJob = default(Areas2Lines);
            area2LinesJob.lines = linesCache;
            area2LinesJob.checklines = checkLinesCache;
            area2LinesJob.points = pointsCache;
            area2LinesJob.hitPos = raycastPoint.m_HitPosition.xz;
            area2LinesJob.range = FillMaxRange;
            area2LinesJob.bthNode = SystemAPI.GetBufferTypeHandle<Game.Areas.Node>();
            area2LinesJob.thArea = SystemAPI.GetComponentTypeHandle<Area>();

            var filterPointsJob = default(FilterPoints);
            filterPointsJob.points = pointsCache;
            filterPointsJob.overlayDist = 0.1f;
            filterPointsJob.range = FillMaxRange;
            filterPointsJob.hitPos = raycastPoint.m_HitPosition.xz;

            var generateRaysJob = default(GenerateRays);
            generateRaysJob.lines = linesCache;
            generateRaysJob.points = pointsCache;
            generateRaysJob.rays = raysCache;
            generateRaysJob.rayStartPoint = raycastPoint.m_HitPosition.xz;

            var dropRaysJob = default(DropIntersectedRays);
            dropRaysJob.raysStartPoint = raycastPoint.m_HitPosition.xz;
            dropRaysJob.checkLines = linesCache;
            dropRaysJob.rays = raysCache;

            var mergeRaysJob = default(MergeRays);
            mergeRaysJob.rays = raysCache;
            mergeRaysJob.angleBound = 5 * Mathf.Deg2Rad;
            mergeRaysJob.minEdgeLength = MinEdgeLength;

            var rays2AreaJob = default(Rays2AreaDefinition);
            rays2AreaJob.sortedRays = raysCache;
            rays2AreaJob.prefab = m_PrefabSystem.GetEntity(_selectedPrefab);
            rays2AreaJob.terrianHeightData = _terrianSystem.GetHeightData();
            rays2AreaJob.gameRaycastPoint = raycastPoint;
            rays2AreaJob.commandBuffer = _toolOutputBarrier.CreateCommandBuffer();

            // run
            if (FillWithNet) jobHandle = Schedule(() => filterEdgesJob.Schedule(edgeGeoEntityQuery, jobHandle), "collect edges");
            jobHandle = Schedule(() => curve2LinesJob.Schedule(jobHandle), "curves to lines");
            if (FillWithArea) jobHandle = Schedule(() => area2LinesJob.Schedule(areaEntityQuery, jobHandle), "area to lines");
            jobHandle = Schedule(() => filterPointsJob.Schedule(jobHandle), "filter points");
            jobHandle = Schedule(() => generateRaysJob.Schedule(jobHandle), "generate rays");
            if (CheckIntersection) jobHandle = Schedule(() => dropRaysJob.Schedule(jobHandle), "drop rays");
            jobHandle = Schedule(() => mergeRaysJob.Schedule(jobHandle), "merge rays");
            jobHandle = Schedule(() => rays2AreaJob.Schedule(jobHandle), "create definitions");
            
            jobHandle.Complete();

            // dispose

            if (frameCount == 0 && Log4Debug)
            {
                Mod.log.Info($"hit point: {raycastPoint.m_HitPosition.x} " +
                    $"{raycastPoint.m_HitPosition.y} " +
                    $"{raycastPoint.m_HitPosition.z}");
                /*if (filterNetsJob.filterResults.Length > 0)
                {
                    LogNetShape(filterNetsJob.filterResults[0]);
                }*/
                Mod.log.Info($"generated rays count: {raysCache.Length}");
                Mod.log.Info($"lines count {curve2LinesJob.linesCache.Length}");
                Mod.log.Info($"area lines count {checkLinesCache.Length}");
                // Mod.log.Info($"generated area nodes count: {rays2AreaJob.generateNodesCount}");
                //for (var i = 0; i < raysCache.Length; i++) LogRay(raysCache[i]);
                //for (var i = 0; i < checkLinesCache.Length; i++)
                {
                    // Mod.log.Info($"area line: {checkLinesCache[i].ToStringEx()}");
                }
            }
            linesCache.Dispose();
            pointsCache.Dispose();
            raysCache.Dispose();
            checkLinesCache.Dispose();
            bezierCurvesCache.Dispose();

            return jobHandle;
        }

        private void LogNetShape(Bezier4x3 curve)
        {
            Mod.log.Info($"net data: " +
                $"{curve.a.x} {curve.a.z}\n" +
                $"{curve.b.x} {curve.b.z}\n" +
                $"{curve.c.x} {curve.c.z}\n" +
                $"{curve.d.x} {curve.d.z}\n"
            );
        }

        private void LogRay(AreaBucketToolJobs.Ray ray)
        {
            Mod.log.Info($"ray: (ray {ray.radian}) {ray.vector.x} {ray.vector.y}");
        }

        private void LogAreaPrefab(AreaPrefab prefab)
        {
            var components = new HashSet<ComponentType>();
            prefab.GetArchetypeComponents(components);
            foreach (var comp in components)
            {
                Mod.log.Info($"componentType: {comp.GetManagedType().Name}");
            }
        }

        private JobHandle Schedule(Func<JobHandle> scheduleFunc, string name)
        {
            if (WatchJobTime)
            {
                timer.Reset();
                timer.Start();
            }
            var jobHandle = scheduleFunc();
            if (JobImmediate) jobHandle.Complete();
            timer.Stop();
            Log($"job {name} time cost(ms): {timer.ElapsedMilliseconds}");
            return jobHandle;
        }
        
        private void Log(string msg)
        {
            if (frameCount == 0 && Log4Debug) Mod.log.Info(msg);
        }
    }
}

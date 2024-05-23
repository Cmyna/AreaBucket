using AreaBucket.Systems.AreaBucketToolJobs;
using Colossal.Logging;
using Colossal.Mathematics;
using Game.Areas;
using Game.Audio;
using Game.Buildings;
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
using UnityEngine;

namespace AreaBucket.Systems
{
    /// <summary>
    /// FIX: performance issue, after testing I believe too much rays is the main reason
    /// After profiling, main performance cost comes from FilterPoints job(fixed), secondary comes from drop rays job
    /// 20240504 updated: after adding subnet filter (and disable it) and I found that the tool becomes super smooth on 100m range. 
    /// Hence the major performance issure comes from this
    /// </summary>
    public partial class AreaBucketToolSystem : ToolBaseSystem
    {

        //public override string toolID => Mod.ToolId;
        public override string toolID => "Area Tool"; // use id same as vanilla area tool

        /// <summary>
        /// this property control the tool can be shown by player or not, it is determined by the selected prefab
        /// </summary>
        public bool ToolEnabled { get; set; } = false;

        /// <summary>
        /// set the tool is active or not. if it is ToolEnabled and active, 
        /// it will start generating area data definitions on update
        /// 
        /// active state is controlled by user switch
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// bucket tool filling range
        /// </summary>
        public float FillRange { get; set; } = 50f;

        /// <summary>
        /// bucket tool max filling range
        /// </summary>
        public float MaxFillingRange { get; set; } = 250f;


        public float MinEdgeLength { get; set; } = 2f;

        /// <summary>
        /// the boundary for area filling tool
        /// </summary>
        public BoundaryMask BoundaryMask { get; set; } = BoundaryMask.Area | BoundaryMask.Net | BoundaryMask.Lot;




        public bool UseExperimentalOptions { get; set; } = false;

        /// <summary>
        /// performance optimization setting
        /// </summary>
        public bool CheckOcclusion { get; set; } = true;

        public bool ExtraPoints { get; set; } = false;


        public bool ShowDebugOptions { get; set; } = false;

        public bool Log4Debug { get; set; } = false;

        public bool CheckIntersection { get; set; } = true;

        public bool JobImmediate { get; set; } = false;

        public bool WatchJobTime { get; set; } = false;

        /// <summary>
        /// actually drop net lane owned by road or building
        /// </summary>
        public bool DropOwnedLane { get; set; } = true;


        private AudioManager _audioManager;

        private EntityQuery _soundQuery;

        private NativeList<ControlPoint> _controlPoints;

        private AreaPrefab _selectedPrefab;

        private TerrainSystem _terrianSystem;

        private ToolOutputBarrier _toolOutputBarrier;

        private ProxyAction _applyAction;

        private ProxyAction _secondaryApplyAction;

        private EntityQuery netEntityQuery;

        private EntityQuery areaEntityQuery;

        private EntityQuery edgeGeoEntityQuery;

        private EntityQuery lotEntityQuery;

        private EntityQuery netLaneQuery;

        private int frameCount = 0;

        private System.Diagnostics.Stopwatch timer;

        protected override void OnCreate()
        {
            base.OnCreate();
            _audioManager = World.GetOrCreateSystemManaged<AudioManager>();
            _soundQuery = GetEntityQuery(ComponentType.ReadOnly<ToolUXSoundSettingsData>());

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
                .WithNone<District>() // exclude district polygons
                .Build(EntityManager);
            edgeGeoEntityQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAny<EdgeGeometry>()
                .WithAny<Game.Net.Node>()
                .WithNone<Hidden>().WithNone<Deleted>()
                .Build(EntityManager);
            lotEntityQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAll<PrefabRef>().WithAll<Game.Objects.Transform>()
                .WithAny<Building>().WithAny<Extension>()
                .WithNone<Deleted>().WithNone<Temp>().WithNone<Overridden>()
                .Build(EntityManager);
            netLaneQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAll<PrefabRef>()
                .WithAll<Curve>()
                .WithNone<Deleted>().WithNone<Temp>().WithNone<Overridden>()
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
            frameCount++;
            if (frameCount >= 10) frameCount = 0;
            // if not active, do nothing
            if (_selectedPrefab == null || !ToolEnabled || !Active) return inputDeps;

            applyMode = ApplyMode.Clear;
            if (!GetRaycastResult(out var raycastPoint))
            {
                return inputDeps;
            }

            if (_applyAction.WasPressedThisFrame())
            {
                _audioManager.PlayUISound(_soundQuery.GetSingleton<ToolUXSoundSettingsData>().m_PlacePropSound);
                applyMode = ApplyMode.Apply;
                return inputDeps;
            }
            
            return ApplyBucket(inputDeps, raycastPoint);
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
            lotEntityQuery.Dispose();
            base.OnDestroy();
        }

        public override PrefabBase GetPrefab()
        {
            return _selectedPrefab;
        }

        public override bool TrySetPrefab(PrefabBase prefab)
        {
            ToolEnabled = CanEnable(prefab);
            if (ToolEnabled) _selectedPrefab = prefab as AreaPrefab;
            return ToolEnabled && Active; 
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



        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = TypeMask.Terrain;
        }

        private JobHandle ApplyBucket(JobHandle inputDeps, ControlPoint raycastPoint)
        {
            var jobHandle = inputDeps;
            // prepare jobs data

            var checkLinesCache = new NativeList<Line2>(Allocator.TempJob);
            var bezierCurvesCache = new NativeList<Bezier4x3>(Allocator.TempJob);

            var jobContext = new CommonContext(); // Area bucket jobs common context
            jobContext.Init(raycastPoint.m_HitPosition.xz, FillRange);


            var lot2LinesJob = default(Lot2LinesJob);
            lot2LinesJob.thPrefabRef = SystemAPI.GetComponentTypeHandle<PrefabRef>();
            lot2LinesJob.thTransform = SystemAPI.GetComponentTypeHandle<Game.Objects.Transform>();
            lot2LinesJob.thBuilding = SystemAPI.GetComponentTypeHandle<Building>();
            lot2LinesJob.luBuildingData = SystemAPI.GetComponentLookup<BuildingData>();
            lot2LinesJob.luObjectGeoData = SystemAPI.GetComponentLookup<ObjectGeometryData>();
            lot2LinesJob.context = jobContext;


            var collectNetEdgesJob = default(CollectNetEdges);
            collectNetEdgesJob.thEdgeGeo = SystemAPI.GetComponentTypeHandle<EdgeGeometry>();
            collectNetEdgesJob.thStartNodeGeometry = SystemAPI.GetComponentTypeHandle<StartNodeGeometry>();
            collectNetEdgesJob.thEndNodeGeometry = SystemAPI.GetComponentTypeHandle<EndNodeGeometry>();
            collectNetEdgesJob.thComposition = SystemAPI.GetComponentTypeHandle<Composition>();
            collectNetEdgesJob.thOwner = SystemAPI.GetComponentTypeHandle<Owner>();
            collectNetEdgesJob.luCompositionData = SystemAPI.GetComponentLookup<NetCompositionData>();
            collectNetEdgesJob.context = jobContext;
            collectNetEdgesJob.filterResults = bezierCurvesCache;
            collectNetEdgesJob.mask = BoundaryMask;
            // subnet mask is experimental (for performance issue)
            if (!UseExperimentalOptions && (collectNetEdgesJob.mask & BoundaryMask.SubNet) != 0)
            {
                collectNetEdgesJob.mask ^= BoundaryMask.SubNet;
            }


            var collectNetLanesJob = default(CollectNetLaneCurves);
            collectNetLanesJob.context = jobContext;
            collectNetLanesJob.curveList = bezierCurvesCache;
            collectNetLanesJob.DropLaneOwnedByRoad = DropOwnedLane;
            collectNetLanesJob.DropLaneOwnedByBuilding = DropOwnedLane;
            collectNetLanesJob.thCurve = SystemAPI.GetComponentTypeHandle<Curve>();
            collectNetLanesJob.thPrefabRef = SystemAPI.GetComponentTypeHandle<PrefabRef>();
            collectNetLanesJob.thOwner = SystemAPI.GetComponentTypeHandle<Owner>();
            collectNetLanesJob.luNetLaneGeoData = SystemAPI.GetComponentLookup<NetLaneGeometryData>();
            collectNetLanesJob.luSubLane = SystemAPI.GetBufferLookup<Game.Net.SubLane>();
            collectNetLanesJob.luRoad = SystemAPI.GetComponentLookup<Road>();
            collectNetLanesJob.luBuilding = SystemAPI.GetComponentLookup<Building>();


            var curve2LinesJob = default(Curve2Lines);
            curve2LinesJob.chopCount = 8;
            curve2LinesJob.curves = bezierCurvesCache;
            curve2LinesJob.context = jobContext;


            var area2LinesJob = default(Areas2Lines);
            area2LinesJob.bthNode = SystemAPI.GetBufferTypeHandle<Game.Areas.Node>();
            area2LinesJob.thArea = SystemAPI.GetComponentTypeHandle<Area>();
            area2LinesJob.checklines = checkLinesCache;
            area2LinesJob.context = jobContext;


            var genExtraPointsJob = default(GenIntersectedPoints);
            genExtraPointsJob.context = jobContext;


            var filterPointsJob = default(FilterPoints);
            filterPointsJob.overlayDist = 0.5f;
            filterPointsJob.context = jobContext;


            var generateRaysJob = default(GenerateRays);
            generateRaysJob.context = jobContext;


            var dropRaysJob = default(DropIntersectedRays);
            dropRaysJob.context = jobContext;


            var mergeRaysJob = default(MergeRays);
            mergeRaysJob.context = jobContext;
            mergeRaysJob.angleBound = 5 * Mathf.Deg2Rad;
            mergeRaysJob.minEdgeLength = MinEdgeLength;


            var rays2AreaJob = default(Rays2AreaDefinition);
            rays2AreaJob.context = jobContext;
            rays2AreaJob.prefab = m_PrefabSystem.GetEntity(_selectedPrefab);
            rays2AreaJob.terrianHeightData = _terrianSystem.GetHeightData();
            rays2AreaJob.gameRaycastPoint = raycastPoint;
            rays2AreaJob.commandBuffer = _toolOutputBarrier.CreateCommandBuffer();


            // run
            if ((BoundaryMask & BoundaryMask.Net) != 0) jobHandle = Schedule(() => collectNetEdgesJob.Schedule(edgeGeoEntityQuery, jobHandle), "collect edges");
            if ((BoundaryMask & BoundaryMask.NetLane) != 0) jobHandle = Schedule(() => collectNetLanesJob.Schedule(netLaneQuery, jobHandle), "collect net lanes");
            jobHandle = Schedule(() => curve2LinesJob.Schedule(jobHandle), "curves to lines");
            if ((BoundaryMask & BoundaryMask.Lot) != 0) jobHandle = Schedule(() => lot2LinesJob.Schedule(lotEntityQuery, jobHandle), "collect lots");
            if ((BoundaryMask & BoundaryMask.Area) != 0) jobHandle = Schedule(() => area2LinesJob.Schedule(areaEntityQuery, jobHandle), "area to lines");
            

            //var sortLinesJob = jobContext.lines.SortJob(new CenterAroundComparer { hitPos = jobContext.hitPos });
            //jobHandle = Schedule(() => sortLinesJob.Schedule(jobHandle), "sort lines");
            if (CheckOcclusion) // TODO: drop lines being obscured
            {
                var filterObscuredLinesJob = new DropObscuredLines { context = jobContext };
                jobHandle = Schedule(() => filterObscuredLinesJob.Schedule(jobHandle), "filter obscured lines");
            }

            jobHandle = Schedule(() => new Lines2Points { context = jobContext }.Schedule(jobHandle), "lines to points");
            // extra points is experimental for performance issue
            if (UseExperimentalOptions && ExtraPoints) jobHandle = Schedule(() => genExtraPointsJob.Schedule(jobHandle), "generate extra points");
            jobHandle = Schedule(() => filterPointsJob.Schedule(jobHandle), "filter points");
            jobHandle = Schedule(() => generateRaysJob.Schedule(jobHandle), "generate rays");
            if (CheckIntersection) jobHandle = Schedule(() => dropRaysJob.Schedule(jobHandle), "drop rays");
            jobHandle = Schedule(() => mergeRaysJob.Schedule(jobHandle), "merge rays");
            jobHandle = Schedule(() => rays2AreaJob.Schedule(jobHandle), "create definitions");
            
            jobHandle.Complete();

            // dispose

            if (frameCount == 0 && Log4Debug)
            {
                Mod.Logger.Info($"hit point: {raycastPoint.m_HitPosition.x} " +
                    $"{raycastPoint.m_HitPosition.y} " +
                    $"{raycastPoint.m_HitPosition.z}");
                Mod.Logger.Info($"generated rays count: {jobContext.rays.Length}");
                Mod.Logger.Info($"lines count {jobContext.lines.Length}");
                Mod.Logger.Info($"area lines count {checkLinesCache.Length}");
            }
            checkLinesCache.Dispose();
            bezierCurvesCache.Dispose();
            jobContext.Dispose();

            return jobHandle;
        }

        private void LogNetShape(Bezier4x3 curve)
        {
            Mod.Logger.Info($"net data: " +
                $"{curve.a.x} {curve.a.z}\n" +
                $"{curve.b.x} {curve.b.z}\n" +
                $"{curve.c.x} {curve.c.z}\n" +
                $"{curve.d.x} {curve.d.z}\n"
            );
        }

        private void LogRay(AreaBucketToolJobs.Ray ray)
        {
            Mod.Logger.Info($"ray: (ray {ray.radian}) {ray.vector.x} {ray.vector.y}");
        }

        private void LogAreaPrefab(AreaPrefab prefab)
        {
            var components = new HashSet<ComponentType>();
            prefab.GetArchetypeComponents(components);
            foreach (var comp in components)
            {
                Mod.Logger.Info($"componentType: {comp.GetManagedType().Name}");
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
            if (WatchJobTime)
            {
                timer.Stop();
                Log($"job {name} time cost(ms): {timer.ElapsedMilliseconds}");
            }
            return jobHandle;
        }
        
        private void Log(string msg)
        {
            if (frameCount == 0 && Log4Debug) Mod.Logger.Info(msg);
        }

        public void LogToolState(ILog logger, string headMsg)
        {
            var msg = $"{headMsg}\n" +
                $"\ttool ID: {toolID}\n" +
                $"\ttool enabled: {ToolEnabled}\n" +
                $"\ttool active: {Active}\n" +
                $"\tfill range: {FillRange}\n" +
                $"\tmax fill range: {MaxFillingRange}\n" +
                $"\tmin edge length: {MinEdgeLength}\n" +
                $"\tuse experimental: {UseExperimentalOptions}\n" +
                $"\tcheck occlusions: {CheckOcclusion}\n" +
                $"\textra points: {ExtraPoints}\n" +
                $"\tdebug: {ShowDebugOptions}\n" +
                $"\tlog for debug: {Log4Debug}\n" +
                $"\tcheck intersection: {CheckIntersection}\n" +
                $"\tjob immediate: {JobImmediate}\n" +
                $"\tprofile job time: {WatchJobTime}\n" +
                $"\tdrop lanes owned by road: {DropOwnedLane}\n";
            logger.Info(msg);
        }
    }

    public enum BoundaryMask
    {
        None = 0,
        Net = 1,
        Lot = 2,
        Area = 4,
        SubNet = 8,
        NetLane = 16,
    }

}

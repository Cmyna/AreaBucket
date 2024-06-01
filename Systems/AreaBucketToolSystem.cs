using AreaBucket.Systems.AreaBucketToolJobs;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using AreaBucket.Systems.DebugHelperJobs;
using Colossal;
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
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

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

        public bool CheckIntersection { get; set; } = true;

        public bool WatchJobTime { get; set; } = false;

        public bool DrawBoundaries { get; set; } = false;

        public bool DrawIntersections { get; set; } = false;

        public bool DrawGeneratedRays { get; set; } = false;

        public bool DrawFloodingCandidates { get; set; } = false;

        public bool MergeRays { get; set; } = true;

        public bool MergePoints { get; set; } = true;

        /// <summary>
        /// (magic number?) in practice, 0.01 (1cm) is a feasible setting
        /// if value too slow, will keeps rays that too closed and affect merge ray calculation 
        /// (I guess too closed rays will make some value like walking vector becomes 0, then breaks some calcs)
        /// value too high will affect intersections checking between rays and boundary lines ()
        /// </summary>
        public float MergePointDist { get; set; } = 0.01f;

        public float MergeRayAngleThreshold { get; set; } = 0.5f;

        public float StrictBreakMergeRayAngleThreshold { get; set; } = 30f;

        /// <summary>
        /// ray intersection tollerance distance seems can be slightly higher than 0 (here choose 1cm as default)
        /// zero will cause twicking, while higher value may cause false positive intersection pass
        /// </summary>
        public float2 RayTollerance { get; set; } = new float2 { x = 0.01f, y = 0.01f };


        private AudioManager _audioManager;

        private EntityQuery _soundQuery;

        private NativeList<ControlPoint> _controlPoints;

        private AreaPrefab _selectedPrefab;

        private TerrainSystem _terrianSystem;

        private ToolOutputBarrier _toolOutputBarrier;

        private ProxyAction _applyAction;

        private ProxyAction _secondaryApplyAction;

        private GizmosSystem _gizmosSystem;



        private int frameCount = 0;

        private System.Diagnostics.Stopwatch timer;

        protected override void OnCreate()
        {
            base.OnCreate();
            _audioManager = World.GetOrCreateSystemManaged<AudioManager>();
            _soundQuery = GetEntityQuery(ComponentType.ReadOnly<ToolUXSoundSettingsData>());

            m_ToolSystem.tools.Remove(this); // rollback added self in base.OnCreate 
            m_ToolSystem.tools.Insert(0, this); // applied before vanilla systems

            _terrianSystem = World.GetOrCreateSystemManaged<TerrainSystem>();
            _toolOutputBarrier = World.GetOrCreateSystemManaged<ToolOutputBarrier>();
            _gizmosSystem = World.GetOrCreateSystemManaged<GizmosSystem>();

            _controlPoints = new NativeList<ControlPoint>(Allocator.Persistent);

            _applyAction = InputManager.instance.FindAction("Tool", "Apply");
            _secondaryApplyAction = InputManager.instance.FindAction("Tool", "Secondary Apply");

            timer = new System.Diagnostics.Stopwatch();

            OnInitEntityQueries();
            CreateDebugPanel();
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
            
            var newHandle = ApplyBucket(inputDeps, raycastPoint);

            return newHandle;
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
            OnDisposeEntityQueries();
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
            var debugDrawJobHandle = jobHandle;

            GizmoBatcher gizmosBatcher = default;
            var terrainHeightData = _terrianSystem.GetHeightData();

            var debugJobActive = DrawIntersections || DrawBoundaries || DrawGeneratedRays;

            if (debugJobActive)
            {
                gizmosBatcher = _gizmosSystem.GetGizmosBatcher(out var gizmosSysDeps);
                debugDrawJobHandle = JobHandle.CombineDependencies(debugDrawJobHandle, gizmosSysDeps);
            }
            // prepare jobs data

            var jobContext = new CommonContext().Init(raycastPoint.m_HitPosition.xz, FillRange); // Area bucket jobs common context

            var debugContext = default(DebugContext).Init();

            var generatedAreaData = new GeneratedArea().Init();

            var genIntersectionPointsJob = default(GenIntersectedPoints);
            genIntersectionPointsJob.context = jobContext;

            // only drop points totally overlayed. higher range causes accidently intersection dropping
            // (points are merged and moved, which cause rays generated from those points becomes intersected)
            var mergePointsJob = default(MergePoints);
            mergePointsJob.overlayDist = MergePointDist;
            mergePointsJob.context = jobContext;


            var generateRaysJob = default(GenerateRays);
            generateRaysJob.context = jobContext;


            var dropRaysJob = default(DropIntersectedRays);
            dropRaysJob.rayTollerance = RayTollerance;
            dropRaysJob.context = jobContext;
            dropRaysJob.debugContext = debugContext;


            var mergeRaysJob = default(MergeRays);
            mergeRaysJob.context = jobContext;
            mergeRaysJob.strictBreakMergeAngleThreshold = StrictBreakMergeRayAngleThreshold * Mathf.Deg2Rad;
            mergeRaysJob.breakMergeAngleThreshold = MergeRayAngleThreshold * Mathf.Deg2Rad;
            mergeRaysJob.minEdgeLength = MinEdgeLength;


            var rays2AreaJob = default(Rays2AreaDefinition);
            rays2AreaJob.context = jobContext;
            rays2AreaJob.prefab = m_PrefabSystem.GetEntity(_selectedPrefab);
            rays2AreaJob.terrianHeightData = terrainHeightData;
            rays2AreaJob.gameRaycastPoint = raycastPoint;
            rays2AreaJob.commandBuffer = _toolOutputBarrier.CreateCommandBuffer();


            // run
            jobHandle = ScheduleDataCollection(jobHandle, jobContext);

            var curve2LinesJob = default(Curve2Lines);
            curve2LinesJob.chopCount = 8;
            curve2LinesJob.context = jobContext;
            jobHandle = Schedule(curve2LinesJob, jobHandle);

            if (DrawBoundaries)
            {
                var drawBoundariesJob = default(DrawBoundaries);
                drawBoundariesJob.context = jobContext;
                drawBoundariesJob.heightData = terrainHeightData;
                drawBoundariesJob.gizmoBatcher = gizmosBatcher;
                debugDrawJobHandle = JobHandle.CombineDependencies(debugDrawJobHandle, jobHandle);
                debugDrawJobHandle = Schedule(drawBoundariesJob, debugDrawJobHandle);
            }

            //var sortLinesJob = jobContext.lines.SortJob(new CenterAroundComparer { hitPos = jobContext.hitPos });
            //jobHandle = Schedule(() => sortLinesJob.Schedule(jobHandle), "sort lines");
            if (CheckOcclusion) 
            {
                var filterObscuredLinesJob = new DropObscuredLines { context = jobContext };
                jobHandle = Schedule(filterObscuredLinesJob, jobHandle);
            }

            jobHandle = Schedule(new Lines2Points { context = jobContext }, jobHandle);
            // extra points is experimental for performance issue
            if (UseExperimentalOptions && ExtraPoints) jobHandle = Schedule(genIntersectionPointsJob, jobHandle);
            if (MergePoints) jobHandle = Schedule(mergePointsJob, jobHandle);
            jobHandle = Schedule(generateRaysJob, jobHandle);

            if (CheckIntersection) jobHandle = Schedule(dropRaysJob, jobHandle);
            if (DrawIntersections)
            {
                debugDrawJobHandle = JobHandle.CombineDependencies(debugDrawJobHandle, jobHandle);

                debugDrawJobHandle = Schedule(new DrawLinesJob
                {
                    lines = debugContext.intersectedLines,
                    gizmoBatcher = gizmosBatcher,
                    heightData = terrainHeightData,
                    color = Color.blue
                }, debugDrawJobHandle);
                debugDrawJobHandle = Schedule(new DrawLinesJob
                {
                    lines = debugContext.intersectedRays,
                    gizmoBatcher = gizmosBatcher,
                    heightData = terrainHeightData,
                    color = Color.magenta
                }, debugDrawJobHandle);
            }

            jobHandle = Schedule(new Rays2Polylines
            {
                context = jobContext,
                generatedArea = generatedAreaData
            }, jobHandle);


            if (MergeRays) jobHandle = Schedule(mergeRaysJob, jobHandle);

            if (DrawGeneratedRays)
            {
                debugDrawJobHandle = JobHandle.CombineDependencies(debugDrawJobHandle, jobHandle);
                var raylines = new NativeList<Line2>(Allocator.TempJob);
                debugDrawJobHandle = Job.WithCode(() =>
                {
                    for (int i = 0; i < jobContext.rays.Length; i++)
                    {
                        var v = jobContext.rays[i].vector;
                        var line = new Line2 { a = jobContext.hitPos, b = jobContext.hitPos + v };
                        raylines.Add(line);
                    }
                }).Schedule(debugDrawJobHandle);
                debugDrawJobHandle = Schedule(new DrawLinesJob 
                { 
                    lines = raylines,
                    gizmoBatcher = gizmosBatcher,
                    heightData = terrainHeightData,
                    color = new Color(0.3f, 0.5f, 0.7f, 1)
                }, debugDrawJobHandle);
                debugDrawJobHandle.Complete();
                raylines.Dispose();
                //debugDrawJobHandle = Job.WithCode(() => raylines.Dispose() ).Schedule(debugDrawJobHandle);
            }




            jobHandle = Schedule(rays2AreaJob, jobHandle);

            if (debugJobActive)
            {
                _gizmosSystem.AddGizmosBatcherWriter(debugDrawJobHandle);
                debugDrawJobHandle.Complete();
                //Job.WithCode(() => debugContext.Dispose()).Schedule(debugDrawJobHandle);
            }

            
            jobHandle.Complete();

            UpdateOtherFieldView("Rays Count", jobContext.rays.Length);
            UpdateOtherFieldView("Boundary Curves Count", jobContext.curves.Length);
            UpdateOtherFieldView("Total Boundary Lines Count", jobContext.totalBoundaryLines.Length);
            UpdateOtherFieldView("Used Boundary Lines Count", jobContext.usedBoundaryLines.Length);


            // disposes
            jobContext.Dispose();
            debugContext.Dispose();

            return jobHandle;
        }


        private JobHandle ScheduleDataCollection(JobHandle inputDeps, CommonContext context)
        {
            var jobHandle = inputDeps;
            if (BoundaryMask.Match(BoundaryMask.Net))
            {
                var collectNetEdgesJob = default(CollectNetEdges);
                collectNetEdgesJob.thEdgeGeo = SystemAPI.GetComponentTypeHandle<EdgeGeometry>();
                collectNetEdgesJob.thStartNodeGeometry = SystemAPI.GetComponentTypeHandle<StartNodeGeometry>();
                collectNetEdgesJob.thEndNodeGeometry = SystemAPI.GetComponentTypeHandle<EndNodeGeometry>();
                collectNetEdgesJob.thComposition = SystemAPI.GetComponentTypeHandle<Composition>();
                collectNetEdgesJob.thOwner = SystemAPI.GetComponentTypeHandle<Owner>();
                collectNetEdgesJob.luCompositionData = SystemAPI.GetComponentLookup<NetCompositionData>();
                collectNetEdgesJob.context = context;
                collectNetEdgesJob.mask = BoundaryMask;
                // subnet mask is experimental (for performance issue)
                if (!UseExperimentalOptions && collectNetEdgesJob.mask.Match(BoundaryMask.SubNet)) collectNetEdgesJob.mask ^= BoundaryMask.SubNet;
                jobHandle = Schedule(collectNetEdgesJob, netEntityQuery, jobHandle);
            }
            if (BoundaryMask.Match(BoundaryMask.Area))
            {
                var collectAreaLinesJob = default(CollectAreaLines);
                collectAreaLinesJob.bthNode = SystemAPI.GetBufferTypeHandle<Game.Areas.Node>();
                collectAreaLinesJob.bthTriangle = SystemAPI.GetBufferTypeHandle<Triangle>();
                collectAreaLinesJob.thArea = SystemAPI.GetComponentTypeHandle<Area>();
                collectAreaLinesJob.context = context;
                jobHandle = Schedule(collectAreaLinesJob, areaEntityQuery, jobHandle);
            }
            if (BoundaryMask.Match(BoundaryMask.Lot))
            {
                var collectLotLines = default(CollectLotLines);
                collectLotLines.thPrefabRef = SystemAPI.GetComponentTypeHandle<PrefabRef>();
                collectLotLines.thTransform = SystemAPI.GetComponentTypeHandle<Game.Objects.Transform>();
                collectLotLines.thBuilding = SystemAPI.GetComponentTypeHandle<Building>();
                collectLotLines.luBuildingData = SystemAPI.GetComponentLookup<BuildingData>();
                collectLotLines.luObjectGeoData = SystemAPI.GetComponentLookup<ObjectGeometryData>();
                collectLotLines.context = context;
                jobHandle = Schedule(collectLotLines, lotEntityQuery, jobHandle);
            }
            if (BoundaryMask.Match(BoundaryMask.NetLane))
            {
                var collectNetLanesJob = default(CollectNetLaneCurves);
                collectNetLanesJob.context = context;
                collectNetLanesJob.thCurve = SystemAPI.GetComponentTypeHandle<Curve>();
                collectNetLanesJob.thPrefabRef = SystemAPI.GetComponentTypeHandle<PrefabRef>();
                collectNetLanesJob.thOwner = SystemAPI.GetComponentTypeHandle<Owner>();
                collectNetLanesJob.luNetLaneGeoData = SystemAPI.GetComponentLookup<NetLaneGeometryData>();
                collectNetLanesJob.luSubLane = SystemAPI.GetBufferLookup<Game.Net.SubLane>();
                collectNetLanesJob.luRoad = SystemAPI.GetComponentLookup<Road>();
                collectNetLanesJob.luBuilding = SystemAPI.GetComponentLookup<Building>();
                collectNetLanesJob.luEditorContainer = SystemAPI.GetComponentLookup<Game.Tools.EditorContainer>();
                jobHandle = Schedule(collectNetLanesJob, netLaneQuery, jobHandle);
            }

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


        private JobHandle Schedule<T>(T job, JobHandle handle) where T : struct, IJob
        {
            return Schedule(() => job.Schedule(handle), job.GetType().Name);
        }


        private JobHandle Schedule<T>(T job, EntityQuery query, JobHandle handle) where T : struct, IJobChunk
        {
            return Schedule(() => job.Schedule(query, handle), job.GetType().Name);
        }

        private JobHandle Schedule(Func<JobHandle> scheduleFunc, string name)
        {
            var missingDebugField = !jobTimeProfile.ContainsKey(name);
            jobTimeProfile[name] = 0;
            if (missingDebugField) AppendJobTimeProfileView(name);

            if (WatchJobTime)
            {
                timer.Reset();
                timer.Start();
            }
            var jobHandle = scheduleFunc();
            if (WatchJobTime)
            {
                jobHandle.Complete();
                timer.Stop();
                jobTimeProfile[name] = timer.ElapsedMilliseconds;
            }
            return jobHandle;
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
                $"\tcheck intersection: {CheckIntersection}\n" +
                $"\tprofile job time: {WatchJobTime}\n";
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

    public static class ToolHelper
    {
        public static bool Match(this BoundaryMask mask, BoundaryMask targetMask)
        {
            return (mask & targetMask) != 0;
        }
    }

}

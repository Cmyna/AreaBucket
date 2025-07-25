﻿using AreaBucket.Systems.AreaBucketToolJobs;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using AreaBucket.Utils.Job.Profiling;
using Colossal;
using Colossal.Logging;
using Colossal.Mathematics;
using Game.Areas;
using Game.Audio;
using Game.Common;
using Game.Input;
using Game.Prefabs;
using Game.Simulation;
using Game.Tools;
using System;
using System.Diagnostics;
using System.Security.Cryptography;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

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
        // public bool ToolEnabled { get; set; } = false;

        /// <summary>
        /// set the tool is active or not. if it is ToolEnabled and active, 
        /// it will start generating area data definitions on update
        /// 
        /// active state is controlled by user switch
        /// </summary>
        public bool Active { get => Mod.modActiveTool == ModActiveTool.AreaBucket; }

        /// <summary>
        /// bucket tool filling range
        /// </summary>
        public float FillRange { get; set; } = 50f;

        /// <summary>
        /// bucket tool max filling range
        /// </summary>
        public float MaxFillingRange => Mod.modSetting?.MaxFillingRange ?? 250f;

        /// <summary>
        /// to control the minimum generated polyline edges length
        /// </summary>
        public float MinEdgeLength => Mod.modSetting?.MinGeneratedLineLength ?? 1f;

        /// <summary>
        /// the boundary for area filling tool
        /// </summary>
        public BoundaryMask BoundaryMask { get; set; } = BoundaryMask.Area | BoundaryMask.Net | BoundaryMask.Lot | BoundaryMask.NetLane;


        public bool UseExperimentalOptions => Mod.modSetting?.UseExperientalOption ?? false;

        /// <summary>
        /// performance optimization setting, use occlsion buffer to filter exposed boundaries from ray start point
        /// </summary>
        public bool CheckOcclusion { get; set; } = true;

        public bool CheckBoundariesCrossing { get; set; } = false;

        public bool CheckIntersection { get; set; } = true;

        public bool WatchJobTime { get; set; } = false;

        public bool DrawBoundaries { get; set; } = false;

        public bool DrawIntersections { get; set; } = false;

        public bool DrawGeneratedRays { get; set; } = false;

        public int DrawRaysDepth { get; set; } = 0;

        public bool DrawFloodingCandidates { get; set; } = false;

        public bool MergeGenedPolylines { get; set; } = true;

        public bool MergePoints { get; set; } = true;

        /// <summary>
        /// (magic number?) in practice, 0.01 (1cm) is a feasible setting
        /// if value too slow, will keeps rays that too closed and affect merge ray calculation 
        /// (I guess too closed rays will make some value like walking vector becomes 0, then breaks some calcs)
        /// value too high will affect intersections checking between rays and boundary lines ()
        /// </summary>
        public float MergePointDist { get; set; } = 0.01f;

        /// <summary>
        /// 5 degrees threshold
        /// </summary>
        public float MergePolylinesAngleThreshold { get; set; } = 5f;

        public bool MergePointsUnderDist { get; set; } = true;

        public bool MergePointsUnderAngleThreshold { get; set; } = true;

        /// <summary>
        /// ray intersection tollerance distance seems can be slightly higher than 0 (here choose 1cm as default)
        /// zero will cause twicking, while higher value may cause false positive intersection pass.
        /// it is two magic numbers too
        /// </summary>
        public float2 RayTollerance { get; set; } = new float2 { x = 0.01f, y = 0.1f };


        /// <summary>
        /// restrict generated ray's radian should between CommonContext.floodRadRange
        /// </summary>
        public bool RayBetweenFloodRange { get; set; } = true;

        /// <summary>
        /// restrict the algorithm flooding depth in one loop
        /// </summary>
        public int RecursiveFloodingDepth { get; set; } = 1;

        /// <summary>
        /// restrict the flooding max times
        /// </summary>
        public int MaxFloodingTimes { get; set; } = 16;

        /// <summary>
        /// enable/disable recursive flooding
        /// </summary>
        public bool RecursiveFlooding { get; set; } = true;

        public bool PreviewSurface
        {
            get => Mod.modSetting?.PreviewSurface ?? false;
            set 
            {
                if (Mod.modSetting == null) return;
                Mod.modSetting.PreviewSurface = value;
            }
        }

        public float Curve2LineAngleLimit = 5f;

        private AudioManager _audioManager;

        private EntityQuery _soundQuery;

        private NativeList<ControlPoint> _controlPoints;

        private AreaPrefab _selectedPrefab;

        private TerrainSystem _terrianSystem;

        private ToolOutputBarrier _toolOutputBarrier;

        // private ProxyAction _applyAction;

#if DEBUG
        private ProxyAction _dumpAction;
#endif

        private GizmosSystem _gizmosSystem;

        private Game.Net.SearchSystem _netSearchSystem;

        private Game.Areas.SearchSystem _areaSearchSystem;

        private int frameCount = 0;

        private System.Diagnostics.Stopwatch timer;

        private int usedBoundariesCount = 0;

        private CollectAreaLines collectAreaLinesJob;

        private CollectNetEdges collectNetEdgesJob;

        private CollectLotLines collectLotLinesJob;

        private CollectNetLaneCurves collectNetLaneCurvesJob;

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
            _netSearchSystem = World.GetOrCreateSystemManaged<Game.Net.SearchSystem>();
            _areaSearchSystem = World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>();
            _controlPoints = new NativeList<ControlPoint>(Allocator.Persistent);

            // _applyAction = Mod.modSetting.GetAction(Mod.kModAreaToolApply);
            //BindingUtils.MimicBuiltinBinding(_applyAction, InputManager.kToolMap, "Apply", nameof(Mouse));
#if DEBUG
            _dumpAction = Mod.modSetting.GetAction("Dump"); 
#endif

            timer = new System.Diagnostics.Stopwatch();


            OnInitEntityQueries();
            CreateDebugPanel();


            collectAreaLinesJob.AssignHandle(ref base.CheckedStateRef);
            collectNetEdgesJob.AssignHandle(ref base.CheckedStateRef);
            collectNetLaneCurvesJob.AssignHandle(ref base.CheckedStateRef);
            collectLotLinesJob.AssignHandle(ref base.CheckedStateRef);

            LogToolState(Mod.Logger, "Initial Area Bucket Tool States: ");
        }

        protected override void OnStartRunning()
        {
            base.OnStartRunning();
#if DEBUG
            Mod.Logger.Info("AreaBucketToolSystem Start Running");
            _dumpAction.shouldBeEnabled = true;
#endif
            // _applyAction.shouldBeEnabled = true;

            applyMode = ApplyMode.Clear;
        }

        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {

            requireAreas = AreaTypeMask.None; // reset requireAreas state

            frameCount++;
            if (frameCount >= 10) frameCount = 0;
            // if not active, do nothing
            if (_selectedPrefab == null || !Mod.areaToolEnabled || !Active) return inputDeps;

            // update requireAreas mask based on selected prefab
            AreaGeometryData componentData = m_PrefabSystem.GetComponentData<AreaGeometryData>(_selectedPrefab);
            if (Mod.modSetting?.DrawAreaOverlay == true) requireAreas = AreaUtils.GetTypeMask(componentData.m_Type);

            if (!applyAction.enabled) applyAction.enabled = true;

            applyMode = ApplyMode.Clear;
            if (!GetRaycastResult(out var raycastPoint))
            {
                return inputDeps;
            }
            

            if (applyAction.WasPressedThisFrame())
            {
                _audioManager.PlayUISound(_soundQuery.GetSingleton<ToolUXSoundSettingsData>().m_PlacePropSound);
            }

            if (applyAction.WasPressedThisFrame() && !PreviewSurface)
            {
                applyMode = ApplyMode.Apply;
                return inputDeps;
            }

            ClearJobTimeProfiles();
            if (WatchJobTime &&　this.jobDebuger.profilers.IsCompleted())
            {
                // refresh debug UI and start next profiling if last time one-tick profiling has been completed 
                this.jobDebuger.Refresh();
                jobProfileSwitch = true;
            }

            Func<JobHandle, JobHandle> schedule = (deps) => 
            {
                return StartAlgorithm(inputDeps, raycastPoint);
            };
            schedule = WithProfiling(schedule, "totalTimeCost");
            var newHandle = schedule(inputDeps);
            
            if (WatchJobTime)
            {
                this.jobDebuger.UpdateProfilers();
                this.jobProfileSwitch = false;
            }
            UpdateOtherFieldView("QueueNum", this.jobDebuger.profilers.QueueNum);

            
            return newHandle;
        }


        protected override void OnStopRunning()
        {
            base.OnStopRunning();
#if DEBUG
            Mod.Logger.Info("AreaBucketToolSystem Stop Running");
            _dumpAction.shouldBeEnabled = false;
#endif
            applyAction.enabled = false;
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
            Mod.areaToolEnabled = CanEnable(prefab);
            if (Mod.areaToolEnabled) _selectedPrefab = prefab as AreaPrefab;
            return Mod.areaToolEnabled && Active;
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

        /// <summary>
        /// Collect BoundaryLines for filling algorithms
        /// </summary>
        /// <param name="inputDeps"></param>
        /// <param name="singletonData"></param>
        /// <returns></returns>
        private JobHandle ScheduleDataCollection(JobHandle inputDeps, SingletonData singletonData)
        {
            var profileJobTime = WatchJobTime;
            var jobHandle = inputDeps;
            Func<JobHandle, JobHandle> schedule;

            if (BoundaryMask.Match(BoundaryMask.Net))
            {
                var netSearchTree = _netSearchSystem.GetNetSearchTree(readOnly: true, out var netSearchDeps);
                jobHandle = JobHandle.CombineDependencies(jobHandle, netSearchDeps);

                // update net mask
                if (collectNetEdgesJob.mask.Match(BoundaryMask.SubNet)) collectNetEdgesJob.mask ^= BoundaryMask.SubNet;
                schedule = (deps) =>
                {
                    collectNetEdgesJob.InitContext(singletonData, BoundaryMask, netSearchTree);
                    collectNetEdgesJob.AssignHandle(ref base.CheckedStateRef);
                    return collectNetEdgesJob.Schedule(deps);
                };
                schedule = WithProfiling(schedule, nameof(collectNetEdgesJob));
                jobHandle = schedule(jobHandle);
                
            }
            if (BoundaryMask.Match(BoundaryMask.Area))
            {
                var areaSearchTree = _areaSearchSystem.GetSearchTree(readOnly: true, out var areaSearchDeps);
                jobHandle = JobHandle.CombineDependencies(jobHandle, areaSearchDeps);

                schedule = (deps) =>
                {
                    collectAreaLinesJob.InitContext(singletonData, areaSearchTree);
                    collectAreaLinesJob.UpdateHandle(ref base.CheckedStateRef);

                    // DEBUG
                    collectAreaLinesJob.collectedAreaCount = new NativeReference<int>(Allocator.TempJob);
                    collectAreaLinesJob.areaLineCount = new NativeReference<int>(Allocator.TempJob);

                    return collectAreaLinesJob.Schedule(deps);
                };
                schedule = WithProfiling(schedule, nameof(collectAreaLinesJob));
                jobHandle = schedule(jobHandle);

                jobHandle.Complete();
                UpdateOtherFieldView("Area Count: ", collectAreaLinesJob.collectedAreaCount.Value);
                UpdateOtherFieldView("Area Line Count: ", collectAreaLinesJob.areaLineCount.Value);
                collectAreaLinesJob.collectedAreaCount.Dispose(jobHandle);
                collectAreaLinesJob.areaLineCount.Dispose(jobHandle);
            }

            if (BoundaryMask.Match(BoundaryMask.Lot))
            {
                var lotLineQueue = new NativeQueue<Line2>(Allocator.TempJob);

                // collect lots geometries
                schedule = (deps) =>
                {
                    collectLotLinesJob.InitContext(singletonData, lotLineQueue).AssignHandle(ref base.CheckedStateRef);
                    collectLotLinesJob.collectedLotCount = new NativeReference<int>(Allocator.TempJob);
                    return collectLotLinesJob.ScheduleParallel(lotEntityQuery, deps);
                };
                schedule = WithProfiling(schedule, nameof(collectAreaLinesJob));
                jobHandle = schedule(jobHandle);

                // lots to segments
                schedule = (deps) =>
                {
                    return Job.WithCode(() =>
                    {
                        while (!lotLineQueue.IsEmpty()) singletonData.AddLine(lotLineQueue.Dequeue());
                    }).WithBurst().Schedule(jobHandle);
                };
                schedule = WithProfiling(schedule, "lot2SegmentsJob");
                jobHandle = schedule(jobHandle);

                // cleanup
                lotLineQueue.Dispose(jobHandle);
                jobHandle.Complete();
                UpdateOtherFieldView("Lot Count: ", collectLotLinesJob.collectedLotCount.Value);
                collectLotLinesJob.collectedLotCount.Dispose(jobHandle);
            }
            if (BoundaryMask.Match(BoundaryMask.NetLane))
            {
                var searchTree = _netSearchSystem.GetLaneSearchTree(true, out var searchDeps);
                jobHandle = JobHandle.CombineDependencies(jobHandle, searchDeps);

                schedule = (deps) => 
                {
                    collectNetLaneCurvesJob.Init(singletonData, searchTree).AssignHandle(ref base.CheckedStateRef);
                    return collectNetLaneCurvesJob.Schedule(deps);
                };
                schedule = WithProfiling(schedule, nameof(collectNetLaneCurvesJob));
                jobHandle = schedule(jobHandle);
            }

            jobHandle.Complete();
            UpdateOtherFieldView("Raw Boundary Line Count: ", singletonData.totalBoundaryLines.Length);

            schedule = (deps) =>
            {
                return default(Curve2Lines).Init(singletonData, Curve2LineAngleLimit).Schedule(deps);
            };
            schedule = WithProfiling(schedule, "Curve2Lines");
            jobHandle = schedule(jobHandle);

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



        private Func<JobHandle, JobHandle> WithProfiling(Func<JobHandle, JobHandle> scheduleFunc, string jobName)
        {
            return (JobHandle depends) =>
            {
                if (!WatchJobTime || !jobProfileSwitch) return scheduleFunc(depends);
                var profiler = new JobProfiler();
                profiler.BeginWith(depends);
                var jobHandle = scheduleFunc(depends);
                profiler.EndWith(jobHandle);
                this.jobDebuger.Post(jobName, profiler);
                return jobHandle;
            };
        }


        public void LogToolState(ILog logger, string headMsg)
        {
            var msg = $"{headMsg}\n" +
                $"\ttool ID: {toolID}\n" +
                $"\ttool enabled: {Mod.areaToolEnabled}\n" +
                $"\ttool active: {Active}\n" +
                $"\tfill range: {FillRange}\n" +
                $"\tmax fill range: {MaxFillingRange}\n" +
                $"\tmin edge length: {MinEdgeLength}\n" +
                $"\tuse experimental: {UseExperimentalOptions}\n" +
                $"\tcheck occlusions: {CheckOcclusion}\n" +
                $"\textra points: {CheckBoundariesCrossing}\n" +
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

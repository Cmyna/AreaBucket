﻿using AreaBucket.Systems;
using AreaBucket.Systems.AreaBucketToolJobs;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using AreaBucket.Systems.DebugHelperJobs;
using AreaBucket.Utils;
using Colossal;
using Colossal.Mathematics;
using Game.Net;
using Game.Tools;
using System;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;


namespace AreaBucket.Systems
{
    public partial class AreaBucketToolSystem : ToolBaseSystem
    {

        public JobHandle StartAlgorithm(JobHandle inputDeps, ControlPoint raycastPoint)
        {
            Func<JobHandle, JobHandle> schedule;

            var jobHandle = inputDeps;

            Stopwatch prepareStopwatch = null;
            if (WatchJobTime) prepareStopwatch = Stopwatch.StartNew();
            GizmoBatcher gizmosBatcher = default;
            var terrainHeightData = _terrianSystem.GetHeightData();

            //var debugJobActive = DrawIntersections || DrawBoundaries || DrawGeneratedRays;
            var debugJobActive = true;

            if (debugJobActive)
            {
                gizmosBatcher = _gizmosSystem.GetGizmosBatcher(out var gizmosSysDeps);
                jobHandle = JobHandle.CombineDependencies(jobHandle, gizmosSysDeps);
            }
            var debugContext = default(DebugContext).Init(gizmosBatcher);

            var generatedAreaData = new GeneratedArea().Init();

            var singletonData = new SingletonData().Init(raycastPoint.m_HitPosition.xz, FillRange, terrainHeightData);

            // collect singleton data
            jobHandle = ScheduleDataCollection(jobHandle, singletonData);
            jobHandle.Complete();

#if DEBUG
            if (_dumpAction.WasReleasedThisFrame())
            {
                Mod.Logger.Info("dump pressed");
                DumpUtils.WriteSegments(singletonData.totalBoundaryLines);
            }
#endif

            if (DrawBoundaries)
            {
                schedule = (deps) =>
                {
                    return new DrawLinesJob2
                    {
                        color = new Color(100, 100, 0),
                        heightData = singletonData.terrainHeightData,
                        gizmoBatcher = debugContext.gizmoBatcher,
                        lines = singletonData.totalBoundaryLines,
                        yOffset = -0.5f
                    }.Schedule(deps);
                };
                schedule(jobHandle);
            }

            if (WatchJobTime)
            {
                jobHandle.Complete();
                AddJobTime("preparing", prepareStopwatch.ElapsedMilliseconds);
            }

            var floodingDefsList = new NativeList<FloodingDefinition>(Allocator.TempJob);
            floodingDefsList.Add(new FloodingDefinition().Init(raycastPoint.m_HitPosition.xz, 1));

            int floodingCount = 0;

            while(floodingCount < MaxFloodingTimes && floodingDefsList.Length > 0)
            {
                var floodingDef = floodingDefsList[0];
                floodingDefsList.RemoveAt(0);
                jobHandle = Flooding(
                    jobHandle,
                    singletonData,
                    floodingDef,
                    generatedAreaData,
                    debugContext,
                    floodingDefsList
                );
                jobHandle.Complete();
                floodingCount++;
            }

            if (MergeGenedPolylines)
            {
                schedule = (deps) =>
                {
                    return new MergePolylines
                    {
                        generatedAreaData = generatedAreaData,
                        minEdgeLength = MinEdgeLength,
                        breakMergeAngleThreshold = MergePolylinesAngleThreshold * Mathf.Deg2Rad,
                        mergeUnderDistances = MergePointsUnderDist,
                        mergeUnderAngleThreshold = MergePointsUnderAngleThreshold,
                    }.Schedule(deps);
                };
                jobHandle = schedule(jobHandle);
            }

            schedule = (deps) =>
            {
                return new Polylines2AreaDefinition
                {
                    points = generatedAreaData.points,
                    prefab = m_PrefabSystem.GetEntity(_selectedPrefab),
                    previewSurface = PreviewSurface,
                    apply = applyAction.WasPressedThisFrame(),
                    ecb = _toolOutputBarrier.CreateCommandBuffer()
                }.Schedule(deps);
            };
            jobHandle = schedule(jobHandle);

            

            jobHandle.Complete();

            //UpdateOtherFieldView("Rays Count", floodingContext.rays.Length);
            UpdateOtherFieldView("Flooding Times", floodingCount);
            UpdateOtherFieldView("Boundary Curves Count", singletonData.curves.Length);
            UpdateOtherFieldView("Total Boundary Lines Count", singletonData.totalBoundaryLines.Length);
            UpdateOtherFieldView("Used Boundary Lines Count", usedBoundariesCount);
            usedBoundariesCount = 0;
            UpdateOtherFieldView("Generated Area Poly Lines Count", generatedAreaData.polyLines.Length);
            UpdateOtherFieldView("Gened Area Points Count", generatedAreaData.points.Length);
            //UpdateOtherFieldView("Exposed Gened Area Lines", exposedList.Length);

            jobHandle = floodingDefsList.Dispose(jobHandle);
            jobHandle = debugContext.Dispose(jobHandle);
            jobHandle = generatedAreaData.Dispose(jobHandle);
            jobHandle = singletonData.Dispose(jobHandle);

            return jobHandle;
        }

        public JobHandle Flooding(
            JobHandle inputDeps,
            SingletonData singletonData,
            FloodingDefinition floodingDefinition,
            GeneratedArea generatedAreaData,
            DebugContext debugContext,
            NativeList<FloodingDefinition> floodingDefinitions
        ) {
            Func<JobHandle, JobHandle> schedule;
            var jobHandle = inputDeps;

            var floodingContext = new CommonContext().Init(floodingDefinition); // Area bucket jobs common context

            schedule = (deps) =>
            {
                return new DropObscuredLines()
                    .Init(floodingContext, singletonData, generatedAreaData, CheckOcclusion)
                    .Schedule(deps);
            };
            schedule = WithProfiling(schedule, "DropObsuredLines");
            jobHandle = schedule(jobHandle);



            // here should ensure last job is complete, to get actual floodingContext.usedBoundaryLines.Length
            // if not do it, the length will be zero and memery leak will happen
            jobHandle.Complete();
            
            if (CheckOcclusion)
            {
                var projectedBoundaries = new NativeList<PolarSegment>(floodingContext.usedBoundaryLines.Length, Allocator.TempJob);
                schedule = (deps) =>
                {
                    return new PolarProjectionJob
                    {
                        polarCenter = floodingContext.floodingDefinition.rayStartPoint,
                        boudaries = floodingContext.usedBoundaryLines.AsParallelReader(),
                        projectedBoundaries = projectedBoundaries.AsParallelWriter(),
                    }.Schedule(floodingContext.usedBoundaryLines.Length, 64, deps);
                };
                schedule = WithProfiling(schedule, "PolarProjection");
                jobHandle = schedule(jobHandle);


                schedule = (deps) =>
                {
                    return new CheckOcclusionJob().Init(floodingContext, projectedBoundaries).Schedule(deps);
                };
                schedule = WithProfiling(schedule, "CheckOcclusionJob");
                jobHandle = schedule(jobHandle);
                jobHandle = projectedBoundaries.Dispose(jobHandle);
            }
            
            
            jobHandle.Complete();
            usedBoundariesCount += floodingContext.usedBoundaryLines.Length;


            if (DrawBoundaries)
            {
                schedule = (deps) =>
                {
                    return new DebugHelperJobs.DrawLinesJob2
                    {
                        color = UnityEngine.Color.red,
                        heightData = singletonData.terrainHeightData,
                        gizmoBatcher = debugContext.gizmoBatcher,
                        lines = floodingContext.usedBoundaryLines,
                        yOffset = -0.25f
                    }.Schedule(jobHandle);
                };
                jobHandle = schedule(jobHandle);
            }

            schedule = (deps) => new Lines2Points().Init(floodingContext, singletonData).Schedule(deps);
            schedule = WithProfiling(schedule, "Line2Points");
            jobHandle = schedule(jobHandle);

            if (UseExperimentalOptions && CheckBoundariesCrossing)
            {
                jobHandle.Complete();
                var linesNum = floodingContext.usedBoundaryLines.Length;
                NativeStream pointsStream;
                schedule = (deps) =>
                {
                    deps = NativeStream.ScheduleConstruct(
                        out pointsStream,
                        floodingContext.usedBoundaryLines,
                        jobHandle,
                        Allocator.TempJob
                    );
                    deps = new GenIntersectedPointsJobParallel
                    {
                        lines = floodingContext.usedBoundaryLines.AsArray().AsReadOnly(),
                        points = pointsStream.AsWriter()
                    }.Schedule(floodingContext.usedBoundaryLines.Length, 64, deps);

                    deps = Job.WithCode(() =>
                    {
                        floodingContext.points.AddRange(pointsStream.ToNativeArray<float2>(Allocator.Temp));
                    }).WithBurst().Schedule(deps);

                    pointsStream.Dispose(deps);

                    return deps;
                };
                schedule = WithProfiling(schedule, "GenIntersectedPoints");
                jobHandle = schedule(jobHandle);
                jobHandle.Complete();
            }

            if (MergePoints)
            {
                // only drop points totally overlayed. higher range causes accidently intersection dropping
                // (points are merged and moved, which cause rays generated from those points becomes intersected)
                schedule = (deps) =>
                {
                    return default(MergePoints)
                        .Init(floodingContext, singletonData, MergePointDist)
                        .Schedule(deps);
                };
                schedule = WithProfiling(schedule, "MergePoints");
                jobHandle = schedule(jobHandle);
            }

            schedule = (deps) =>
            {
                return default(GenerateRays)
                    .Init(floodingContext, singletonData, RayBetweenFloodRange)
                    .Schedule(deps);
            };
            schedule = WithProfiling(schedule, "GenerateRays");
            jobHandle = schedule(jobHandle);

            if (CheckIntersection)
            {
                schedule = (deps) =>
                {
                    return default(DropIntersectedRays)
                        .Init(floodingContext, debugContext, RayTollerance, singletonData)
                        .Schedule(deps);
                };
                schedule = WithProfiling(schedule, "DropIntersectedRays");
                jobHandle = schedule(jobHandle);
            }

            if (DrawGeneratedRays && (DrawRaysDepth == 0 || DrawRaysDepth == floodingDefinition.floodingDepth))
            {
                var raylines = new NativeList<Line2>(Allocator.TempJob);
                jobHandle = Job.WithCode(() =>
                {
                    for (int i = 0; i < floodingContext.rays.Length; i++)
                    {
                        var v = floodingContext.rays[i].vector;
                        var rayStartPoint = floodingContext.floodingDefinition.rayStartPoint;
                        var line = new Line2 { a = rayStartPoint, b = rayStartPoint + v };
                        raylines.Add(line);
                    }
                }).Schedule(jobHandle);
                schedule = (deps) =>
                {
                    return new DebugHelperJobs.DrawLinesJob
                    {
                        lines = raylines,
                        gizmoBatcher = debugContext.gizmoBatcher,
                        heightData = singletonData.terrainHeightData,
                        color = new UnityEngine.Color(0.3f, 0.5f, 0.7f, 1)
                    }.Schedule(deps);
                };
                jobHandle = schedule(jobHandle);
                jobHandle.Complete();
                jobHandle = raylines.Dispose(jobHandle);
            }

            if (DrawIntersections)
            {
                schedule = (deps) =>
                {
                    new DebugHelperJobs.DrawLinesJob
                    {
                        lines = debugContext.intersectedLines,
                        gizmoBatcher = debugContext.gizmoBatcher,
                        heightData = singletonData.terrainHeightData,
                        color = UnityEngine.Color.blue
                    }.Schedule(deps);
                    new DebugHelperJobs.DrawLinesJob
                    {
                        lines = debugContext.intersectedRays,
                        gizmoBatcher = debugContext.gizmoBatcher,
                        heightData = singletonData.terrainHeightData,
                        color = UnityEngine.Color.magenta
                    }.Schedule(deps);
                    return deps;
                };
                schedule(jobHandle);
            }

            var newPointsIndicesRange = new NativeReference<int2>(Allocator.TempJob);
            if (RecursiveFlooding || floodingContext.floodingDefinition.floodingDepth == 1)
            {
                var r2plJob = new Rays2Polylines().Init(
                    floodingContext, 
                    singletonData, 
                    generatedAreaData, 
                    floodingDefinitions, 
                    newPointsIndicesRange
                    );
                schedule = (deps) => r2plJob.Schedule(deps);
                schedule = WithProfiling(schedule, "Ray2Polylines");
                jobHandle = schedule(jobHandle);
            }

            // collect new flooding definitions
            
            if (UseExperimentalOptions && floodingDefinition.floodingDepth < RecursiveFloodingDepth)
            {
                schedule = (deps) =>
                {
                    return new CollectFloodingDefinitions().Init(
                        floodingContext,
                        generatedAreaData,
                        floodingDefinitions,
                        newPointsIndicesRange
                    ).Schedule(deps);
                };
                schedule = WithProfiling(schedule, "CollectFloodingDefinitions");
                jobHandle = schedule(jobHandle);
            }

            // filter flooding definitions (that has been obscured by new geneated polylines)
            var filterFloodingDefsDeps = jobHandle;
            schedule = (deps) =>
            {
                return Job.WithCode(() =>
                {
                    var usableFloodingDefs = new NativeList<FloodingDefinition>(Allocator.Temp);
                    for (int i = 0; i < floodingDefinitions.Length; i++)
                    {
                        var floodingDef = floodingDefinitions[i];
                        var line = floodingDef.floodingSourceLine;
                        var vector = line.b - line.a;
                        var middle = math.lerp(line.a, line.b, 0.5f); // choose middle point of exposed line

                        var exposed = true;
                        for (int j = 0; j < generatedAreaData.polyLines.Length; j++)
                        {
                            // if satisfy this condition, means it is flooding candidate source line
                            if (j == floodingDef.newAreaPointInsertStartIndex) continue;

                            var boundaryLine = generatedAreaData.polyLines[j];
                            if (FoundIntersection(line, vector, middle, boundaryLine))
                            {
                                exposed = false;
                                break;
                            }
                        }
                        if (exposed) usableFloodingDefs.Add(floodingDef);
                    }

                    floodingDefinitions.Clear();
                    floodingDefinitions.AddRange(usableFloodingDefs.AsArray());
                    usableFloodingDefs.Dispose();
                }).Schedule(filterFloodingDefsDeps);
            };
            schedule = WithProfiling(schedule, "filterFloodingDefs");
            jobHandle = schedule(jobHandle);

            jobHandle = newPointsIndicesRange.Dispose(jobHandle);

            if (DrawFloodingCandidates)
            {
                var exposedList = new NativeList<Line2>(Allocator.TempJob);
                jobHandle = Job.WithCode(() => {
                    for (int i = 0; i < floodingDefinitions.Length; i++) exposedList.Add(floodingDefinitions[i].floodingSourceLine);
                }).Schedule(jobHandle);

                jobHandle = new DebugHelperJobs.DrawLinesJob
                {
                    color = UnityEngine.Color.green,
                    heightData = singletonData.terrainHeightData,
                    gizmoBatcher = debugContext.gizmoBatcher,
                    lines = exposedList,
                    yOffset = 2f
                }.Schedule(jobHandle);
                jobHandle = exposedList.Dispose(jobHandle);
            }

           
            jobHandle = floodingContext.Dispose(jobHandle);

            return jobHandle;
        }

        private static bool FoundIntersection(Line2 line, float2 vector, float2 middle, Line2 boundaryLine)
        {
            if (!MathUtils.Intersect(UnamangedUtils.GetBounds(boundaryLine), UnamangedUtils.GetBounds(line))) return false;
            var pVector = Utils.Math.Perpendicular(vector, 0.5f);
            var p1 = middle + pVector;
            var p2 = middle - pVector;

            var line2 = new Line2(p1, p2);

            // check if line2 intersect with boundaryLine
            MathUtils.Intersect(line2, boundaryLine, out var t);
            return UnamangedUtils.Between(t.x, 0, 1) && UnamangedUtils.Between(t.y, 0, 1);
        }


    }


}
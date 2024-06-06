using AreaBucket.Systems.AreaBucketToolJobs;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using AreaBucket.Systems.DebugHelperJobs;
using Colossal;
using Colossal.Mathematics;
using Game.Simulation;
using Game.Tools;
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
            var jobHandle = inputDeps;

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

            if (DrawBoundaries)
            {
                /*var drawBoundariesJob = default(DrawBoundaries).Init(gizmosBatcher, singletonData);
                jobHandle = Schedule(drawBoundariesJob, jobHandle);*/
                jobHandle = Schedule(new DrawLinesJob
                {
                    color = new Color(100, 100, 0),
                    heightData = singletonData.terrainHeightData,
                    gizmoBatcher = debugContext.gizmoBatcher,
                    lines = singletonData.totalBoundaryLines,
                    yOffset = -0.5f
                }, jobHandle);
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
                jobHandle = Schedule(new MergePolylines
                {
                    generatedAreaData = generatedAreaData,
                    minEdgeLength = MinEdgeLength,
                    breakMergeAngleThreshold = MergeRayAngleThreshold * Mathf.Deg2Rad,
                    strictBreakMergeAngleThreshold = StrictBreakMergeRayAngleThreshold * Mathf.Deg2Rad
                }, jobHandle);
            }


            var polyLines2AreaDefsJob = new Polylines2AreaDefinition
            {
                generatedAreaData = generatedAreaData,
                prefab = m_PrefabSystem.GetEntity(_selectedPrefab),
                terrianHeightData = terrainHeightData,
                commandBuffer = _toolOutputBarrier.CreateCommandBuffer()
            };
            jobHandle = Schedule(polyLines2AreaDefsJob, jobHandle);

            

            jobHandle.Complete();

            //UpdateOtherFieldView("Rays Count", floodingContext.rays.Length);
            UpdateOtherFieldView("Flooding Times", floodingCount);
            UpdateOtherFieldView("Boundary Curves Count", singletonData.curves.Length);
            UpdateOtherFieldView("Total Boundary Lines Count", singletonData.totalBoundaryLines.Length);
            //UpdateOtherFieldView("Used Boundary Lines Count", floodingContext.usedBoundaryLines.Length);
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
            var jobHandle = inputDeps;

            var floodingContext = new CommonContext().Init(floodingDefinition); // Area bucket jobs common context

            var filterObscuredLinesJob = new DropObscuredLines().Init(floodingContext, singletonData, generatedAreaData, CheckOcclusion);
            jobHandle = Schedule(filterObscuredLinesJob, jobHandle);

            if (DrawBoundaries)
            {
                jobHandle = Schedule(new DrawLinesJob
                {
                    color = Color.red,
                    heightData = singletonData.terrainHeightData,
                    gizmoBatcher = debugContext.gizmoBatcher,
                    lines = floodingContext.usedBoundaryLines,
                    yOffset = -0.25f
                }, jobHandle);
            }

            jobHandle = Schedule(new Lines2Points().Init(floodingContext, singletonData), jobHandle);

            if (UseExperimentalOptions && ExtraPoints)
            {
                var genIntersectionPointsJob = default(GenIntersectedPoints).Init(floodingContext);
                jobHandle = Schedule(genIntersectionPointsJob, jobHandle);
            }

            if (MergePoints)
            {
                // only drop points totally overlayed. higher range causes accidently intersection dropping
                // (points are merged and moved, which cause rays generated from those points becomes intersected)
                var mergePointsJob = default(MergePoints).Init(floodingContext, singletonData, MergePointDist);
                jobHandle = Schedule(mergePointsJob, jobHandle);
            }

            var generateRaysJob = default(GenerateRays).Init(floodingContext, singletonData, RayBetweenFloodRange);
            jobHandle = Schedule(generateRaysJob, jobHandle);

            if (CheckIntersection)
            {
                var dropRaysJob = default(DropIntersectedRays).Init(floodingContext, debugContext, RayTollerance, singletonData);
                jobHandle = Schedule(dropRaysJob, jobHandle);
            }

            if (DrawGeneratedRays)
            {
                //debugDrawJobHandle = JobHandle.CombineDependencies(debugDrawJobHandle, jobHandle);
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
                jobHandle = Schedule(new DrawLinesJob
                {
                    lines = raylines,
                    gizmoBatcher = debugContext.gizmoBatcher,
                    heightData = singletonData.terrainHeightData,
                    color = new Color(0.3f, 0.5f, 0.7f, 1)
                }, jobHandle);
                jobHandle.Complete();
                jobHandle = raylines.Dispose(jobHandle);
            }

            if (DrawIntersections)
            {
                jobHandle = Schedule(new DrawLinesJob
                {
                    lines = debugContext.intersectedLines,
                    gizmoBatcher = debugContext.gizmoBatcher,
                    heightData = singletonData.terrainHeightData,
                    color = Color.blue
                }, jobHandle);
                jobHandle = Schedule(new DrawLinesJob
                {
                    lines = debugContext.intersectedRays,
                    gizmoBatcher = debugContext.gizmoBatcher,
                    heightData = singletonData.terrainHeightData,
                    color = Color.magenta
                }, jobHandle);
            }

            //var rfMaxSectorRadian = new NativeReference<float>(Allocator.TempJob);
            //var rfMaxSectorIndex = new NativeReference<int>(Allocator.TempJob);
            //var newPoints = new NativeList<float2>(Allocator.TempJob);
            var newPointsIndicesRange = new NativeReference<int2>(Allocator.TempJob);
            if (RecursiveFlooding || floodingContext.floodingDefinition.floodingDepth == 1)
            {


                // build points
                /*jobHandle = Job.WithCode(() => {
                    var rays = floodingContext.rays;
                    var rayStartPoint = floodingContext.floodingDefinition.rayStartPoint;
                    UnamangedUtils.FindLargestSector(rays, out var maxSectorRad, out var maxSectorIndex);
                    rfMaxSectorRadian.Value = maxSectorRad;
                    rfMaxSectorIndex.Value = maxSectorIndex;
                    bool addHitPoint = (maxSectorRad > Mathf.PI) && floodingContext.floodingDefinition.FloodingCirle();
                    if (addHitPoint) newPoints.Add(rayStartPoint);

                    var raysCount = rays.Length;
                    for (int i = maxSectorIndex; i < maxSectorIndex + raysCount; i++)
                    {
                        var ray = rays[i % raysCount];
                        var p = rayStartPoint + ray.vector;
                        newPoints.Add(p);
                    }
                }).Schedule(jobHandle);

                // insert points
                jobHandle = Job.WithCode(() => {
                    var cache = new NativeList<float2>(Allocator.Temp);
                    var insertIndex = floodingContext.floodingDefinition.newAreaPointInsertStartIndex;
                    var cache2 = new NativeList<float2>(Allocator.Temp);

                    if (insertIndex < generatedAreaData.points.Length)
                    {
                        for (int i = 0; i < insertIndex + 1; i++) cache2.Add(generatedAreaData.points[i]);
                    }
                    cache2.AddRange(newPoints.AsArray());
                    for (int i = insertIndex + 1; i < generatedAreaData.points.Length; i++) cache2.Add(generatedAreaData.points[i]);

                    generatedAreaData.points.Clear();
                    generatedAreaData.points.AddRange(cache2.AsArray());

                    cache.Dispose();
                    cache2.Dispose();
                }).Schedule(jobHandle);

                // update other flooding defs
                jobHandle = Job.WithCode(() => {
                    for (int i = 0; i < floodingDefinitions.Length; i++)
                    {
                        var otherDef = floodingDefinitions[i];
                        if (otherDef.newAreaPointInsertStartIndex >= floodingContext.floodingDefinition.newAreaPointInsertStartIndex)
                        {
                            otherDef.newAreaPointInsertStartIndex += newPoints.Length;
                            floodingDefinitions[i] = otherDef;
                        }
                    }
                }).Schedule(jobHandle);


                // build polylines
                jobHandle = Job.WithCode(() => {
                    UnamangedUtils.BuildPolylines(generatedAreaData.points, generatedAreaData.polyLines);
                }).Schedule(jobHandle);*/

                var r2plJob = new Rays2Polylines().Init(
                    floodingContext, 
                    singletonData, 
                    generatedAreaData, 
                    floodingDefinitions, 
                    newPointsIndicesRange
                    );
                jobHandle = Schedule(r2plJob, jobHandle);

                
            }
            //jobHandle = rfMaxSectorRadian.Dispose(jobHandle);
            //jobHandle = rfMaxSectorIndex.Dispose(jobHandle);
            //jobHandle = newPoints.Dispose(jobHandle);



            var exposedList = new NativeList<Line2>(Allocator.TempJob);
            if (floodingDefinition.floodingDepth < MaxRecursiveFloodingDepth)
            {
                jobHandle = Schedule(new FilterExposedPolylines().Init(
                    floodingContext,
                    generatedAreaData,
                    exposedList,
                    floodingDefinitions,
                    LineCollinearTollerance,
                    newPointsIndicesRange
                    ), jobHandle);
            }
            jobHandle = newPointsIndicesRange.Dispose(jobHandle);

            if (DrawFloodingCandidates)
            {
                jobHandle = JobHandle.CombineDependencies(jobHandle, jobHandle);
                jobHandle = Schedule(new DrawLinesJob
                {
                    color = Color.green,
                    heightData = singletonData.terrainHeightData,
                    gizmoBatcher = debugContext.gizmoBatcher,
                    lines = exposedList,
                    yOffset = 2f
                }, jobHandle);
            }

            jobHandle = exposedList.Dispose(jobHandle);
            jobHandle = floodingContext.Dispose(jobHandle);

            return jobHandle;
        }
    }
}
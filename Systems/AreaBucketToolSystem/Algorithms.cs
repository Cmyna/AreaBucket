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

            if (DrawGeneratedRays && (DrawRaysDepth == 0 || DrawRaysDepth == floodingDefinition.floodingDepth))
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
                jobHandle = Schedule(r2plJob, jobHandle);
            }

            // collect new flooding definitions
            var exposedList = new NativeList<Line2>(Allocator.TempJob);
            if (UseExperimentalOptions && floodingDefinition.floodingDepth < RecursiveFloodingDepth)
            {
                jobHandle = Schedule(new CollectFloodingDefinitions().Init(
                    floodingContext,
                    generatedAreaData,
                    exposedList,
                    floodingDefinitions,
                    newPointsIndicesRange
                    ), jobHandle);
            }

            // filter flooding definitions
            jobHandle = Job.WithCode(() =>
            {
                var usableFloodingDefs = new NativeList<FloodingDefinition>(Allocator.Temp);
                for (int i = 0; i < floodingDefinitions.Length; i++)
                {
                    var floodingDef = floodingDefinitions[i];
                    var line = floodingDef.floodingSourceLine;
                    var vector = line.b - line.a;
                    var middle = math.lerp(line.a, line.b, 0.5f);

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
            }).Schedule(jobHandle);

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
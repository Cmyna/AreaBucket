using AreaBucket.Systems.AreaBucketToolJobs;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Game.Tools;
using Unity.Collections;
using Unity.Jobs;


namespace AreaBucket.Systems
{
    public partial class AreaBucketToolSystem : ToolBaseSystem
    {

        public JobHandle Flooding(
            JobHandle inputDeps,
            SingletonData singletonData,
            FloodingDefinition floodingDefinition,
            GeneratedArea generatedAreaData,
            DebugContext debugContext
        ) {
            var jobHandle = inputDeps;

            var floodingContext = new CommonContext().Init(floodingDefinition); // Area bucket jobs common context

            if (CheckOcclusion)
            {
                var filterObscuredLinesJob = new DropObscuredLines().Init(floodingContext, singletonData);
                jobHandle = Schedule(filterObscuredLinesJob, jobHandle);
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

            var exposedList = new NativeList<Line2>(Allocator.TempJob);
            var floodingDefinitions = new NativeList<FloodingDefinition>(Allocator.TempJob);
            if (floodingDefinition.iteraction <= MaxFloodingIterations)
            {
                jobHandle = Schedule(new FilterExposedPolylines().Init(
                    floodingContext,
                    generatedAreaData,
                    exposedList,
                    floodingDefinitions,
                    LineCollinearTollerance
                    ), jobHandle);
            }

            throw new System.NotImplementedException();
        }
    }
}
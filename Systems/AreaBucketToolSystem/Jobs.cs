using AreaBucket.Systems.AreaBucketToolJobs;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace AreaBucket.Systems
{
    public partial class AreaBucketToolSystem : ToolBaseSystem
    {

        public CollectNetLaneCurves CollectNetLanesJob(CommonContext jobContext)
        {
            var collectNetLanesJob = default(CollectNetLaneCurves);
            collectNetLanesJob.context = jobContext;
            collectNetLanesJob.DropLaneOwnedByRoad = DropOwnedLane;
            collectNetLanesJob.DropLaneOwnedByBuilding = DropOwnedLane;
            collectNetLanesJob.thCurve = SystemAPI.GetComponentTypeHandle<Curve>();
            collectNetLanesJob.thPrefabRef = SystemAPI.GetComponentTypeHandle<PrefabRef>();
            collectNetLanesJob.thOwner = SystemAPI.GetComponentTypeHandle<Owner>();
            collectNetLanesJob.luNetLaneGeoData = SystemAPI.GetComponentLookup<NetLaneGeometryData>();
            collectNetLanesJob.luSubLane = SystemAPI.GetBufferLookup<Game.Net.SubLane>();
            collectNetLanesJob.luRoad = SystemAPI.GetComponentLookup<Road>();
            collectNetLanesJob.luBuilding = SystemAPI.GetComponentLookup<Building>();
            collectNetLanesJob.luEditorContainer = SystemAPI.GetComponentLookup<Game.Tools.EditorContainer>();
            return collectNetLanesJob;
        }
    }
}

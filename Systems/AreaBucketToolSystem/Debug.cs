using AreaBucket.Systems.AreaBucketToolJobs;
using Game.Debug;
using Game.Tools;
using System;
using System.Collections.Generic;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace AreaBucket.Systems
{
    public partial class AreaBucketToolSystem : ToolBaseSystem
    {

        /// <summary>
        /// dictionary stores jobs time cost
        /// </summary>
        private Dictionary<string, float> jobTimeProfile = new Dictionary<string, float>();


        /// <summary>
        /// Here we borrow Unity's HDRP debug panel (also the CO developer panel) to shows our debug options
        /// </summary>
        private void RefreshDevPanel()
        {
            var panel = DebugManager.instance.GetPanel("Area Bucket Tool", createIfNull: true, groupIndex: 0, overrideIfExist: true);
            List<DebugUI.Widget> list = new List<DebugUI.Widget>
            {
                new DebugUI.BoolField
                {
                    displayName = "Check Intersection",
                    getter = () => CheckIntersection,
                    setter = (v) => CheckIntersection = v,
                },
                new DebugUI.BoolField
                {
                    displayName = "Check Occlusion",
                    getter = () => CheckOcclusion,
                    setter = (v) => CheckOcclusion = v,
                },
                new DebugUI.BoolField
                {
                    displayName = "Draw Boundaries",
                    getter = () => DrawBoundaries,
                    setter = (v) => DrawBoundaries = v,
                },
                new DebugUI.BoolField
                {
                    displayName = "Draw Generated Rays",
                    getter = () => DrawGeneratedRays,
                    setter = (v) => DrawGeneratedRays = v,
                },
                new DebugUI.BoolField
                {
                    displayName = "Draw Intersections",
                    getter = () => DrawIntersections,
                    setter = (v) => DrawIntersections = v,
                },
                new DebugUI.BoolField
                {
                    displayName = "Merge Rays",
                    getter = () => MergeRays,
                    setter = (v) => MergeRays = v,
                },
                new DebugUI.Container(
                    "Ray Intersection Tollerance",
                    new ObservableList<DebugUI.Widget>
                    {
                        new DebugUI.FloatField
                        {
                            displayName = "Start",
                            getter = () => RayTollerance.x,
                            setter = (v) =>
                            {
                                v = math.clamp(v, 0, 5);
                                RayTollerance = new float2 { x = v, y = RayTollerance.y };
                            }
                        },
                        new DebugUI.FloatField
                        {
                            displayName = "End",
                            getter = () => RayTollerance.y,
                            setter = (v) =>
                            {
                                v = math.clamp(v, 0, 5);
                                RayTollerance = new float2 { x = RayTollerance.x, y = v };
                            }
                        }
                    }
                ),
                
            };

            var profileListContainer = new DebugUI.Container("Job Time Cost (ms)");
            RefreshProfiledJobTime(profileListContainer);
            list.Add(profileListContainer);
            panel.children.Clear();
            panel.children.Add(list);
        }

        private void RefreshProfiledJobTime(DebugUI.Container container)
        {
            List<DebugUI.Widget> list = new List<DebugUI.Widget>()
            {
                new DebugUI.BoolField
                {
                    displayName = "Profile Job Time",
                    getter = () => WatchJobTime,
                    setter = (v) => WatchJobTime = v
                },
            };

            foreach (var entry in jobTimeProfile)
            {
                var jobName = entry.Key;
                list.Add(new DebugUI.Value { displayName = jobName, getter = () => jobTimeProfile[jobName] });
            }
            container.children.Add(list);
        }

    }
}

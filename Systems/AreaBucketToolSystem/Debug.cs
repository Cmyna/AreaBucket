using AreaBucket.Systems.AreaBucketToolJobs;
using Game.Buildings;
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
        private readonly Dictionary<string, float> jobTimeProfile = new Dictionary<string, float>();

        private readonly Dictionary<string, string> otherProfile = new Dictionary<string, string>();

        private readonly DebugUI.Container jobTimeProfileContainer = new DebugUI.Container("Job Time Cost");

        private readonly DebugUI.Container otherProfileContainer = new DebugUI.Container("Others");

        /// <summary>
        /// Here we borrow Unity's HDRP debug panel (also the CO developer panel) to shows our debug options
        /// </summary>
        private void CreateDebugPanel()
        {
            jobTimeProfileContainer.children.Add(new DebugUI.BoolField
            {
                displayName = "Profile Job Time",
                getter = () => WatchJobTime,
                setter = (v) => WatchJobTime = v
            });
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
                    displayName = "Merge Rays",
                    getter = () => MergeRays,
                    setter = (v) => MergeRays = v,
                },
                
                new DebugUI.FloatField
                {
                    displayName = "Merge Rays Angle Threshold",
                    incStep = 0.5f,
                    getter = () => MergeRayAngleThreshold,
                    setter = (v) => MergeRayAngleThreshold = math.clamp(v, 0, 5),
                },
                new DebugUI.FloatField
                {
                    displayName = "Merge Rays Angle Threshold Strict",
                    incStep = 5f,
                    getter = () => StrictBreakMergeRayAngleThreshold,
                    setter = (v) => StrictBreakMergeRayAngleThreshold = math.clamp(v, 0, 90),
                },
                CreateVisualizeDebugUI(),
                CreateMergePointsDebugUI(),
                new DebugUI.Container(
                    "Ray Intersection Tollerance",
                    new ObservableList<DebugUI.Widget>
                    {
                        new DebugUI.FloatField
                        {
                            displayName = "Start",
                            incStep = 0.01f,
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
                            incStep = 0.01f,
                            getter = () => RayTollerance.y,
                            setter = (v) =>
                            {
                                v = math.clamp(v, 0, 5);
                                RayTollerance = new float2 { x = RayTollerance.x, y = v };
                            }
                        }
                    }
                ),

                jobTimeProfileContainer,
                otherProfileContainer,
                // OtherProfiles(),

            };
            panel.children.Clear();
            panel.children.Add(list);
        }

        


        private DebugUI.Container CreateMergePointsDebugUI()
        {
            return new DebugUI.Container("Merge Points", new ObservableList<DebugUI.Widget>
            {
                new DebugUI.BoolField { displayName = "enable", getter = () => MergePoints, setter = (v) => MergePoints = v },
                new DebugUI.BoolField
                {
                    displayName = "Merge Points Excatly Overlayed",
                    getter = () => MergePointDist <= 0.01f,
                    setter = (v) =>
                    {
                        if (v) MergePointDist = 0.01f;
                        else MergePointDist = 0.5f;
                    },
                },
            });
        }

        private DebugUI.Container CreateVisualizeDebugUI()
        {
            return new DebugUI.Container(
                "Visualize",
                new ObservableList<DebugUI.Widget>
                {
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
                        displayName = "Draw Flooding Candidate Rays",
                        getter = () => DrawFloodingCandidates,
                        setter = (v) => DrawFloodingCandidates = v,
                    },
                }
            );
        }


        private void RefreshOtherProfilesDebugUI()
        {
            List<DebugUI.Widget> list = new List<DebugUI.Widget>();
            foreach (var entry in otherProfile)
            {
                var key = entry.Key;
                list.Add(new DebugUI.Value 
                {
                    displayName = entry.Key,
                    getter = () =>
                    {
                        if (!otherProfile.ContainsKey(key)) return "null";
                        else return otherProfile[key];
                    }
                });
            }
            otherProfileContainer.children.Clear();
            otherProfileContainer.children.Add(list);
        }


        private void AppendJobTimeProfileView(string jobName)
        {
            jobTimeProfileContainer.children.Add(new DebugUI.Value
            {
                displayName = jobName,
                getter = () => jobTimeProfile[jobName]
            });
        }

        private void UpdateOtherFieldView<V>(string key, V value)
        {
            if (!otherProfile.ContainsKey(key))
            {
                otherProfileContainer.children.Add(new DebugUI.Value
                {
                    displayName = key,
                    getter = () => otherProfile[key]
                });
            }
            otherProfile[key] = value.ToString();
        }
    }
}

﻿using AreaBucket.Systems.AreaBucketToolJobs;
using AreaBucket.Utils.Job.Profiling;
using Game.Buildings;
using Game.Debug;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
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
        [Obsolete("Deprecated")]
        private readonly Dictionary<string, float> jobTimeProfile = new Dictionary<string, float>();

        private readonly Dictionary<string, string> otherProfile = new Dictionary<string, string>();

        [Obsolete("Deprecated")]
        private readonly DebugUI.Container jobTimeProfileContainer = new DebugUI.Container("Job Time Cost");

        private readonly DebugUI.Container otherProfileContainer = new DebugUI.Container("Others");

        private readonly JobDebuger jobDebuger = new JobDebuger("Job Time Cost2");

        /// <summary>
        /// a flag that control activating/deactivating one tick job time profiling
        /// enable at the beginning of loop (if allowed) and disable it at the end of loop
        /// </summary>
        private bool jobProfileSwitch = true;

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

            

            // var panel = DebugManager.instance.GetPanel("Area Bucket Tool", createIfNull: true, groupIndex: 0, overrideIfExist: true);
            ObservableList<DebugUI.Widget> list = new ObservableList<DebugUI.Widget>
            {
                new DebugUI.Value
                {
                    displayName = "IsApplyActionEnabled",
                    getter = () => applyAction?.enabled ?? false,
                },

                new DebugUI.BoolField
                {
                    displayName = nameof(PreviewSurface),
                    getter = () => PreviewSurface,
                    setter = (v) => PreviewSurface = v,
                },

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
                    displayName = "Check Boundaries Crossing",
                    getter = () => CheckBoundariesCrossing,
                    setter = (v) => CheckBoundariesCrossing = v,
                },

                new DebugUI.BoolField
                {
                    displayName = "Recursive Flooding",
                    getter = () => RecursiveFlooding,
                    setter = (v) => RecursiveFlooding = v
                },

                new DebugUI.BoolField
                {
                    displayName = "Ray Between Flood Range",
                    getter = () => RayBetweenFloodRange,
                    setter = (v) => RayBetweenFloodRange = v
                },

                new DebugUI.IntField
                {
                    displayName = "Max Recursive Flooding Depths",
                    incStep = 1,
                    getter = () => RecursiveFloodingDepth,
                    setter = (v) => RecursiveFloodingDepth = math.clamp(v, 1, 3)
                },

                new DebugUI.IntField
                {
                    displayName = "Max Flooding Times",
                    incStep = 1,
                    getter = () => MaxFloodingTimes,
                    setter = (v) => MaxFloodingTimes = math.clamp(v, 1, 32)
                },

                new DebugUI.FloatField
                {
                    displayName = nameof(Curve2LineAngleLimit),
                    incStep = 1f,
                    min = () => 0f, max = () => 30f,
                    getter = () => Curve2LineAngleLimit,
                    setter = (v) => Curve2LineAngleLimit = v,
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
                jobDebuger.uiContainer,
                otherProfileContainer,
                // OtherProfiles(),

            };
            var debugUIFoldout = new DebugUI.Foldout("Area Bucket Tool System", list);
            Mod.AreaBucketDebugUI.children.Add(debugUIFoldout);
        }

        private DebugUI.Container CreateMergePointsDebugUI()
        {
            return new DebugUI.Container("Merge Points", new ObservableList<DebugUI.Widget>
            {
                new DebugUI.BoolField { displayName = "Merge Point Before Generate Rays", getter = () => MergePoints, setter = (v) => MergePoints = v },
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

                new DebugUI.BoolField
                {
                    displayName = nameof(MergePointsUnderDist),
                    getter = () => MergePointsUnderDist,
                    setter = (v) => MergePointsUnderDist = v,
                },

                new DebugUI.BoolField
                {
                    displayName = nameof(MergePointsUnderAngleThreshold),
                    getter = () => MergePointsUnderAngleThreshold,
                    setter = (v) => MergePointsUnderAngleThreshold = v,
                },

                new DebugUI.BoolField
                {
                    displayName = "Merge Rays",
                    getter = () => MergeGenedPolylines,
                    setter = (v) => MergeGenedPolylines = v,
                },

                new DebugUI.FloatField
                {
                    displayName = "Merge Polylines Angle Threshold",
                    incStep = 5f,
                    getter = () => MergePolylinesAngleThreshold,
                    setter = (v) => MergePolylinesAngleThreshold = math.clamp(v, 0, 90),
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
                    new DebugUI.IntField
                    {
                        displayName = "Drawed Rays Depth",
                        incStep = 1,
                        getter = () => DrawRaysDepth,
                        setter = (v) => DrawRaysDepth = math.clamp(v, 0, RecursiveFloodingDepth + 1),
                    },
                    new DebugUI.BoolField
                    {
                        displayName = "Draw Intersections",
                        getter = () => DrawIntersections,
                        setter = (v) => DrawIntersections = v,
                    },
                    new DebugUI.BoolField
                    {
                        displayName = "Draw Flooding Candidate Lines",
                        getter = () => DrawFloodingCandidates,
                        setter = (v) => DrawFloodingCandidates = v,
                    },
                }
            );
        }


        private void AddJobTime(string jobName, float time)
        {
            if (!jobTimeProfile.ContainsKey(jobName))
            {
                AppendJobTimeProfileView(jobName);
                jobTimeProfile[jobName] = 0;
            }
            jobTimeProfile[jobName] += time;
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

        [Obsolete("Deprecated")]
        private void ClearJobTimeProfiles()
        {
            var keys = jobTimeProfile.Keys.ToArray();
            foreach (var key in keys) jobTimeProfile[key] = 0;
        }
        
    }
}

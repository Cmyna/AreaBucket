using Colossal.IO.AssetDatabase.Internal;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Unity.Jobs;
using UnityEngine.Rendering;

// idea from https://discussions.unity.com/t/solved-c-job-system-vs-managed-threaded-code/711575/4
namespace AreaBucket.Utils.Job.Profiling
{
    public struct WeightedValue
    {
        public double current;

        public double weight;

        public void Update(double newValue)
        {
            this.current = (newValue * this.weight + this.current) / (1 + this.weight);
        }
    }


    public class JobProfiler
    {

        private JobHandle watchDepends;

        private Stopwatch stopwatch;

        private double timePassed = -1;


        public JobProfiler BeginWith(JobHandle depend)
        {
            this.watchDepends = ManageJobHelper.WithCode(() =>
            {
                stopwatch = Stopwatch.StartNew();
            }).Schedule(depend);
            return this;
        }


        public JobProfiler EndWith(JobHandle depend)
        {
            var depends = JobHandle.CombineDependencies(depend, this.watchDepends);
            this.watchDepends = ManageJobHelper.WithCode(() =>
            {
                stopwatch.Stop();
                timePassed = stopwatch.Elapsed.TotalMilliseconds;
                // Mod.Logger.Info($"[test]time pass: {timePassed}ms");
            }).Schedule(depends);
            return this;
        }


        public bool IsCompleted()
        {
            return this.watchDepends.IsCompleted;
        }


        public void ForceComplete()
        {
            this.watchDepends.Complete();
        }


        public JobHandle Schedule(Func<JobHandle> callback)
        {
            var jobHandle = callback();
            EndWith(jobHandle);
            return jobHandle;
        }



        public double GetTimeMs()
        {
            ForceComplete();
            return timePassed;
        }

    }


    public class JobProfilers
    {
        private List<(string, JobProfiler)> waitedQueue;

        private Dictionary<string, WeightedValue> timeCostsMap;

        private double weight = 0.05;

        private WeightedValue avgWaitNum;

        public double QueueNum { get => avgWaitNum.current; }

        public JobProfilers(double weight = 0.05)
        {
            this.waitedQueue = new List<(string, JobProfiler)>();
            this.timeCostsMap = new Dictionary<string, WeightedValue>();
            this.weight = weight;
            this.avgWaitNum = default;
            this.avgWaitNum.weight = weight;
        }

        public void Post(string name, JobProfiler profiler)
        {
            this.waitedQueue.Add((name, profiler));
        }


        public void Update()
        {
            this.waitedQueue = this.waitedQueue.Where((value) =>
            {
                var (name, profiler) = value;
                if (!profiler.IsCompleted()) return true;
                WeightedValue time = default;
                time.weight = this.weight;
                if (this.timeCostsMap.ContainsKey(name))
                {
                    time = this.timeCostsMap[name];
                }
                time.Update(profiler.GetTimeMs());
                this.timeCostsMap[name] = time;
                return false;
            }).ToList();
            this.avgWaitNum.Update(this.waitedQueue.Count);
        }

        public double Get(string name)
        {
            if (!this.timeCostsMap.ContainsKey(name)) return -1;
            return this.timeCostsMap[name].current;
        }

        public string[] Keys()
        {
            return this.timeCostsMap.Keys.ToArray();
        }

        public List<DebugUI.Value> AsDebugUIList()
        {
            var keys = Keys();
            Array.Sort(keys);
            return keys.Select((key) =>
            {
                var uiValue = new DebugUI.Value
                {
                    displayName = key,
                    getter = () =>
                    {
                        if (this.timeCostsMap.ContainsKey(key))
                        {
                            return this.timeCostsMap[key].current.ToString("n2") + "ms";
                        }
                        else return -1;
                    }
                };
                return uiValue;
            }).ToList();
        }
    }


    public class JobDebuger
    {
        public JobProfilers profilers;

        public DebugUI.Container uiContainer;

        public JobDebuger(string name, double weight = 0.05)
        {
            this.profilers = new JobProfilers(weight);
            this.uiContainer = new DebugUI.Container();
            this.uiContainer.displayName = name;
        }

        /// <summary>
        /// refresh debug UI 
        /// time consuming (roughly up to 1ms each call?)
        /// </summary>
        public void Refresh()
        {
            this.uiContainer.children.Clear();
            var list = this.profilers.AsDebugUIList();
            foreach (var item in list)
            {
                this.uiContainer.children.Add(item);
            }
        }


        public void Post(string jobName, JobProfiler profiler)
        {
            this.profilers.Post(jobName, profiler);
        }


        public void UpdateProfilers()
        {
            this.profilers.Update();
        }
    }
}

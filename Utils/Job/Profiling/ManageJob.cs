using System;
using System.Runtime.InteropServices;
using Unity.Jobs;

// idea from https://discussions.unity.com/t/solved-c-job-system-vs-managed-threaded-code/711575/4
namespace AreaBucket.Utils.Job.Profiling
{
    public interface ITask
    {
        void Execute();

    }


    /// <summary>
    /// manage job that execute ITask object
    /// </summary>
    public struct ManageJob: IJob
    {

        public GCHandle _task;

        public void Execute()
        {
            ((ITask)_task.Target).Execute();
        }
        

    }


    public static class ManageJobHelper
    {
        private struct GCHandleDisposeJob : IJob
        {
            public GCHandle handle;

            public void Execute()
            {
                handle.Free();
            }
        }

        private class ActionTask : ITask
        {
            private Action action;

            public ActionTask(Action action)
            {
                this.action = action;
            }

            void ITask.Execute()
            {
                action();
            }
        }

        public static ITask WithCode(Action action)
        {
            return new ActionTask(action);
        }


        public static JobHandle Schedule(this ITask task, JobHandle depends)
        {
            var gcHandle = GCHandle.Alloc(task);
            var job = new ManageJob { _task = gcHandle };
            var jobHandle = job.Schedule(depends);
            new GCHandleDisposeJob { handle = gcHandle }.Schedule(jobHandle);
            return jobHandle;
        }
    }


}

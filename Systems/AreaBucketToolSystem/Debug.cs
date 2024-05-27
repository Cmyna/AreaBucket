using Game.Debug;
using Game.Tools;
using System.Collections.Generic;
using UnityEngine.Rendering;

namespace AreaBucket.Systems
{
    public partial class AreaBucketToolSystem : ToolBaseSystem
    {

        private Dictionary<string, float> jobTimeProfile = new Dictionary<string, float>();


        private void RefreshDevPanel()
        {
            var panel = DebugManager.instance.GetPanel("Area Bucket Tool", createIfNull: true, groupIndex: 0, overrideIfExist: true);
            List<DebugUI.Widget> list = new List<DebugUI.Widget>
            {
                new DebugUI.BoolField
                {
                    displayName = "Profile Job Time",
                    getter = () => WatchJobTime,
                    setter = (v) => WatchJobTime = v,
                },
            };

            var profileListContainer = new DebugUI.Container("job time cost (ms)");
            RefreshProfiledJobTime(profileListContainer);
            list.Add(profileListContainer);
            panel.children.Clear();
            panel.children.Add(list);
        }

        private void RefreshProfiledJobTime(DebugUI.Container container)
        {
            List<DebugUI.Widget> list = new List<DebugUI.Widget>();
            foreach (var entry in jobTimeProfile)
            {
                var jobName = entry.Key;
                list.Add(new DebugUI.Value { displayName = jobName, getter = () => jobTimeProfile[jobName] });
            }
            container.children.Add(list);
        }


    }
}

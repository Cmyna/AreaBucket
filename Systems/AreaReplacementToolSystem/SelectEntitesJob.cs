using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace AreaBucket.Systems.Jobs
{
    public struct SelectEntitiesJob : IJob
    {

        public NativeList<Entity> tobeSelectedEntites;

        public EntityCommandBuffer ecb;

        public void Execute()
        {
            for (int i = 0; i < tobeSelectedEntites.Length; i++)
            {
                Entity selectDefinition = ecb.CreateEntity();

                CreationDefinition creationDefinition = default;
                creationDefinition.m_Original = tobeSelectedEntites[i];
                creationDefinition.m_Flags = CreationFlags.Select;

                ecb.AddComponent(selectDefinition, creationDefinition);

                ecb.AddComponent(selectDefinition, default(Updated));
            }
        }
    }
}
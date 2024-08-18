// Substantial portions derived from decompiled game code. 
// Copyright (c) Colossal Order and Paradox Interactive.
// Not for reuse or distribution except in accordance with the Paradox Interactive End User License Agreement.

using Game.Common;
using Game.Prefabs;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using static Game.Tools.ObjectToolBaseSystem;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    /// <summary>
    /// it seems that find attachment is highly used for specialized-lot building creation
    /// </summary>
    [BurstCompile]
    public struct FindAttachmentBuildingJobCopy : IJob
    {
        [ReadOnly]
        public EntityTypeHandle m_EntityType;

        [ReadOnly]
        public ComponentTypeHandle<BuildingData> m_BuildingDataType;

        [ReadOnly]
        public ComponentTypeHandle<SpawnableBuildingData> m_SpawnableBuildingType;

        [ReadOnly]
        public BuildingData m_BuildingData;

        [ReadOnly]
        public RandomSeed m_RandomSeed;

        [ReadOnly]
        public NativeList<ArchetypeChunk> m_Chunks;

        public NativeReference<AttachmentData> m_AttachmentPrefab;

        public void Execute()
        {
            Random random = m_RandomSeed.GetRandom(2000000);
            int2 lotSize = m_BuildingData.m_LotSize;
            bool2 @bool = new bool2((m_BuildingData.m_Flags & BuildingFlags.LeftAccess) != 0, (m_BuildingData.m_Flags & BuildingFlags.RightAccess) != 0);
            AttachmentData value = default(AttachmentData);
            BuildingData buildingData = default(BuildingData);
            float num = 0f;
            for (int i = 0; i < m_Chunks.Length; i++)
            {
                ArchetypeChunk archetypeChunk = m_Chunks[i];
                NativeArray<Entity> nativeArray = archetypeChunk.GetNativeArray(m_EntityType);
                NativeArray<BuildingData> nativeArray2 = archetypeChunk.GetNativeArray(ref m_BuildingDataType);
                NativeArray<SpawnableBuildingData> nativeArray3 = archetypeChunk.GetNativeArray(ref m_SpawnableBuildingType);
                for (int j = 0; j < nativeArray3.Length; j++)
                {
                    if (nativeArray3[j].m_Level != 1)
                    {
                        continue;
                    }
                    BuildingData buildingData2 = nativeArray2[j];
                    int2 lotSize2 = buildingData2.m_LotSize;
                    bool2 bool2 = new bool2((buildingData2.m_Flags & BuildingFlags.LeftAccess) != 0, (buildingData2.m_Flags & BuildingFlags.RightAccess) != 0);
                    if (math.all(lotSize2 <= lotSize))
                    {
                        int2 @int = math.select(lotSize - lotSize2, 0, lotSize2 == lotSize - 1);
                        float num2 = (float)(lotSize2.x * lotSize2.y) * random.NextFloat(1f, 1.05f);
                        num2 += (float)(@int.x * lotSize2.y) * random.NextFloat(0.95f, 1f);
                        num2 += (float)(lotSize.x * @int.y) * random.NextFloat(0.55f, 0.6f);
                        num2 /= (float)(lotSize.x * lotSize.y);
                        num2 *= math.csum(math.select(0.01f, 0.5f, @bool == bool2));
                        if (num2 > num)
                        {
                            value.m_Entity = nativeArray[j];
                            buildingData = buildingData2;
                            num = num2;
                        }
                    }
                }
            }
            if (value.m_Entity != Entity.Null)
            {
                float z = (float)(m_BuildingData.m_LotSize.y - buildingData.m_LotSize.y) * 4f;
                value.m_Offset = new float3(0f, 0f, z);
            }
            m_AttachmentPrefab.Value = value;
        }
    }
}
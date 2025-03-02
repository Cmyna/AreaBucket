using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using AreaBucket.Utils;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Buildings;
using Game.Objects;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Collections.AllocatorManager;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    /// <summary>
    /// convert building lots to lines
    /// </summary>
    [BurstCompile]
    public struct CollectLotLines : IJobChunk, IHandleUpdatable
    {

        [ReadOnly] public ComponentTypeHandle<PrefabRef> thPrefabRef;

        [ReadOnly] public ComponentTypeHandle<Extension> thExtension;

        [ReadOnly] public ComponentTypeHandle<Building> thBuilding;

        [ReadOnly] public ComponentTypeHandle<Transform> thTransform;

        [ReadOnly] public ComponentLookup<BuildingData> luBuildingData;

        [ReadOnly] public ComponentLookup<BuildingExtensionData> luBuildingExtData;

        [ReadOnly] public ComponentLookup<ObjectGeometryData> luObjectGeoData;

        //public CommonContext context;

        //public SingletonData singletonData;

        private NativeQueue<Line2>.ParallelWriter lotLinesWriter;

        private float2 playerHitPos;

        private float fillingRange;

        public CollectLotLines InitContext(SingletonData singletonData, NativeQueue<Line2> lotLinesCollectorQueue)
        {
            //this.context = context;
            //this.singletonData = singletonData;
            playerHitPos = singletonData.playerHitPos;
            fillingRange = singletonData.fillingRange;
            lotLinesWriter = lotLinesCollectorQueue.AsParallelWriter();
            return this;
        }


        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            // any entity that has PrefabRef
            var prefabRefs = chunk.GetNativeArray(ref thPrefabRef);
            var transforms = chunk.GetNativeArray(ref thTransform);

            //var hasBuildingExtension = chunk.Has(ref thExtension);

            // get lot size from BuildingData/BuildingExtensionData
            

            var hasBuilding = chunk.Has(ref thBuilding);
            var hasBuildingExt = chunk.Has(ref thExtension);

            if (!hasBuilding && !hasBuildingExt) return;
            if (transforms.Length == 0) return;

            for (var i = 0; i < transforms.Length; i++)
            {
                var prefabEntity = prefabRefs[i].m_Prefab;
                var transform = transforms[i];

                if (hasBuilding) HandleBuildingData(prefabEntity, transform);
                //if (hasBuildingExt) HandleBuildingExtData(prefabEntity, transform);
            }

        }

        private void HandleBuildingData(Entity prefabEntity, Transform transform)
        {
            var buildingData = luBuildingData[prefabEntity];
            var lotSize = (float2)buildingData.m_LotSize * 8;
            var objectGeoData = luObjectGeoData[prefabEntity];
            if (IsSquareLot(objectGeoData)) CollectSquareLotData(lotSize, transform.m_Position, transform.m_Rotation);
        }


        public void InitHandles(ref SystemState state)
        {
            thPrefabRef.Update(ref state);
            thTransform.Update(ref state);
            luBuildingData.Update(ref state);
            luObjectGeoData.Update(ref state);
        }

        private bool IsSquareLot(ObjectGeometryData data)
        {
            var hasLot = (data.m_Flags & GeometryFlags.HasLot) != 0;
            var notCircular = (data.m_Flags & GeometryFlags.Circular) == 0;
            return hasLot && notCircular;
        }

        private void CollectSquareLotData(float2 lotShape, float3 pos, quaternion rotation)
        {
            var c = new float3(lotShape.x, 0, lotShape.y);

            // the testing shows that should put lot shape square center to (0, 0)
            float3 p1 = default; p1 -= c / 2;
            float3 p2 = default; p2.x += lotShape.x; p2 -= c / 2;
            float3 p3 = default; p3.x += lotShape.x; p3.z += lotShape.y; p3 -= c / 2;
            float3 p4 = default; p4.z += lotShape.y; p4 -= c / 2;

            p1 = math.rotate(rotation, p1);
            p2 = math.rotate(rotation, p2);
            p3 = math.rotate(rotation, p3);
            p4 = math.rotate(rotation, p4);

            p1 += pos;
            p2 += pos;
            p3 += pos;
            p4 += pos;


            var min = math.min(p1, p2); min = math.min(min, p3); min = math.min(min, p4);
            var max = math.max(p1, p2); max = math.max(max, p3); max = math.max(max, p4);
            var bounds = new Bounds2(min.xz, max.xz);

            var hitPos = playerHitPos;
            var filterRange = fillingRange;
            var dist = MathUtils.Distance(bounds, hitPos);
            if (dist > filterRange) return;

            /*context.points.Add(p1.xz);
            context.points.Add(p2.xz);
            context.points.Add(p3.xz);
            context.points.Add(p4.xz);*/

            var l1 = new Line2(p1.xz, p2.xz);
            var l2 = new Line2(p2.xz, p3.xz);
            var l3 = new Line2(p3.xz, p4.xz);
            var l4 = new Line2(p4.xz, p1.xz);


            lotLinesWriter.Enqueue(l1);
            lotLinesWriter.Enqueue(l2);
            lotLinesWriter.Enqueue(l3);
            lotLinesWriter.Enqueue(l4);
            /*singletonData.totalBoundaryLines.Add(l1); 
            singletonData.totalBoundaryLines.Add(l2);
            singletonData.totalBoundaryLines.Add(l3);
            singletonData.totalBoundaryLines.Add(l4);*/

        }

        public void AssignHandle(ref SystemState state)
        {
            this.thBuilding = state.GetComponentTypeHandle<Building>(isReadOnly: true);
            this.thExtension = state.GetComponentTypeHandle<Extension>(isReadOnly: true);
            this.thPrefabRef = state.GetComponentTypeHandle<PrefabRef>(isReadOnly: true);
            this.thTransform = state.GetComponentTypeHandle<Transform>(isReadOnly: true);
            this.luBuildingData = state.GetComponentLookup<BuildingData>(isReadOnly: true);
            this.luBuildingExtData = state.GetComponentLookup<BuildingExtensionData>(isReadOnly: true);
            this.luObjectGeoData = state.GetComponentLookup<ObjectGeometryData>(isReadOnly: true);
            
        }

        public void UpdateHandle(ref SystemState state)
        {
            this.thBuilding.Update(ref state);
            this.thExtension.Update(ref state);
            this.thPrefabRef.Update(ref state);
            this.thTransform.Update(ref state);
            this.luBuildingData.Update(ref state);
            this.luBuildingExtData.Update(ref state);
            this.luObjectGeoData.Update(ref state);
        }
    }
}

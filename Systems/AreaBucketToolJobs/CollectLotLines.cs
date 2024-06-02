using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using Colossal.Mathematics;
using Game.Buildings;
using Game.Objects;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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
    public struct CollectLotLines : IJobChunk
    {

        [ReadOnly] public ComponentTypeHandle<PrefabRef> thPrefabRef;

        [ReadOnly] public ComponentTypeHandle<Extension> thExtension;

        [ReadOnly] public ComponentTypeHandle<Building> thBuilding;

        [ReadOnly] public ComponentTypeHandle<Transform> thTransform;

        [ReadOnly] public ComponentLookup<BuildingData> luBuildingData;

        [ReadOnly] public ComponentLookup<BuildingExtensionData> luBuildingExtData;

        [ReadOnly] public ComponentLookup<ObjectGeometryData> luObjectGeoData;

        public CommonContext context;

        public SingletonData signletonData;

        public CollectLotLines InitContext(CommonContext context, SingletonData signletonData)
        {
            this.context = context;
            this.signletonData = signletonData;
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
                if (hasBuildingExt) HandleBuildingExtData(prefabEntity, transform);
            }

        }

        private void HandleBuildingData(Entity prefabEntity, Transform transform)
        {
            var buildingData = luBuildingData[prefabEntity];
            var lotSize = (float2)buildingData.m_LotSize * 8;
            var objectGeoData = luObjectGeoData[prefabEntity];
            if (IsSquareLot(objectGeoData)) CollectSquareLotData(lotSize, transform.m_Position, transform.m_Rotation);
        }

        private void HandleBuildingExtData(Entity prefabEntity, Transform transform)
        {
            // TODO

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

            var hitPos = signletonData.playerHitPos;
            var filterRange = signletonData.fillingRange;
            var dist = MathUtils.Distance(bounds, hitPos);
            if (dist > filterRange) return;

            context.points.Add(p1.xz);
            context.points.Add(p2.xz);
            context.points.Add(p3.xz);
            context.points.Add(p4.xz);

            var l1 = new Line2(p1.xz, p2.xz);
            var l2 = new Line2(p2.xz, p3.xz);
            var l3 = new Line2(p3.xz, p4.xz);
            var l4 = new Line2(p4.xz, p1.xz);

            signletonData.totalBoundaryLines.Add(l1); //context.totalBoundaryLines.Add(l1); 
            signletonData.totalBoundaryLines.Add(l2); //context.totalBoundaryLines.Add(l2); 
            signletonData.totalBoundaryLines.Add(l3); //context.totalBoundaryLines.Add(l3); 
            signletonData.totalBoundaryLines.Add(l4); //context.totalBoundaryLines.Add(l4); 

        }

    }
}

using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using AreaBucket.Utils;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using System;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{

    [BurstCompile]
    public struct CollectNetEdges: IJob, IHandleUpdatable
    {
        [ReadOnly] public ComponentLookup<EdgeGeometry> luEdgeGeo;

        [ReadOnly] public ComponentLookup<StartNodeGeometry> luStartNodeGeometry;

        [ReadOnly] public ComponentLookup<EndNodeGeometry> luEndNodeGeometry;

        [ReadOnly] public ComponentLookup<Composition> luComposition;

        [ReadOnly] public ComponentLookup<Owner> luOwner;

        [ReadOnly] public ComponentLookup<NetCompositionData> luCompositionData;

        public NativeQuadTree<Entity, QuadTreeBoundsXZ> netSearchTree;

        public BoundaryMask mask;

        public SingletonData signletonData;

        public CollectNetEdges InitContext(
            SingletonData signletonData, 
            BoundaryMask mask, 
            NativeQuadTree<Entity, QuadTreeBoundsXZ> netSearchTree
            )
        {
            this.mask = mask;
            this.signletonData = signletonData;
            this.netSearchTree = netSearchTree;
            return this;
        }

        public void Execute()
        {
            var candidateEntites = new NativeList<Entity>(Allocator.Temp);
            var iterator = new In2DHitRangeEntitesIterator<Entity>();
            iterator.items = candidateEntites;
            iterator.hitPos = signletonData.playerHitPos;
            iterator.range = signletonData.fillingRange;
            netSearchTree.Iterate(ref iterator);

            for (int i = 0; i < candidateEntites.Length; i++)
            {
                var entity = candidateEntites[i];
                var isSubnet = luOwner.HasComponent(entity);
                var useSubNetAsBounds = (mask & BoundaryMask.SubNet) != 0;
                if (isSubnet && !useSubNetAsBounds) continue;

                if (!luEdgeGeo.TryGetComponent(entity, out var geo)) continue;
                if (!luStartNodeGeometry.TryGetComponent(entity, out var startNodeGeo)) continue;
                if (!luEndNodeGeometry.TryGetComponent(entity, out var endNodeGeo)) continue;
                if (!luComposition.TryGetComponent(entity, out var composition)) continue;

                if (!IsBounds(luCompositionData[composition.m_Edge])) continue;

                var distance = MathUtils.Distance(geo.m_Bounds.xz, signletonData.playerHitPos);
                if (distance > signletonData.fillingRange) continue;

                signletonData.curves.Add(geo.m_Start.m_Left);
                signletonData.curves.Add(geo.m_Start.m_Right);

                signletonData.curves.Add(geo.m_End.m_Left);
                signletonData.curves.Add(geo.m_End.m_Right);

                TryAddNodeGeometry(startNodeGeo.m_Geometry);
                TryAddNodeGeometry(endNodeGeo.m_Geometry);
            }
        }

        /// <summary>
        /// Is net that can be boundaries for area filling
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private bool IsBounds(NetCompositionData data)
        {
            var hasSurface = (data.m_State & CompositionState.HasSurface) != 0;
            var checker = CompositionFlags.General.Elevated | 
                CompositionFlags.General.Tunnel;
            var flag = data.m_Flags.m_General;
            return (flag & checker) == 0 && hasSurface;
        }

        private void TryAddNodeGeometry(EdgeNodeGeometry node)
        {
            var isValid = IsValid(node);
            if (isValid) 
            {
                // add outside edges of node
                signletonData.curves.Add(node.m_Left.m_Left); signletonData.curves.Add(node.m_Left.m_Left);
                signletonData.curves.Add(node.m_Right.m_Right); signletonData.curves.Add(node.m_Right.m_Right);
            }

            // TODO: I am not sure this code should be used or not..
            // guess the inside edge used if the node is part of roundabout
            if (isValid && node.m_MiddleRadius > 0f)
            {
                signletonData.curves.Add(node.m_Left.m_Right); signletonData.curves.Add(node.m_Left.m_Right);
                signletonData.curves.Add(node.m_Right.m_Left); signletonData.curves.Add(node.m_Right.m_Left);
            }
        }

        /// <summary>
        /// Copied from NetDebugSystem.NetGizmosJob.IsValid
        /// Check a node geometry is valid or not
        /// </summary>
        /// <param name="nodeGeometry"></param>
        /// <returns></returns>
        private bool IsValid(EdgeNodeGeometry nodeGeometry)
        {
            float3 @float = nodeGeometry.m_Left.m_Left.d - nodeGeometry.m_Left.m_Left.a;
            float3 float2 = nodeGeometry.m_Left.m_Right.d - nodeGeometry.m_Left.m_Right.a;
            float3 float3 = nodeGeometry.m_Right.m_Left.d - nodeGeometry.m_Right.m_Left.a;
            float3 float4 = nodeGeometry.m_Right.m_Right.d - nodeGeometry.m_Right.m_Right.a;
            return math.lengthsq(@float + float2 + float3 + float4) > 1E-06f;
        }

        public void AssignHandle(ref SystemState state)
        {

            this.luComposition = state.GetComponentLookup<Composition>(isReadOnly: true);
            this.luCompositionData = state.GetComponentLookup<NetCompositionData>(isReadOnly: true);
            this.luEdgeGeo = state.GetComponentLookup<EdgeGeometry>(isReadOnly: true);
            this.luEndNodeGeometry = state.GetComponentLookup<EndNodeGeometry>(isReadOnly: true);
            this.luOwner = state.GetComponentLookup<Owner>(isReadOnly: true);
            this.luStartNodeGeometry = state.GetComponentLookup<StartNodeGeometry>(isReadOnly: true);
        }

        public void UpdateHandle(ref SystemState state)
        {
            this.luComposition.Update(ref state);
            this.luCompositionData.Update(ref state);
            this.luEdgeGeo.Update(ref state);
            this.luEndNodeGeometry.Update(ref state);
            this.luOwner.Update(ref state);
            this.luStartNodeGeometry.Update(ref state);
        }
    }
}

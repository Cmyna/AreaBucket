using Colossal;
using Colossal.Mathematics;
using Game;
using Game.Common;
using Game.Debug;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Scripting;

namespace AreaBucket.Systems
{
    public partial class NetDebugSystemCopy : BaseDebugSystem
    {
        [BurstCompile]
        private struct NetGizmoJob : IJobChunk
        {
            [ReadOnly]
            public bool m_NodeOption;

            [ReadOnly]
            public bool m_EdgeOption;

            [ReadOnly]
            public bool m_OutlineOption;

            [ReadOnly]
            public ComponentTypeHandle<Edge> m_EdgeType;

            [ReadOnly]
            public ComponentTypeHandle<Node> m_NodeType;

            [ReadOnly]
            public ComponentTypeHandle<Curve> m_CurveType;

            [ReadOnly]
            public ComponentTypeHandle<Temp> m_TempType;

            [ReadOnly]
            public ComponentTypeHandle<Composition> m_CompositionType;

            [ReadOnly]
            public ComponentTypeHandle<EdgeGeometry> m_EdgeGeometryType;

            [ReadOnly]
            public ComponentTypeHandle<StartNodeGeometry> m_StartGeometryType;

            [ReadOnly]
            public ComponentTypeHandle<EndNodeGeometry> m_EndGeometryType;

            [ReadOnly]
            public BufferTypeHandle<ConnectedNode> m_ConnectedNodeType;

            [ReadOnly]
            public ComponentLookup<Node> m_NodeData;

            [ReadOnly]
            public ComponentLookup<NetCompositionData> m_CompositionData;

            public GizmoBatcher m_GizmoBatcher;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                NativeArray<Edge> nativeArray = chunk.GetNativeArray(ref m_EdgeType);
                NativeArray<Node> nativeArray2 = chunk.GetNativeArray(ref m_NodeType);
                Color color;
                Color color2;
                Color color3;
                Color color4;
                Color blueColor = Color.blue;
                var redColor = Color.red;
                if (chunk.Has(ref m_TempType))
                {
                    color = Color.blue;
                    color2 = Color.blue;
                    color3 = Color.blue;
                    color4 = Color.blue;
                }
                else
                {

                    color = Color.cyan;
                    color2 = Color.white;
                    color3 = Color.yellow;
                    color4 = Color.green;
                }
                if (nativeArray.Length != 0)
                {
                    NativeArray<Curve> nativeArray3 = chunk.GetNativeArray(ref m_CurveType);
                    NativeArray<EdgeGeometry> nativeArray4 = chunk.GetNativeArray(ref m_EdgeGeometryType);
                    BufferAccessor<ConnectedNode> bufferAccessor = chunk.GetBufferAccessor(ref m_ConnectedNodeType);
                    if (m_EdgeOption)
                    {
                        if (nativeArray3.Length != 0)
                        {
                            for (int i = 0; i < nativeArray.Length; i++)
                            {
                                Edge edge = nativeArray[i];
                                Curve curve = nativeArray3[i];
                                DynamicBuffer<ConnectedNode> dynamicBuffer = bufferAccessor[i];
                                Node node = m_NodeData[edge.m_Start];
                                Node node2 = m_NodeData[edge.m_End];
                                m_GizmoBatcher.DrawCurve(curve, color);
                                if (m_NodeOption)
                                {
                                    if (math.lengthsq(curve.m_Bezier.a - node.m_Position) > 1E-06f)
                                    {
                                        m_GizmoBatcher.DrawLine(curve.m_Bezier.a, node.m_Position, color3);
                                        m_GizmoBatcher.DrawWireNode(curve.m_Bezier.a, 1f, color3);
                                    }
                                    if (math.lengthsq(curve.m_Bezier.d - node2.m_Position) > 1E-06f)
                                    {
                                        m_GizmoBatcher.DrawLine(curve.m_Bezier.d, node2.m_Position, color3);
                                        m_GizmoBatcher.DrawWireNode(curve.m_Bezier.d, 1f, color3);
                                    }
                                    for (int j = 0; j < dynamicBuffer.Length; j++)
                                    {
                                        ConnectedNode connectedNode = dynamicBuffer[j];
                                        Node node3 = m_NodeData[connectedNode.m_Node];
                                        float3 @float = MathUtils.Position(curve.m_Bezier, connectedNode.m_CurvePosition);
                                        m_GizmoBatcher.DrawLine(@float, node3.m_Position, color4);
                                        m_GizmoBatcher.DrawWireNode(@float, 1f, color4);
                                    }
                                }
                            }
                        }
                        else
                        {
                            for (int k = 0; k < nativeArray.Length; k++)
                            {
                                Edge edge2 = nativeArray[k];
                                Node node4 = m_NodeData[edge2.m_Start];
                                Node node5 = m_NodeData[edge2.m_End];
                                m_GizmoBatcher.DrawLine(node4.m_Position, node5.m_Position, color);
                            }
                        }
                    }
                    if (m_OutlineOption && nativeArray4.Length != 0)
                    {
                        NativeArray<Composition> nativeArray5 = chunk.GetNativeArray(ref m_CompositionType);
                        NativeArray<StartNodeGeometry> nativeArray6 = chunk.GetNativeArray(ref m_StartGeometryType);
                        NativeArray<EndNodeGeometry> nativeArray7 = chunk.GetNativeArray(ref m_EndGeometryType);
                        for (int l = 0; l < nativeArray4.Length; l++)
                        {
                            Composition composition = nativeArray5[l];
                            NetCompositionData netCompositionData = m_CompositionData[composition.m_Edge];
                            NetCompositionData netCompositionData2 = m_CompositionData[composition.m_StartNode];
                            NetCompositionData netCompositionData3 = m_CompositionData[composition.m_EndNode];
                            EdgeGeometry edgeGeometry = nativeArray4[l];
                            StartNodeGeometry startNodeGeometry = nativeArray6[l];
                            EndNodeGeometry endNodeGeometry = nativeArray7[l];
                            if (IsValid(startNodeGeometry.m_Geometry))
                            {
                                bool c = IsStartContinuous(edgeGeometry, startNodeGeometry.m_Geometry, netCompositionData, netCompositionData2);
                                DrawSegment(edgeGeometry.m_Start, netCompositionData, blueColor, color4, 1f, 1f);
                                if (startNodeGeometry.m_Geometry.m_MiddleRadius > 0f)
                                {
                                    DrawSegment(startNodeGeometry.m_Geometry.m_Left, netCompositionData2, color, redColor, 1f, 1f);
                                    DrawSegment(startNodeGeometry.m_Geometry.m_Right, netCompositionData2, color3, color2, 1f, 1f);
                                }
                                else
                                {
                                    DrawSegment(startNodeGeometry.m_Geometry.m_Left, netCompositionData2, color, redColor, 1f, 0.5f);
                                    DrawSegment(startNodeGeometry.m_Geometry.m_Right, netCompositionData2, color3, color2, 0.5f, 1f);
                                }
                            }
                            else
                            {
                                DrawSegment(edgeGeometry.m_Start, netCompositionData, blueColor, color4, 1f, 1f);
                            }
                            if (IsValid(endNodeGeometry.m_Geometry))
                            {
                                bool c2 = IsEndContinuous(edgeGeometry, endNodeGeometry.m_Geometry, netCompositionData, netCompositionData3);
                                DrawSegment(edgeGeometry.m_End, netCompositionData, blueColor, color4, 1f, 1f);
                                if (endNodeGeometry.m_Geometry.m_MiddleRadius > 0f)
                                {
                                    DrawSegment(endNodeGeometry.m_Geometry.m_Left, netCompositionData3, color, redColor, 1f, 1f);
                                    DrawSegment(endNodeGeometry.m_Geometry.m_Right, netCompositionData3, color3, color2, 1f, 1f);
                                }
                                else
                                {
                                    DrawSegment(endNodeGeometry.m_Geometry.m_Left, netCompositionData3, color, redColor, 1f, 0.5f);
                                    DrawSegment(endNodeGeometry.m_Geometry.m_Right, netCompositionData3, color3, color2, 0.5f, 1f);
                                }
                            }
                            else
                            {
                                DrawSegment(edgeGeometry.m_End, netCompositionData, blueColor, color4, 1f, 1f);
                            }
                        }
                    }
                }
                if (m_NodeOption && nativeArray2.Length != 0)
                {
                    for (int m = 0; m < nativeArray2.Length; m++)
                    {
                        Node node6 = nativeArray2[m];
                        m_GizmoBatcher.DrawWireNode(node6.m_Position, 2f, color);
                    }
                }
            }

            private bool IsValid(EdgeNodeGeometry nodeGeometry)
            {
                float3 @float = nodeGeometry.m_Left.m_Left.d - nodeGeometry.m_Left.m_Left.a;
                float3 float2 = nodeGeometry.m_Left.m_Right.d - nodeGeometry.m_Left.m_Right.a;
                float3 float3 = nodeGeometry.m_Right.m_Left.d - nodeGeometry.m_Right.m_Left.a;
                float3 float4 = nodeGeometry.m_Right.m_Right.d - nodeGeometry.m_Right.m_Right.a;
                return math.lengthsq(@float + float2 + float3 + float4) > 1E-06f;
            }

            private bool IsStartContinuous(EdgeGeometry edgeGeometry, EdgeNodeGeometry nodeGeometry, NetCompositionData edgeComposition, NetCompositionData nodeComposition)
            {
                float3 x;
                float3 x2;
                if (nodeGeometry.m_MiddleRadius > 0f)
                {
                    x = edgeGeometry.m_Start.m_Right.a - nodeGeometry.m_Left.m_Left.a;
                    x2 = edgeGeometry.m_Start.m_Left.a - nodeGeometry.m_Left.m_Right.a;
                }
                else
                {
                    x = edgeGeometry.m_Start.m_Right.a - nodeGeometry.m_Left.m_Left.a;
                    x2 = edgeGeometry.m_Start.m_Left.a - nodeGeometry.m_Right.m_Right.a;
                }
                if (math.lengthsq(x) <= 1E-06f && math.lengthsq(x2) <= 1E-06f && math.abs(edgeComposition.m_HeightRange.min - nodeComposition.m_HeightRange.min) <= 0.001f)
                {
                    return math.abs(edgeComposition.m_HeightRange.max - nodeComposition.m_HeightRange.max) <= 0.001f;
                }
                return false;
            }

            private bool IsEndContinuous(EdgeGeometry edgeGeometry, EdgeNodeGeometry nodeGeometry, NetCompositionData edgeComposition, NetCompositionData nodeComposition)
            {
                float3 x;
                float3 x2;
                if (nodeGeometry.m_MiddleRadius > 0f)
                {
                    x = edgeGeometry.m_End.m_Left.d - nodeGeometry.m_Left.m_Left.a;
                    x2 = edgeGeometry.m_End.m_Right.d - nodeGeometry.m_Left.m_Right.a;
                }
                else
                {
                    x = edgeGeometry.m_End.m_Left.d - nodeGeometry.m_Left.m_Left.a;
                    x2 = edgeGeometry.m_End.m_Right.d - nodeGeometry.m_Right.m_Right.a;
                }
                if (math.lengthsq(x) <= 1E-06f && math.lengthsq(x2) <= 1E-06f && math.abs(edgeComposition.m_HeightRange.min - nodeComposition.m_HeightRange.min) <= 0.001f)
                {
                    return math.abs(edgeComposition.m_HeightRange.max - nodeComposition.m_HeightRange.max) <= 0.001f;
                }
                return false;
            }

            private void DrawSegment(Segment segment, NetCompositionData composition, Color color, Color color2, float left, float right)
            {
                Bezier4x3 bezier = segment.m_Left + new float3(0f, composition.m_HeightRange.min, 0f);
                Bezier4x3 bezier2 = segment.m_Left + new float3(0f, composition.m_HeightRange.max, 0f);
                Bezier4x3 bezier3 = segment.m_Right + new float3(0f, composition.m_HeightRange.max, 0f);
                Bezier4x3 bezier4 = segment.m_Right + new float3(0f, composition.m_HeightRange.min, 0f);
                if (left != 0f)
                {
                    color = color * left;
                    m_GizmoBatcher.DrawCurve(bezier, segment.m_Length.x, color);
                    m_GizmoBatcher.DrawCurve(bezier2, segment.m_Length.x, color);
                }
                if (right != 0f)
                {
                    color2 = color2 * right;
                    m_GizmoBatcher.DrawCurve(bezier3, segment.m_Length.y, color2);
                    m_GizmoBatcher.DrawCurve(bezier4, segment.m_Length.y, color2);
                }
                /*if (start != 0f)
                {
                    Color color4 = color * start;
                    m_GizmoBatcher.DrawLine(bezier.a, bezier2.a, color4);
                    m_GizmoBatcher.DrawLine(bezier2.a, bezier3.a, color4);
                    m_GizmoBatcher.DrawLine(bezier3.a, bezier4.a, color4);
                    m_GizmoBatcher.DrawLine(bezier4.a, bezier.a, color4);
                }
                if (end != 0f)
                {
                    Color color5 = color * end;
                    m_GizmoBatcher.DrawLine(bezier.d, bezier2.d, color5);
                    m_GizmoBatcher.DrawLine(bezier2.d, bezier3.d, color5);
                    m_GizmoBatcher.DrawLine(bezier3.d, bezier4.d, color5);
                    m_GizmoBatcher.DrawLine(bezier4.d, bezier.d, color5);
                }*/
            }

            void IJobChunk.Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Execute(in chunk, unfilteredChunkIndex, useEnabledMask, in chunkEnabledMask);
            }
        }

        private struct TypeHandle
        {
            [ReadOnly]
            public ComponentTypeHandle<Edge> __Game_Net_Edge_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<Node> __Game_Net_Node_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<Curve> __Game_Net_Curve_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<Temp> __Game_Tools_Temp_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<Composition> __Game_Net_Composition_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<EdgeGeometry> __Game_Net_EdgeGeometry_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<StartNodeGeometry> __Game_Net_StartNodeGeometry_RO_ComponentTypeHandle;

            [ReadOnly]
            public ComponentTypeHandle<EndNodeGeometry> __Game_Net_EndNodeGeometry_RO_ComponentTypeHandle;

            [ReadOnly]
            public BufferTypeHandle<ConnectedNode> __Game_Net_ConnectedNode_RO_BufferTypeHandle;

            [ReadOnly]
            public ComponentLookup<Node> __Game_Net_Node_RO_ComponentLookup;

            [ReadOnly]
            public ComponentLookup<NetCompositionData> __Game_Prefabs_NetCompositionData_RO_ComponentLookup;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void __AssignHandles(ref SystemState state)
            {
                __Game_Net_Edge_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Edge>(isReadOnly: true);
                __Game_Net_Node_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Node>(isReadOnly: true);
                __Game_Net_Curve_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Curve>(isReadOnly: true);
                __Game_Tools_Temp_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Temp>(isReadOnly: true);
                __Game_Net_Composition_RO_ComponentTypeHandle = state.GetComponentTypeHandle<Composition>(isReadOnly: true);
                __Game_Net_EdgeGeometry_RO_ComponentTypeHandle = state.GetComponentTypeHandle<EdgeGeometry>(isReadOnly: true);
                __Game_Net_StartNodeGeometry_RO_ComponentTypeHandle = state.GetComponentTypeHandle<StartNodeGeometry>(isReadOnly: true);
                __Game_Net_EndNodeGeometry_RO_ComponentTypeHandle = state.GetComponentTypeHandle<EndNodeGeometry>(isReadOnly: true);
                __Game_Net_ConnectedNode_RO_BufferTypeHandle = state.GetBufferTypeHandle<ConnectedNode>(isReadOnly: true);
                __Game_Net_Node_RO_ComponentLookup = state.GetComponentLookup<Node>(isReadOnly: true);
                __Game_Prefabs_NetCompositionData_RO_ComponentLookup = state.GetComponentLookup<NetCompositionData>(isReadOnly: true);
            }
        }

        private EntityQuery m_NetGroup;

        private GizmosSystem m_GizmosSystem;

        private Option m_NodeOption;

        private Option m_EdgeOption;

        private Option m_OutlineOption;

        private TypeHandle __TypeHandle;

        [Preserve]
        protected override void OnCreate()
        {
            base.OnCreate();
            m_GizmosSystem = World.GetOrCreateSystemManaged<GizmosSystem>();
            m_NetGroup = GetEntityQuery(new EntityQueryDesc
            {
                Any = new ComponentType[2]
                {
                ComponentType.ReadOnly<Node>(),
                ComponentType.ReadOnly<Edge>()
                },
                None = new ComponentType[2]
                {
                ComponentType.ReadOnly<Deleted>(),
                ComponentType.ReadOnly<Hidden>()
                }
            });
            m_NodeOption = AddOption("Draw Nodes", defaultEnabled: true);
            m_EdgeOption = AddOption("Draw Edges", defaultEnabled: true);
            m_OutlineOption = AddOption("Draw Outlines", defaultEnabled: true);
            RequireForUpdate(m_NetGroup);
            Enabled = false;
        }

        [Preserve]
        protected override JobHandle OnUpdate(JobHandle inputDeps)
        {
            __TypeHandle.__Game_Prefabs_NetCompositionData_RO_ComponentLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Net_Node_RO_ComponentLookup.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Net_ConnectedNode_RO_BufferTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Net_EndNodeGeometry_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Net_StartNodeGeometry_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Net_EdgeGeometry_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Net_Composition_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Net_Curve_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Net_Node_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
            __TypeHandle.__Game_Net_Edge_RO_ComponentTypeHandle.Update(ref CheckedStateRef);
            NetGizmoJob jobData = default;
            jobData.m_NodeOption = false;
            jobData.m_EdgeOption = false;
            jobData.m_OutlineOption = true;
            jobData.m_EdgeType = __TypeHandle.__Game_Net_Edge_RO_ComponentTypeHandle;
            jobData.m_NodeType = __TypeHandle.__Game_Net_Node_RO_ComponentTypeHandle;
            jobData.m_CurveType = __TypeHandle.__Game_Net_Curve_RO_ComponentTypeHandle;
            jobData.m_TempType = __TypeHandle.__Game_Tools_Temp_RO_ComponentTypeHandle;
            jobData.m_CompositionType = __TypeHandle.__Game_Net_Composition_RO_ComponentTypeHandle;
            jobData.m_EdgeGeometryType = __TypeHandle.__Game_Net_EdgeGeometry_RO_ComponentTypeHandle;
            jobData.m_StartGeometryType = __TypeHandle.__Game_Net_StartNodeGeometry_RO_ComponentTypeHandle;
            jobData.m_EndGeometryType = __TypeHandle.__Game_Net_EndNodeGeometry_RO_ComponentTypeHandle;
            jobData.m_ConnectedNodeType = __TypeHandle.__Game_Net_ConnectedNode_RO_BufferTypeHandle;
            jobData.m_NodeData = __TypeHandle.__Game_Net_Node_RO_ComponentLookup;
            jobData.m_CompositionData = __TypeHandle.__Game_Prefabs_NetCompositionData_RO_ComponentLookup;
            jobData.m_GizmoBatcher = m_GizmosSystem.GetGizmosBatcher(out var dependencies);
            JobHandle jobHandle = jobData.ScheduleParallel(m_NetGroup, JobHandle.CombineDependencies(inputDeps, dependencies));
            m_GizmosSystem.AddGizmosBatcherWriter(jobHandle);
            return jobHandle;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void __AssignQueries(ref SystemState state)
        {
        }

        protected override void OnCreateForCompiler()
        {
            base.OnCreateForCompiler();
            __AssignQueries(ref CheckedStateRef);
            __TypeHandle.__AssignHandles(ref CheckedStateRef);
        }

        [Preserve]
        public NetDebugSystemCopy()
        {
        }
    }
}

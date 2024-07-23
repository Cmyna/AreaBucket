using Colossal.Collections;
using Colossal.Mathematics;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using System;
using Unity.Collections;
using Unity.Entities;

namespace AreaBucket.Utils
{
    /// <summary>
    /// Copied private struct from Game.Tools.SelectionToolSystem (with some modifications)
    /// </summary>
    public struct AreaIterator : 
        INativeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ>, 
        IUnsafeQuadTreeIterator<AreaSearchItem, QuadTreeBoundsXZ>
    {
        public Quad2 m_Quad;

        public AreaType m_AreaType;

        public ComponentLookup<PrefabRef> m_PrefabRefData;

        public ComponentLookup<AreaGeometryData> m_AreaGeometryData;

        public BufferLookup<Node> m_Nodes;

        public BufferLookup<Triangle> m_Triangles;

        public NativeList<Entity> m_Entities;

        /// <summary>
        /// check intersect by the bounds
        /// </summary>
        /// <param name="bounds"></param>
        /// <returns></returns>
        public bool Intersect(QuadTreeBoundsXZ bounds)
        {
            return MathUtils.Intersect(bounds.m_Bounds.xz, m_Quad);
        }

        public void Iterate(QuadTreeBoundsXZ bounds, AreaSearchItem areaItem)
        {
            if (!MathUtils.Intersect(bounds.m_Bounds.xz, m_Quad))
            {
                return;
            }
            PrefabRef prefabRef = m_PrefabRefData[areaItem.m_Area];
            if (m_AreaGeometryData[prefabRef.m_Prefab].m_Type == m_AreaType)
            {
                Triangle2 triangle = AreaUtils.GetTriangle2(m_Nodes[areaItem.m_Area], m_Triangles[areaItem.m_Area][areaItem.m_Triangle]);
                if (MathUtils.Intersect(m_Quad, triangle))
                {
                    m_Entities.Add(in areaItem.m_Area);
                }
            }
        }
    }
}

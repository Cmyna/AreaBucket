using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using static Game.Common.RaycastSystem;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace AreaBucket.Utils
{
    /// <summary>
    /// copy private struct from Game.Common.RaycastSystem.FindEntitiesFromTreeJob,
    /// it seems like a common entity search iterator in a quad tree
    /// </summary>
    public struct In2DHitRangeEntitesIterator<TItem> : 
        INativeQuadTreeIterator<TItem, QuadTreeBoundsXZ>, 
        IUnsafeQuadTreeIterator<TItem, QuadTreeBoundsXZ>
        where TItem: unmanaged, System.IEquatable<TItem>
    {

        public float2 hitPos;

        public float range;

        public NativeList<TItem> items;


        public bool Intersect(QuadTreeBoundsXZ bounds)
        {
            var distance = MathUtils.Distance(bounds.m_Bounds.xz, hitPos);
            return distance <= range;
        }

        public void Iterate(QuadTreeBoundsXZ bounds, TItem item)
        {
            var distance = MathUtils.Distance(bounds.m_Bounds.xz, hitPos);
            if (distance <= range)
            {
                items.Add(item);
            }
        }
    }

    
}

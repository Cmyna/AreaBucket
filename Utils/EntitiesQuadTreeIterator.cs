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
    public struct In2DHitRangeEntitesIterator : INativeQuadTreeIterator<Entity, QuadTreeBoundsXZ>, IUnsafeQuadTreeIterator<Entity, QuadTreeBoundsXZ>
    {

        public float2 hitPos;

        public float range;

        public NativeList<Entity> entites;


        public bool Intersect(QuadTreeBoundsXZ bounds)
        {
            var distance = MathUtils.Distance(bounds.m_Bounds.xz, hitPos);
            return distance <= range;
        }

        public void Iterate(QuadTreeBoundsXZ bounds, Entity entity)
        {
            var distance = MathUtils.Distance(bounds.m_Bounds.xz, hitPos);
            if (distance <= range)
            {
                entites.Add(entity);
            }
        }
    }
}

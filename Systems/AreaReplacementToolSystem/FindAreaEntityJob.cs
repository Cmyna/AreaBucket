using AreaBucket.Utils;
using Colossal.Collections;
using Game.Areas;
using Game.Common;
using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace AreaBucket.Jobs
{
    internal class FindAreaEntityJob : IJob
    {

        [ReadOnly] public NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> areaSearchTree;

        [ReadOnly] public Entity ToBeReplacedPrefabEntity;

        [ReadOnly] public AreaType areaType;

        [ReadOnly] public BufferLookup<Triangle> bluTriangles;

        [ReadOnly] public BufferLookup<Node> bluNodes;

        [ReadOnly] public ComponentLookup<AreaGeometryData> cluAreaGeoData;

        [ReadOnly] public ComponentLookup<PrefabRef> cluPrefabRef;

        public NativeReference<Entity> hitAreaEntity;

        public void Execute()
        {
            var hitEntitites = new NativeList<Entity>(Allocator.Temp);
            var iterator = default(AreaIterator);
            iterator.m_AreaType = areaType;
            iterator.m_Triangles = bluTriangles;
            iterator.m_Nodes = bluNodes;
            iterator.m_AreaGeometryData = cluAreaGeoData;
            iterator.m_PrefabRefData = cluPrefabRef;
            iterator.m_Entities = hitEntitites;

            areaSearchTree.Iterate(ref iterator);

            // TODO: instead of pick the one has smallest Entity id, select one that has highest render priority(so it is been rendered on the top)
            hitEntitites.Sort(default(SimpleEntityComparer));
            if (hitEntitites.Length > 0)
            {
                hitAreaEntity.Value = hitEntitites[0];
            }

            hitEntitites.Dispose();
        }
    }
}

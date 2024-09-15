using AreaBucket.Components;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using AreaBucket.Utils;
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Areas;
using Game.Common;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;


namespace AreaBucket.Systems.AreaBucketToolJobs
{
    [BurstCompile]
    public struct CollectAreaLines : IJob
    {

        [ReadOnly] public BufferLookup<Node> bluNode;

        [ReadOnly] public BufferLookup<Triangle> bluTriangle;

        [ReadOnly] public ComponentLookup<Area> cluArea;

        [ReadOnly] public ComponentLookup<MapTile> cluMapTile;

        [ReadOnly] public ComponentLookup<District> cluDistrict;

        [ReadOnly] public ComponentLookup<SurfacePreviewMarker> cluSurfacePreviewMarker;

        public SingletonData signletonData;

        private NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> areaSearchTree;

        public CollectAreaLines InitContext(SingletonData signletonData, NativeQuadTree<AreaSearchItem, QuadTreeBoundsXZ> areaSearchTree)
        {
            this.signletonData = signletonData;
            this.areaSearchTree = areaSearchTree;
            return this;
        }

        public void Execute()
        {
            var items = new NativeList<AreaSearchItem>(Allocator.Temp);
            var iterator = new In2DHitRangeEntitesIterator<AreaSearchItem>();
            iterator.items = items;
            iterator.hitPos = signletonData.playerHitPos;
            iterator.range = signletonData.fillingRange;
            areaSearchTree.Iterate(ref iterator);

            var entitySet = new NativeHashSet<Entity>(100, Allocator.Temp);

            for (int i = 0; i < items.Length; i++) entitySet.Add(items[i].m_Area);

            var entities = entitySet.ToNativeArray(Allocator.Temp);

            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];

                if (cluSurfacePreviewMarker.HasComponent(entity)) continue;

                if (!bluTriangle.TryGetBuffer(entity, out var triangles)) continue;
                if (!bluNode.TryGetBuffer(entity, out var nodes)) continue;
                if (!cluArea.TryGetComponent(entity, out var area)) continue;

                if (cluMapTile.HasComponent(entity)) continue;
                if (cluDistrict.HasComponent(entity)) continue;

                if ((area.m_Flags & AreaFlags.Complete) == 0) continue;

                // if no triangle, means it is an invalid area shape,
                // this kinds of entities is generated from area entity that players insert/drag point on one edge to another edge
                // the area entity just "invisible" because no more triangle, but still there
                if (triangles.Length == 0) continue;

                HandleBuffer(nodes);
            }
        }

        private void HandleBuffer(DynamicBuffer<Node> buffer)
        {
            for (var i = 0; i < buffer.Length - 1; i++)
            {
                HandleLine(buffer[i].m_Position.xz, buffer[i + 1].m_Position.xz);
            }
            HandleLine(buffer[buffer.Length - 1].m_Position.xz, buffer[0].m_Position.xz);
        }

        private void HandleLine(float2 p1, float2 p2)
        {
            if (math.distance(p1, p2) < 0.5f) return;
            var line = new Line2(p1, p2);
            if (!InRange(line)) return;
            signletonData.totalBoundaryLines.Add(line);
        }

        private bool InRange(Line2 line)
        {
            var hitPos = signletonData.playerHitPos;
            var dist1 = MathUtils.Distance(line, hitPos, out var t);
            var onSegment = t >= 0 && t <= 1;
            if (onSegment) return dist1 <= signletonData.fillingRange;
            else
            {
                var dist2 = math.distance(line.a, hitPos);
                var dist3 = math.distance(line.b, hitPos);
                return math.min(dist2, dist3) <= signletonData.fillingRange;
            }
        }

        private bool InRange2(Line2 line)
        {
            var hitPos = signletonData.playerHitPos;
            var dist1 = math.length(line.a - hitPos);
            var dist2 = math.length(line.b - hitPos);
            var minDist = math.min(dist1, dist2);
            return minDist <= signletonData.fillingRange;
        }
    }
}

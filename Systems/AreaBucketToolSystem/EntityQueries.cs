using Colossal.Reflection;
using Game.Areas;
using Game.Buildings;
using Game.Common;
using Game.Net;
using Game.Prefabs;
using Game.Tools;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;

namespace AreaBucket.Systems
{
    public partial class AreaBucketToolSystem : ToolBaseSystem
    {
        private EntityQuery netEntityQuery;

        private EntityQuery areaEntityQuery;

        private EntityQuery edgeGeoEntityQuery;

        private EntityQuery lotEntityQuery;

        private EntityQuery netLaneQuery;

        private void OnInitEntityQueries()
        {
            netEntityQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAll<Curve>()
                .WithAll<Edge>()
                .Build(EntityManager);
            areaEntityQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAll<Area>()
                .WithAll<Game.Areas.Node>()
                .WithNone<Updated>()
                .WithNone<Deleted>().WithNone<Temp>().WithNone<Overridden>()
                .WithNone<CreationDefinition>()
                .WithNone<MapTile>()
                .WithNone<District>() // exclude district polygons
                .Build(EntityManager);
            edgeGeoEntityQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAny<EdgeGeometry>()
                .WithAny<Game.Net.Node>()
                .WithNone<Hidden>().WithNone<Deleted>()
                .Build(EntityManager);
            lotEntityQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAll<PrefabRef>().WithAll<Game.Objects.Transform>()
                .WithAny<Building>().WithAny<Extension>()
                .WithNone<Deleted>().WithNone<Temp>().WithNone<Overridden>()
                .Build(EntityManager);

            netLaneQuery = new EntityQueryBuilder(Allocator.Persistent)
                .WithAll<PrefabRef>()
                .WithAll<Curve>()
                .WithNone<Deleted>().WithNone<Temp>().WithNone<Overridden>()
                .Build(EntityManager);
        }

        private void OnDisposeEntityQueries()
        {
            netEntityQuery.Dispose();
            areaEntityQuery.Dispose();
            edgeGeoEntityQuery.Dispose();
            lotEntityQuery.Dispose();
            netLaneQuery.Dispose();
        }


    }
}

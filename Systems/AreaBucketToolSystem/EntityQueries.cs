using AreaBucket.Components;
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

        private EntityQueryDesc CommonDroppedEntites;

        private void OnInitEntityQueries()
        {
            CommonDroppedEntites = new EntityQueryDesc 
            {
                None = new ComponentType[]
                {
                    ComponentType.ReadOnly<Deleted>(),
                    ComponentType.ReadOnly<Temp>(),
                    ComponentType.ReadOnly<Overridden>(),
                    ComponentType.ReadOnly<Hidden>(),
                }
            };

            netEntityQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Curve>(),
                    ComponentType.ReadOnly<Edge>()
                }
            });

            areaEntityQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<Area>(),
                    ComponentType.ReadOnly<Game.Areas.Node>()
                },
                None = CommonDroppedEntites.None.Concat(new ComponentType[]
                {
                    ComponentType.ReadOnly<Updated>(),
                    ComponentType.ReadOnly<MapTile>(),
                    ComponentType.ReadOnly<CreationDefinition>(),
                    ComponentType.ReadOnly<District>(),
                    ComponentType.ReadOnly<SurfacePreviewMarker>()
                }).ToArray()
            });

            edgeGeoEntityQuery = GetEntityQuery(new EntityQueryDesc 
            {
                Any = new ComponentType[]
                {
                    ComponentType.ReadOnly<Game.Net.Node>(),
                    ComponentType.ReadOnly<EdgeGeometry>(),
                },
                None = CommonDroppedEntites.None
            });

            lotEntityQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[]
                {
                     ComponentType.ReadOnly<PrefabRef>(),
                     ComponentType.ReadOnly<Game.Objects.Transform>(),
                },
                Any = new ComponentType[]
                {
                    ComponentType.ReadOnly<Building>(),
                    ComponentType.ReadOnly<Extension>(),
                },
                None = CommonDroppedEntites.None
            });

            netLaneQuery = GetEntityQuery(new EntityQueryDesc 
            {
                All = new ComponentType[]
                {
                    ComponentType.ReadOnly<PrefabRef>(),
                    ComponentType.ReadOnly<Curve>(),
                },
                None = CommonDroppedEntites.None
            });

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

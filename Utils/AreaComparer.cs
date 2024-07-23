using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace AreaBucket.Utils
{


    public struct SimpleEntityComparer : IComparer<Entity>
    {
        public int Compare(Entity x, Entity y)
        {
            return x.Index - y.Index;
        }
    }

    /// <summary>
    /// the struct that specifies render priority to each area entity
    /// </summary>
    public struct AreaEntityWithRenderPriority
    {
        public Entity areaEntity;
        public int renderPriority;
    }

    public struct SurfaceEntityComparer : IComparer<AreaEntityWithRenderPriority>
    {
        public int Compare(AreaEntityWithRenderPriority x, AreaEntityWithRenderPriority y)
        {
            return x.renderPriority - y.renderPriority;
        }
    }




}

using Game.Routes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems
{
    internal class Utils
    {
        public static float3[] TestSquareArea(float3 pos)
        {
            var size = 50;
            float3[] res = { 
                new float3() { x=pos.x + size, y=0, z=pos.z + size },
                new float3() { x=pos.x + size, y=0, z=pos.z - size },
                new float3() { x=pos.x - size, y=0, z=pos.z - size },
                new float3() { x=pos.x - size, y=0, z=pos.z + size },
            };
            return res;
        }

        public static float2[] TestSquareArea2()
        {
            var size = 50;
            float2[] res =
            {
                new float2() { x = size, y = size },
                new float2() { x = size, y = -size },
                new float2() { x = -size, y = -size },
                new float2() { x = -size, y = size },
            };
            return res;
        }
    }

}

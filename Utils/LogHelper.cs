using Colossal.Mathematics;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Mathematics;

namespace AreaBucket.Utils
{
    public static class LogHelper
    {
        public static string ToStringEx(this Line2 line)
        {
            return $"[{line.a.ToStringEx()}, {line.b.ToStringEx()}]";
        }

        public static string ToStringEx(this float2 v)
        {
            return $"({v.x} {v.y})";
        }
    }
}

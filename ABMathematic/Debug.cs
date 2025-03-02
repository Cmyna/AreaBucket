using ABMathematics.LineSweeping;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaBucket.Mathematics
{
    public static class Debug
    {
        public static IntersectionJob.Debuger intersectionJobDebuger;

        public static IntersectionJob.Debuger AttachDebuger(IntersectionJob job)
        {
            intersectionJobDebuger = new IntersectionJob.Debuger(job);
            return intersectionJobDebuger;
        }
    }
}

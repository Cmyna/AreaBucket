using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace AreaBucket.Components
{
    /// <summary>
    /// component to specify area entity to be hidden
    /// </summary>
    public struct AreaHiddenDefinition : IComponentData
    {
        public Entity target;
    }

    /// <summary>
    /// mark an exist area entity is hidden
    /// </summary>
    public struct AreaHiddenMarker : IComponentData
    {
    }
}

﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Entities;

namespace AreaBucket.Components
{
    public struct SurfacePreviewDefinition: IComponentData
    {
        public int key;

        public Entity prefab;

        public bool applyPreview;
    }
}

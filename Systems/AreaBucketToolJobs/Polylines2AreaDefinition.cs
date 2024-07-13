using AreaBucket.Components;
using AreaBucket.Systems.AreaBucketToolJobs.JobData;
using AreaBucket.Utils;
using Game.Areas;
using Game.Common;
using Game.Simulation;
using Game.Tools;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace AreaBucket.Systems.AreaBucketToolJobs
{
    public struct Polylines2AreaDefinition : IJob
    {

        public NativeList<float2> points;

        [ReadOnly] public Entity prefab;

        public EntityCommandBuffer ecb;

        public bool previewSurface;

        public bool apply;
        public void Execute()
        {
            if (points.Length <= 0) return;

            var defEntity = ecb.CreateEntity();


            AreaDefinitionCreation.AsDynmaicBufferNodes(ecb, defEntity, points, true);

            if (previewSurface)
            {
                var previewDefinition = new SurfacePreviewDefinition
                {
                    prefab = prefab,
                    applyPreview = apply,
                };
                ecb.AddComponent(defEntity, previewDefinition);
                ecb.AddComponent(defEntity, default(Updated));
            }
            else
            {
                AreaDefinitionCreation.WithCreationDefinition(ecb, defEntity, prefab);
            }

            
        }
    }
}

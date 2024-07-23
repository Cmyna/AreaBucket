using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Unity.Collections;

namespace AreaBucket.Utils
{
    public static class AreaPrefabsHelper
    {
        /// <summary>
        /// wraps all prefabs to native data
        /// </summary>
        /// <param name="prefabBases"></param>
        /// <param name="nativeRenderedAreas"></param>
        public static void WrapRenderedArea2Native(List<PrefabBase> prefabBases, NativeList<NativeRenderedArea> nativeRenderedAreas)
        {
            nativeRenderedAreas.Clear();
            foreach (var prefabBase in prefabBases)
            {
                var hasComponent = prefabBase.TryGet<RenderedArea>(out var renderedArea);
                nativeRenderedAreas.Add(new NativeRenderedArea
                {
                    hasComponent = hasComponent,
                    renderPriority = renderedArea.m_RendererPriority
                });
            }
        }
    }

    /// <summary>
    /// the prefab data has m_Index, which indicates the index of prefab instance in PrefabSystem (managed) List `PrefabSystem.m_Prefabs`
    /// to bring prefab metadata into native code, we need a struct to collect those data
    /// </summary>
    public struct NativeRenderedArea
    {
        /// <summary>
        /// declare the related PrefabBase instance has RenderedArea component or not
        /// </summary>
        public bool hasComponent;

        /// <summary>
        /// target properties to native: area prefab render priority
        /// </summary>
        public int renderPriority;
    }
}

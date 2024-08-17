using Game.Prefabs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AreaBucket.Utils
{
    public static class AreaPrefabUtils
    {

        /// <summary>
        /// Base on assumption that 'BuildingWithPolygonLot' that holds lot area entity as one of its sub areas
        /// </summary>
        /// <param name="ownerPrefab"></param>
        /// <param name="prefabSystem"></param>
        /// <returns></returns>
        public static bool TryGetLotPrefab(PrefabBase ownerPrefab, PrefabSystem prefabSystem, out LotPrefab lotPrefab)
        {
            lotPrefab = null;
            var hasSubAreaBuffer = prefabSystem.TryGetBuffer<SubArea>(ownerPrefab, true, out var subAreasBuffer);
            if (!hasSubAreaBuffer) return false;

            for (int i = 0; i < subAreasBuffer.Length; i++)
            {
                var subArea = subAreasBuffer[i];
                var hasLotPrefab = prefabSystem.TryGetPrefab<LotPrefab>(subArea.m_Prefab, out lotPrefab);
                if (!hasLotPrefab) continue;
                return true;
            }

            return false;
        }
    }
}

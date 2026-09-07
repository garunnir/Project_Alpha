// ============================================================
// StratumProfile — 지층 생성용 바닥 prefabId 레이어 목록 (SO)
// ============================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    [CreateAssetMenu(fileName = "StratumProfile", menuName = "Iso/Map/Stratum Profile")]
    public sealed class StratumProfile : ScriptableObject
    {
        [Serializable]
        public struct LayerEntry
        {
            public string prefabId;
        }

        [Tooltip("깊이별 순환 선택. 비어 있으면 MapDigConsts.DefaultStratumFloorPrefabId.")]
        public List<LayerEntry> layers = new List<LayerEntry>();

        public string PickPrefabId(int stratumSeed, int x, int z, int depth)
        {
            if (layers == null || layers.Count == 0)
                return MapDigConsts.DefaultStratumFloorPrefabId;

            int index = StratumGenerator.MixSeed(stratumSeed, x, z, depth) % layers.Count;
            string prefabId = layers[index].prefabId;
            return string.IsNullOrEmpty(prefabId)
                ? MapDigConsts.DefaultStratumFloorPrefabId
                : prefabId;
        }
    }
}

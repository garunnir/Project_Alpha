// ============================================================
// BuildingPrefabRoot — 건물 프리팹 루트 (Prefab Mode 편집용)
// ============================================================
// 맵/런타임/청크에는 넣지 않는다. BuildingPrefabUnpack으로 타일만 펼친다.
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 건물 한 채 저작 프리팹의 루트 마커.
    /// 자식에 <see cref="TileView"/>를 두고 Prefab Mode에서 편집한다.
    /// 에셋 폴더 SSOT: <see cref="PrefabFolderAssetPath"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildingPrefabRoot : MonoBehaviour
    {
        /// <summary>건물 통째 프리팹 저장 폴더 (타일 조각 <c>MapTiles/</c>와 분리).</summary>
        public const string PrefabFolderAssetPath = "Assets/Dist/Visual/Prefabs/Buildings";
    }
}

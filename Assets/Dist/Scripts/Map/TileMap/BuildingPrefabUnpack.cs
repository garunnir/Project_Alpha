// ============================================================
// BuildingPrefabUnpack — 건물 프리팹 → 월드 타일 펼침 (청크용)
// ============================================================
// 프리팹 GO는 맵에 남기지 않는다. 데이터만 타일로 풀어 IMapModel에 넣는다.
// outdoor(-1) / Outdoor/ 아래 타일은 건물 프리팹에 두면 안 되며 건너뛴다.
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// <see cref="BuildingPrefabRoot"/> 아래 TileView를 월드 그리드 TileData로 펼칩니다.
    /// </summary>
    public static class BuildingPrefabUnpack
    {
        /// <summary>
        /// 프리팹 인스턴스(또는 Prefab Mode 루트)의 타일을 월드 좌표 목록으로 만듭니다.
        /// <paramref name="worldOriginCell"/> = 프리팹 루트 셀에 해당하는 월드 원점.
        /// <paramref name="seedBuildingId"/> &gt; 0이면 펼친 구조 타일에 시드
        /// (이후 <c>AssignAll</c>이 연결 bake로 재할당·맞닿으면 합침 가능).
        /// </summary>
        public static List<TileData> UnpackToWorldTiles(
            BuildingPrefabRoot root,
            Vector3Int worldOriginCell,
            float cellSize,
            int seedBuildingId = TileIdentity.BuildingIdUnassigned)
        {
            var result = new List<TileData>();
            if (root == null)
                return result;

            cellSize = Mathf.Max(1e-4f, cellSize);
            TileView[] views = root.GetComponentsInChildren<TileView>(includeInactive: true);
            if (views == null || views.Length == 0)
                return result;

            var accepted = new List<TileView>(views.Length);
            for (int i = 0; i < views.Length; i++)
            {
                TileView view = views[i];
                if (view == null)
                    continue;

                if (BuildingViewHierarchy.IsUnderOutdoorRoot(view.transform.parent))
                {
                    Debug.LogWarning(
                        $"[BuildingPrefabUnpack] Outdoor/ 아래 타일은 맵 outdoor 레이어로만 넣으세요: '{view.name}'.",
                        view);
                    continue;
                }

                accepted.Add(view);
            }

            if (accepted.Count == 0)
                return result;

            List<TileData> snapshot = TileViewSceneGather.BuildTileDataSnapshot(accepted);
            if (snapshot == null || snapshot.Count == 0)
                return result;

            // 프리팹 로컬 그리드 = 스냅샷 그리드 − 루트 셀.
            Vector3Int rootCell = TileHelper.ConvertWorldToGrid(root.transform.position, cellSize);

            for (int i = 0; i < snapshot.Count; i++)
            {
                TileData td = snapshot[i];
                if (BuildingIdBakeRules.IsImmutableOutdoorBuildingId(td.identity.buildingId))
                {
                    Debug.LogWarning(
                        $"[BuildingPrefabUnpack] outdoor(-1) 타일 '{td.identity.PrefabId}'는 건물 프리팹에 둘 수 없습니다.",
                        root);
                    continue;
                }

                Vector3Int local = td.identity.GridPos - rootCell;
                Vector3Int world = local + worldOriginCell;
                int buildingId = seedBuildingId > 0
                    ? seedBuildingId
                    : TileIdentity.BuildingIdUnassigned;

                var identity = new TileIdentity
                {
                    PrefabId = td.identity.PrefabId,
                    GridPos = world,
                    sizeUnit = td.identity.sizeUnit,
                    placementSlot = td.identity.placementSlot,
                    wallFace = td.identity.wallFace,
                    floorFace = td.identity.floorFace,
                    collisionFlags = td.identity.collisionFlags,
                    buildingId = buildingId,
                    roomId = 0,
                };

                result.Add(new TileData
                {
                    tileDefId = td.tileDefId,
                    state = td.state,
                    identity = identity,
                    plant = td.plant,
                });
            }

            return result;
        }

        /// <summary>
        /// 레지스트리에서 시드 id를 받아 펼칩니다. 인스턴스 GO 제거는 호출부 책임.
        /// </summary>
        public static List<TileData> UnpackInstance(
            BuildingPrefabRoot instance,
            Vector3Int worldOriginCell,
            float cellSize,
            BuildingGroupRegistry registryForSeed)
        {
            int seed = registryForSeed != null
                ? registryForSeed.AllocateBuildingId()
                : TileIdentity.BuildingIdUnassigned;

            return UnpackToWorldTiles(instance, worldOriginCell, cellSize, seed);
        }
    }
}

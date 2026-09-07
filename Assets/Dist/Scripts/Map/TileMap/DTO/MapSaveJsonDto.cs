using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    [Serializable]
    public class MapSaveJsonDto
    {
        /// <summary>1 = placementSlot v1. 2 = floorFaces walkable coords. 0·누락 = 레거시 tiles[].tileType.</summary>
        public int schemaVersion;

        /// <summary>그리드 1칸 월드 길이. 0 이하·누락(구 JSON)이면 로더 fallback 사용.</summary>
        public float gridCellSize = 1f;

        /// <summary>true면 mapBounds* 필드가 유효. false(구 JSON)면 로드 시 MapBoundsBake fallback.</summary>
        public bool hasMapBounds;

        /// <summary>맵 XZ 직육면체 + Y 하단만. maxY 없음 — bounds 안 Y 상한 없음.</summary>
        public int mapBoundsMinX;
        public int mapBoundsMaxX;
        public int mapBoundsMinZ;
        public int mapBoundsMaxZ;
        public int mapBoundsMinY;

        public List<TileSaveData> tiles = new List<TileSaveData>();
        public List<WallEdgeSaveData> wallEdges = new List<WallEdgeSaveData>();
        public List<FloorFaceSaveData> floorFaces = new List<FloorFaceSaveData>();

        /// <summary>맵 혈흔 스탬프 (월드 좌표). tiles와 별 레이어. 구 JSON 누락 시 empty.</summary>
        public List<BloodStampSaveData> bloodStamps = new List<BloodStampSaveData>();

        /// <summary>
        /// 에디터 물 저작 마커 (바닥 +Y 면 앵커 = CellBelow). 타일 모델에 진입하지 않으며
        /// liquidCells 시드·bake의 입력이다. 구 JSON은 누락 — floorFaces의 워터 태그로 폴백한다.
        /// </summary>
        public List<FloorFaceSaveData> liquidAuthoringFaces = new List<FloorFaceSaveData>();

        /// <summary>맵 액체 셀 (grid 좌표). tiles와 별 레이어. 구 JSON 누락 시 empty.</summary>
        public List<MapLiquidCellSaveData> liquidCells = new List<MapLiquidCellSaveData>();

        /// <summary>true면 liquidCells를 그대로 신뢰(재시드 금지). 구 JSON은 false — 로드 시 물 저작 면으로 1회 시드.</summary>
        public bool hasLiquidSnapshot;

        /// <summary>
        /// true면 liquidCells[].tempDeciC가 유효하다. false(구 JSON)면 0이 물의 어는점과 겹쳐
        /// 전부 얼어버리므로, 로드 시 기본 기온으로 초기화한다.
        /// </summary>
        public bool hasLiquidTemperature;

        /// <summary>Legacy only. Load migrates to OccupiedCell plant tiles then cleared. New saves write null.</summary>
        public List<PlantCellSaveData> plantCells = new List<PlantCellSaveData>();

        /// <summary>WorldClock 스냅샷. 구 JSON은 false — 로드 시 SetTime 생략.</summary>
        public bool hasClockSnapshot;
        public int dayIndex;
        public int minuteOfDay;

        /// <summary>true면 <see cref="playerProgressJson"/>에 possessed 플레이어 진행 스냅샷이 유효.</summary>
        public bool hasPlayerProgressSnapshot;

        /// <summary><see cref="PlayerProgressSaveDto"/> JsonUtility JSON.</summary>
        public string playerProgressJson;

        /// <summary>
        /// 지층 생성 시드 (schema ≥ <see cref="MapSaveSchema.StratumSeedV3"/>).
        /// 0·누락(구 JSON)이면 최초 굴착 시 런타임이 할당한다.
        /// </summary>
        public int stratumSeed;
    }
}
// ============================================================
// BuildingSaveData — V5 건물 단위 직렬화 (피벗 + 로컬 tiles/walls/floors)
// ============================================================

using System;
using System.Collections.Generic;

namespace IsoTilemap
{
    /// <summary>
    /// 맵 JSON schema ≥ <see cref="MapSaveSchema.OutdoorStructureLayersV5"/> 구조물 레이어.
    /// 피벗은 월드 min(x,y,z); 목록 좌표는 로컬(월드 − 피벗). 로드 후 모델은 월드 그리드만 쓴다.
    /// </summary>
    [Serializable]
    public class BuildingSaveData
    {
        /// <summary>
        /// 뷰 <c>Building_{id}</c> 저장 힌트. 로드 후 연결 bake가 재할당(맞닿으면 합침)할 수 있음.
        /// 0·누락(구 JSON)이면 로드 시 배열 순번+1 스탬프 후 역시 bake.
        /// </summary>
        public int buildingId;

        public int pivotX;
        public int pivotY;
        public int pivotZ;

        public List<TileSaveData> tiles = new List<TileSaveData>();
        public List<WallEdgeSaveData> wallEdges = new List<WallEdgeSaveData>();
        public List<FloorFaceSaveData> floorFaces = new List<FloorFaceSaveData>();
    }
}

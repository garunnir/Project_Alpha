using System;
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    public struct TileData
    {
        public Guid tileDefId{init; get;}
        public TileState state;
        public TileIdentity identity{init; get;}
        /// <summary>OccupiedCell plant instance. Empty seedItemId = not a plant tile.</summary>
        public PlantTileInstance plant;
    }

    [Serializable]
    public struct PlantTileInstance
    {
        public string seedItemId;
        public int plantedWorldMinute;
        public bool fertilized;
        /// <summary>Tree fruit harvest world minute. <see cref="MapPlantConsts.NoFruitHarvestMinute"/> = never.</summary>
        public int lastFruitHarvestWorldMinute;

        public bool HasSeed => !string.IsNullOrEmpty(seedItemId);
    }
    public struct TileState
    {
        /// <summary>DTO·기본값 호환. 런타임 맵·뷰 동기화는 <see cref="TileViewPresentationApplier"/>.</summary>
        public float characterOcclusion;
        /// <summary>DTO·기본값 호환. 런타임 표현은 applier <c>SetGhosted</c>.</summary>
        public bool isGhosted;
        /// <summary>DTO·기본값 호환. 런타임 표현은 applier <c>SetSelected</c>.</summary>
        public bool isSelected;
    }
    public readonly struct TileIdentity
    {
        public string PrefabId{init; get;}

        /// <summary>
        /// <see cref="TilePlacementSlot.OccupiedCell"/>이면 점유 셀.
        /// <see cref="TilePlacementSlot.HorizontalFace"/>이면 walkable(서는 층).
        /// <see cref="TilePlacementSlot.VerticalFace"/>이면 면 앵커 셀.
        /// </summary>
        public Vector3Int GridPos{init; get;}
        public Vector3Int sizeUnit{init; get;}
        public byte placementSlot{init; get;}
        /// <summary><see cref="TilePlacementSlot.VerticalFace"/>일 때 <see cref="WallFace"/>.</summary>
        public byte wallFace{init; get;}
        /// <summary><see cref="TilePlacementSlot.HorizontalFace"/>일 때 <see cref="FloorFace"/>.</summary>
        public byte floorFace{init; get;}

        /// <summary>
        /// bake: <see cref="BuildingIdUnassigned"/> 미할당(수신만), <see cref="BuildingIdTerrain"/> plaza(확장 원점 아님), &gt;0 건물(전파·merge 시드).
        /// 전파 원점 규칙: <c>BuildingGroupBuilder.CanPropagateBuildingIdFrom</c>. 런타임 야외 분기는 <c>IsOutdoorEvaluation</c>.
        /// </summary>
        public int buildingId { init; get; }

        /// <summary>bake 초기·건물 BFS 대기 Floor.</summary>
        public const int BuildingIdUnassigned = 0;

        /// <summary>MinCellY 광장 Floor (야외 BFS 확정 후).</summary>
        public const int BuildingIdTerrain = -1;

        /// <summary>0=room 미할당; 같은 buildingId·cellY 내 방 번호.</summary>
        public int roomId { init; get; }

        /// <summary><see cref="TileDefinition"/> 충돌·오클루전 bake. <see cref="TileCollisionProfile.FromDefinitionForSlot"/>.</summary>
        public byte collisionFlags { init; get; }
    }

    /// <summary>플레이어 월드와 벽 간 거리에 따른 오클루전 강도(0~1) 매핑.</summary>
    [Serializable]
    public struct OcclusionProximitySettings
    {
        [Tooltip("그리드 1 칸 길이(월드 단위). TileHelper와 동일해야 합니다.")]
        public float CellSize;

        [Tooltip("이 거리(월드 XZ) 미만에서는 occlusion≈1")]
        public float OcclusionFullWithinDistance;

        [Tooltip("이 거리보다 멀면 occlusion=0 (Full 값보다 커야 함)")]
        public float OcclusionNoneBeyondDistance;

        [Tooltip("근접도 재계산 시 이전 값과 차이가 이 미만이면 배치 적용 스킵")]
        public float ApplyEpsilon;

        [Tooltip("0=즉시 반영. 클수록 프레임 간 occlusion 변화가 완만해집니다.")]
        public float OcclusionSmoothSpeed;

        [Tooltip("플레이어 주변 삼각 마스크로 추가 숨김 벽을 포함합니다. 끄면 BFS·거리 오클루전만 적용됩니다.")]
        public bool PlayerProximityMaskEnabled;

        public static OcclusionProximitySettings DefaultUnity => new OcclusionProximitySettings
        {
            CellSize = 1f,
            OcclusionFullWithinDistance = 1.25f,
            OcclusionNoneBeyondDistance = 10f,
            ApplyEpsilon = 0.01f,
            OcclusionSmoothSpeed = 6f,
            PlayerProximityMaskEnabled = true,
        };
    }

}

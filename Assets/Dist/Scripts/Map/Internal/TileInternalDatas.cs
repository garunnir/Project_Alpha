using System;
using System.Collections.Generic;
using UnityEngine;namespace IsoTilemap
{
    public struct TileData
    {
        public Guid tileDefId{init; get;}
        public TileState state;
        public TileIdentity identity{init; get;}
    }
    public struct TileState
    {
        /// <summary>캐릭터 오클루전에 의한 표시 차단 강도(0 표시 유지 ~ 1 완전 가림).</summary>
        public float characterOcclusion;
        public bool isGhosted;
        public bool isSelected;
    }
    public readonly struct TileIdentity
    {
        /// <summary>255이면 칸 타일. 0=+X 면, 1=+Z 면(GridPos 정렬 앵커 기준, WallFace와 동일).</summary>
        public const byte EdgeFaceNone = 255;

        public string PrefabId{init; get;}

        /// <summary>
        /// 칸 타일이면 점유 셀입니다. 엣지 타일이면 점유 셀이 아니라 두 셀 사이 변을 정렬/저장하기 위한 앵커입니다.
        /// </summary>
        public Vector3Int GridPos{init; get;}
        public Vector3Int sizeUnit{init; get;}
        public byte tileType{init; get;}
        public byte edgeFace{init; get;}
    }

    /// <summary>플레이어 월드와 벽 간 거리에 따른 <see cref="TileState.characterOcclusion"/> 매핑.</summary>
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

        public static OcclusionProximitySettings DefaultUnity => new OcclusionProximitySettings
        {
            CellSize = 1f,
            OcclusionFullWithinDistance = 0.75f,
            OcclusionNoneBeyondDistance = 8f,
            ApplyEpsilon = 0.015f,
        };
    }

}

using IsoTilemap;
using System;
using UnityEngine;

public class CharacterState : MonoBehaviour
{
    [Tooltip("플레이어 그리드/오클루전에 쓰이는 타일 단위 한 칸의 월드 길이")]
    [SerializeField] private float _gridCellSize = 1f;

    public Vector3 SightDir { get; private set; } = Vector3.zero;
    /// <summary>조준 레이의 끝(시야) 월드 위치.</summary>
    public Vector3 AimWorldPoint { get; private set; } = Vector3.zero;
    /// <summary>플레이어 몸의 월드 위치. 조준이 아닐 때 오클루전 등의 기준점.</summary>
    public Vector3 BodyWorldPoint { get; private set; } = Vector3.zero;
    public Vector3 MoveDir { get; private set; } = Vector3.zero;
    public Vector3Int GridPos { get; private set; } = Vector3Int.zero;

    /// <summary>그리드·오클루전용 셀 크기(Isomap TileHelper 및 OcclusionProximitySettings.CellSize와 통일).</summary>
    public float GridCellSize => Mathf.Max(1e-4f, _gridCellSize);

    public bool IsAiming { get; private set; }
    public event Action<Vector3Int> GridPosChanged;
    /// <summary>매 <see cref="UpdateGridPos"/> 호출 때마다(셀 변경 없이 포함) 발생.</summary>
    public event Action<Vector3> WorldPoseChanged;
    public event Action<Vector3> AimWorldPointChanged;

    internal void SetMoveDir(Vector3 desiredMove)
    {
        if (desiredMove == Vector3.zero) return;
        MoveDir = desiredMove;
    }

    internal void SetAimDir(Vector3 dir, Vector3 aimWorldPoint)
    {
        if (dir == Vector3.zero) return;
        SightDir = dir;
        AimWorldPoint = aimWorldPoint;
        IsAiming = true;
        AimWorldPointChanged?.Invoke(aimWorldPoint);
    }

    internal Vector3 GetFacingDir()
    {
        if (IsAiming)
            return SightDir;
        return MoveDir;
    }

    internal void ClearAim()
    {
        IsAiming = false;
        AimWorldPoint = Vector3.zero;
        AimWorldPointChanged?.Invoke(Vector3.zero);
    }

    internal void UpdateGridPos(Vector3 worldPos)
    {
        BodyWorldPoint = worldPos;

        var gridPos = TileHelper.ConvertWorldToGrid(worldPos, GridCellSize);
        if (GridPos != gridPos)
        {
            GridPos = gridPos;
            GridPosChanged?.Invoke(gridPos);
            if (Config.DebugMode.PlayerPosUpdate)
                Debug.Log($"Player GridPos Changed: {GridPos}");
        }

        WorldPoseChanged?.Invoke(worldPos);
    }
}

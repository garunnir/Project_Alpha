using IsoTilemap;
using System;
using UnityEngine;

public class CharacterState : MonoBehaviour
{
    public Vector3 FacingDir { get; private set; }=Vector3.zero;
    public Vector3Int GridPos { get; private set; }=Vector3Int.zero;
    public bool IsAiming { get; private set; }
    public event Action<Vector3Int> GridPosChanged;

    internal void UpdateState(Vector3 desiredMove)
    {
        if (IsAiming) return; // 조준 중엔 이동 방향이 시선 방향을 덮어쓰지 않음
        FacingDir=(desiredMove!=Vector3.zero)?desiredMove:FacingDir;
    }

    internal void SetAimDir(Vector3 dir)
    {
        if (dir == Vector3.zero) return;
        FacingDir = dir;
        IsAiming = true;
    }

    internal void ClearAim()
    {
        IsAiming = false;
    }
    internal void UpdateGridPos(Vector3 worldPos)
    {
        var gridPos = TileHelper.ConvertWorldToGrid(worldPos);
        if (GridPos != gridPos)
        {
            GridPos = gridPos;
            GridPosChanged?.Invoke(gridPos);
            if(Config.DebugMode.PlayerPosUpdate)
                Debug.Log($"Player GridPos Changed: {GridPos}");
        }
    }
}

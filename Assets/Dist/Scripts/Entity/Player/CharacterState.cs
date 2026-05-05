using IsoTilemap;
using System;
using UnityEngine;

public class CharacterState : MonoBehaviour
{
    public Vector3 SightDir { get; private set; }=Vector3.zero;
    public Vector3 AimWorldPoint { get; private set; } = Vector3.zero; // 조준 월드 지점, ClearAim 시 zero
    public Vector3 MoveDir { get; private set; }=Vector3.zero;
    public Vector3Int GridPos { get; private set; }=Vector3Int.zero;
    public bool IsAiming { get; private set; }
    public event Action<Vector3Int> GridPosChanged;
    public event Action<Vector3> AimWorldPointChanged;

    internal void SetMoveDir(Vector3 desiredMove)
    {
        if (desiredMove == Vector3.zero) return;
        MoveDir=desiredMove;
    }

    internal void SetAimDir(Vector3 dir, Vector3 aimWorldPoint)
    {
        if (dir == Vector3.zero) return;
        SightDir = dir;
        AimWorldPoint = aimWorldPoint;
        IsAiming = true;
        AimWorldPointChanged?.Invoke(aimWorldPoint);
    }
    internal Vector3 GetFacingDir(){
        if(IsAiming){
            return SightDir;
        }
        else return MoveDir;
    }
    internal void ClearAim()
    {
        IsAiming = false;
        AimWorldPoint = Vector3.zero;
        AimWorldPointChanged?.Invoke(Vector3.zero);
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

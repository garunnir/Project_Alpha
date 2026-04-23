using IsoTilemap;
using System;
using UnityEngine;

public class CharacterState : MonoBehaviour
{
    public Vector3 SightDir { get; private set; }=Vector3.zero;
    public Vector3 MoveDir { get; private set; }=Vector3.zero;
    public Vector3Int GridPos { get; private set; }=Vector3Int.zero;
    public bool IsAiming { get; private set; }
    public event Action<Vector3Int> GridPosChanged;

    internal void SetMoveDir(Vector3 desiredMove)
    {
        if (desiredMove == Vector3.zero) return;
        MoveDir=desiredMove;
    }

    internal void SetAimDir(Vector3 dir)
    {
        if (dir == Vector3.zero) return;
        SightDir = dir;
        IsAiming = true;
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

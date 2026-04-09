using IsoTilemap;
using System;
using UnityEngine;

public class CharacterState : MonoBehaviour
{
    public Vector3 FacingDir { get; private set; }=Vector3.zero;
    public Vector3Int GridPos { get; private set; }=Vector3Int.zero;
    public event Action<Vector3Int> GridPosChanged;

    internal void UpdateState(Vector3 desiredMove)
    {
        FacingDir=(desiredMove!=Vector3.zero)?desiredMove:FacingDir;
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

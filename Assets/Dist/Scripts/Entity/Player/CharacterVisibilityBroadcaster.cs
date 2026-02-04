using IsoTilemap;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(CharacterState))]
public class CharacterVisibilityBroadcaster : MonoBehaviour
{
    //캐릭터 보임 관련 요소를 맵과 상호작용하기 위한 클래스
    private CharacterState _characterState;
    [SerializeField] private TileMapContext _tileMapRuntime;
    private void Awake()
    {
        _characterState = GetComponent<CharacterState>();
        if (_tileMapRuntime == null) Debug.LogWarning("not exist tilemapruntime");
    }
    private void OnEnable()
    {
        if (_characterState != null)
        {
            _characterState.GridPosChanged += BroadcastWallHide;
        }
    }
    private void OnDisable()
    {
        if (_characterState != null)
        {
            _characterState.GridPosChanged -= BroadcastWallHide;
        }
    }
    //연결해서 캐릭터의 그리드포지션이 바뀔때마다 런타임 타일맵에 벽 감춤 명령을 내림.
    private void BroadcastWallHide(Vector3Int vector3Int)
    {
        List<TileData> walls = _tileMapRuntime.GetOccludingWalls(vector3Int);
        foreach (TileData wall in walls)
        {
            if (_tileMapRuntime.TryGetTile(wall.tileDefId, out TileInfo tileInfo))
            {
                tileInfo.state.isHiddenCharacter = true;
            }
        }
    }
}

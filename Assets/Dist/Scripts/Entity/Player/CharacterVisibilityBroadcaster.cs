using IsoTilemap;
using System.Collections.Generic;
using UnityEngine;
[RequireComponent(typeof(CharacterState))]
public class CharacterVisibilityBroadcaster : MonoBehaviour
{
    //캐릭터 보임 관련 요소를 맵과 상호작용하기 위한 클래스
    private CharacterState _characterState;
    [SerializeField] private TileMapManager _tileMapManager;
    private void Awake()
    {
        _characterState = GetComponent<CharacterState>();
        if (_tileMapManager == null) Debug.LogWarning("not exist TileMapManager");
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
        _tileMapManager.Model?.HideOcclusionTileWall(vector3Int);
    }
}

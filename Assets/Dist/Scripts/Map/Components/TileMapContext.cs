using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace IsoTilemap
{
    // 타일맵 도메인 데이터 관리 클래스
    // 뷰와는 독립적으로 타일맵 데이터를 관리하고 제공하는 역할을 합니다.
    [DisallowMultipleComponent]
    public class TileMapContext : MonoBehaviour
    {
        public IMapModelReadOnly Model { get; private set; }
        private TileMapRuntime _runtimeData;
        private HashSet<Vector3Int> _cachedCurrentRoomID;
        private List<TileData> _cachedtiles;
        public void Initialize(IMapSession mapSession)
        {
            Model = mapSession.Model;
            _runtimeData = mapSession.Runtime as TileMapRuntime;
        }
        public List<TileData> GetOccludingWalls(Vector3Int playerCellPos)
        {
            // 주어진 플레이어 셀 위치(playerCellPos)를 기준으로
            // 2D(XZ) flood-fill을 수행하여 플레이어가 닿을 수 있는 빈 공간을 찾습니다.
            // 그 과정에서 빈 공간과 접한 Wall 타입의 타일을 수집하여 반환합니다.
            // 반환값: 숨겨야 할 Wall 타일들의 리스트(중복 제거)
            
            if (_runtimeData == null || _runtimeData.tiles == null)
                return new List<TileData>();

            var alltiles = _runtimeData.tiles;


            //이미 계산된구역이면 재계산하지 않음. 이 효력은 다른 구역으로 이동하면 사라짐

            if (_cachedCurrentRoomID != null && _cachedCurrentRoomID.Contains(playerCellPos))
            {
                return _cachedtiles;
            }
            IEnumerable<TileCellSnapshot> wallResult = Model.GetOccludingWalls(playerCellPos,_runtimeData.tiles);
            var visited = wallResult.Select(x => x.Position).ToHashSet();
            List<TileData> resultTiles = wallResult.SelectMany(x => x.Tiles).ToList();

            _cachedCurrentRoomID = visited.ToHashSet();
            _cachedtiles = resultTiles;
            return resultTiles;
        }
    }
}
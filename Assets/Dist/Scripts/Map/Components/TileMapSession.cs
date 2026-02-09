using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace IsoTilemap
{
    /// <summary>
    /// 타일맵 도메인 데이터 관리 클래스
    /// 컨트롤러의 세션 단위 데이터 관리 담당
    /// </summary>
    public class TileMapSession : IMapSession
    {
        private IMapRuntime _runtimeData;
        private HashSet<Vector3Int> _cachedCurrentRoomID;
        private List<TileData> _cachedtiles;
        private IMapViewBuilder _visualizer;

        public IMapModelReadOnly Model => throw new NotImplementedException();

        public IMapRuntimeReadOnly Runtime => _runtimeData;

        public void Initialize(IMapRuntime runtime, IMapViewBuilder viewBuilder)
        {
            _runtimeData = runtime;
            _visualizer = viewBuilder;
            _visualizer.Bind(runtime);
        }
        public IReadOnlyList<TileData> GetOccludingWalls(Vector3Int playerCellPos)
        {
            // 주어진 플레이어 셀 위치(playerCellPos)를 기준으로
            // 2D(XZ) flood-fill을 수행하여 플레이어가 닿을 수 있는 빈 공간을 찾습니다.
            // 그 과정에서 빈 공간과 접한 Wall 타입의 타일을 수집하여 반환합니다.
            // 반환값: 숨겨야 할 Wall 타일들의 리스트(중복 제거)
            
            //이미 계산된구역이면 재계산하지 않음. 이 효력은 다른 구역으로 이동하면 사라짐

            if (_cachedCurrentRoomID != null && _cachedCurrentRoomID.Contains(playerCellPos))
            {
                return _cachedtiles;
            }
            IReadOnlyList<TileData> resultTiles = _runtimeData.GetOccludingWalls(playerCellPos);
            IEnumerable<Vector3Int> visited = resultTiles.Select(x => x.identity.GridPos);
            _cachedCurrentRoomID = visited.ToHashSet();
            _cachedtiles = resultTiles.ToList();
            return resultTiles;
        }
        public void HideOcclusionTileWall(Vector3Int playerCellPos)
        {
            List<TileData> walls = GetOccludingWalls(playerCellPos);
            for (int i = 0; i < walls.Count; i++)
            {
                TileData wall = walls[i];
                TileState tileState = wall.state;
                    tileState.isHiddenCharacter = true;
                    wall.state = tileState;
            }
        }

        internal bool TryGetTile(Guid tileDefId, out TileView tileInfo)
        {
            throw new NotImplementedException();
        }
    }
}
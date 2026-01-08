using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace IsoTilemap
{
    //맵 데이터 입출력 담당



    public interface IMapSerializer
    {
        MapSaveJsonDto Read(string path);
        void Write(string path, MapSaveJsonDto dto);
    }
//맵 데이터 구조 변환 담당
public interface IMapMapper
{
    IMapTilesReadOnly ToPrepared(MapSaveJsonDto dto);
    MapSaveJsonDto FromPrepared(IMapTilesReadOnly prepared);
}
    //맵 도메인 모델 빌더 담당
public interface IMapModelBuilder
{
    IMapModel BuildRuntime(IMapTilesReadOnly prepared);
}
    public interface IMapTilesReadOnly
{
    bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tiles);
    IEnumerable<Vector3Int> Positions { get; }
}
    //맵 도메인 모델 읽기 전용 인터페이스
    public interface IMapModelReadOnly
    {
    }
    public interface IMapModel : IMapModelReadOnly
    {
    }

    //맵 도메인 모델 구현체
    public sealed class MapTilesDTO : IMapTilesReadOnly
    {
        private readonly TileMapRuntime _runtimeData;

        public IEnumerable<Vector3Int> Positions => _runtimeData.tiles.Keys;

        public bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tiles)
        {
            if (_runtimeData.tiles.TryGetValue(pos, out var tileList))
            {
                tiles = tileList;
                return true;
            }
            tiles = null;
            return false;
        }

        public MapTilesDTO(TileMapRuntime runtimeData)
        {
            _runtimeData = runtimeData;
        }
    }
    // //맵 도메인 스냅샷 구현체
    //     public sealed class MapModelSnapshot : IMapModelReadOnly
    // {
    //     private readonly IEnumerable<TileCellSnapshot>  _tiles;
    //     public MapModelSnapshot(MapModelSnapshot tiles)
    //     {
    //         //복사 생성자
    //     }
    //     public MapModelSnapshot(IEnumerable<TileCellSnapshot> tiles)
    //     {
    //         _tiles = tiles;
    //     }

    //     public IEnumerable<TileCellSnapshot> GetOccludingWalls(Vector3Int playerCellPos, Dictionary<Vector3Int, List<TileData>> alltiles)
    //     {
    //         throw new NotImplementedException();
    //     }
    // }
    //맵 뷰 빌더 담당
    public interface IMapViewBuilder

    {
        void Build(IMapModelReadOnly runtimeData);
    }
}
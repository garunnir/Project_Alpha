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
         IEnumerable<TileCellSnapshot> Tiles();
    }
    public interface IMapModel : IMapModelReadOnly
    {
    }
    public sealed class MapModelReadOnlyImpl: IMapModel
    {
        private readonly IMapModelReadOnly _domainReadOnly;
        public MapModelReadOnlyImpl(IMapModelReadOnly domainReadOnly)
        {
            _domainReadOnly = domainReadOnly;
        }
        public IEnumerable<TileCellSnapshot> Tiles()
        {
            return _domainReadOnly.Tiles();
        }
    }
    //맵 도메인 모델 구현체
    public sealed class MapTiles : IMapTilesReadOnly
    {
        private readonly TileMapRuntimeData _runtimeData;

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

        public MapTiles(TileMapRuntimeData runtimeData)
        {
            _runtimeData = runtimeData;
        }
        public MapTiles(Dictionary<Vector3Int, List<TileData>> runtimeData)
        {
            _runtimeData = new TileMapRuntimeData()
            {
                tiles = runtimeData
            };
        }
        //복사 생성자
        public MapTiles(MapTiles other)
        {
            _runtimeData = new TileMapRuntimeData()
            {
                tiles = new Dictionary<Vector3Int, List<TileData>>(other._runtimeData.tiles)
            };
        }
    }
    //맵 도메인 스냅샷 구현체
        public sealed class MapDomainSnapshot : IMapModelReadOnly
    {
        private readonly IEnumerable<TileCellSnapshot>  _tiles;
        public MapDomainSnapshot(MapDomainSnapshot tiles)
        {
            //복사 생성자
        }
        public MapDomainSnapshot(IEnumerable<TileCellSnapshot> tiles)
        {
            _tiles = tiles;
        }
        public IEnumerable<TileCellSnapshot> Tiles()
        {
            return _tiles;
        }
    }
    //맵 뷰 빌더 담당
    public interface IMapViewBuilder

    {
        void Build(IMapModelReadOnly runtimeData);
    }
}
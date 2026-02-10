using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace IsoTilemap
{

    public interface IMapSession
    {
        public IMapModel Model { get; }
        public IMapRuntime Runtime { get; }
    }
    public interface IMapSessionReadOnly
    {
        public IMapModelReadOnly Model { get; }
        public IMapRuntimeReadOnly Runtime { get; }
    }
    public interface IMapRuntimeReadOnly
    {
        public event Action<Vector3Int, List<TileData>> OnRuntimeDataChanged;
        public IReadOnlyList<TileData> GetOccludingWalls(Vector3Int playerCellPos);
    }
    public interface IMapRuntime : IMapRuntimeReadOnly
    {
        
    }

    public sealed class MapInstance : IMapSession
    {
        public IMapModel Model { get; }
        public IMapRuntime Runtime { get; }

        public MapInstance(IMapModel model, IMapRuntime runtime)
        {
            Model = model;
            Runtime = runtime;
        }
    }


    //맵 데이터 입출력 담당



    public interface IMapSerializer
    {
        MapSaveJsonDto Read(string path);
        void Write(string path, MapSaveJsonDto dto);
    }
    //맵 데이터 구조 변환 담당
    public interface IMapMapper
    {
        IMapModelReadOnly ToPrepared(MapSaveJsonDto dto);
        MapSaveJsonDto FromPrepared(IMapModelReadOnly prepared);
    }
    public interface IMapRuntimeBuilder

    {
        IMapRuntime Build(IMapModelReadOnly prepared);
    }
    //맵 도메인 모델 빌더 담당
    public interface IMapModelBuilder
    {
        IMapModel Build(IMapModelReadOnly prepared);
    }
    //맵 도메인 모델 읽기 전용 인터페이스
    public interface IMapModelReadOnly
    {
        bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tiles);
        IEnumerable<Vector3Int> Positions { get; }
        IReadOnlyList<TileData> Tiles();
    }
    public interface IMapModel : IMapModelReadOnly
    {

    }

    public sealed class MapModelDTO : IMapModelReadOnly
    {
        private readonly IReadOnlyDictionary<Vector3Int, IReadOnlyList<TileData>> _dto;

        public IEnumerable<Vector3Int> Positions => _dto.Keys;

        public bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tiles)
        {
            if (_dto.TryGetValue(pos, out var tileList))
            {
                tiles = tileList;
                return true;
            }
            tiles = null;
            return false;
        }

        public IReadOnlyList<TileData> Tiles()
        {
            List<TileData> allTiles = new List<TileData>();
            foreach (var tileList in _dto.Values)
            {
                allTiles.AddRange(tileList);
            }
            return allTiles;
        }

        public MapModelDTO(Dictionary<Vector3Int, List<TileData>> dto)
        {
            _dto = dto.ToDictionary(kvp => kvp.Key, kvp => (IReadOnlyList<TileData>)kvp.Value);
        }
        public MapModelDTO(IReadOnlyDictionary<Vector3Int, IReadOnlyList<TileData>> dto)
        {
            _dto = dto;
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
    /// <summary>
    /// 맵 뷰 빌더 담당 초기화,실시간 뷰 구성
    /// </summary>
    public interface IMapViewBuilder
    {
        void Build(IMapModelReadOnly model);
        void Bind(IMapRuntimeReadOnly runtime);
    }
}
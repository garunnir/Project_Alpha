using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    public interface IMapSerializer
    {
        MapSaveJsonDto Read(string path);
        void Write(string path, MapSaveJsonDto dto);
    }
    public interface IMapMapper
    {
        Dictionary<Vector3Int, List<TileData>> ToDomain(MapSaveJsonDto dto);
        MapSaveJsonDto FromDomain(Dictionary<Vector3Int, List<TileData>> domain);
    }
    public interface IMapDomainBuilder
    {
        TileMapRuntimeData BuildRuntime(Dictionary<Vector3Int, List<TileData>> domainData);
        TileMapRuntimeData GetRuntimeData();
    }
    public interface IMapViewBuilder
    {
        void Build(TileMapRuntimeData runtimeData);
    }

    public sealed class MapLoadPipeline
    {
        private readonly IMapSerializer _serializer;
        private readonly IMapDomainBuilder _domainBuilder;
        private readonly IMapViewBuilder _viewBuilder;
        private readonly IMapMapper _mapper;

        public MapLoadPipeline(IMapSerializer serializer,
                               IMapDomainBuilder domainBuilder,
                               IMapViewBuilder viewBuilder,
                               IMapMapper mapper)
        {
            _serializer = serializer;
            _domainBuilder = domainBuilder;
            _viewBuilder = viewBuilder;
            _mapper = mapper;
        }

        public void Load(string path)
        {
            // Deserialize JSON to DTO
            MapSaveJsonDto dto = _serializer.Read(path);
            // Map DTO to Domain Model
            Dictionary<Vector3Int, List<TileData>> domain = _mapper.ToDomain(dto);
            // Build Runtime Data
            TileMapRuntimeData view = _domainBuilder.BuildRuntime(domain);
            // Build View
            _viewBuilder.Build(view); 
        }
        public void Save(string path)
        {
            TileMapRuntimeData domain = _domainBuilder.GetRuntimeData();
            MapSaveJsonDto dto = _mapper.FromDomain(domain.tiles);
            // Serialize dto to JSON and save to path
            _serializer.Write(path, dto);
        }
    }
}
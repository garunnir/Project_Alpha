using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
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
            IMapDomainReadOnly domain = _mapper.ToDomain(dto);
            // Build Runtime Data
            TileMapDomainData domainData = _domainBuilder.BuildRuntime(domain);
            // Build View
            _viewBuilder.Build(domainData, domain); 
        }
        public void Save(string path)
        {
            TileMapDomainData domain = _domainBuilder.GetRuntimeData();
            MapSaveJsonDto dto = _mapper.FromDomain(domain);
            // Serialize dto to JSON and save to path
            _serializer.Write(path, dto);
        }
    }
}
using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    public sealed class MapLoadPipeline
    {
        private readonly IMapSerializer _serializer;
        private readonly IMapModelBuilder _modelBuilder;
        private readonly IMapMapper _mapper;
        private readonly IMapRuntimeBuilder _runtime;

        public MapLoadPipeline(IMapSerializer serializer,
                               IMapModelBuilder domainBuilder,
                               IMapMapper mapper,
                               IMapRuntimeBuilder runtimeBuilder)
        {
            _serializer = serializer;
            _modelBuilder = domainBuilder;
            _mapper = mapper;
            _runtime = runtimeBuilder;
        }

        public IMapSession LoadModel(string path)
        {
            // Deserialize JSON to DTO
            MapSaveJsonDto dto = _serializer.Read(path);
            // Map DTO to Domain Model
            IMapTilesReadOnly prepared = _mapper.ToPrepared(dto);
            // Build Runtime Data
            IMapModelReadOnly modelData = _modelBuilder.Build(prepared);
            IMapRuntimeReadOnly runtimeData = _runtime.Build(modelData);
            // Build View
            return new MapInstance(modelData, runtimeData);
        }
    }
}

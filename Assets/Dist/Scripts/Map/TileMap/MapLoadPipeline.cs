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

        public MapLoadPipeline(IMapSerializer serializer,
                               IMapModelBuilder domainBuilder,
                               IMapMapper mapper)
        {
            _serializer = serializer;
            _modelBuilder = domainBuilder;
            _mapper = mapper;
        }

        public IMapModelReadOnly LoadModel(string path)
        {
            // Deserialize JSON to DTO
            MapSaveJsonDto dto = _serializer.Read(path);
            // Map DTO to Domain Model
            IMapTilesReadOnly prepared = _mapper.ToPrepared(dto);
            // Build Runtime Data
            IMapModelReadOnly modelData = _modelBuilder.BuildRuntime(prepared);
            // Build View
            return modelData;
        }
    }
}

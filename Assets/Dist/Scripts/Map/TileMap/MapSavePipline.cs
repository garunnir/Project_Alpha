using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace IsoTilemap
{
    public class MapSavePipline
    {
        private readonly IMapSerializer _serializer;
        private readonly IMapModelBuilder _modelBuilder;
        private readonly IMapMapper _mapper;
        private readonly TileMapRuntime _runtime;

        public MapSavePipline(
            IMapSerializer serializer,
            IMapModelBuilder modelBuilder,
            IMapMapper mapper)
        {
            _serializer = serializer;
            _modelBuilder = modelBuilder;
            _mapper = mapper;
        }

        public void Save(string fullPath)
        {
            IMapTilesReadOnly mapData = _runtime.GetTiles();
            MapSaveJsonDto mapDatas = _mapper.FromPrepared(mapData);

            string json = JsonUtility.ToJson(mapDatas, true);

            File.WriteAllText(fullPath, json);

            Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapDatas.tiles.Count})");
        }
    }
}
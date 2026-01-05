using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace IsoTilemap
{
    public class MapSavePipline
    {
        private readonly IMapSerializer _serializer;
        private readonly IMapModelReadOnly _domainBuilder;
        private readonly IMapViewBuilder _viewBuilder;
        private readonly IMapMapper _mapper;

        public MapSavePipline(
            IMapSerializer serializer,
            IMapModelBuilder domainBuilder,
            IMapViewBuilder viewBuilder,
            IMapMapper mapper)
        {
            _serializer = serializer;
            _domainBuilder = domainBuilder;
            _viewBuilder = viewBuilder;
            _mapper = mapper;
        }

        public void Save(string fullPath)
        {
            IEnumerable<TileCellSnapshot> mapData = _domainBuilder.Tiles();
            foreach (var snapshot in mapData)
            {
                Debug.Log($"Snapshot Tile: Pos({snapshot.Identity.GridPos}), PrefabId({snapshot.Identity.PrefabId})");
            }
            MapSaveJsonDto mapDatas = _mapper.FromPrepared(mapData);

            string json = JsonUtility.ToJson(mapDatas, true);

            File.WriteAllText(fullPath, json);

            Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapDatas.tiles.Count})");
        }
    }
}
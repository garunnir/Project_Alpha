using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace IsoTilemap
{
    [DisallowMultipleComponent]
    public class TileMapSaver : MonoBehaviour
    {
    [SerializeField] IMapSerializer _serializer;
    [SerializeField] IMapModelBuilder _domainBuilder;
    [SerializeField] IMapViewBuilder _viewBuilder;
    [SerializeField] IMapMapper _mapper;
        [Header("Where to save/read the map file")]
        [SerializeField] private string fileName = "map01.json";      // 파일 이름
        //런타임에 사용한다.
        public void SaveMap()
        {
            var mapData = _domainBuilder.GetRuntimeData();

            MapSaveJsonDto mapDatas = _mapper.FromDomain(mapData.tiles);

            string json = JsonUtility.ToJson(mapDatas, true);

            string fullPath = GetFullPath();
            File.WriteAllText(fullPath, json);

            Debug.Log($"TileMap saved to: {fullPath} (tiles: {mapDatas.tiles.Count})");
        }
    }

}

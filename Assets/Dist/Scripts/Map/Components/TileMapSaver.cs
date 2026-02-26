using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
namespace IsoTilemap
{
    [DisallowMultipleComponent]
    public class TileMapSaver : MonoBehaviour
    {
    [SerializeField] IMapModel _model;
    [SerializeField] IMapModelBuilder _domainBuilder;
    [SerializeField] IMapMapper _mapper;

        [Header("Where to save/read the map file")]
        [SerializeField] private string fileName = "map01.json";      // 파일 이름
        //런타임에 사용한다.
        public void SaveMap()
        {
            new MapSavePipline(
                _model,
                _mapper
            ).Save(Path.Combine(Application.persistentDataPath, fileName));
        }
    }

}

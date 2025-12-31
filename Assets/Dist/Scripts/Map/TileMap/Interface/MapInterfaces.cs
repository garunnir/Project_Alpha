using System;
using System.Collections.Generic;
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
        IMapDomainReadOnly ToDomain(MapSaveJsonDto dto);
        MapSaveJsonDto FromDomain(IMapDomainReadOnly domain);
    }
    //맵 도메인 모델 빌더 담당
    public interface IMapDomainBuilder
    {
        TileMapRuntimeData BuildRuntime(IMapDomainReadOnly domainData);
        TileMapRuntimeData GetRuntimeData();
    }
    //맵 도메인 모델 읽기 전용 인터페이스
    public interface IMapDomainReadOnly
    {
        TileMapRuntimeData GetRuntimeData();
    }
//맵 뷰 빌더 담당
    public interface IMapViewBuilder
    {
        void Build(IMapDomainReadOnly runtimeData);
    }
}
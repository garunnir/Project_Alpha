using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace IsoTilemap
{
    /// <summary>
    /// DTO 로부터 모델을 생성하는 빌더 클래스
    /// 존재의의 : 상황에 따른 다양한 모델 빌드 방식이 필요할 때, IMapModelBuilder 인터페이스를 구현하여 여러 빌더 클래스를 만들 수 있습니다.
    /// </summary>
    public class TileMapModelBuilder : IMapModelBuilder
    {
        public IMapModel Build(MapModelDTO prepared)
        {
            TileMapModel data = new TileMapModel();
            data.Initialize(prepared);
            return data;
        }
        public IMapModel DefaultBuild(MapModelDTO prepared)//추후 맵 생성을 어떤식으로 할것인가에 따라 필요해질수 있음..
        {
            throw new NotImplementedException();
        }

    }
}



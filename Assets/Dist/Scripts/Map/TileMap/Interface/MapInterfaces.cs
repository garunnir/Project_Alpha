using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace IsoTilemap
{

        /// <summary>
        /// 맵 뷰 빌더 담당 초기화,실시간 뷰 구성
        /// </summary>
    public interface IMapViewBuilder
    {
        void Build(IMapModelReadOnly model);
        void Bind(IMapModelReadOnly runtime);
        void RefreshCell(Vector3Int cellPos, IReadOnlyList<TileData> tiles);
    }

    public interface IMapSerializer
    {
        MapSaveJsonDto Read(string path);
        void Write(string path, MapSaveJsonDto dto);
    }
    //맵 데이터 구조 변환 담당
    public interface IMapMapper
    {
        MapModelDTO ToPrepared(MapSaveJsonDto dto);
        MapSaveJsonDto FromPrepared(MapModelDTO prepared);
    }
    //맵 도메인 모델 빌더 담당
    public interface IMapModelBuilder
    {
        IMapModel Build(MapModelDTO prepared);
    }
    //맵 도메인 모델 읽기 전용 인터페이스
    public interface IMapModelReadOnly
    {
        public event Action<Vector3Int, List<TileData>> OnRuntimeDataChanged;
        public IReadOnlyList<TileData> GetOccludingWalls(Vector3Int playerCellPos);
        public IReadOnlyList<TileData> TilesSnapshot { get; }
        public bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tileList);
    }
    public interface IMapModel : IMapModelReadOnly
    {
        void SetTile(TileData tileDatas);
        void HideOcclusionTileWall(Vector3Int playerCellPos);
        public void Initialize(MapModelDTO prepared);
    }
/// <summary>
/// DTO는 어느 범위까지 데이터를 담아야 하는가?
/// - DTO는 데이터 전달을 위한 객체이므로, 필요한 데이터만 담는 것이 좋습니다. 너무 많은 데이터를 담으면 불필요한 복잡성이 생길 수 있습니다.
/// 같은 데이터지만 쓰기 좋은 형태로 변환해서 추가 프로퍼티로 제공하는것은 옳은지?
/// - DTO는 단순히 데이터를 담는 객체이므로, 변환된 형태의 데이터를 추가 프로퍼티로 제공하는 것은 괜찮습니다. 다만, DTO가 너무 많은 책임을 지지 않도록 주의해야 합니다. DTO는 데이터 전달에 집중하고, 복잡한 로직이나 변환은 별도의 서비스나 유틸리티 클래스에서 처리하는 것이 좋습니다.
/// 그렇다면 굳이 할 이유도 없다는 말처럼 들리는데
/// - DTO는 데이터 전달에 집중하는 것이 좋지만, 때로는 변환된 형태의 데이터를 추가 프로퍼티로 제공하는 것이 편리할 수 있습니다. 예를 들어, 원본 데이터에서 자주 사용되는 특정 형태의 데이터를 미리 계산하여 제공하면, 이를 사용하는 코드에서 반복적으로 변환 로직을 작성할 필요가 없어집니다. 다만, 이 경우에도 DTO가 너무 많은 책임을 지지 않도록 주의해야 합니다. DTO는 여전히 단순히 데이터를 담는 객체로 유지하면서, 필요한 경우에만 추가 프로퍼티를 제공하는 것이 좋습니다.
/// 그 적당함을 가늠하는것이 항상 어려운일이구만
/// - 네, DTO의 적절한 범위와 책임을 가늠하는 것은 항상 어려운 일입니다. 일반적으로는 다음과 같은 원칙을 따르는 것이 좋습니다:
/// 1. DTO는 단순히 데이터를 담는 객체로 유지한다.
/// 2. DTO에 포함되는 데이터는 필요한 최소한으로 제한한다.
/// 3. DTO에 복잡한 로직이나 변환을 포함시키지 않는다.
/// 4. 필요한 경우, 변환된 형태의 데이터를 추가 프로퍼티로 제공할 수 있지만, 이 경우에도 DTO가 너무 많은 책임을 지지 않도록 주의한다.
/// 이러한 원칙을 따르면서, 실제 프로젝트의 요구사항과 상황에 맞게 DTO의 범위와 책임을 조정하는 것이 중요합니다. 또한, 팀 내에서 DTO의 사용에 대한 명확한 가이드라인을 설정하는 것도 도움이 될 수 있습니다.
/// 확장성을 고려했을때 불필요하게 여러 형태의 프로퍼티로 만드는것도 별로 좋진 않겠는걸
/// - 네, 확장성을 고려할 때 불필요하게 여러 형태의 프로퍼티를 만드는 것은 좋지 않습니다. DTO는 단순히 데이터를 전달하는 역할을 해야 하며, 너무 많은 프로퍼티를 포함하면 유지보수가 어려워질 수 있습니다. 또한, 여러 형태의 프로퍼티가 존재하면, 이를 사용하는 코드에서 어떤 프로퍼티를 사용해야 하는지 혼란스러울 수 있습니다. 따라서, DTO에는 필요한 최소한의 데이터만 포함시키고, 복잡한 변환이나 로직은 별도의 서비스나 유틸리티 클래스에서 처리하는 것이 좋습니다.
/// DTO는 단순히 데이터를 담는 객체로 유지하면서, 필요한 경우에만 추가 프로퍼티를 제공하는 것이 좋습니다.
/// </summary>
    public sealed class MapModelDTO
    {
        public readonly IReadOnlyList<TileData> TilesData;

        public MapModelDTO(IMapModelReadOnly runtime)
        {
            TilesData = runtime.TilesSnapshot;
        }
        public MapModelDTO(IReadOnlyList<TileData> dto)
        {
            TilesData = dto;
        }
    }

}
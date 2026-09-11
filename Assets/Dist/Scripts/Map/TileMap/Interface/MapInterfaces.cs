using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;


namespace IsoTilemap
{
    /// <summary>셀 dict와 분리된 수직·수평 면 타일 레지스트리.</summary>
    public interface ITileFaceBinderReadOnly
    {
        /// <summary>내부 dict 직접 순회용. bake·집계는 <see cref="CopyWallFacesTo"/> / <see cref="CopyFloorFacesTo"/> 사용.</summary>
        IReadOnlyDictionary<WallEdgeKey, TileData> WallFaceIndex { get; }
        IReadOnlyDictionary<FloorFaceKey, TileData> FloorFaceIndex { get; }
        /// <summary>벽 면 타일 스냅샷. buffer를 비운 뒤 채웁니다.</summary>
        void CopyWallFacesTo(List<TileData> buffer);
        /// <summary>바닥 면 타일 스냅샷. buffer를 비운 뒤 채웁니다.</summary>
        void CopyFloorFacesTo(List<TileData> buffer);
        void AppendFacesAtCell(Vector3Int cell, List<TileData> appendTo);
        void AppendWallFacesAtCell(Vector3Int cell, List<TileData> appendTo);
        void AppendFloorFacesAtCell(Vector3Int cell, List<TileData> appendTo);
    }

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
        MapSaveJsonDto FromHierarchyViews(System.Collections.Generic.IEnumerable<TileView> views);
    }
    //맵 도메인 모델 빌더 담당
    public interface IMapModelBuilder
    {
        IMapModel Build(MapModelDTO prepared);
    }
    //맵 도메인 모델 읽기 전용 인터페이스
    public interface IMapModelReadOnly
    {
        public event Action<Vector3Int, IReadOnlyList<TileData>> OnRuntimeDataChanged;
        public event Action<IReadOnlyCollection<Vector3Int>> OnRuntimeBatchChanged;
        event Action<TileData> OnRuntimeTileAdded;
        event Action<TileData> OnRuntimeTileRemoved;
        ITileFaceBinderReadOnly FaceBinder { get; }
        /// <summary>walkable 셀 (x,cellY,z)에 발밑 Floor face가 있는지.</summary>
        bool CellHasWalkableFloor(int x, int cellY, int z);
        bool TryGetFloorFaceForWalkableCell(int x, int cellY, int z, out TileData face);
        /// <summary>셀에 놓인 타일 + 인시던트 EdgeWall·Floor face(렌더 갱신용).</summary>
        void GatherRenderableTiles(Vector3Int cellPos, List<TileData> buffer);
        /// <summary>점유 셀 + 면 타일의 시점 스냅샷. 읽기·집계 전용. 런타임 변경 시 <c>MarkTilesDirty</c> 후 갱신.</summary>
        public IReadOnlyList<TileData> TilesSnapshot { get; }
        public bool TryGetTiles(Vector3Int pos, out IReadOnlyList<TileData> tileList);
        /// <summary>점유·앵커 기준 셀 타일. hub가 있으면 topology, 없으면 <see cref="TryGetTiles"/> 폴백.</summary>
        bool TryGetCellTiles(int x, int z, int cellY, out IReadOnlyList<TileData> tileList);
        /// <summary>점유된 (x,z,y) 셀. hub가 있으면 topology, 없으면 앵커 dict 키 기준 폴백.</summary>
        IEnumerable<(int x, int z, int y)> EnumerateOccupiedCells();
        bool TryGetTileById(Guid tileId, out TileData tileData);
    }
    public interface IMapModel : IMapModelReadOnly
    {
        void SetTile(TileData tileDatas);
        void RemoveTile(TileData tileData);
        /// <summary>bake용 buildingId·roomId만 갱신하고 ID 인덱스를 동기화합니다.</summary>
        void PatchTileIdentity(Guid tileDefId, int buildingId, int roomId);
        /// <summary>런타임 타일 전체 스냅샷 순회. 콜백에서 <see cref="PatchTileIdentity"/> 등 수정 가능.</summary>
        void ForEachRuntimeTileMutating(Action<TileData> visit);
        /// <summary><see cref="TileIdentity"/>·배치가 바뀔 때. 토폴로지(방/건물) 갱신 포함.</summary>
        void ApplyTiles(IReadOnlyList<TileData> tiles);
        /// <summary>레거시 API. 프레젠테이션은 <see cref="TileViewPresentationApplier"/> 사용.</summary>
        void ApplyTileStates(IReadOnlyList<TileData> tiles);
        void HideOcclusionTileWall(Vector3Int playerCellPos);

        /// <summary>플레이어 월드 위치로 오클루전(셀 변경 시 BFS + 거리 occlusion) 갱신. 호환용 — evaluate 셀 자동 해석.</summary>
        void UpdateOcclusionFromPlayerWorld(Vector3 visibilityWorld, OcclusionProximitySettings settings);

        /// <summary>호환: floor Y만 지정. evaluate 셀은 <see cref="OccupiedCellCoord.ResolveFromWorld"/>.</summary>
        void UpdateOcclusionFromPlayerWorld(
            Vector3 visibilityWorld,
            int playerFloorCellY,
            OcclusionProximitySettings settings);

        /// <summary>
        /// SSOT: <paramref name="evaluationCell"/>는 <see cref="PlayerVisibilityWorldResolve"/>와 동일.
        /// room·야외 BFS는 이 셀만 사용 (<c>GetVisitedForWorld</c> 금지).
        /// </summary>
        void UpdateOcclusionFromPlayerWorld(
            Vector3 visibilityWorld,
            Vector3Int evaluationCell,
            OcclusionProximitySettings settings,
            System.Func<TileData, bool> occlusionTileVisible,
            Vector3Int playerFootprint);
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
        public MapModelDTO(IReadOnlyList<TileData> tiles)
        {
            TilesData = tiles;
        }
    }

}
# Progress Tracker

## Status: COMPLETED

## Completed
- [x] Map 폴더 컴파일 에러 수정 (2026-02-26)
  - [x] `MapInterfaces.cs` — `IMapViewBuilder`에 `RefreshCell` 추가
  - [x] `TileMapModel.cs` — `SetTile(TileData)` 구현, `TryGetTiles` 구현, `SetTiles` 구현
  - [x] `CachedTileMapRuntime.cs` — 생성자 추가, `SetTile(TileData)` 위임 추가
  - [x] `TileMapVisualizer.cs` — 생성자 NullRef 제거, `RefreshCell` 구현
  - [x] `TileMapSaver.cs` — 필드명 `_serializer` → `_model` 수정
  - [x] `TileMapSession.cs` — 신규 생성 후 제거됨 (IMapModel 중복 래퍼였음)
- [x] MVC 아키텍처 기반 타일 파이프라인 구조 확립
  - [x] Model 레이어: TileMapModel, CachedTileMapRuntime
  - [x] View 레이어: TileMapVisualizer
  - [x] Controller 레이어: TileMapController, TileMapLoader

## In Progress
- (없음)

## Remaining
- [ ] Unity Editor에서 실제 컴파일 에러 없음 최종 확인
- [ ] `TileMapLoader.LoadMapRuntime()` 실행 흐름 통합 테스트
- [ ] `TileMapController.FlushDirty()` → `RefreshCell` 경로 런타임 테스트
- [ ] Unity Inspector에서 `CharacterVisibilityBroadcaster._tileMapLoader` 참조 재연결 (TileMapSession → TileMapLoader로 변경됨)

## Blockers
(없음)

## Notes
- `TileMapModel.SetTile(Vector3Int, TileData)` 는 `private`으로 변경됨 — 외부에서 직접 호출 불가
- `CachedTileMapRuntime.OnRuntimeDataChanged` 이벤트는 현재 내부에서 발행하지 않음 (래퍼 구조상 _runtimeData 이벤트가 직접 전달되지 않음) — 필요시 추후 연결 검토
- `TileMapSession` 제거됨 (2026-02-26) — Inspector에서 이 컴포넌트를 참조하던 GameObject는 재설정 필요

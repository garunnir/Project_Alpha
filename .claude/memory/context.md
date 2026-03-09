# Context & Background

## Why We're Doing This
Building a Unity tile pipeline with MVC architecture to clearly separate data, logic, and rendering layers.
The goal is to improve maintainability and scalability, minimizing the impact on existing code when adding new features.

## Constraints
- Unity (specify version), C#
- MonoBehaviour allowed only in View/Controller layers — Model must be pure C# classes
- Dependency direction: View → Controller → Model (reverse direction strictly prohibited)
- Event/callback pattern: (e.g. C# event / Action — update to match current implementation)
- New file creation or structural changes require prior approval before proceeding
- Refactoring and new feature additions must not be mixed in the same task/commit

## Relevant References
- Current folder structure: (e.g. Assets/Scripts/Model, /View, /Controller)
- Core class list: (e.g. TileModel, TileView, TileController)
- Related Unity packages: (e.g. Tilemap, URP)

## Relevant References
- 핵심 클래스: `TileMapModel`, `CachedTileMapRuntime`, `TileMapVisualizer`, `TileMapController`, `TileMapLoader`, `TileMapSaver`
- 인터페이스: `IMapModel`, `IMapModelReadOnly`, `IMapViewBuilder`, `IMapSerializer`, `IMapMapper`, `IMapModelBuilder`
- 인터페이스 파일: `Assets/Dist/Scripts/Map/TileMap/Interface/MapInterfaces.cs`
- 컴포넌트 폴더: `Assets/Dist/Scripts/Map/Components/`
- 타일맵 폴더: `Assets/Dist/Scripts/Map/TileMap/`

## Decisions Log
| Decision | Reason | Date |
|----------|--------|------|
| MVC 아키텍처 채택 | 타일 데이터·렌더링·로직을 레이어별로 독립 변경 가능하게 함 | - |
| Model에 MonoBehaviour 금지 | Unity 라이프사이클 의존 제거, 테스트 가능성 확보 | - |
| View → Model 직접 접근 금지 | 단방향 데이터 흐름 강제, 결합도 감소 | - |
| `SetTile(TileData)` 인터페이스 단일화 | `Vector3Int` 별도 전달 제거 — `tileData.identity.GridPos` 사용으로 통일 | 2026-02-26 |
| `TileMapModel.SetTile(Vector3Int, TileData)` private으로 변경 | 내부 구현 세부사항 은닉, 인터페이스 일관성 유지 | 2026-02-26 |
| `CachedTileMapRuntime` 생성자에서 의존성 주입 | readonly 필드 초기화를 위해 생성자 패턴 사용 | 2026-02-26 |
| `TileMapSession` 제거 — `TileMapLoader.Model`로 통합 | `IMapSession`/`MapInstance` 래퍼 레이어가 `IMapModel` 중복이었음. `TileMapLoader`가 `public IMapModel Model`을 직접 노출 | 2026-02-26 |
| 변경 전 반드시 설계 의도 설명 요구 | 의도치 않은 구조 위반 방지 | - |
| 파이프라인 구성요소(`IMapSerializer`, `IMapModelBuilder`)를 `new`로 직접 생성 | 현재 교체 요구 없음. 교체 필요 시 ScriptableObject 전략 패턴으로 전환 예정 (인스펙터에서 구현체 드래그 교체 가능) | 2026-03-09 |

## Extension Directions
- **파이프라인 구성요소 교체**: `TileMapSerializer`, `TileMapModelBuilder` 등을 ScriptableObject로 만들면 인스펙터에서 JSON/Binary 등 구현체를 교체 가능. 현재는 단일 구현만 존재하므로 보류.
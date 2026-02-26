# Task Plan

## Goal
Build a Unity tile pipeline using MVC architecture that cleanly separates tile data, rendering, and control logic.
The pipeline should be extensible, testable, and easy to maintain as new tile types or behaviors are added.

## Design Decisions
- **MVC over monolithic scripts**: Keeps data (Model), display (View), and logic (Controller) independently changeable
- **Pure C# Models**: No MonoBehaviour in the Model layer to ensure testability and remove Unity lifecycle dependency
- **Unidirectional data flow**: View never accesses Model directly — all updates go through Controller via events/callbacks
- **Event-driven communication**: Loose coupling between layers using C# events or Actions (specify which)
- **Explicit approval before structural changes**: Prevents unintentional architecture violations during development

## Steps
- [x] Model 레이어 핵심 구조 정의 (TileMapModel, IMapModel, TileData)
- [x] Controller 로직 구현 (TileMapController — MarkDirty/FlushDirty 패턴)
- [x] View 레이어 구현 (TileMapVisualizer — Build/Bind/RefreshCell)
- [x] 레이어 간 이벤트 연결 (`OnRuntimeDataChanged` → View 구독)
- [x] MVC 경계 준수 검증 및 컴파일 에러 수정 (2026-02-26)
- [x] `TileMapSession` 제거 — 세션 래퍼 레이어 플래튼, `TileMapLoader.Model`로 통합 (2026-02-26)
- [ ] 각 레이어 독립 테스트 (관심사 분리 확인)

## Out of Scope
- No editor tooling or custom Inspector windows (unless explicitly requested)
- No procedural map generation logic at this stage
- No cross-layer dependency exceptions — MVC boundaries are non-negotiable
- No refactoring of existing code unless it directly blocks current task progress
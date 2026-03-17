# Internal — 핵심 데이터 구조체

Unity 비의존 순수 구조체. 시스템 전체에서 공유.

```mermaid
classDiagram
    class TileData {
        +Guid tileDefId
        +TileState state
        +TileIdentity identity
    }
    class TileState {
        +bool isHiddenCharacter
    }
    class TileIdentity {
        +string PrefabId
        +Vector3Int GridPos
        +Vector3Int sizeUnit
        +byte tileType
    }

    TileData *-- TileState : 가변 런타임 상태
    TileData *-- TileIdentity : 불변 정의
```

---

## 필드 메모

| 필드 | 설명 |
|------|------|
| `tileDefId` | Guid — 런타임 바인딩 키. 저장하지 않으며 로드 시 `Guid.NewGuid()`로 생성. `TileMapVisualizer`가 TileData → TileView를 찾을 때 사용 |
| `isHiddenCharacter` | BFS 오클루전으로 숨김 처리됨 |
| `PrefabId` | `TilePrefabDB` 딕셔너리 키 |
| `sizeUnit` | 점유 그리드 크기 (예: `2,1,1`) |
| `tileType` | `1`=Floor, `2`=Wall, `3`=Obstacle |

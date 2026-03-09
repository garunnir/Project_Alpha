# Map System — 전체 개요

패턴: MVC + Pipeline + Observer
세부 문서: [COMPONENTS.md](Components/COMPONENTS.md) | [DATA.md](Internal/DATA.md) | [TILEMAP.md](TileMap/TILEMAP.md)

---

## 의존성 다이어그램

```mermaid
graph TD
    subgraph Components["Components (MonoBehaviour)"]
        Loader[TileMapLoader]
        Saver[TileMapSaver]
        Controller[TileMapController]
    end

    subgraph Pipeline["TileMap / Pipeline"]
        LoadPipe[MapLoadPipeline]
        SavePipe[MapSavePipline]
    end

    subgraph Serialization["TileMap / Serialization"]
        Serializer[TilemapSerializer]
        Mapper[TileMapDtoMapper]
    end

    subgraph Model["TileMap / Model"]
        Builder[TileMapModelBuilder]
        TileModel[TileMapModel]
        Cached[CachedTileMapRuntime]
    end

    subgraph View["TileMap / View"]
        Visualizer[TileMapVisualizer]
        Factory[TileObjFactory]
        PrefabDB[TilePrefabDB]
        TileView[TileView]
    end

    subgraph DTO["TileMap / DTO"]
        JsonDto[MapSaveJsonDto]
        TileSave[TileSaveData]
    end

    subgraph Interfaces["TileMap / Interface"]
        IModel[IMapModel]
        IView[IMapViewBuilder]
        ISerial[IMapSerializer]
        IMap[IMapMapper]
        IBuilder[IMapModelBuilder]
    end

    subgraph Internal["Internal"]
        TileData[TileData / TileIdentity / TileState]
    end

    %% Load flow
    Loader --> LoadPipe
    LoadPipe --> Serializer
    LoadPipe --> Mapper
    LoadPipe --> Builder
    Serializer --> JsonDto
    JsonDto --> TileSave
    Mapper --> TileData
    Builder --> TileModel

    %% Save flow
    Saver --> SavePipe
    SavePipe --> Mapper
    SavePipe --> Serializer

    %% Controller flow
    Controller --> IView
    Controller --> IModel

    %% Model → View (Observer)
    TileModel -- "OnRuntimeDataChanged" --> Visualizer
    Visualizer --> Factory
    Factory --> PrefabDB
    Factory --> TileView
    Cached --> TileModel

    %% Interface bindings
    TileModel -.implements.-> IModel
    Visualizer -.implements.-> IView
    Serializer -.implements.-> ISerial
    Mapper -.implements.-> IMap
    Builder -.implements.-> IBuilder
```

---

## 데이터 흐름 요약

```mermaid
sequenceDiagram
    participant F as JSON File
    participant P as MapLoadPipeline
    participant M as TileMapModel
    participant V as TileMapVisualizer

    F->>P: Read (TilemapSerializer)
    P->>P: ToPrepared (TileMapDtoMapper)
    P->>M: Build (TileMapModelBuilder)
    M-->>V: Bind (OnRuntimeDataChanged 구독)
    M->>V: Build (초기 GameObject 생성)
    Note over V: TileObjFactory → TileView

    Note over M,V: 런타임 수정
    M->>M: SetTile()
    M-->>V: OnRuntimeDataChanged
    V->>V: RefreshCell → TileView.UpdateTile()
```

---

## 레이어별 역할

| 레이어 | 위치 | 역할 |
|--------|------|------|
| Entry | `Components/` | 씬에 붙는 MB, 파이프라인 조합 |
| Data | `Internal/` | 순수 구조체 (Unity 비의존) |
| Interface | `TileMap/Interface/` | 레이어 간 계약, 결합도 최소화 |
| DTO | `TileMap/DTO/` | JSON 직렬화 전용 포맷 |
| Model | `TileMap/` | 런타임 상태, BFS 오클루전 |
| View | `TileMap/` | GameObject 생성·갱신 |
| Pipeline | `TileMap/` | 단계 조합 (교체 가능) |

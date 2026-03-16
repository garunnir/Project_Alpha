# Map System — 전체 개요

패턴: MVC + Pipeline + Observer
세부 문서: [COMPONENTS.md](Components/COMPONENTS.md) | [DATA.md](Internal/DATA.md) | [TILEMAP.md](TileMap/TILEMAP.md)

---

## 의존성 다이어그램

```mermaid
graph TD
    subgraph Components["Components (MonoBehaviour)"]
        Manager[TileMapManager]
        Loader[MapFileLoader]
        Saver[MapFileSaver]
        Controller[TileMapController]
        Manager --> Loader
        Manager --> Saver
        Manager --> Controller
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
    participant Mgr as TileMapManager
    participant L as MapFileLoader
    participant P as MapLoadPipeline
    participant M as TileMapModel
    participant V as TileMapVisualizer
    participant C as TileMapController

    Mgr->>L: Load()
    L->>P: LoadModel(path)
    P->>P: Read → ToPrepared → Build
    P-->>L: IMapModel + IMapViewBuilder
    L-->>Mgr: Model, ViewBuilder
    Mgr->>C: Init(model, viewBuilder)
    C->>V: Bind(model) — OnRuntimeDataChanged 구독
    C->>V: Build(model) — 초기 GameObject 생성
    Note over V: TileObjFactory → TileView

    Note over M,V: 런타임 수정
    M->>M: SetTile()
    M-->>V: OnRuntimeDataChanged
    V->>V: RefreshCell → TileView.UpdateTile()

    Note over Mgr: 저장 요청
    Mgr->>Mgr: Save()
    Mgr->>Mgr: _saver.Save()
```

---

## 레이어별 역할

| 레이어 | 위치 | 역할 |
|--------|------|------|
| Coordinator | `Components/` | `TileMapManager` — 생명주기 조율, wiring |
| Entry | `Components/` | `MapFileLoader`, `MapFileSaver`, `TileMapController` |
| Data | `Internal/` | 순수 구조체 (Unity 비의존) |
| Interface | `TileMap/Interface/` | 레이어 간 계약, 결합도 최소화 |
| DTO | `TileMap/DTO/` | JSON 직렬화 전용 포맷 |
| Model | `TileMap/` | 런타임 상태, BFS 오클루전 |
| View | `TileMap/` | GameObject 생성·갱신 |
| Pipeline | `TileMap/` | 단계 조합 (교체 가능) |

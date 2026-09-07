# Map Liquid — 액체 시뮬레이션 SSOT

> LLM/에이전트용 Dist 맵 액체 SSOT. 스크립트 수정 전 이 문서를 읽는다.
> 진입: [`docs/map/SYSTEM.md`](SYSTEM.md) · 경로: `Assets/Dist/Scripts/Map/Liquid/`

## 개요

BN(Cataclysm: Bright Nights)의 셀당 1000L 용량·ml 콘텐츠와, Minecraft류 셀룰러 오토마타 흐름을 결합한
compressible-water CA. `SHALLOW_WATER`/`DEEP_WATER` 정적 바닥 태그를 동적 ml 레이어로 흡수한다.

### 물은 타일이 아니다 (구조 계약)

물은 `TileData` 모델에 **진입하지 않는다**. 씬의 물 프리팹은 `LiquidAuthoringView`(저작 마커)이고,
JSON에서는 `liquidAuthoringFaces` 별 레이어로만 왕복한다. 이 한 줄이 아래를 예외 코드 없이 보장한다:

| 결과 | 이유 |
|------|------|
| 가려짐(캐릭터 오클루전·구조물 숨김) 대상 아님 | `TileViewPresentationApplier`는 `TileView`만 본다 — 물에는 `TileView`가 없다 |
| 충돌·논리 바닥 아님 | `FloorMapIndex`가 물 face를 모르므로 점유·`CellHasFloor` 입력에 없다 |
| building·space bake 오염 없음 | bake 입력이 `TileData`뿐이다 |
| Play에서 타일 메시와 수면이 겹치지 않음 | 마커는 Play에 스폰되지 않는다 (`LiquidAuthoringSceneSpawner`는 에디터 로드 전용) |

뷰 계층: `MapPlacedView`(그리드 앵커·pose·기즈모) ← `TileView`(구조 타일·프레젠테이션) / `LiquidAuthoringView`(물 마커).
공통 부모는 배치 편의만 공유하고 프레젠테이션·충돌 계약은 갖지 않는다.

## 셀 표현

`MapLiquidCell` (`Assets/Dist/Scripts/Map/Liquid/MapLiquidCell.cs`):

- `byte Level` — 대략치, 렌더 LOD·직렬화 압축용
- `ushort RemainderMl` — 정밀 잔여값
- `int EffectiveMl => Level * MlPerLevel + RemainderMl` — **flow 연산은 이 값만 사용**
- `short TempDeciC` — 자체 온도(0.1 °C 단위). `IsSolid`가 타입별 어는점과 비교한다

상수 (`MapLiquidConsts.cs`):

| 상수 | 값 | 의미 |
|------|-----|------|
| `DefaultMaxVolumeMl` | 1,000,000 (BN 1000L) | 셀당 최대 ml (현재 전 셀 공통 — terrain별 bake는 향후 과제) |
| `MaxLevel` | 255 | byte 표현 상한 |
| `MlPerLevel` | ≈3921 | `DefaultMaxVolumeMl / MaxLevel` |
| `MinFlowMl` | ≈490 | 이 이하 diff는 흐르지 않음 — 진동·튐·폭주 방지의 단일 게이트 |
| `OverCompressMl` | ≈196 | 수직 압축 시 아래 칸의 여유. 중력 목표(1번)와 수직 탈출 임계(3번)가 **공유하는 상한** — § 확산의 유한성 |
| `MaxUpdatesPerTick` | 512 | `WorldClock.MinuteChanged` 1회당 처리할 dirty 셀 상한 |
| `DeciCPerC` | 10 | 온도 저장 단위. `short`로 ±3276.7 °C — 용암까지 커버 |
| `DefaultAmbientDeciC` | 200 (20.0 °C) | `MapLiquidAmbient.TempCProvider` 미주입 시 기온 |
| `MinTempStepDeciC` | 2 (0.2 °C) | 이웃·대기 평균과 이만큼 차이나지 않으면 무동작 — 열 확산판 정지 게이트 |
| `ThermalRelaxDivisor` | 2 | 한 틱에 평균과의 차이 1/2만 이동 |
| `MaxThermalUpdatesPerTick` | 512 | 틱당 thermal dirty 셀 상한 |
| `AmbientResampleStepDeciC` | 5 (0.5 °C) | 기온이 이만큼 움직였을 때만 노출면을 재표집 |
| `MinSolidSupportMl` | cap/2 | 고체가 위 셀의 바닥이 되기 위한 최소 보유량 — 살얼음 제외 |

## Flow 알고리즘 (`MapLiquidFlowSolver.cs`)

`WorldClock.MinuteChanged` 1회당 dirty 큐에서 최대 `MaxUpdatesPerTick`개를 pop해 처리한다.
**고체 셀은 즉시 반환**한다 — 얼음은 흐르지 않고, 받지도 않는다. 순서:

0. **OutOfMap** — `!hub.IsInMapBounds(self)`이면 ml 제거 후 return (저장 `mapBounds` SSOT)
1. **중력** — self에 바닥이 없으면(`!hub.CellHasFloor(self)`) **AirGap(점유 없음) 포함** 아래 칸과 stable-state까지 채움. below는 bounds 안·`CanHoldLiquid`(얼음·`BlocksOccupiedCells` 제외)이어야 함
2. **수평 equalize** — 4방향. 대상은 `IsHorizontalFlowTarget`: bounds XZ 안 + `CellHasOccupancy` + `CanHoldLiquid` (AirGap으로 옆 확산 금지)
3. **수직 탈출** — 2번에서 옮길 곳이 없고 압축 상한 초과 시, 위 칸이 `IsHorizontalFlowTarget`이면 초과분 이동
4. 차단: `MapTopologyGridSegment`와 동일 — 수평 한 스텝에서 `CellHasSolidWall`(목표 셀 `BlocksOccupiedCells`) + `TryGetEdgeBetween`·`EdgeBlocksPassage`(EdgeWall). 수직은 `CellHasFloor`(논리 바닥). `mapBounds`(맵 밖). **엣지 타일이 없어도** 점유 벽(`ThickWall` 등)만으로 pit 테두리를 만들면 `CellHasSolidWall`로 막힌다.

**거절이 없는 이유**: 오픈 지형에서 위 칸은 거의 항상 열려 있으므로 3번이 항상 탈출구를 제공한다. 완전 밀폐(위도 막힘)는 지형이 아니라 컨테이너(아이템/탱크) 정의의 몫이며, 그 경우는 `MapLiquidMlBridge.Pour` 호출 이전에 소비자가 걸러야 한다.

### 확산의 유한성

- 공간: 총 물량 `V`가 `N`칸에 퍼지면 칸당 `V/N`. `V/N <= MinFlowMl`이 되면 그 경계에서 정지 → `N_max ≈ V / MinFlowMl`
- 시간(수평): 2번의 이동은 `diff / 4`뿐이라 `|diff|`를 줄이는 방향으로만 발생하고 오버슈트가 없다(단조감소) → 유한 스텝 내 정지
- 시간(수직): 1번과 3번은 서로 반대 방향으로 물을 옮기므로 단조감소 논증이 통하지 않는다. 대신 **두 단계가 아래 칸 상한을 같은 값으로 공유**해 고정점을 만든다:
 - 1번의 목표 `StableBelowMl`은 최대 `capMl + OverCompressMl`
 - 3번은 `capMl + OverCompressMl` **초과분만** 위로 보냄
 - → 아래 칸이 상한 이하면 3번이 쉬고, 상한을 넘으면 3번이 상한까지 내린 뒤 1번의 `moveDown`이 0 이하가 되어 쉰다. 왕복 불가.
 - 3번째 분기(`total >= 2*capMl + OverCompressMl`)는 위 칸이 이미 `capMl` 이상이라 `room == 0`으로 3번이 진입만 하고 아무것도 옮기지 않는다.

**두 상한을 어긋나게 바꾸지 말 것.** 3번의 임계를 `capMl`로 낮추면 1번이 올린 압축분을 3번이 즉시 내리는 무한 왕복이 되고, dirty 큐가 비지 않아 § 정적 셀 무연산 보증까지 깨진다. 현재 `OverCompressMl`(≈196) < `MinFlowMl`(≈490)이라 그 왕복이 게이트에 우연히 걸리지만, `OverCompressMl`을 원전 수준(정원의 2%)으로 올리면 게이트가 막지 못한다 — 정지 보증은 상한 일치에서만 나온다.

### 정적 셀 무연산 보증 (바다맵 폭주 방지)

세 곳에서 명시적으로 지켜야 한다:

1. **시드는 dirty를 유발하지 않는다** — `MapLiquidOverlay.SeedFromAuthoringFaces`/`SeedEffectiveMl`은 `MarkDirty`를 호출하지 않음. 균일한 정지 바다는 시드 직후 dirty 큐가 비어 있다.
2. **FlowSolver는 순수 반응형** — `ProcessDirty`는 큐 pop만 한다. 전체 overlay를 훑는 폴링 로직 금지. dirty 진입점은 `MarkDirty` 호출(흐름 발생 시 이웃, `MapLiquidMlBridge.Pour/Draw`)뿐.
3. **렌더러/쿼리는 좌표 단건 조회만** — `MapLiquidQuery`는 전체 순회 API를 제공하지 않는다. `MapLiquidSurfaceRenderer`도 오버레이를 매 프레임 순회하지 않고, `CellChanged` 통지를 받은 청크만 다시 굽는다(§ 수면 렌더러).
4. **맵 밖 무한 확산 차단** — 저장 시 bake된 `mapBounds`(XZ 직육면체 + `minY`만, maxY 없음). `TileMapCacheHub.IsInMapBounds` / `IsInMapBoundsXZ`가 OutOfMap을 판정한다. **AirGap**(bounds 안·점유 없음)과 **OutOfMap**(bounds 밖)을 구분 — 점유(`CellHasOccupancy`)는 막힘이 아니라 수평 equalize 대상 여부. 구 JSON(`hasMapBounds=false`)은 로드 시 `MapBoundsBake` fallback.
5. **열 확산도 같은 계약** — `MapLiquidThermalSolver`는 별 dirty 큐를 pop만 한다. 평형 셀은 `MinTempStepDeciC` 게이트에서 잘려 자신·이웃을 재등록하지 않으므로, 기온과 평형인 바다는 열 확산 비용도 0이다. 유일한 O(액체 셀) 경로는 `MarkAmbientBoundaryDirty`이며 `AmbientResampleStepDeciC` 임계를 통과할 때만 실행된다(매 틱 아님).

## 상변화 (`MapLiquidThermalSolver.cs` · `MapLiquidTypeProps.cs`)

액체는 자체 온도를 갖고, 타입별 어는점 이하로 내려가면 **고체**가 된다.

| 항목 | 계약 |
|------|------|
| 어는점 SSOT | `MapLiquidTypeProps` — `water = 0 °C`. **미등록 typeId는 `short.MinValue`로 폴백해 절대 얼지 않는다** (새 액체를 추가해도 등록 전에는 조용한 오작동이 없다) |
| 확산 규칙 | 6방향 이웃(액체만) + 대기 노출면의 평균으로 relax: `delta = (평균 - self) / ThermalRelaxDivisor` |
| 대기 경계조건 | 위 셀이 비어 있고 그 경계가 열려 있으면 그 셀에 기온이 걸린다 |
| 기온 공급 | `MapLiquidAmbient.TempCProvider` 훅. `Dist.Map`은 날씨 어셈블리를 참조하지 않으므로 `MapLiquidAmbientService`(DistScript)가 `WorldWeatherHost` + `WorldClock` + `WeatherExposure`로 주입한다 — `MapClockSnapshot`과 같은 패턴 |
| 정지 | 평균과의 차이가 `MinTempStepDeciC` 미만이면 무동작. 평균이 결합 수로 희석되므로 평형 오차 상한은 `MinTempStepDeciC × 결합 수`(물 이웃 2 + 대기 1이면 약 0.3 °C) |
| 틱 순서 | `SyncAmbient` → 열 확산 → 흐름. 상 교차가 flow dirty를 넣으므로 **열이 먼저**여야 같은 틱에 해동이 흐름으로 이어진다 |

### 고체의 결과

| 소비처 | 동작 |
|--------|------|
| `MapLiquidFlowSolver` | 고체는 흐르지 않고(즉시 반환), 이동 목표에서도 제외된다(`IsTargetEligible`) |
| `MapTopologyQuery.CellHasFloor` | 아래 셀의 고체가 `MinSolidSupportMl` 이상이면 **바닥이 있다**고 답한다 → 얼음 위를 걷는다 |
| 해동 | 상 교차 시 자신·이웃을 flow dirty로 넣어 흐름이 자동 재개된다 (반응형 솔버라 명시적 wake-up이 필요하다) |

**얼음 지지를 `FloorMapIndex`에 넣지 말 것.** 그쪽 `CellHasFloor`는 building·space bake와 가려짐의 입력이라
얼음을 주입하면 방 판정·구조물 숨김까지 오염되고, `SyncOccupancyForCell`은 실제로 전체 리빌드라 상변화마다
호출할 수 없다. 합성 지점은 이동·지각 seam인 `IMapTopologyQuery` **한 곳**이다.

**advection 없음(문서화된 한계):** 액체가 이동해도 온도는 따라가지 않는다. 이동 후 relax로 수렴하므로
정지 상태의 결과는 같고 과도 구간만 다르다.

## ml ↔ 셀 (`MapLiquidMlBridge.cs`)

비대칭 규칙, 항상 플레이어 손해 방향:

- **Pour**: 요청 ml **전액**이 셀에 반영, 호출부는 반환값(=요청값) 전량을 인벤에서 차감. cap 초과분은 소멸이 아니라 `MarkDirty`로 위임되어 다음 틱부터 FlowSolver가 이웃/위로 전달.
- **Draw**: `Min(요청, 셀 보유량)`만 정확히 지급, 낭비 없음.

## 수면 렌더러 (`MapLiquidSurfaceRenderer` / `MapLiquidChunkMesher`)

경로: `Assets/Dist/Scripts/Map/Liquid/`. 셰이더 `Assets/Dist/Visual/View/Shaders/Liquid/MapLiquidSurface.shader`, 머티리얼 `Assets/Dist/Resources/Map/MapLiquidSurface.mat`. 상수 SSOT는 `MapLiquidRenderConsts`.

머티리얼이 `Resources` 아래 있는 이유: `MapLiquidHost`는 씬에 배치되지 않고 `TileMapManager.SetupMapLiquid`이 `AddComponent`로 만든다. 따라서 Inspector 참조가 비어 있는 것이 기본값이고, 그대로 두면 셰이더가 빌드에 포함되지 않는다. 해석 순서는 **Inspector → `Resources.Load` → `Shader.Find`**.

### 통지 계약

`MapLiquidOverlay`가 두 이벤트를 낸다. sim의 dirty 큐와는 **별개**다.

| 이벤트 | 발생 | 렌더러 반응 |
|--------|------|-------------|
| `CellChanged(cell)` | `AddEffectiveMl` (증감·제거 전부), `RaiseCellChanged`(상 교차) | 해당 청크를 dirty로. 경계 셀이면 맞닿은 청크, 모서리 셀이면 대각 청크까지 (코너를 공유하므로) |
| `BulkChanged` | `Clear` / `LoadFromDto` / `SeedFromAuthoringFaces` 완료 | 전체 무효화 후 청크 목록 재구성 |

시드는 셀 단위로 통지하지 않는다(§정적 셀 무연산 보증 1항 유지). 바다맵 시드에서 수십만 건 통지가 나가지 않게 `SeedFromAuthoringFaces`가 끝난 뒤 `BulkChanged` 1회만 낸다.

온도 변화 자체는 통지하지 않는다 — **상 교차(액체↔고체)에서만** `RaiseCellChanged`를 낸다. 매 틱 온도마다
통지하면 평형으로 가는 동안 리메시가 폭주한다.

### 이음매 없는 연결

수면 높이는 셀이 아니라 **격자 코너**에서 결정된다. 코너 값은 그 코너를 공유하는 4개 셀의 평균이고, 이웃 셀을 오버레이에서 직접 읽으므로 청크 경계 밖 셀도 같은 값이 나온다.

| 상황 | 처리 |
|------|------|
| 이웃 수위가 다름 | 코너 평균으로 경사 — 계단 없음 |
| 물가(마른 이웃) | 마른 이웃을 0으로 쳐 수면이 내려앉음. 색은 젖은 이웃 평균만 |
| 위 칸에도 물(잠긴 셀) | 코너 높이 1.0. **측면만** 천장까지(`SideSurfaceLift`) |
| 물끼리 맞닿은 면 | 측면 생략. 이웃 `EffectiveFill01` ≥ 자신×`SideWallConnectMinRatio01`일 때만 연결 |

파도·폼 노이즈 UV는 **월드 XZ**라 패턴도 경계에서 이어진다.

### 정점 색 계약 (`MapLiquidChunkMesher` → 셰이더)

| 채널 | 의미 |
|------|------|
| `r` | `depth01` — 젖은 이웃 기준 Fill01 평균 |
| `g` | `foam01` — 마른 이웃 비율. 물가 폼 밴드 |
| `b` | `isTop` — 1 = 수면(윗면), 0 = 측면 |

### 렌더 경로

- **씬 깊이(`_CameraDepthTexture`)**: URP `Require Depth Texture` ON. 수면 윗면에서 **바닥·타일이 수면보다 뒤**일 때만 해안 폼(`sceneEye > fragEye`). 수면 위 오브젝트(앞)는 폼 0.
- Opaque 텍스처(refraction)는 쓰지 않는다.
- 셰이더 시간은 `_MapLiquidTime` 전역 프로퍼티. 렌더러가 `TimeScaleService.TimeNow(TimeScaleChannel.World)`로 매 프레임 채워, 배속·정지가 파도에 반영된다(`Time.timeScale` 미사용).
- 그리기는 청크마다 `MeshRenderer` 뷰(표준 URP 투명 큐). `Graphics.RenderMesh`·커스텀 Renderer Feature는 intermediate RT와 합성이 어긋날 수 있어 쓰지 않는다.
- 청크 분할 SSOT는 `TileMapChunkStreamer.ChunkSize`이며, 스트리밍이 없으면 `MapLiquidRenderConsts.FallbackChunkSize`.
- **타일(`SpriteUV4Point`)** — `AlphaTest` opaque cutout, `ZWrite On`. 캐릭터 가림은 `_CharacterOcclusion` 화면 디더 디졸브(`clip`). 타일은 depth buffer·`_CameraDepthTexture`에 기록된다.
- **투명 정렬** — **물** 등 투명 drawable만 `IsoDepthSortKey` + `IsoVisibleDepthSortRegistry`. 타일은 opaque depth가 SSOT.
 - `RenderQueue` 승격 금지 — 위층 타일까지 덮는다.
 - `TransparencySortMode.CustomAxis` **금지** — bounds 중심 dot은 바닥/벽 그리드 키와 어긋난다. Y축 단독 `(0,1,0)`도 동층 동점 회귀. **물** 투명 정렬 SSOT는 `IsoDepthSortKey` + `IsoVisibleDepthSortRegistry`.

### 비용 계약

| 항목 | 계약 |
|------|------|
| 매 프레임 순회 | **로드된 청크**만(`CollectLoadedChunks` ∩ 물 청크). 맵 전체 물 청크 수와 무관 |
| 층 스캔 범위 | 청크마다 **자기 Y 범위**만. 리메시 때 액체가 남아 있는 범위로 다시 좁힌다 (삼각형 유무가 아니라 **액체 유무** 기준 — 사방이 물인 내부 층은 노출면이 0개라 삼각형도 0개다) |
| 빈 층 | 셀 수만큼의 조회 후 즉시 스킵 — 코너 계산·정점 생성 없음 |
| 리메시 예산 | 신규 `MaxChunkBuildPerFrame`, 갱신 `MaxChunkRemeshPerFrame`. 갱신은 FIFO 큐라 starvation 없음 |
| 대량 교체 | `BulkChanged`가 한 프레임에 여러 번 와도 재구성은 1회로 합친다 |

### 배선

`TileMapManager.SetupMapLiquid()`가 `BindMapContext` → `BindRenderContext` → `LoadFromDto` 순으로 호출한다. **`BindRenderContext`가 `LoadFromDto`보다 먼저**여야 로드 시 발생하는 `BulkChanged`를 렌더러가 받는다. 머티리얼은 `MapLiquidSurfaceRenderer`의 Inspector에 지정한다(비우면 `Shader.Find` 폴백 — 에디터 전용이므로 빌드에서는 반드시 지정).

## 에디터 저작 (물 마커)

에디터에는 `MapLiquidHost`가 없으므로 **물 프리팹을 깔고 Save**하는 것이 liquid 저작 경로다.
물 프리팹에는 `TileView`가 아니라 `LiquidAuthoringView`가 붙어 있어, 배치 조작감은 floor face와 같지만
타일 모델에는 들어가지 않는다.

| 단계 | 동작 |
|------|------|
| 1 | `TileMapManager` → Load Editor — 기존 `liquidAuthoringFaces`가 `LiquidAuthoringSceneSpawner`로 마커로 복원된다 |
| 2 | **`Liquid/Water`** 프리팹을 바닥 +Y 면에 배치 (권장). 스냅은 `FloorFacePicker`이므로 floor face와 동일 |
| 3 | `MapFileSaver` → **Save Map To JSON** |
| 4 | 마커 → `liquidAuthoringFaces`(앵커 = `CellBelow`), 이어서 `MapLiquidAuthoringBake`가 walkable 셀(앵커 y+1) `liquidCells`로 bake |

**에디터 표시:** sim 없이 `MapLiquidAuthoringPreviewRenderer`가 씬 `LiquidAuthoringView`를 cap-full synthetic overlay로 시드한 뒤 **`MapLiquidChunkMesher`(Play와 동일)** 로 청크 단일 mesh를 굽는다. 인접 full 셀은 내부 면을 생략해 한 덩어리로 이어진다. 마커 큐브 mesh는 프리뷰 활성 시 숨긴다. Play에서는 마커가 스폰되지 않고 `MapLiquidSurfaceRenderer`만 본다.

**SO:** `Assets/Dist/SOData/Tile/Liquid/Water.asset` — `category: Liquid`, flags `WATER`+`FISHABLE`, `placementSlot: HorizontalFace`, 충돌 전부 off, `providesLogicalFloor: 0`.

**프리팹:** `Assets/Dist/Visual/Prefabs/MapTiles/Liquid/Water.prefab` — `LiquidAuthoringView` + 1×1 큐브 저작 마커(`Grp_TP Offset`/`Grp_Rotation`/`Renderer`). Tile Palette `Liquid/Water`만 사용한다. (구 `LiquidMarker/WaterMarker`는 제거됨.)

**레거시 prefabId:** `Floor/ShallowWater` · `Floor/DeepWater`는 BN JSON·구 맵 호환을 위해 DB에 남긴다. bake·시드는 동일(`DefaultMaxVolumeMl`). 신규 저작은 `Liquid/Water`만 쓴다.
시드 온도는 그 셀의 기온(`MapLiquidAmbient`)이라, 추운 맵은 로드 즉시 얼어 있다.

**우선순위 (저장):** Play `MapLiquidHost` → 에디터 물 저작 면 bake → 디스크 liquidCells 계승.  
`liquidAuthoringFaces`가 **비어 있으면** bake하지 않고 기존 `liquidCells`를 계승한다(Play에서 Draw로 비운 웅덩이가 다시 차지 않게).

**저작 면의 진실원:** 에디터 저장은 씬 마커가 이기고(빈 리스트도 "물 없음" 확정으로 존중), Play 저장은
씬을 읽지 않으므로(`TileMapDtoMapper.FromPrepared`가 `null`을 쓴다) 디스크 값을 계승한다.
`MapSaveLayerCarryOver.CarryLiquidAuthoring`의 `null` = "미지정" 규약이 이 구분의 전부다.

**레거시 승격(one-way):** 구 JSON은 물이 `floorFaces`에 Floor 타일로 들어 있다. `TileMapSerializer.Read`가
로드 경계에서 `MapLiquidAuthoringBake.PromoteLegacyFloorFaces`로 한 번 저작 레이어로 옮기므로,
이후 경로(DtoMapper·시드·bake·마커 복원)는 `liquidAuthoringFaces`만 본다. `TileMapDtoMapper`에도 방어 게이트가
남아 있어 승격을 지나친 물 face는 타일이 되지 않고 경고로 남는다.

## 저장 (`MapLiquidCellSaveData` / `MapSaveJsonDto`)

```json
"liquidAuthoringFaces": [{ "x":0,"y":0,"z":0, "face":0, "prefabId":"Liquid/Water" }],
"liquidCells": [{ "x":0,"y":1,"z":0, "typeId":"water", "level":5, "remainderMl":250, "tempDeciC":200 }],
"hasLiquidSnapshot": true,
"hasLiquidTemperature": true
```

- `hasLiquidSnapshot == true` → 저장된 상태만 신뢰, 재시드 안 함(플레이어가 비운 웅덩이가 다시 안 참)
- `false`(레거시/최초 로드) → `SeedFromAuthoringFaces`로 1회 시드(Shallow/Deep **둘 다** `capMl` 가득)
- `hasLiquidTemperature == false` → 저장된 `tempDeciC`를 **버리고** 기본 기온으로 초기화한다. 구 JSON의 `0`은
 물의 어는점과 겹쳐 그대로 읽으면 바다 전체가 얼어버린다 — 이 플래그가 "누락"과 "0 °C"를 가르는 유일한 수단이다
- `EffectiveMl == 0` 셀은 저장 시 제외(스파스 유지)

## 조회 (`MapLiquidQuery.cs`)

`GetEffectiveMl(cell)` / `Fill01(cell)` / `TryGetTypeId(cell)` / `HasAnyLiquid(cell)` / `IsSolid(cell)` / `ProvidesSolidSupport(cell)` — 전부 좌표 단건 조회. `MapLiquidHost.Runtime`이 없으면 0/false 반환(안전 폴백).

### 국소 충만도 vs 컬럼 수심

`Fill01`만으로는 **몇 셀 깊이인가**를 알 수 없다. 한 셀은 `capMl + OverCompressMl`에서 클램프되므로 얕은 물 한 겹과 깊은 분지의 최상단 셀이 같은 `Fill01 = 1`을 낸다. 깊이를 요구하는 소비처는 `ColumnMlDownward(topCell)`을 써야 한다.

`ColumnMlDownward`는 `topCell`에서 아래로 내려가며 ml을 누적하고, **물이 없는 셀을 만나면 즉시 멈춘다**. 상한은 `MapLiquidConsts.MaxColumnScanCells`이며, 물이 끊기는 지점에서 먼저 멈추므로 실제 비용은 보통 1~2셀이다(§정적 셀 무연산 보증 3항 — 전체 순회 아님).

| 소비처 | 판정 | 이유 |
|--------|------|------|
| 낚시 Cast · 통발 배치 (`MapFishService.CellHasFishableWater`) | `ColumnMlDownward ≥ MapFishConsts.FishableColumnMl` | 물고기가 살 **수심**이 필요 — 얕은 한 겹은 제외 |
| 수중창 S3 (`MapFishService.IsShooterInWater`) | `Fill01 ≥ MapFishConsts.UnderwaterShooterFill01` | 사수가 잠겼는지만 보면 됨 — 발밑 국소 상태 |

`FishableColumnMl = 2,000,000`은 셀 클램프(약 1,065,390)의 2배 근처라 **수직 2셀 이상**을 강제한다. 즉 평지에 물을 부어 한 겹 깔아도 낚시는 안 되고, 분지(아래 셀이 점유돼 있고 위 셀에는 바닥이 없는 지형)가 필요하다.

`UnderwaterShooterFill01`은 구 `SHALLOW_WATER` 시드 비율(`MapLiquidConsts.ShallowSeedFraction`)을 그대로 참조한다 — 얕은 물에서도 되던 기존 동작과의 패리티가 이 상수의 존재 이유이므로 값을 따로 적지 않는다.

### 분지·void가 성립하는 조건 (지형 계약)

| 용어 | 판정 | 물 동작 |
|------|------|---------|
| **InMapBounds** | `minX≤x≤maxX`, `minZ≤z≤maxZ`, `y≥minY` (저장 `mapBounds`) | 붓기·시뮬 가능 |
| **AirGap** | InMapBounds ∧ ¬`CellHasOccupancy` | 중력 transit OK |
| **OutOfMap** | bounds 밖 XZ 또는 `y<minY` | Pour 거부·ml 제거 |

`mapBounds`는 저장 시 `MapBoundsBake`가 tiles·wallEdges·floorFaces·liquidAuthoringFaces·liquidCells footprint union으로 bake한다.

물이 **수평으로** 퍼지려면 이웃이 `CellHasOccupancy`를 만족해야 한다(`IsHorizontalFlowTarget`). pit 분지 = 바닥 face + wall edge(pit 테두리)로 점유만 주는 패턴.

물이 **수직으로** 쌓이려면 각 **체류** 셀에 topology 점유·바닥 계약이 필요하다. AirGap은 낙하 **통로**일 뿐 최종 웅덩이가 아니다 — 아래에 floor/점유가 있어야 멈춘다.

## 배선

`TileMapManager.SetupMapLiquid()`가 `MapBloodHost`/`MapPlantHost`와 동일한 패턴으로 `MapLiquidHost`를 바인딩·로드한다. 저장은 `MapSavePipeline.Save(...)`/`MapFileSaver`가 `MapLiquidHost.Runtime.WriteToDto`를 호출한다.

## 남은 작업 (이 패스에서 미구현)

- **층 가시성**: 수면 렌더러는 `IFloorVisibilitySync`에 물려 있지 않다. `PlayerFloorVisibilityDriver`가 sink 1개만 받는 구조라, 위층 물이 아래층을 가릴 수 있다. 실내 다층 물이 생기면 composite sink로 연결할 것.
- **얼음 렌더**: 수면 렌더러는 `IsSolid`를 모른다 — 얼어붙은 셀도 물결치는 물로 그려진다. 정점 색에 채널을 하나 더 주거나 별 머티리얼로 분기할 것.
- **실내 기온**: `MapLiquidAmbientService`가 모든 셀을 `outdoor: true`로 본다. 실내 수조는 `WeatherExposure.IndoorAmbientTempC`를 써야 하지만 셀 단위 실내 판정(building/space)을 이 경로에 물리는 작업이 남았다.
- **노출면 증분 추적**: `MarkAmbientBoundaryDirty`가 액체 셀 전체를 훑는다(기온 변화 시에만). 노출 셀 집합을 `AddEffectiveMl` 시점에 증분 갱신하면 O(수면적)으로 줄어든다.
- **advection**: 흐름이 온도를 운반하지 않는다(§상변화).
- **낚시 가능 지형 없음**: `map01`의 웅덩이는 한 겹(최대 약 1,065,390 ml)이라 `FishableColumnMl` 미달 — 현재 맵에서 낚시는 의도적으로 불가다. 분지를 저작해야 낚시가 다시 열린다(§분지가 성립하는 조건).
- **Fields/emits**: BN `phase: liquid` 아이템 필드 증발/침전.
- **Consumers**: 소화기(extinguisher) 등. **젖음(wetness)**: Wade/Swim/Dive immersion → `CharacterClimateHost`가 `MapSwimConsts.LiquidWetnessGain*`를 날씨 gain과 max ([`../locomotion/SWIM.md`](../locomotion/SWIM.md)). 비(rain)→맵 액체 Pour는 미연결.
- **terrain별 capMl bake**: 현재 전 셀 `DefaultMaxVolumeMl` 공통.

## See also

`docs/map/SYSTEM.md` · `docs/map/FISHING.md` · `docs/equipment/BN_BAKE.md`(`volume_ml`, `phase: liquid`)

# Farming

> Dist 농사: **Plant = OccupiedCell 가구**, **Till = 바닥재 레이어 덮어쓰기**. 규칙은 `TileDefinition` 플래그.
> 인덱스: [`docs/README.md`](../README.md) · 맵 호스트: [`map/SYSTEM.md`](../map/SYSTEM.md) · 시계: [`time/TIME.md`](../time/TIME.md)

경로(호스트·모델·뷰): `Assets/Dist/Scripts/Map/Plant/`  
경로(규칙): `PlantGrowth` (`Gameplay/Definitions/BN/PlantGrowth.cs`) · `TileFlags`  
경로(행위): `MapPlantService` · 인벤/타일 컨텍스트 메뉴 (plant / harvest / till / fertilize / chop)  
경로(설치 SSOT): `TilePlaceUtil` (건설 `GridCursor.TryPlace`·본편 `ConstructionService`와 공유)  
경로(셀 타겟 3D 프리뷰 SSOT): `CellTargetPreview3D` (`docs/construction/CONSTRUCTION.md`)

---

## Layers

| 레이어 | 역할 | 같은 셀 정책 |
|--------|------|----------------|
| 바닥재 (`HorizontalFace`) | Grass / Dirt / `Floor/Tilled` 등 표면 | **덮어쓰기** (`TileMapController.TryReplaceFloorMaterial`) |
| OccupiedCell 가구 | Planter, `Furniture/Plant_*`, 일반 가구 | `BlocksOccupiedCells`면 겹침 금지→높이 위; 없으면 겹침 가능 |
| Plant | OccupiedCell 중 작물 | 건설 place와 동일 (`TilePlaceUtil.ResolveOccupiedInstallCell`) |

경작: 현재 바닥재가 `PLOWABLE`/`DIGGABLE`이면 `Floor/Tilled`(`PLANTABLE`)로 덮어씀. 가구/Plant는 유지.  
심기: 바닥재 `PLANTABLE` 또는 `IsTilled`(Tilled prefab / PLANTABLE∧¬PLOWABLE) 또는 Planter 등 셀 플래그. `tilledCells` HashSet 없음.

---

## View = TileView + plant sprites

맵 작물 뷰는 청크 `TileView` (`Furniture/Plant_*` 프리팹). 스프라이트 우선순위:

1. `PlantOverlaySpriteCatalog` — 단계별(및 선택적 seedItemId) Sprite 오버라이드  
2. BN `plant_sprites.json` — 범용 `f_plant_seed` → … → `f_plant_harvest`  
3. Dist primitive 폴백

`PlantTileInteractable`이 단계 스프라이트를 갱신. `Withered`는 Catalog 또는 Harvestable BN + `OverlayColorWithered` 틴트.

| Flag | 역할 |
|------|------|
| `PLANTABLE` | 심기 게이트. 바닥(`Floor/Tilled`, GrassFloor 등) 또는 가구(`Furniture/Planter`) |
| `PLOWABLE` / `DIGGABLE` | 경작(till) 게이트. DIG 품질 도구 |
| `PLANT` / `GROWTH_*` | Plant OccupiedCell 단계 identity (`Furniture/Plant_Seed` … `Plant_Withered`) |
| `GREENHOUSE` | 서리 스킵. OccupiedCell `PLANTABLE` 가구도 서리 스킵에 참여 |

스프라이트 bake:

```text
python Tools/bn_converter/export_plant_sprites.py --bn-path <Cataclysm-BN> --output Assets/StreamingAssets/BNData/tileset
```

---

## Model = tiles (not plantCells overlay)

런타임 SSOT: `TileMapModel` OccupiedCell plant tiles + floor-material faces. `MapPlantHost`가 조회·심기·경작·비료 API.  
구 JSON `plantCells[]`는 로드 시 Plant 타일(+ `tilled`면 floor Tilled)로 **승격 후 폐기**. 세이브는 `tiles`만 (plant 필드는 `TileSaveData`).

CatchUp는 plant prefabId 단계를 `PlantTileIds.PrefabIdForStage`로 패치하고, wither 시 제거.

---

## Grow from world minutes

분당 growth tick 없음. `elapsed = CurrentWorldMinute - plantedWorldMinute`를 `seed.grow_minutes`와 비교한다 (`PlantGrowth.Resolve`).

| Stage | 조건 (effective grow 기준) | prefabId |
|-------|---------------------------|----------|
| Seed | `elapsed < 0.25 × grow` | `Furniture/Plant_Seed` |
| Seedling | `≥ 0.25` | `Furniture/Plant_Seedling` |
| Mature | `≥ 0.75` | `Furniture/Plant_Mature` |
| Harvestable | `≥ grow` | `Furniture/Plant_Harvestable` |
| Withered | `≥ grow + WitherSlackMinutes` 또는 frost (Field만) | `Furniture/Plant_Withered` |

### Tree (`SeedDetailData.crop_kind = Tree`)

- 시듦 없음. 과일 수확 후 `Mature`로 복귀, `fruit_regrow_minutes` 후 재수확.
- 겨울 야외: 성장·과일 수확 정지(휴면). 겨울 일수는 성장 elapsed에서 제외(day 단위).
- 벌목: 성장 단계 무관 항상 가능(`AXE` 품질). `chop_yields`로 단계별 드롭. 제거.
- 저장: `lastFruitHarvestWorldMinute` (`TileSaveData`, 미수확 시 생략/≤0 → -1).

---

## Seasons / frost

`WorldCalendar.Season` / `SpanIncludesSeason`(심은 날~현재 날 inclusive)으로 겨울 구간을 본다. 서리는 기후 frostbite가 아니라 그 day-span 플래그 (`PlantGrowthContext.FrostKills`).

서리 적용: 야외이고 온실/화분이 아닐 때. 실내(`IsOutdoorCell == false`)는 스킵. `GREENHOUSE` 타일 또는 OccupiedCell `PLANTABLE` 가구(Planter)도 스킵. 판정은 inspect/CatchUp 경로의 `PlantGrowth.Resolve`.

---

## Fertilizer

셀당 1회 (`PlantTileInstance.fertilized` / `PlantCell.Fertilized`). `FERTILIZER` 플래그 아이템. `PlantGrowth.FertilizerGrowFactor`(0.5)로 필요 grow 분을 줄인다.

---

## Weather Kind assumption

성장 배율은 `WorldWeatherHost.TryGetKindAt(cell.x, cell.z)` (없으면 Clear).  
지금은 stub라 **글로벌 Kind**와 동일. 셀별 필드는 Phase D Parked — [`weather/WEATHER.md`](../weather/WEATHER.md).

| Kind | Factor |
|------|--------|
| Clear (그 외) | `WeatherClearGrowFactor` (1) |
| Rain | `WeatherRainGrowFactor` (0.75, 빠름) |
| Wind | `WeatherWindGrowFactor` (1.25, 느림) |
| Snow | `WeatherSnowGrowFactor` (1.25, 느림) |

inspect/harvest CatchUp는 이 Kind를 쓴다.

---

## Player actions (cell targeting)

진입: 인벤·타일·**Plant TileView** 컨텍스트 메뉴 (`Plant` / `Till` / `Fertilize` / `Harvest` Contributor·Catalog 유지).

`Execute()` 이후 **항상** 동일 파이프라인:

1. **타겟팅** — `FarmCellTargetSession` + `GridCursor`. 셀 해석은 **카메라 스크린 레이** → `MapPlantHost.ResolveCellFromWorld` (Physics miss 시 발 높이 수평면). 호버 셀마다 커서 표시. `CanApply(cell)` 기준 **녹색=가능 / 붉은색=불가**. 심기 프리뷰는 `Resources/Farming/FarmPlantTargetPreview` (MeshVisual + SpriteVisual 자식 — 같은 GO에 MeshFilter/SpriteRenderer 금지).
2. **확정** — 녹색 칸에서만 LMB·UiSubmit. 붉은 칸 클릭 무시.
3. **취소** — 타겟팅 중 RMB·ESC (`UiCancelPriority.FarmCellTarget`). 건설 UI 열림·농사 세션 동시 불가.
4. **Arrive** — `CharacterActionHost.TryRunFarm` → `CharacterCellFarmPipeline` → `CharacterArriveHost` + `NpcSteer` (possessed도 `CharacterMotor.ScriptedLocomotion`, Player TimeScale). 이동 목표는 클릭 셀 중심. **심기**는 목표 셀 기준 XZ Chebyshev ≤ `MapPlantConsts.PlantActionRangeCells`(1)이면 Work 진입(셀 중심 불필요). 경작·비료·수확은 `CellArriveStoppingDistance`(= CellSize × 0.55) 월드 도착. 자동이동 중 공통 게이지 위치에 `Img_AutoProgressIcon` 표시(fill 숨김).
5. **Work** — 심기·경작·수확: `CharacterTimedWorkPlayback` + `FarmWorkClipCatalog`. 심기/경작 대기초(`PlantWorkDurationSeconds`/`TillWorkDurationSeconds`, Catalog Inspector)와 클립 length의 max. `CharacterActionHost.Progress01`(Cell) → 공통 게이지 fill. 비료는 Work 없음.
6. **적용** — `MapPlantService.Try*At(cell)` (발밑 게이트 없음). 심기는 Apply 직전 `IsWithinPlantActionRange` 재검증.

`GetDisabledReason`(메뉴)은 아이템 소유·무드·DIG 품질 등 **세션 시작 가능**만. 특정 칸 가능 여부는 타겟팅 프리뷰가 담당.

Arrive/Work 중 **ESC** 또는 **이동 입력**(WASD/스틱) → `CharacterActionHost.CancelAll` (`CharacterActionKind.Cell`, apply 없음).

---

## Migration parity (OccupiedCell plant)

- 심기 = `TilePlaceUtil` + 건설 높이·겹침 (`BlocksOccupiedCells` → 위 셀)
- 경작 = plow/dig 바닥재 → `Floor/Tilled` 덮어쓰기 (가구 미삭제)
- 바닥재끼리 연속 덮어쓰기
- CatchUp·서리·비료·날씨
- FarmCell 파이프라인
- 청크 재진입 TileView
- 구 `plantCells` / `tilled` 마이그레이션
- Planter 위 심기 (점유 비claim → 같은 셀)
- plant sprites / Catalog

---

## No hunger CatchUp

식물 CatchUp은 wither/byproduct·제거만. `PlayerNeedsHost` 허기/갈증을 맵 로드나 시간 점프로 따라잡지 않는다.

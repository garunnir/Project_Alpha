# Character Action (게이지·큐)

> LLM/에이전트용 행위자 행동 직렬화 SSOT.
> 인덱스: [`docs/README.md`](../README.md)
> 관련: [`DEFINITION.md`](DEFINITION.md) · [`../body/BODY.md`](../body/BODY.md) · [`../ui/SETTINGS.md`](../ui/SETTINGS.md) · [`../equipment/GEAR.md`](../equipment/GEAR.md) · **파이프라인 매뉴얼** [`../equipment/COMBAT_PIPELINE.md`](../equipment/COMBAT_PIPELINE.md)

경로: `Assets/Dist/Scripts/Entity/Character/CharacterActionHost.cs`  
지연: `CharacterActionDelayCatalog` / `CharacterActionDelay` (`Dist.Gameplay.Data`)  
UI: `Assets/Dist/Visual/Prefabs/UIComponents/Character/Grp_CharacterActionGauge.prefab`  
Patch: `Dist/MCP/Character/Patch Action Gauge On Player`

게이지·큐·취소는 **그 행동을 하는 행위자 GO**에 붙는다. 플레이어 전역/`GameplayData` 아님.

---

## 계약 (패리티)

| Before | After |
|--------|--------|
| 장비 타이머·인벤 이동·전투 쿨·제작이 겹칠 수 있음 | 같은 행위자에서 **한 줄만** 진행, 나머지는 종류별 큐 |
| busy면 거절 | `TryRunOrEnqueue` — idle이면 즉시 Start, busy면 종류 정책 |
| ESC가 행동을 안 끊음 | possessed만 `CancelAll` (현재 작업+큐). 전투 쿨은 스킵하지 않음 |
| 상태이상이 행동 시간에 무관 | `BodyPartEffect` → `TickScale`을 실제 dt에 곱함 |

```text
요청 → CharacterActionHost.TryRunOrEnqueue
         idle  → Start (기존 Gear/Inv/Craft/Attack 타이머)
         busy  → 종류별 EnqueueOrReplace
         완료  → dequeue 다음 Start
CancelAll → 현재 작업 취소(적용 없음) + 큐 전부 폐기
            (누가 호출하든 동일. possessed ESC / AI / 상호작용 중단)
```

종류별 큐 정책 (`EnqueueOrReplace`) — 이산 작업과 연타 입력을 같은 FIFO에 넣지 않는다.

### `CharacterActionKind.Cell` (구 Map)

행동큐 **종류 태그** — 맵 로드·타일맵 API(`MapVaultQuery` 등)와 이름을 섞지 않는다.

| 소비자 | 역할 |
|--------|------|
| `CharacterArriveHost` | 셀/월드 목표 자동이동 |
| `CharacterActionHost` Cell pipelines | 농사·낚시·건설 (`CharacterCellFarmPipeline` / Fish / Construction) |
| `CharacterVaultHost` | vault |

### Work Layer (Cell 소비자 공통)

| 항목 | SSOT |
|------|------|
| 레이어 이름 | `CharacterWorkLayerAnim.LayerName` |
| 재생 API | `CharacterWorkLayerAnim.TryPlay` / `Stop` |
| 컨트롤러 배선 | `CharacterWorkLayerAnim.DefaultControllerPath` — `ArmOverlayAnimatorBuilder` Rebuild·Ensure Work Layer |
| 에이전트 동기화 | Unity MCP `Dist/MCP/Ensure Work Layer (Vault/Farm/Fish)`. MCP 꺼짐·오류 시 → **사용자에게 MCP 복구 요청** (수동 메뉴·YAML 직수정으로 넘기지 않음 — `.cursor/rules/collaboration-unity.mdc` §Unity MCP) |
| 카탈로그 → 상태 | Work 클립 카탈로그 저장 시 `WorkLayerCatalogPostprocessor`가 상태 자동 동기화 |
| Play 진입 검사 | `WorkLayerPlayModeContractGuard` — 레이어·클립 상태 누락 시 LogError |
| 런타임 검사 | `CharacterWorkLayerAnim.ValidateOrLog` — 맵 바인드·`CharacterVaultHost` Awake |

클립 이름 = Animator 상태 이름 = `Animator.Play(clip.name, workLayer)` (동작명 파라미터 추가 금지).

| Kind | busy일 때 | 이유 |
|------|-----------|------|
| Gear / Inventory / Craft / Cell | FIFO append | 클릭 1 = 작업 1. 착용 중 인벤 이동은 대기. **Cell** = 그리드/월드 셀 스크립트 행동 (`CharacterArriveHost` 도착 · ActionHost Cell pipeline 작업 · vault). Arrive 중 게이지는 `Img_AutoProgressIcon`. TileMap 시스템과 무관 |
| Combat | 큐에 **최대 1개**. 이미 Combat이 있으면 Start만 교체 | LMB 연타는 “지금 한 대”이지 N대 예약이 아님 |

교차 종류는 그대로 한 줄: 착용 중 공격은 Combat 1칸이 뒤에 앉는다. 쿨 중 연타는 그 1칸만 최신 클릭으로 덮는다.

**Dig** / **Chop**은 Combat perform 계열(홀드·Structure pipeline). 표·핸들러: [`COMBAT_PIPELINE.md`](../equipment/COMBAT_PIPELINE.md). 맵 파괴: [`DIG.md`](../map/DIG.md).

### 플레이어 조준·시전 3층 파이프라인

표·driver·Leaf↔handler·Catalog Resolve: **[`COMBAT_PIPELINE.md`](../equipment/COMBAT_PIPELINE.md)** (이 절은 ActionHost와의 경계만).

| Layer | SSOT | 역할 |
|-------|------|------|
| 1 조준 해석 | `IAimSightProvider` sample + `CharacterSightHost` | 입력은 sample 제안. 본체 Free/StructureLock으로 구독. 플레이어 진입=`SessionSightHost` |
| 2 시전 입력 | `ICombatPerformDriver` + `ICombatAttackInput` | Host = `PlayerCombatController` |
| 3 cue 실행 | `CharacterAttacker` · `IActionHandler` | Leaf `logicId` |

애니 `_attackActionQueue`(길이 2, 초과 drop)와 별개. Combat 연타 큐 정책은 위 Kind 표.

```text
현재 Gear, Combat 연타 → 큐 [Combat×1]
현재 Combat, 큐 비움, 클릭 → 큐 [Combat] (버퍼 1)
현재 Combat, 큐 [Combat], 클릭 → 그 잡 Start만 교체
현재 Combat, 큐 [Inv], 클릭 → 큐 [Inv, Combat]
```

```mermaid
flowchart LR
  Effects[BodyPartEffect]
  Delay[ActionTickScale]
  Req[Wear Inv Craft Attack]
  Host[CharacterActionHost]
  Cur[CurrentJob]
  Q[KindQueue]
  Gauge[WorldGauge]
  Cancel[CancelAll]
  Effects --> Delay
  Delay -->|"dt * scale"| Cur
  Req --> Host
  Host --> Cur
  Host --> Q
  Q --> Cur
  Cur --> Gauge
  Cancel --> Host
```

타이머 **식**은 Host에 복붙하지 않는다. Start 콜백이 기존 시스템을 켠다. 완료는 해당 소스 idle 전이.

---

## TickScale

`CharacterActionDelay.TickScale(body)` — 트리 효과 순회, catalog 배율을 intensity만큼 곱. 미등록 1. 그다음 `BodyCapacity.ManipulationTickScale` 한 번 곱. 하한 `MinTickScale`.

적용: GearTimedAction, InventoryTimedMove, 전투 쿨, 제작 경과. 이동 속도(`BodyLocomotionPenalties`)와 별개. Feeling/습윤/과적은 후속 가산.

---

## CancelAll

모든 Host 공통. 전투 쿨은 남긴다. 작업/큐가 있으면 그것만 취소.

possessed ESC: `CharacterActionCancelConsumer` → `UiCancelPriority.CharacterAction` (60). 메뉴(100)가 먼저. 다른 오브젝트 큐를 ESC가 끊지 않음.

---

## UI

월드 캔버스 fill. idle이면 Canvas 비활성. 레이아웃은 프리팹 SSOT (`CharacterActionGaugeLayout`은 Patch 수치). 라벨/TMP 없음. 정면은 `WorldBillboard` (기본 Realtime, Inspector 토글).

---

## 검증

IsoLand `>PlayerCharacter`: 착용 중 인벤 이동은 큐. ESC는 미적용+큐 소멸. 컨텍스트 메뉴 ESC는 메뉴만. 쿨만 남은 ESC는 세팅. 골절 등이 있으면 진행이 더 김. 조준 중 LMB 연타 → 쿨 끝난 뒤 **한 대만** (손 뗀 뒤 지연 연타 없음).

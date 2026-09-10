# Dist Remaining Plan

> **남은 일만** 적는다. 구현되면 그 항목을 **삭제**한다 (완료 체크를 남기지 않음).  
> 이미 있는 동작의 계약은 도메인 문서(`docs/<topic>/`)가 SSOT다. 이 문서는 **아직 없는 것**과 그걸 넣을 때 지킬 지침이다.

## 용도

1. **뭐 남았지?** → 아래 남은 항목만 답한다. 도메인 문서 Pending를 다시 긁지 않는다.
2. **구현할 때** → 해당 항목의 **지침** + 링크된 도메인 문서를 읽고 따른다. 끝나면 이 항목을 지우고, 도메인 Pending 행도 제거하거나 ‘연동됨’으로 맞춘다.

항목을 추가할 때도 같은 형식: 한 줄 상태 + 지침 + 도메인 링크. 지침 없는 할 일 줄만 넣지 않는다.

---

## 세이브 / 로드

맵 JSON(`MapSaveJsonDto`)에 타일·월드 시계·플레이어 진행 스냅샷(`hasPlayerProgressSnapshot` / `playerProgressJson`)을 병합 저장. 인벤·장비는 후속.

**지침**
- 교차 도메인 저장은 기존 `MapSavePipeline` + `ToDto`/`FromDto` 패턴을 따른다.
- Pixel Crushers / Legacy 세이브를 Dist SSOT로 쓰지 않는다.
- 로드 시 정의 시드 후 `PlayerProgressSaveBridge.TryRestorePossessed`가 성장 데이터를 덮어쓴다.

**도메인:** [`time/TIME.md`](time/TIME.md) · [`body/BODY.md`](body/BODY.md) · [`map/SYSTEM.md`](map/SYSTEM.md)

---

## 시간

### 불릿타임 실전 콘텐츠

API·채널 소비 경로만 준비. 키 바인딩·연출 없음.

**지침**
- 배속은 `TimeScaleService` Push/Pop. Unity `Time.timeScale` 금지.
- 플레이어 면제가 필요하면 `Player` 채널만. `World`와 섞어 덮어쓰지 않는다.
- 신규 시뮬 시간은 `Delta(World|Player)` / `FixedDelta` / UI는 `Realtime`.

**도메인:** [`time/TIME.md`](time/TIME.md) · [`tech-stack.md`](tech-stack.md)

### 날씨 XZ 그리드 필드 (Parked)

글로벌 `WorldWeatherHost` + 스케줄·Period 라이팅·Kind VFX는 ship. 셀별 Perlin 필드·BN climate bake는 **맵 규모·지역 이동** 전까지 보류.

**지침**
- Kind 소비는 `WorldWeatherHost.TryGetKindAt`만. 글로벌 Kind를 다시 GearHost에 두지 않는다.
- 필드는 청크 mesh와 분리 (model memory). player-focus 스트리밍 재도입 금지.
- 게이트·설계: [`weather/WEATHER.md`](weather/WEATHER.md) Phase D.

**도메인:** [`weather/WEATHER.md`](weather/WEATHER.md) · [`time/TIME.md`](time/TIME.md)

---

## 전투 / 장비

### 치명타

`AttackOutcome.WeaponReach01`(0=손/자루 … 1=끝)만 기록. 배율·판정 없음.

**지침**
- 기록값은 유지하고 로직만 추가. 근접 connect에 `HitChance`를 되돌리지 않는다.
- 배율·임계는 이름 있는 SSOT 한곳. 본문 매직 복붙 금지.

**도메인:** [`equipment/GEAR.md`](equipment/GEAR.md)

### 충전 공격 (후순위)

조준+좌클릭은 즉시 시전만. 홀드 충전·차지 클립/VFX 없음. **우선 낮음 — Auto 홀드(`AimHoldAttackInput`)와 LMB hold가 겹침.**

**지침**
- 새 Charge Leaf / Catalog Charge 행 / 컨트롤러 Charge 상태·`LibraryKeys` 금지 (`arm-anim-layers.mdc`).
- 입력은 `performed` 시전을 나누지 말고 `started`/`canceled` 프레스 래치. 충전 off=눌림 즉시, on=릴리즈. 탭=`Charge01=0` 패리티.
- Entry `allowCharge` + 선택 `chargeClips`/`chargeVfx`. 클립 있으면 Aim thin 덮고 Aim `Play(0)`, 논루프면 `normalizedTime` 끝 고정. 진행도=포즈면 충전 중에만 `Play(Aim, layer, Charge01)` — 상태 Motion Time은 컨트롤러 고정(런타임 토글 없음)이라 쓰지 않음. Attack Speed Parameter / `CueNormalizedTime`과 섞지 않음. 클립 적용은 Override thin 덮기만이 후보 아님 — Playable·직접 재생 등 Override 밖도 검토(미정). VFX는 Entry → `WeaponCombatFallbacks` 기본 루프, Catalog 동사 행 폴백 없음.
- `Charge01`은 `ActionHandlerContext`/`PendingAttack`에만. Animator float 아님. `offenseFactor`와 섞지 않음. HP/J 배율은 `WeaponAttack` SSOT.
- `allowCharge` Auto는 릴리즈 1볼리. Auto 홀드 연사와 동시에 켜지 않는다.

**도메인:** [`equipment/GEAR.md`](equipment/GEAR.md) · [`locomotion/LOCOMOTION.md`](locomotion/LOCOMOTION.md)

### 건모드 합산

원거리 쿨 게이트 없음. `effective`(조임+반동 잔여+dispersion)가 탄착·부위 유지. 모드별 합산 없음.

**지침**
- 동작 쿨(`WeaponPresentation.Entry`)과 무기 쿨을 한 타이머로 합치지 않는다.
- BN `gun.modes` JSON은 아직 Parked. Dist Leaf 마스크로 먼저 합산하고, bake promote는 Dist 소비처가 생긴 뒤에.

**도메인:** [`equipment/GEAR.md`](equipment/GEAR.md) · [`equipment/BN_BAKE.md`](equipment/BN_BAKE.md)

### Wear/Wield → 숙련 modifier

바디 효과·과적(`PlayerEncumbranceHost`)은 `ISkillModifierSource`. 장비 자체 보너스는 없음.

**지침**
- 새 경로를 만들지 말고 `ISkillModifierSource` + Refresh. 합산은 Refresh 한 번.
- 장비 수치를 스킬 Base에 직접 쓰지 않는다 (Buffed 소스).

**도메인:** `.claude/memory/plan.md` (숙련 잠금) · [`equipment/GEAR.md`](equipment/GEAR.md)

### 플레이어 Defeat 소비

판정 레이어(`ICharacterDefeat`)는 있음. NPC는 `NpcManager`가 Dead로 정지. 플레이어는 메시지 로그. **게임오버 UI·입력 차단 없음.**

**지침**
- 소비처는 `IsDefeated`만 본다. Body/Skills OR를 다시 계산하지 않는다.
- NPC Dead 경로를 플레이어에 복붙하지 않는다. possessed 입력·세션만 막는다.

**도메인:** [`character/DEFINITION.md`](character/DEFINITION.md)

---

## 캐릭터 / 몸

### Appearance·Alignment 소비처

`CharacterAppearanceHost`는 성향·초상·체형·이름 오버라이드 **저장만**. `alignment`도 소비처 없음.

**지침**
- 저장 필드를 버리고 새 컴포넌트에 복제하지 않는다. 표시/AI가 호스트를 읽게 한다.
- PC/NPC를 Definition 필드로 나누지 않는다. 조종은 possess.

**도메인:** [`character/DEFINITION.md`](character/DEFINITION.md)

### NPC 바디 모델

PC/NPC 공용 `CharacterBodyHost`는 있음. NPC Defeat는 바디 없이 **StatCollapse만**인 경로가 남아 있다.

**지침**
- 본체 프리팹(`NpcSample`)에 몸 그래프를 전제한다. NPC 전용 바디 타입을 만들지 않는다.
- Defeat는 Body∨Skills OR 유지.

**도메인:** [`character/DEFINITION.md`](character/DEFINITION.md) · [`body/BODY.md`](body/BODY.md)

### 과적 ↔ `RemainingMassKg`

절단 시 부위 kg 차감은 됨. encumbrance는 미연동. Feeling/습윤/과적의 **행동 TickScale 가산**도 후속.

**지침**
- 질량 SSOT는 `CharacterAppearanceHost.RemainingMassKg`. 과적 쪽에 kg를 다시 세지 않는다.
- 이동 배율(`BodyLocomotionPenalties`)과 행동 `TickScale`을 한 공식에 섞지 않는다. TickScale 가산은 [`character/ACTION.md`](character/ACTION.md).

**도메인:** [`body/BODY.md`](body/BODY.md) · [`character/DEFINITION.md`](character/DEFINITION.md) · [`character/ACTION.md`](character/ACTION.md)

### 미이관 스펙

ActorSO에서 아직 Dist에 없는 것: HP 풀, Passive 슬롯, Dialogue int id, Bark 테이블.

**지침**
- Legacy `ActorSO`/Dialogue id를 Dist 기본 의존으로 가져오지 않는다.
- HP는 `BodyDamageService` 부위 HP와 별 풀이면 경계를 문서에 먼저 적고 승인받는다.

**도메인:** [`character/DEFINITION.md`](character/DEFINITION.md) · [`legacy/LEGACY_README.md`](legacy/LEGACY_README.md)

---

## 인벤 / 제작

### 쓰러진 NPC 루팅

`PlayerInventoryHost.IsAvailableToPlayer`: 자기 몸, `ICharacterDefeat.IsDefeated`, 또는 `CharacterPainHost.IsPainShocked`. 살아 있는(쇼크 아닌) NPC 몸은 Nearby에 안 뜬다.

**지침**
- 게이트는 `PlayerInventoryHost.IsAvailableToPlayer`. 살아 있는 몸을 Nearby에 넣지 않는다.
- 루팅은 몸 컨테이너를 Nearby 탭으로 쓰는 계약 ([`inventory/INVENTORY_UI.md`](inventory/INVENTORY_UI.md)). 새 루팅 창을 만들지 않는다.
- Defeat과 고통 쇼크 모두 게이트를 연다. 쇼크는 Dead가 아니다.

**도메인:** [`inventory/INVENTORY_UI.md`](inventory/INVENTORY_UI.md) · [`character/DEFINITION.md`](character/DEFINITION.md)

### 부피 soft 한도 / 정리정돈 스킬

provider 훅(`PlayerInventoryHost` Func)만 있음. 한도 상승 미구현.

**지침**
- 훅을 버리고 인벤 한도를 본문에 하드코딩하지 않는다. 숙련 Refresh 값을 훅에 공급한다.

**도메인:** [`inventory/INVENTORY_UI.md`](inventory/INVENTORY_UI.md)

---

## 이동 / 애니

### Dominant 손 교체

Aim/Attack 클립이 없으면 같은 손 Hold thin으로 내림. L↔R·TwoHand 자기미러 없음. Dominant 교체 없음.

**지침**
- 자기미러 폴백을 넣지 않는다. 교체는 명시 슬롯 재resolve.
- 컨트롤러에 손 이름 상태를 추가하지 않는다.

**도메인:** [`locomotion/LOCOMOTION.md`](locomotion/LOCOMOTION.md)

### NPC 청크 컬링·공간 해시

`NpcManager`는 씬 활성 유닛만 틱.

**지침**
- 맵 청크 스트리밍 desired에 player focus를 재도입하지 않는다 (`tile-chunk-streaming.mdc`).
- 컬링은 NpcManager 틱 범위. 타일 스트리밍 SSOT와 이중 리스트를 만들지 않는다.

**도메인:** [`locomotion/LOCOMOTION.md`](locomotion/LOCOMOTION.md) · [`map/SYSTEM.md`](map/SYSTEM.md)

---

## 맵

### 가시성 BFS membership 저장소 통합

evaluate는 `TileMapModel`, emit은 applier entry. 동작은 맞춰졌으나 저장소가 둘.

**지침**
- 새 증상 패치 전에 `.cursor/rules/bug-fix-gate.mdc` · [`map/TILEMAP_VISIBILITY.md`](map/TILEMAP_VISIBILITY.md) §7 표.
- membership·scalar를 applier entry store 단일로. model에 BFS SSOT를 남긴 채 분기를 더하지 않는다.
- 이미 제거된 `AppendTransitionOcclusionCandidates` 등을 되돌리지 않는다.

**도메인:** [`map/TILEMAP_VISIBILITY.md`](map/TILEMAP_VISIBILITY.md)

---

## Parked (외부 대기 — 지시 전까지 구현하지 않음)

| 항목 | 이유 | 재개 조건 |
|------|------|-----------|
| Animancer 몸 경로 | 구매 대기. 지금은 Mecanim | 구매 후. TimeScale 채널 틱·thin 클립 SSOT 유지 |
| BN `gun.modes` JSON bake | 컨버터 미반입. Dist는 Presentation Leaf로 대체 | Dist가 mode id → Leaf 마스크를 수동 Ensure 없이 매핑할 때 |
| Gunmod 블록 bake | Dist 장착/슬롯 소비처 없음 | 설치·슬롯 소비처가 생긴 뒤 |
| ProPixelizer Dist RG 포크 | 벤더 원본 인플레이스 금지 | 공식 v2(RenderGraph)로 `Assets/ProPixelizer` 교체 후 Dist 포크 폴더 삭제 |
| `URP_COMPATIBILITY_MODE` | Dist RG가 Execute와 패리티될 때까지 | v2 교체 또는 Dist RG 완성, 다른 의존 없으면 define 제거 |

---

## 하지 않음

이 문서에 새 항목으로 넣지 않는다. 구현 요청이 오면 거절하거나 Legacy 격리를 유지한다.

- Pixel Crushers `GameTime` / `DialogueTime`을 Dist 시계 SSOT로 채택
- Legacy API를 Dist 신규 경로의 기본 의존으로 확대
- `UIActorView` / `UIButtonContainer` 등 구 MVC 껍데기를 “남은 일”로 완성 (현재 Dist UI 경로 아님)

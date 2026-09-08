# Gear (Wear / Wield) — M0

Canonical for BN-style **착용(Wear)** vs **들기(Wield)** and the Character window equipment tab.

Related: [`docs/inventory/INVENTORY_UI.md`](../inventory/INVENTORY_UI.md) · Status parity lives in Character **상태** tab (`UICharacterWindow`).  
Anim/VFX 폴더 맵: [`WEAPON_VISUAL.md`](WEAPON_VISUAL.md).  
**액션·무기·도구 파이프라인 매뉴얼:** [`COMBAT_PIPELINE.md`](COMBAT_PIPELINE.md) (Layer1–3 · Leaf · Catalog Resolve · Dig/Chop).  
Anatomy / climate / sever: [`docs/body/BODY.md`](../body/BODY.md) (PC/NPC 분기는 [`DEFINITION.md`](../character/DEFINITION.md)).

## Terms

| Term | Meaning |
|------|---------|
| Wear | Armor/clothes with `armor.covers` → worn list only |
| Wield | L/R hand slots (weapons/tools). Two-hand = same stack on both slots; **no extra UI cell** |
| Character window | Tabs: 상태 \| 장비 \| 방해 \| 체온. Key = `StatusToggle` (`Tab`) |
| Primary | Highest DPS hand → `CharacterAttacker.SetWieldedItem` |
| SelectedLeaf | `ItemInstance.SelectedLeaf` — 손별 선택 **Leaf** (`CombatLeaf`). 영속은 인스턴스 |
| Action layers | **Family** = 에디터·UI 묶음 — **Melee**(Strike/Pierce), **Trigger**(Semi/Burst/Auto), **Etc**(Excavate/Chop/Raise; 미분류 묶음). **Leaf** (`CombatLeaf`) = 선택·시전 슬롯. **동작 SSOT** = Entry `Attack.logicId` → handler. **AnimVerb** (Swing/Thrust/Dig/…) = Pipeline·슬롯 클립만. Excavate = `MeleeBlock`·`melee_block_target`. Chop = `MeleePlant`·`melee_plant_target`. 플레이어 조준·시전 3층: `IAimSightProvider` · `ICombatAttackInput`/`ICombatPerformDriver` · `CharacterAttacker`/`IActionHandler` — [`ACTION.md`](../character/ACTION.md) · [`DIG.md`](../map/DIG.md) · [`BN_BAKE.md`](BN_BAKE.md) |
| Action rows | `WeaponPresentation` Entry = **Leaf** 라우팅 행 (가용 마스크 + Attack + **Hold/Aim/Attack 클립** + 연출 + `useHold` + **동작 쿨**). 클립·VFX 비면 Catalog 같은 Leaf. 가용 SSOT = Entry 존재 → `CombatLeafRows.Available` |
| Action VFX coalesce | Action: Entry.vfx → Catalog **같은 Leaf** 행. Hit: Entry → Attack VFX → Defaults[bash/cut/bullet] → fallback |
| Action clip coalesce | Action: Entry Hold/Aim/Attack 손 클립 → Catalog **같은 Leaf** 손 클립. Recoil/Blocked: Entry → Catalog Impact 행. Override 클립 맵 없음 |
| Visual hub | `WeaponPresentationCatalog` — Pipeline / Tag Impact VFX / item·skill·category → Presentation |

## Domain SSOT

| Type | Role |
|------|------|
| `GearConstants` | GramsPerStr=500, TwoHandWeightFactor=2, SoftMargin, LiftStrainMoveFactor=0.9, OffHandDpsFactor |
| `GearHandleRules` | CanLift / RequiredStr / LiftStrain / IsWearable |
| `EquipmentWearState` | Worn stacks |
| `WieldSlots` | L/R (+ two-hand mode). 든 스택 한손→반대 한손은 출발 칸을 비움 |
| `CharacterGearService` | Timed Wear/Wield/Unequip + deposit. Deposit/source remove → `InventorySession.NotifyExternalStacksChanged`. `TryBeginDomainTimed` / `NotifyAmmoChanged` for 삽탄·장착. 든 스택 손 전환=`TryBeginWieldGrip` |
| `WeaponAmmoFit` | Dist.Inventory — 허용 탄창 id / 탄종 / clip vs well |
| `WeaponAmmoService` | 삽탄·장착·교체·분리·탄 빼기. 탄창=`SupplyRounds`, 총=`ItemStack.LoadedMagazine` (Nested 아님) |
| `WeaponAmmoDuration` | reload moves → 초 (`CombatMath.MovesPerSecond`, 0이면 1s) |
| `WeaponChamber` | 발사 보급: LoadedMagazine.SupplyRounds → Chamber. clip_size는 클립 용량 |
| `PlayerGearHost` | Player Wear/Wield + Primary + LiftStrain + `HelmetVision` + Kind **포워드** (`WorldWeatherHost`). BodyTemp / EnvExposure / **Weather(ambient 캐시)** 는 `CharacterClimateHost` 포워드 |
| `CharacterSpawnGearApplier` | 스폰 직후 Definition 로드아웃 즉시 Wear/Wield + 총 탄 채움 (`WeaponAmmoService` 타이머 아님) |
| `ItemInstance.SelectedLeaf` | 선택 동사 SSOT |
| `CombatLeafRows` | Presentation 행 → available / default / instance select |
| `PrimaryWieldResolver` | DPS primary; dual secondary score |
| `ToolUseWieldSession` | Snapshot → temp wield → restore (M0 API; consumers later) |
| `CharacterHandWork` | 손 비움(Unwield→body) → 대상 Wield → act. ESC=`CancelAll`, 완료 단계 유지(원복 아님). 섭취 등 |
| `GearActionDuration` | Wear/TakeOff/Wield/Unwield seconds (proxy) |
| `InventoryTransferDuration` | MoveStacks / bag draw seconds — `draw_moves`→초(`CombatMath.MovesPerSecond`) **+** weight/volume/nest handling |
| `InventoryTimedMoveHost` | Per-stack sequential transfer (no summed delay); `ActiveStacks` = current only |
| `ItemTimedNameProgress` | Name-bar query SSOT: inv transfer → gear timed → durability |
| `WornPocketRules` | Wear 목록 → Nested ensure / owner lookup (사이드바) |
| `ArmorStorageNested` | Dist.Inventory — storage/pockets → Nested + draw_moves |
| `CharacterAttacker` dual | TwoHand=1회; 듀얼=호출 1회에 한 손 (`TryPerformSelected`, busy면 Cooling). 이번 손 NoAmmo/Unsupported면 **같은 호출 안** 반대손. OffHandDpsFactor는 offense만. 든 양손 사이 `SetWieldedItem`은 `PresentationChanged` 없음 ([`LOCOMOTION.md`](../locomotion/LOCOMOTION.md) 시전 분기) |
| `HandProficiencyIds` | `hand_l` / `hand_r` (skills.json seeded) |
| `WearStatsAggregator` | Wear-only armor aggregates (Phase A fields); combat reads via `WearCombatDefense` |
| `WearCombatDefense` | Phase D: ArmorRating 3단 주사 / MitigateDamage + WearEncAccuracyFactor |
| `WearEnvExposure` | Phase E/G: weather wetness rate + env_prot → ExposureFactor |
| `BodyTemp` | 부위별 °C (`ThermalParts`). 코어 getter = chest. 틱 소유 = `CharacterClimateHost`. [`docs/body/BODY.md`](../body/BODY.md) |
| `WeatherExposure` | `Resolve(kind, period, outdoor)` → AmbientTempC + wetness gain |
| `HelmetVision` | Phase G: head covers → VisionFactor (host + Character UI + camera) |
| `GearEnvPenalties` | Phase H: **코어** `BodyTemp.Feeling` + wetness → move / HitChance. 부위별 Feeling 아님 |
| `WearOverlapRules` | Phase C: same part + layer(/sided) conflict → Wear **reject** |
| `WeaponPresentationCatalog` | 허브. Resolve = 베이스(아이템 → `gun.skill` → `weapon_category` → Unarmed) 후 **By Quality Id**로 Leaf 합산(베이스에 없는 행만). Entry = Leaf 라우팅 |
| `ArmAnimSlotCatalog` | **Leaf마다** 기본 동사 폴백(클립+VFX). Dig/Semi/Burst/Auto 줄 필수. Entry 빈 클립·VFX → 같은 Leaf 행(없으면 thin). 표시=Melee/Trigger/Etc 묶음 |
| `WeaponAnimClipSpeeds` | Override 서브에셋. 할당한 클립→재생 배속. thin 슬롯 속도 아님. 없으면 1 |
| `WeaponAttack` | 핸들러·cue·발사체·Recoil/Blocked·근접 히트박스 (`Attack_MeleeHit` = logic 이름, **채널 아님**). 동작 쿨 아님 |
| `AttackDamageTags` | 특성 채널. Trigger→탄 `damage_type`(없으면 bullet). 근접은 양 있는 채널 전부(cut+bash 가능). 원거리 양 = 탄 `damage` + 총 `ranged_damage`. 계산기·Hit 키 공유 |
| `MeleeHitbox` | 근접 cue OverlapBox. 겹침 = 확정 히트. `WeaponReach01` 기록(치명타 Pending) |
| `WeaponImpactVfxDefaults` | Hit 테이블(bash/cut/bullet + fallback) + 자상/절단 피 오버레이. Recoil/Blocked(Reaction) 아님 |
| `CombatHitStopSettings` | 근접 로컬 히트스톱 지속(Realtime). 전역 `TimeScaleService` Push 아님 |
| `CharacterHitStop` | 공격자+피격자만 애니·모터·시전 틱 정지. `AnyAttackJudged` / 공격자 `AttackJudged` |

### Dual perform (호출 공통)

`TryPerformSelected`는 시전 **의도 1회**. 플레이어 클릭과 NPC Attack 틱이 같은 계약을 쓴다.

| 들기 | 한 호출 |
|------|---------|
| TwoHand / 한 손만 | 그 손 1회 |
| L·R 듀얼 | 교대할 손 1회. 그 손이 NoAmmo/Unsupported면 반대손만 이어서 시도 |

`TryPerformSelected` 진입에서 `IsActionBusy`(pending cue 또는 어느 손 쿨)면 Cooling — 호출측이 가드를 복제하지 않음. 듀얼에서 활성 손을 `SetWieldedItem`하는 것은 데미지·Entry resolve용이며, 이미 든 양손 스택 사이 교체는 `PresentationChanged`를 올리지 않는다.

### Melee connect (cue 히트박스)

**변경 전:** `GateAction`이 타깃·사거리를 막으면 스윙 없음. cue에서 잠긴 1명에게 `HitChance` 굴림.

**변경 후 (기본 경로):**

| 항목 | 계약 |
|------|------|
| 모션 | Cooling/pending/Unsupported만 시전 게이트. 근접 `NoTarget`/`OutOfRange`는 스윙을 막지 않음 |
| 판정 시점 | 이번 Attack 사이클이 cue 미만을 지난 뒤 `CueNormalizedTime`(또는 Animation Event). 잔여 Attack이 이미 cue를 지났다고 즉시 발사하지 않음 |
| 연결 | `MeleeHitbox` OverlapBox (조준 축, 길이 `CombatMath.RangeMeters`, 반폭/반높이 `WeaponAttack`) |
| 확정 | 겹친 `CharacterBodyHost`마다 `HitChance` 없이 피해. 방어는 `WearCombatDefense.MitigateDamage` 유지 |
| 히트스톱 | 근접 `Performed`/`Obstructed`만. 공격자+피격자 `CharacterHitStop` (지속=`CombatHitStopSettings`, Realtime, 겹치면 max). 원거리·미스 없음. `Time.timeScale` / 채널 Push 금지 |
| 허공 | 쿨·연습치만. `AttackJudged` Miss 없음 |
| 기습 | 피격자가 공격자를 **시력**으로 인지하지 못하면 피해 ×`CombatSurprise.DamageMultiplier`. 청력만으로는 인지 아님. 근접은 STR 비교로 기절(`CharacterPainHost` 래치) 또는 목 조준(오버킬 판정, 확정 파괴 아님). Attack thin은 Entry `surpriseAttackClips` |
| 치명타 | `AttackOutcome.WeaponReach01` (0=손/자루 … 1=끝)만 기록. **로직 Pending** |
| 디버그 | `DebugLogController` Player → Melee Hitbox (`Config.DebugMode.MeleeHitbox`). GL 와이어. 노랑=현재 자세, 주황=cue 허공, 초록=cue 히트 + 접촉점 |

NPC는 여전히 사거리 안에서만 `TryPerform` (AI). 플레이어 시전은 조준(RMB) 입력 게이트 유지.

### 플레이어 조준·시전 3층 (Layer1–3)

전체 표·Resolve·확장 체크리스트: [`COMBAT_PIPELINE.md`](COMBAT_PIPELINE.md).  
Excavate: **RMB 조준** + LMB hold → `AimWorldPoint` → Dig ([`DIG.md`](../map/DIG.md)). Layer1 Sight/Resolve/Preview: [`COMBAT_PIPELINE.md`](COMBAT_PIPELINE.md).

### Ranged fire (조준축)

원거리는 엔티티 락온을 쓰지 않는다. 유도탄 핸들러가 생기면 그 경로만 예외.

| 항목 | 계약 |
|------|------|
| 모션 | Cooling/pending/NoAmmo만 시전 게이트. `NoTarget`/`OutOfRange`는 발사를 막지 않음 |
| 방향 | 조준축 + **effective** yaw (`CombatMath.DispersionYawDegrees`, 60 단위=1°). 반동은 게이트 아님. 킥=`(gun.recoil+ammo.recoil)*handlingFactor`. 회복=지수 `remaining*=exp(-λ·dt)`, `λ=MovesPerSecond/RecoilRecoverRefUnits`(Ref=100 → λ=1). 상한=`RecoilKickUnits × RecoilRemainingMaxKicks`(4). 가산=`remaining + kick*(1 - remaining/cap)` 점근 후 clamp. 넉백 Δv는 미클램프 |
| 연결 | Attack 프리팹 있으면 `DistProjectile` 비행. 없으면 cue `CombatHitscan` |
| 조임 | RMB `IsAiming` 동안 `aim01` 0→1. `aim_speed` 0/없음=즉시 1. NPC `AimHeld`=즉시 1. `sight_dispersion*(1-aim01)` |
| 조준 포인터 | **Ranged:** RMB + `TryPreviewRangedSpread` → `UIAimPointer`(TopMost). 센터 프리팹 고정, 퍼짐=`Dist/UI/AimRing` SDF. UI는 식 복제 안 함. **MeleeBlock(Excavate):** RMB + `TryPreviewMeleeBlockTarget` → `SetDigHighlight` on HorizontalFace (UI 링 없음; 유클리드 3D clamp=`DigActionRangeCells`). 근접 Strike/Pierce Pending |
| 명중 | 레이/탄이 `CharacterBodyHost`에 닿으면 피해. 마스크 기본 `~0`(Character 포함). 자기 콜라이더는 `IsOwnCollider`/`IsSelf` 제외. 맵 벽은 `MapTopologyLineCast`(조준과 동일)에서 멈추고 `Obstructed`+`ImpactPoint` — `MapStructureDamageSink` Occupied 벽 remaining HP. `effective`=`gun/ammo.dispersion`+sightExtra+`shot_spread`+recoilRemaining → yaw와 부위 유지 공유. `HitChance` 실패=`ScatterToNeighbor`. 허공 히트스캔=사거리 끝 Miss, 비행=사거리·수명 소멸 |

동작 쿨과 무기 쿨은 **별 타이머**. 동작 쿨=`WeaponPresentation.Entry`(시전 시작, 0=생략). 근접 무기 쿨=`CombatMath.AttackIntervalSeconds`(무게/부피). 원거리는 무기 쿨 게이트 없음 — `effective`(조임+반동 잔여+dispersion)가 탄착 퍼짐과 부위 유지. cue 시점은 쿨이 아님. 건모드 합산은 후속.

```mermaid
flowchart LR
  input[TryPerform]
  gate[Gate_Cooling_only_for_melee]
  anim[AttackResolved_overlay]
  cue[Attack_cue]
  box[MeleeHitbox_Overlap]
  hit[ResolveCommittedHit_no_chance]
  input --> gate --> anim --> cue --> box --> hit
```

Checklist: `.claude/checklists/migration-parity.md`.

### CanLift

- One hand: `Ceil(weight_g / 500)`
- Two hand: `Ceil(weight_g / (500 * 2))`
- Wear uses one-hand formula only
- LiftStrain when lift succeeds but `(strength - RequiredStr) < SoftMargin` → move ×0.9, **hover only**

### Timing

- All Wear/TakeOff/Wield/Unwield are timed; wield/unwield short
- Bag → gear: `GearActionDuration + InventoryTransferDuration`
- Hand work (eat/drink/use): `CharacterHandWork` — stow other wielded → draw/wield target → `ConsumeDuration` (Eat/Drink 250 moves). ESC stops at current step (no rollback)
- Inventory MoveStacks / quick transfer / outside drop: `InventoryTransferDuration` (**SSOT**, same host)
- Transfer duration: `access`(source `draw_moves`→초 via `CombatMath.MovesPerSecond`, 0 if unset) **+** `handling`(base + weight + volume + nest). No storage-ml hint. **Multi-stack = sequential** (one `SecondsForStackFrom` + move each; no summed timer). Bag = item: `ItemStack.TotalWeight` includes Nested contents (volume = shell only).
- **이름 겹침 바**: 조회 SSOT=`ItemTimedNameProgress` (InventoryTimedMove → Gear Timed → 내구도). 소비자: 인벤 행·사이드 중첩가방 탭·Worn·Wield. Name 셀 stretch fill이 글자 **뒤**. 패널 Progress Slider 없음.
- 삽탄·장착/교체·분리·탄 빼기: `WeaponAmmoDuration` + `CharacterGearService.TryBeginDomainTimed` (`AmmoLoad` / `MagAttach`)

```mermaid
flowchart LR
  ui[ContextMenu_or_item_DnD]
  drop[WeaponAmmoDrop]
  svc[WeaponAmmoService]
  timed[GearTimedAction]
  mag[ItemStack.LoadedMagazine]
  supply[ItemInstance.SupplyRounds]
  ui --> drop --> svc
  ui --> svc
  svc --> timed --> mag
  timed --> supply
```

### Equip (from inventory)

- Inventory `Item` drag → Character L/R slot **or HUD QuickSlot L/R**: ammo/mag이면 `WeaponAmmoDrop` → `WeaponAmmoService` (삽탄·장착/교체). 아니면 `TryBeginWield` (`GearInventoryDrop`; two-hand item → `WieldHand.TwoHand`)
- HUD `Grp_QuickSlot` L/R = 같은 `WieldSlots` 소비자 (`UIHudQuickSlotController` + `UICharacterWieldSlotView`). 숫자 슬롯(1–9)은 별 경로.
- Inventory `Item` drag → Worn list / worn row: `TryBeginWear` if wearable; else no-op
- Success: `InventoryDragState.MarkConsumed` (floor drop suppressed). Drop on registered overlay window never floors.

### Wield slot chrome (Character + HUD)

| Corner | Content |
|--------|---------|
| Top-left | Action mode label (`CombatLeafRows.ResolveSelected`, none=`—`) |
| Top-right | Gun rounds only: mag-fed=`{Supply}/{cap}+{Chamber}` (`ItemAmmo.WieldGunRounds`); clip-fed=`{Chamber}/{clip_size}` (`ItemAmmo.WieldClipRounds`); non-gun hidden |

Fire consume → `CharacterAttacker.CommitAttempt` → `NotifyAmmoChanged` → both surfaces re-Bind.

### Unequip

- Worn row / wield slot: take off / unwield
- Double-click → body inventory; slot RMB includes 바닥에 놓기. Deposit/source remove는 `NotifyExternalStacksChanged`로 인벤 리스트를 갱신한다.
- Drag to floor: Character = outside Character window; HUD = outside overlay hit-test (`UIOverlayWindow` on QuickSlot). Ghost: shared `UIItemDragGhostService`
- Worn filter: **body part click** (toggle); FilterLabel click → 전체; hover does not sticky-filter
- Worn hover uses `AppendItemArmorHover` (Phase A/C fields)
- Two-hand unwield once
- Slot RMB **잡기**: 반대 한손 / 양손 (`TryBeginWieldGrip`). `TWO_HAND` 플래그는 한손 전환 불가. 이미 그 그립이면 disabled. 힘·손 결손은 신규 들기와 동일. 반대 칸에 다른 스택이 있으면 displace→body. 인출 딜레이 없이 `WieldSeconds`만.

## Phase A — Armor detail aggregates (UI only)

**Scope:** surface baked `ArmorDetailData` fields on Character **방해** tab + worn/item hover. Combat hit hooks = Phase D (`WearCombatDefense`). Wetness = E; BodyTemp tick = F; weather/vision = G. Layer/sided = Phase C (below). Pockets = Phase B (below).

### Aggregator contract (`WearStatsAggregator`)

Wear stacks only (Wield excluded).

| Field | Per body part | Totals (`WearArmorTotals`) | Rule |
|-------|---------------|----------------------------|------|
| `encumbrance` | sum | `TotalEncumbrance` | M0 (unchanged) |
| `warmth` | sum | `TotalWarmth` | M0 (unchanged; BodyTemp tab) |
| `coverage` | **max** | `MaxCoverage` | % semantics — sum would exceed 100 across layers |
| `max_encumbrance` | sum | `TotalMaxEncumbrance` | |
| `environmental_protection` | sum | `TotalEnvironmentalProtection` | **Phase E** → `WearEnvExposure` |
| `material_thickness` | sum | `TotalMaterialThickness` | combat ArmorRating = Phase D |
| `power_armor` | any covering piece | `AnyPowerArmor` | boolean OR |

API: `Aggregate(wear) → WearArmorTotals`, `ForPart(wear, partId) → WearPartArmorStats`, plus thin `*ForPart` helpers.

### UI surfaces

| Surface | Behavior |
|---------|----------|
| 방해 tab body graphic | Still shows **enc** per part (M0). Part hover → per-part A stats text via gear hover |
| 방해 tab totals line | `FormatEncTotals` (+ Phase E `FormatEncTotalsWithWetness`) |
| Worn row hover | Item A fields via `AppendItemArmorHover` (enc, max_enc, coverage, env_prot, thickness, power_armor, warm) |
| 장비 / 체온 | Worn list + filter; wield slots only on **장비**; 체온 = warmth graphic + Phase F BodyTemp totals |

### Migration parity (Character UX)

- Status tab / Mood HUD / StatusToggle launcher unchanged
- 방해 adds totals + hover detail only; does not replace enc graphic
- Checklist: `.claude/checklists/migration-parity.md`

## Phase B — Worn storage pockets → inventory sidebar

**Scope:** worn armor with `storage` / `pockets` appears as PlayerOnly inventory sidebar containers (parity with nested bags). Multi-pocket BN/DDA defs are **aggregated** into one Nested per stack. Combat/layer = not B.

### Pocket model

| Piece | Contract |
|-------|----------|
| Capacity | `ArmorDetailData.storage` (ml) or sum of `pockets[].volume_ml` |
| Nested | One `ItemStack.Nested` `InventoryContainer` (`armor_pocket:{itemId}`) |
| Draw moves | `pockets[].moves` → max → `ContainerData.draw_moves`; 0 = access 0 (handling only) |
| Sidebar | `UIInventoryListWindow` PlayerOnly: body + nested bags + **worn pockets** |
| Wear change | `PlayerGearHost` → `InventorySession.NotifySidebarLayoutChanged` |
| Icon | `ContainerVisualPresenter` resolves worn owner via `WornPocketRules.TryFindOwnerStack` |
| Tab drag | Worn pockets are **fixed** tabs (not container-as-stack); body-held storage bags remain movable |

### Bake (`Tools/bn_converter/convert.py`)

- `storage` parsed with `parse_volume_to_ml` (BN `"3 L"` / `"500 ml"`)
- Optional `pocket_data` → `armor.pockets[{volume_ml, moves}]`; if storage missing, sum pocket volumes
- BN currently uses legacy `storage` more than `pocket_data` — moves often absent → formula OK

### Migration parity (inventory path)

- Existing nested bag tabs / NearbyOnly / floor promote unchanged
- PlayerOnly sidebar show rule: bags **or** worn storage
- Transfer duration: convert moves→seconds and **add** handling (not replace)
- Checklist: `.claude/checklists/migration-parity.md` · UI: [`docs/inventory/INVENTORY_UI.md`](../inventory/INVENTORY_UI.md)

## Phase C — Armor layer + sided + wear overlap

**Scope:** bake `layer` / `sided` on `ArmorDetailData`; Wear gate rejects incompatible overlap. **No** auto take-off/replace (unlike Wield displace). Combat consume = D.

### Data

| Field | Type | Default when missing |
|-------|------|----------------------|
| `armor.layer` | string | `GearConstants.DefaultArmorLayer` = `NORMAL` |
| `armor.sided` | bool | `false` |

Bake: `Tools/bn_converter/convert.py` `export_armor_detail` — BN `layer` field **or** flags (`SKINTIGHT`→`UNDER`, `OUTER`/`BELTED`/`WAIST`/`PERSONAL`/`AURA`); `sided` field or infer (`*_either` / single unilateral part); covers expand (`hands`→`hand_l`+`hand_r`, …). Full catalog → `StreamingAssets/BNData` (RefData). Demo seeds remain in `GameData/items.json`.

### Overlap policy (SSOT: `WearOverlapRules`) — **reject**

Conflict when candidate and a worn piece share ≥1 `covers` part **and** same normalized layer (case-insensitive; empty→`NORMAL`), except:

- Both `sided` → allow up to `GearConstants.MaxSidedPerLayer` (2) peers on that part+layer
- Sided candidate vs non-sided peer on same part+layer → conflict

**No replace:** Wear does not auto-remove the conflicting piece (Wield may displace; Wear does not). User must TakeOff first.

Gate: `CharacterGearService.GetWearBlockedReason` → context menu disabled reason / hover. Defense: `EquipmentWearState.TryAdd` also refuses.

### UI

| Surface | Behavior |
|---------|----------|
| Wear context menu | Disabled + reason `FormatWearOverlap(otherName)` |
| Worn / item armor hover | `layer` (+ `sided` when true) via `AppendItemArmorHover` |

### Migration parity (Wear path)

- Prior Wear gates (busy / tool session / strength / already equipped) unchanged order; overlap after already-equipped
- Phase B worn pockets: reject keeps Nested ownership intact (no half-worn state)
- Checklist: `.claude/checklists/migration-parity.md`

## UI

- `UICharacterWindow` + `UICharacterController` (StatusToggle)
- Equipment: L/R slots + worn list + cover filter via body part click
- Encumbrance / BodyTemp tabs: worn enc/warmth on body graphic (BodyTemp tick = phase F)
- Phase A: Encumbrance totals + armor hover (above)
- Phase C: Wear overlap reject reason + layer/sided hover

## BN Bake Omissions

Catalog SSOT: [`BN_BAKE.md`](BN_BAKE.md) (converter whitelist + not-baked list + rebake log). Gear-only Dist stand-ins:

| Dist stand-in | BN not baked |
|---------------|--------------|
| `GearActionDuration` | wear/wield move-cost field |
| `WeatherKind` | overmap climate JSON / Dist `WorldWeatherHost` (XZ field = [`WEATHER.md`](../weather/WEATHER.md) Phase D Parked) |
| `HelmetVision` | helmet / visor FOV JSON |

Promote fields in `BN_BAKE.md` + `convert.py` together — do not grow a second table here.

## Phase D — Coverage / thickness combat + WearEnc hit hook

**Scope:** On hit, mitigate with worn layers (thickness+resist → ArmorRating, 3-way roll). Attacker WearEnc multiplies **ranged** stay-on-part chance (`HitChance`). Fail = neighbor scatter, still damage. Melee connect is overlap (no HitChance). **No** E env/wetness, F BodyTemp, G weather/vision.

### Parity contract (combat path) — before switch defaults

| Before D | After D (default) |
|----------|-------------------|
| `HitChance` = `CombatMath.HitChance` × `offenseFactor` only | × **`WearEncAccuracyFactor`** (attacker `WearStatsAggregator.Aggregate.TotalEncumbrance`) — **원거리만**, 조준 부위 유지. 실패=인접 산란(Miss 아님). 근접은 cue Overlap 확정 |
| Hit damage = `CombatMath.Damage` raw → `BodyDamageService.ApplyHit` | Same raw, then **`WearCombatDefense.MitigateDamage`** using **target** Wear |
| Aggregator unused by combat | Coverage/thickness via `ForPart`; material resist scanned on covering pieces only |
| No Wear → unchanged hit/damage | No `PlayerGearHost` / empty Wear → factor 1 / no mitigate (NPC targets OK) |
| Empty aimed part | Mitigate uses `BodyPartIds.Torso` |

Boundary SSOT: `CombatMath` = offense numbers; `WearCombatDefense` = Wear defense + WearEnc accuracy; `CharacterAttacker.TryPerform` wires both. Checklist: `.claude/checklists/migration-parity.md`.

### Named formulas (`WearCombatDefense`)

| Name | Formula |
|------|---------|
| `WearEncAccuracyFactor` | `1 − min(TotalEnc × WearEncHitPenaltyPerPoint, WearEncHitPenaltyCap)` |
| `ArmorRating` | `min(ArmorRatingCap, thickness × ThicknessToArmorRating + resist × ResistToArmorRating)` |
| `ArmorPen` | 원거리 `ammo.pierce` &gt; 0이면 그 값. 아니면 `raw × MeleeArmorPenPerDamage` |
| `MitigateDamage` | 덮는 조각 바깥→안. `유효=Rating−AP`. 0–100 난수: &lt;유효/2 튕김(0), ≤유효 절반, 아니면 무시. 완화 후 Sharp(cut/bullet)→bash |
| `MaterialResistForPart` | UI용: covering pieces max resist. 히트 Rating은 조각별 `MaxMaterialResist` |

Consts: `ThicknessToArmorRating=10`, `ResistToArmorRating=10`, `ArmorRatingCap=200`, `ArmorRollMax=100`, `MeleeArmorPenPerDamage=1.5`, `WearEncHitPenaltyPerPoint=0.01`, `WearEncHitPenaltyCap=0.35`. Layer 순서: `GearConstants.ArmorLayerOutsideRank` (AURA→…→PERSONAL).

**Lock (2026-08):** Phase D 완화는 **림월드 3단 주사**. BN `raw − (absorb − AP)` 차감은 폐기. 두께+resist는 ArmorRating만 — 차감과 동시에 쓰지 않음. coverage 주사는 히트에 쓰지 않음(`CoversPart`만). 이 공식을 다시 바꾸면 BN 데이터와 더 어긋남. **적용 전 사용자에게 알릴 것.**

```mermaid
flowchart LR
  stay[HitChance_stay]
  enc[WearEncAccuracyFactor]
  scatter[ScatterToNeighbor]
  raw[CombatMath_Damage]
  mit[MitigateDamage]
  apply[BodyDamageService_ApplyHit]
  stay --> enc
  enc -->|stay| raw
  enc -->|fail| scatter
  scatter --> raw
  raw --> mit
  mit --> apply
```

### Migration parity (combat)

- Dual wield / OffHand factor / obstruction Miss unchanged
- Ranged `HitChance` fail = neighbor scatter (body hit never Miss)
- Armor is post-hit only (does not convert to Miss). **덮는 조각** 바깥→안 3단 주사. 튕김=피해 0. Sharp 완화=bash(Bleed 시드 없음). `ammo.pierce`는 Rating 공간 AP (횟수 관통은 히트스캔/발사체와 공유)
- Outcome damage = post-mitigation. **하한 1 없음. HP 0 허용** (막힌 타 = 밀침 최대). Defeat BodyFatal = 의식 0 (뇌/피/감염/고통1/독소). 가슴·장기 HP0만으로는 즉사 아님 — [`docs/body/BODY.md`](../body/BODY.md)
- Phase B/C Wear/pockets/overlap untouched

## Phase E — env_prot + wetness

**Scope:** `TotalEnvironmentalProtection` reduces ambient wetness gain. Minimal 방해 UI. **No** BodyTemp tick (F), **no** weather/helmet vision (G).

### Parity contract (env path)

| Before E | After E (default) |
|----------|-------------------|
| Aggregator `env_prot` UI-only | Consumed by `WearEnvExposure.Tick` on `PlayerGearHost` |
| No wetness state | `Wetness01` 0..1 on host (`EnvExposure`) |
| No ambient moisture | Constant ambient pressure until Phase G weather replaces it |
| 방해 totals = A fields | + wetness / exposure% / env_prot line |

Boundary SSOT: `WearStatsAggregator` = armor sums; `WearEnvExposure` = wetness/exposure formulas; host ticks with `TimeScaleService.Delta(World)`. Checklist: `.claude/checklists/migration-parity.md`.

### Named formulas (`WearEnvExposure`)

| Name | Formula |
|------|---------|
| `ExposureFactor` | `1 − min(TotalEnvProt × EnvProtWetnessReductionPerPoint, EnvProtWetnessReductionCap)` |
| `WetnessDelta` | `(BaseAmbient × ExposureFactor − BaseDry × (1 − ExposureFactor)) × dt` |
| `Wetness01` | clamp 0..1 after delta |

Consts: `BaseAmbientWetnessGainPerSecond=0.005`, `EnvProtWetnessReductionPerPoint=0.05`, `EnvProtWetnessReductionCap=0.95`, `BaseDryRatePerSecond=0.01`.

```mermaid
flowchart LR
  wear[WearStatsAggregator_env]
  tick[WearEnvExposure_Tick]
  ui[Enc_tab_wetness_line]
  wear --> tick
  tick --> ui
```

### UI

| Surface | Behavior |
|---------|----------|
| 방해 totals | `FormatEncTotalsWithWetness` — A totals + wetness% / exposure% / env_prot |
| 방해 part hover | Part A stats + same wetness line |

### Migration parity (env)

- Phase D combat path unchanged
- Ambient stand-in only — weather intensity = G
- BodyTemp / 체온 tick = Phase F (below)

## Phase F — BodyTemp → 체온 탭

**Shipped now:** per-part `BodyTemp` + 틱 호스트 = `CharacterClimateHost`. 이 Phase F 표는 단일 코어 도입 계약. 현재 anatomy/틱 SSOT: [`docs/body/BODY.md`](../body/BODY.md).

**Scope (F 당시):** Tick body temperature from worn warmth (+ slight wetness cool). Wire Character **체온** tab. **No** weather/wind/helmet vision (G).

Time base: `TimeScaleService.Delta(World)` — F/G는 `PlayerGearHost`, 이후 ClimateHost. See [`docs/time/TIME.md`](../time/TIME.md).

### Parity contract (body temp path)

| Before F | After F (then) | Now |
|----------|----------------|-----|
| 체온 tab = warmth graphic + worn list only | + BodyTemp totals / feeling / target | 부위 그래픽 = `TryGetPartTempC` |
| No body °C state | `BodyTemp` on `PlayerGearHost` | ClimateHost 소유. GearHost는 포워드 |
| Warmth UI-only | `BodyTemp.Tick` (`TotalWarmth`) | `WarmthForPart` × `ThermalParts` |
| Ambient weather | Constant `BaseAmbientTempC` until G | `WeatherExposure.Resolve(kind, period, outdoor)` |

Boundary SSOT: `WearStatsAggregator` = warmth sums; `BodyTemp` = °C formulas (now per-part, ClimateHost 틱). Checklist: `.claude/checklists/migration-parity.md`.

### Named formulas (`BodyTemp`)

F 당시 단일 코어. **Shipped:** 부위 `WarmthForPart` + heat flow — [`BODY.md`](../body/BODY.md).

| Name | Formula |
|------|---------|
| `TargetTempC` | `clamp(ambient + warmth × DegreesPerWarmth − Wetness01 × WetnessCool, min, max)` — 부위별. 코어 min/max vs 말단 min/max |
| `BodyTempC` / `Feeling` | **chest**. 가슴 없으면 Comfort. Feeling 밴드 unchanged |
| Heat flow | arms←chest, hands←arms, legs←chest, feet←legs |

Consts: `ComfortBodyTempC=37`, `BodyTempMinC=27`, `BodyTempMaxC=43`, `ExtremityTempMinC=-10`, `ExtremityTempMaxC=48`, `FrostbiteOnsetTempC=0`, `BaseAmbientTempC=18`, `DegreesPerWarmth=0.5`, `WetnessCoolDegreesC=2`, `ConvergencePerSecond=0.08`, `ComfortBandHalfWidthC=1`, `HypothermiaBodyTempC=32`.

```mermaid
flowchart LR
  warm[WearStatsAggregator_warmth]
  wet[WearEnvExposure_wetness]
  tick[BodyTemp_Tick]
  ui[BodyTemp_tab_totals]
  warm --> tick
  wet -->|optional cool| tick
  tick --> ui
```

### UI

| Surface | Behavior |
|---------|----------|
| 체온 body graphic | 부위 `TryGetPartTempC` vs Comfort 편차 (`PlayerStatusBodyGraphicDisplay`). 없으면 present=false |
| 체온 totals line | `FormatBodyTempTotals` — °C / feeling / TotalWarmth / target |
| 체온 part hover | Part warmth + `FormatBodyTempLine` |

### Migration parity (body temp)

- Phase E wetness / D combat unchanged
- Weather multipliers / wind / helmet vision = Phase G (below)

## Phase G — Weather / wind + helmet vision

**Scope:** WeatherKind drives BodyTemp ambient °C and WearEnvExposure wetness gain (replaces F/E hard ambient stand-ins on host path). Head-covering worn armor → `VisionFactor` on `PlayerGearHost` + Character totals/hover line. **No** new window/key; **no** M0–F redesign.

Time base: World delta on `CharacterClimateHost`. Kind는 `WorldWeatherHost` (`TryGetKindAt` / `CurrentKind`). Period = `WorldClock.Period`. outdoor = `TileMapCacheHub.IsOutdoorEvaluation`. See [`docs/time/TIME.md`](../time/TIME.md) · [`docs/body/BODY.md`](../body/BODY.md) · [`docs/weather/WEATHER.md`](../weather/WEATHER.md).

### Parity contract (weather / vision path)

| Before G | After G (default) | Now |
|----------|-------------------|-----|
| BodyTemp ambient = const `BaseAmbientTempC` | Host passes `WeatherExposure.AmbientTempC` | `Resolve(kind, period, outdoor)` |
| Wetness ambient = const `BaseAmbientWetnessGainPerSecond` | Host passes `WeatherExposure.AmbientWetnessGainPerSecond` | 실내면 wetness 0 |
| No weather state | `WeatherKind` on GearHost | Kind = `WorldWeatherHost`; period/outdoor = ClimateHost |
| No helmet vision | `HelmetVision` → `PlayerGearHost.VisionFactor` | 동일 (GearHost) |
| 방해/체온 totals = E/F lines | + weather ambient + vision% | 체온 그래픽 부위별 |

Boundary SSOT: `WorldWeatherHost` = Kind; `WeatherExposure` = ambient; `HelmetVision` = GearHost; BodyTemp/wetness 틱 = ClimateHost. Checklist: `.claude/checklists/migration-parity.md`.

### Named formulas (`WeatherExposure` / `HelmetVision`)

| Name | Formula |
|------|---------|
| `Resolve(kind, period, outdoor)` | 실내 → `IndoorAmbientTempC` + wetness 0. 야외 → kind ambient + `ResolvePeriodOffsetC` |
| `AmbientTempC(Clear)` | `ClearAmbientTempC` (= `BodyTemp.BaseAmbientTempC`) |
| `AmbientTempC(Rain)` | `RainAmbientTempC` |
| `AmbientTempC(Wind)` | `ClearAmbientTempC − WindChillDegreesC` |
| `AmbientTempC(Snow)` | `SnowAmbientTempC` |
| Period offset | Night `-6` / Dawn `-3` / Day·Dusk `0` |
| `WetnessGain(Clear/Rain/Wind/Snow)` | Clear / Rain / Wind / `SnowWetnessGainPerSecond` |
| `BodyTemp.Target` | `ambient + WarmthForPart × DegreesPerWarmth − Wetness01 × WetnessCool` (부위) |
| `WetnessDelta` | `(weatherGain × ExposureFactor − BaseDry × (1 − ExposureFactor)) × dt` |
| `VisionFactor` | head covered → `HeadCoverVisionFactor`; else `FullVisionFactor` |

Consts: `ClearAmbientTempC=18`, `RainAmbientTempC=10`, `SnowAmbientTempC=-4`, `WindChillDegreesC=4`, `NightAmbientOffsetC=-6`, `DawnAmbientOffsetC=-3`, `IndoorAmbientTempC=18`, `ClearWetnessGainPerSecond=0`, `RainWetnessGainPerSecond=0.02`, `WindWetnessGainPerSecond=0.002`, `SnowWetnessGainPerSecond=0.004`, `IndoorWetnessGainPerSecond=0`, `HeadCoverVisionFactor=0.85`, `FullVisionFactor=1`, cover part = `BodyPartIds.Head`.

```mermaid
flowchart LR
  kind[WorldWeatherHost]
  period[WorldClock_Period]
  outdoor[IsOutdoorEvaluation]
  wx[WeatherExposure_Resolve]
  wear[WearStatsAggregator]
  env[WearEnvExposure_Tick]
  temp[BodyTemp_Tick]
  helm[HelmetVision]
  climate[CharacterClimateHost]
  ui[Character_totals]
  kind --> wx
  period --> wx
  outdoor --> wx
  climate --> wx
  wx -->|ambientTemp| temp
  wx -->|wetnessGain| env
  wear -->|WarmthForPart| temp
  wear -->|env_prot| env
  wear -->|head covers| helm
  env --> ui
  temp --> ui
  helm --> ui
```

### UI

| Surface | Behavior |
|---------|----------|
| 방해 / 체온 totals | weather label + ambient °C + vision% |
| Part hover (방해/체온) | same weather/vision line |
| Camera / tile FOV | **wired** — `CameraZoomController` ortho size × `PlayerGearHost.VisionFactor` (iso FOV stand-in) |

### Migration parity (weather / vision)

- Phase D combat / B pockets / C overlap unchanged
- Clear default preserves dry + 18°C ambient feel (wetness gain 0 vs legacy 0.005 stand-in — intentional G clear)
- No new Character key or window
- Ortho zoom target (scroll) unchanged; applied lens size = logical × VisionFactor; streaming `MaxOrthographicSize` unscaled

## Phase H — BodyTemp / wetness → move + combat

**Scope:** Consume Feeling + wetness for locomotion and attacker stay-on-part chance. Named consts in `GearEnvPenalties`. **No** Gear redesign; **no** new UI.

**Feeling still drives `GearEnvPenalties`:** 코어 `BodyTemp.Feeling` (chest) + `Wetness01`만. 부위별 °C는 frostbite(`FrostbiteOnsetTempC`)용 — 이동·히트 배율에 안 넣음.

Boundary SSOT: Feeling + wetness → `GearEnvPenalties`; ClimateHost → `CharacterMotor.SetEnvMovement`와 possessed `PlayerMovement.SetEnvMovement` **같은 값** (`BodyLocomotionPenalties.CombinedMoveSpeedFactor`); `CharacterAttacker.ResolveAttackerEnvAccuracyFactor`는 ClimateHost Feeling+wetness (없으면 1). 근접 확정 히트는 미적용. Checklist: `.claude/checklists/migration-parity.md`. See [`docs/locomotion/LOCOMOTION.md`](../locomotion/LOCOMOTION.md) · [`docs/body/BODY.md`](../body/BODY.md).

### Parity contract (move / combat path)

| Before H | After H (then) | Now |
|----------|----------------|-----|
| Move = base × enc × LiftStrain | × **`GearEnvPenalties.MoveSpeedFactor`** | × combined (Feeling+wetness × limp) |
| HitChance = CombatMath × offense × WearEnc | × **`GearEnvPenalties.HitAccuracyFactor`** × **`CombatImbalance.HitAccuracyFactor`** (원거리만, 부위 유지) | ClimateHost Feeling. Imbalance는 `1 − Imbalance`(호스트 없으면 1). 실패=산란 |
| Feeling / wetness UI-only (E/F) | Consumed for gameplay penalties | 코어 Feeling 유지 |
| No `PlayerGearHost` | NPC factors = **1** (스탠드인) | **ClimateHost가 대체.** 모터 env + 공격자 ClimateHost |

### Named formulas (`GearEnvPenalties`)

| Name | Formula |
|------|---------|
| `MoveSpeedFactor` | `FeelingMove(feeling) × (1 − Wetness01 × WetnessMovePenaltyPerUnit)` |
| `HitAccuracyFactor` | `FeelingHit(feeling) × (1 − Wetness01 × WetnessHitPenaltyPerUnit)` |
| FeelingMove | Cold 0.88 / Cool 0.95 / Comfortable 1 / Warm 0.97 / Hot 0.9 |
| FeelingHit | Cold 0.9 / Cool 0.96 / Comfortable 1 / Warm 0.97 / Hot 0.92 |

Consts: `WetnessMovePenaltyPerUnit=0.15`, `WetnessHitPenaltyPerUnit=0.1`, plus per-feeling move/hit factors above.

```mermaid
flowchart LR
  feel[BodyTemp_Feeling_core]
  wet[WearEnvExposure_Wetness]
  limp[BodyLocomotionPenalties]
  pen[GearEnvPenalties]
  climate[CharacterClimateHost]
  motor[CharacterMotor_SetEnv]
  move[PlayerMovement_SetEnv]
  hit[CharacterAttacker_HitChance]
  feel --> pen
  wet --> pen
  pen --> limp
  climate --> limp
  limp -->|same_factor| motor
  limp -->|same_factor| move
  pen -->|HitAccuracyFactor| hit
```

### Migration parity (env penalties)

- Enc / LiftStrain / WearEnc / dual-wield paths unchanged order (multiplicative)
- Phase H NPC factor-1 스탠드인은 **ClimateHost로 대체** (모터 env + 공격자 Feeling). ClimateHost 없는 공격자만 1
- `GearEnvPenalties` 입력은 코어 Feeling 유지 (per-part 아님)
- Phase G weather still feeds BodyTemp/wetness; H only consumes
- Checklist: `.claude/checklists/migration-parity.md`

## ClimateHost vs PlayerGearHost (post-H)

체온·습윤 틱을 GearHost에서 분리. Wear 합·시야·월드 Kind는 Gear 유지.

| Before | After (기본 경로) |
|--------|-------------------|
| GearHost가 BodyTemp + EnvExposure + Weather 틱/캐시 | ClimateHost가 World dt로 틱·ambient 캐시. GearHost는 포워드 |
| 단일 코어 °C | `ThermalParts` 부위별. Feeling(코어)만 GearEnv |
| NPC move/hit env = 1 | ClimateHost → `CharacterMotor.SetEnvMovement`; possessed는 같은 값을 `PlayerMovement.SetEnvMovement` |
| `WeatherExposure.Resolve()` Kind만 | `Resolve(kind, period, outdoor)`. outdoor = `IsOutdoorEvaluation` |

경계: Kind/`HelmetVision`/Wear = `PlayerGearHost`. ambient °C·wetness gain 캐시·틱·실내외·frostbite/heat/env 이동 = `CharacterClimateHost` (`Weather` getter 포워드). 전문: [`docs/body/BODY.md`](../body/BODY.md).

## Migration parity (Status → Character)

- Status content preserved on **상태** tab (body / vitals / skills)
- Mood Summary HUD unchanged
- Key/launcher still StatusToggle / one launcher → Character window
- Checklist: `.claude/checklists/migration-parity.md`

## UI layout SSOT / audit

**Prefab SSOT:** `Assets/Dist/Visual/Prefabs/UIComponents/PlayerStatus/Grp_PlayerStatusWindow.prefab`  
**Patch (keep permanently, MCP):** `Dist/MCP/PlayerStatus/Patch Character Tabs And Gear Panel`

| Issue | Status |
|-------|--------|
| TabBar / GearPanel missing on prefab → runtime magic chrome | **Fixed** — prefab hosts `TabBar` + `GearPanelRoot`; `Ensure*` only fills missing, does not overwrite existing Rect sizes |
| Tab = Button+TMP same GO, weak hit | **Fixed** — Button+Image parent, child TMP `Label` |
| Progress Slider AddComponent-only (no fill) | **Fixed** — Background + Fill Area/Fill wired on prefab |
| `ApplyTabVisibility` hid only vitals/skills Text | **Fixed** — also toggles `_statusContentRoot` (`Area_Content`) |
| Equipment body diagram still HP | **Intentional** — keep HP on Equipment; Enc/BodyTemp remap only |
| Font for created TMP | **Fixed** — DistUiFont / Galmuri7 (copy from prefab Title) |
| Legacy `UIPlayerStatusWindow` | **Debt** — leave until unused/safe to delete; scene uses `UICharacterController` |

### Ensure behavior (after patch)

- Prefab refs preferred (`_tabBarRoot`, `_gearPanelRoot`, `_gearPanel`, `_statusContentRoot`)
- Missing chrome → warn + fallback create once
- Gear list rows / wield labels may still spawn dynamically; chrome roots stay prefab
- Non-Status tabs: hide `Area_Content` + vitals/skills; show gear panel; body diagram stays

### Remaining debt

- TabBar vs hand-tuned BodyProfile spacing may need a one-line Prefab Mode nudge after play test
- Worn row chrome still mostly runtime for list items (Icon+Label roots patched on create)
- Localization keys for Character.* tabs may still fall back to `CharacterGearLabels` defaults until Merge Localization covers them

## UI Plan Parity Checklist

Contract: M0 `UICharacterWindow` schema in equipment plan · this doc.  
Gate: all rows **Pass** before declaring Character gear UI done.  
Last run: **2026-08-06 post-P0** (Play MCP smoke Pass).

| # | Contract | Status | Evidence |
|---|----------|--------|----------|
| L1 | Equipment: body diagram **left** + wield/worn **right** (2-col) | Pass | `GearPanelRoot` right; `Area_BodyStatus` off on gear tabs |
| L2 | Wield L\|R **horizontal** | Pass | `WieldRoot` HorizontalLayoutGroup |
| S1 | Wield: **item icon** primary; **no always-on name** | Pass | `Icon` Image; Label alpha 0 for name-bar only |
| S2 | Wield: **action** top-left; **ammo** top-right (gun only) | Pass | `ActionIcon` left; `Ammo` = `ItemAmmoLabels.FormatWieldGunRounds` |
| S3 | Worn: **icon** + name + covers + name-overlay bar | Pass | `UICharacterWornRow` Icon + Label + `ItemNameStatusBar` |
| H1 | Hover = text hover (strain/need Str hover-only) | Pass | `UITextHoverService` + Worn `AppendItemArmorHover` |
| A1 | Slot RMB = **사용 액션** group (CombatLeafRows.Available+None) + **잡기**(반대손/양손) + unwield/floor | Pass | `WieldSlotActionsContributor` → `HandActionGroup` + `WieldGripGroup` |
| F1 | Worn filter by body **click** (toggle); FilterLabel clears | Pass | `OnPartClick` / not hover sticky |
| T1 | Enc tab: enc body + worn; wield hidden | Pass | `showWield` Equipment-only |
| T2 | BodyTemp tab: warmth + BodyTemp totals | Pass | `FormatBodyTempTotals` |
| T3 | Tabs 상태\|장비\|방해\|체온 + key C | Pass | TabBar + StatusToggle |
| P1 | Progress = name-overlay bar only (no panel Slider) | Pass | Progress/HoverDetail removed from prefab |
| U1 | Status tab vitals/skills parity | Pass | Area_Content + BodyStatus on Status |

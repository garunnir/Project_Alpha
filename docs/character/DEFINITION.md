# Character Definition (Dist)

**SSOT:** `Assets/Dist/Scripts/Gameplay/Definitions/CharacterDefinition.cs`  
**에셋:** `Assets/Dist/SOData/Gameplay/Character/`  
**레거시:** `Assets/Dist/Legacy/Character/ActorSO.cs` — **참조하지 않음** (필드 계약만 이전)

## 편집 허브

Data Definitions (`Tools/Data Definitions`) **Characters/** 트리(Definitions · Factions · Emote)가 편집 진입점이다. BN Reference/Custom JSON과 섞이지 않는다. 리스트는 `Assets/Dist/SOData/Gameplay/Character/`. 상세는 SO Odin/Inspector(+ Alignment 위젯). `Characters/+ Create Definition`이 같은 폴더에 에셋을 만든다. 저장은 Unity 에셋(Ctrl+S). 창의 Save Changes는 Catalog(아이템/레시피) 전용.

### Editor (CustomEditor · Odin)

**MUST — Odin을 최대한 활용한다.**

| 계층 | 규칙 |
|------|------|
| SO (`CharacterDefinition` 등) | 섹션·라벨·툴팁은 `[FoldoutGroup]`, `[LabelText]`, `[Tooltip]` 등 **Odin 속성**으로 SSOT. Data Definitions 창과 Unity Inspector가 같은 트리를 본다. |
| `[CustomEditor]` | `OdinEditor` 상속 + `base.OnInspectorGUI()` 호출. Odin이 그린 필드 목록을 **다시 그리지 않는다** (`EditorGUILayout` foldout·`PropertyField` 전체 재구현 금지). |
| 예외만 수동 | Odin만으로 불가한 위젯만 `OnInspectorGUI`에 최소 추가 (예: `CharacterAlignmentDrawer` — `_alignment`는 `[HideInInspector]`). |

**MUST NOT:** Odin 속성 없이 CustomEditor만으로 인스펙터 레이아웃을 새로 짜기 — Data Definitions / Inspector 불일치·중복 유지보수.

**참조:** `CharacterDefinition.cs` (Odin attrs) · `CharacterDefinitionEditor.cs` (`OdinEditor` + Alignment drawer).

## Runtime debug

Play 전용 `Tools/Character Runtime Debug` (Odin). 대상 루트는 `CharacterBodyHost`. Hierarchy 선택 또는 창 상단 라이브 호스트 드롭다운. 인벤·Wear/Wield·정의 SO와 분리한다. 구현: `CharacterRuntimeDebugWindow` / `CharacterRuntimeDebugModel` (`CharacterRuntimeDebugDomain` = 커버 목록 SSOT).

| 탭 | 내용 |
|----|------|
| Target | possessed / Faction / Dead / Defeat·Revive / CancelAll |
| Body | `StatusConditionParts` HP(max−부상 합)·절단·복원·부위 효과 (`bruise`/`cut`/`gunshot`/`fracture`, intensity=HP 점수; `bandaged`/`bandage_dirty`/`hemostatic`는 붕대·지혈) |
| Illness | Blood/Toxin/Infection |
| Needs | 위장·Vitals·대사·Fatigue/SleepDebt·TrySleep/Wake ([`../needs/NEEDS.md`](../needs/NEEDS.md), possessed만) |
| Climate | Thermal °C·Wetness ([`../body/BODY.md`](../body/BODY.md)) |
| Skills | Attribute + 카탈로그 Base/Potential/Practice |
| Combat | Imbalance·**Pain**(Total/Effective/Factor·shock/wake 문턱·부위 Pain)·**Surprise**(시력 인지·기습 스턴 잔여)·Capacity·과적 Stage·**Senses**(Definition/Effective sight·hearing 반경)·**Locomotion**(Definition walk / motor base / current speed) |
| Mood | `CharacterMoodHost` 수치·사고·기억·Wander 붕괴 ([`../mood/MOOD.md`](../mood/MOOD.md), possessed만). HUD 칩과 별개 |
| Emote | `CharacterEmoteHost` resolved id/source·표시/숨김 사유 ([`EMOTES.md`](EMOTES.md)) |
| Chips | `PlayerStatusMoodEntries` HUD 칩 읽기 전용 |

**MUST:** 새 런타임 스테이터스(`ICharacterBody` 필드, `*Host` 시뮬 값, Vital 키, `BodyPartEffectIds`, `ThoughtId`/붕괴, 칩 입력, Defeat/Capacity 레이어)를 넣으면 **같은 변경에서** 위 인벤토리 행 + `CharacterRuntimeDebugDomain` + 창 탭을 갱신한다. BODY/NEEDS/MOOD 문서만 늘리고 창을 미루면 미완.

## 역할

`CharacterDefinition` SO는 캐릭터 **생성 스펙**이다. PC/NPC 구분은 스펙에 없다. Dist는 Dialogue `Actor` / `ActorSO`에 의존하지 않는다.

| 경로 | 책임 |
|------|------|
| `CharacterDefinition` | 스펙 SO (`CreateAssetMenu`: Dist/Character/Definition) |
| `CharacterDefinitionBinder` | 씬/프리팹 GO에 Apply (`DefaultExecutionOrder` -80) |
| `CharacterAppearanceHost` | 성향·초상·체형·이름 오버라이드 **저장만** (소비처 후속) |
| `CharacterFactory.Instantiate` | prefab + Apply. 맵 바인딩은 `MapGameplayBootstrap.BindSpawnedCharacter` |
| `CharacterSessionHub` | 본체 인벤·기어·액션·**Sight** 입구. Possessed만 `BecomePlayer` (`SessionSightHost`) |
| `CharacterSpawner` | 셀 SSOT 행 → Factory → `SetActive` → `CharacterSpawnGearApplier` → possess / `NpcManager.Register` |

## 스폰

씬 `CharacterSpawner`가 Play 시 행을 소환한다. PC/NPC 구분은 Definition이 아니라 행의 `CharacterSpawnRole`이다.

| 항목 | 계약 |
|------|------|
| 본체 프리팹 | `NpcSample` 하나. `PlayerParity` / `NpcParity` 모두 이 프리팹 |
| 위치 | `Vector3Int` 셀. 런타임 `IWorldGrid.CellToWorld`. 마커는 에디터 뷰 |
| 부모 | `Map/Characters` (`CharacterWorldRoot`, 맵 루트 이름은 `SmallItemSpawner.WorldMapRootName`) |
| possessed | `PlayerManager.Possess` → 입력 리그는 `PlayerPossessedInputHost`, 플레이어 세션은 본체 `CharacterSessionHub.BecomePlayer` (`GameplayData`·인벤 Runtime·Gear/Encumbrance/TimedMove `Active`) |
| NPC | `NpcManager.Register`. 프리팹에 AI MB 없음 |
| 맵 | 스폰 직후 증분 바인드 (Bootstrap Start Find만으로는 이후 스폰이 빠짐) |
| 로드아웃 | Definition `wearItemIds` / `wieldLoadout` / `bodyItemSeeds`. `SetActive` 직후 즉시 Wear/Wield (타이머 없음). 총이면 호환 탄창·탄을 채움 (카탈로그에 없으면 빈 총 + 경고) |
| 셀 기즈모 | `TileHelper.DrawOccupiedCellWire` (TileView Selected와 동일 박스) |

`CharacterMotor`는 `IPlayControllable` 존재만으로 possessed가 되지 않는다. `PlayerManager`가 켠 것만.

플레이어 전용 장치(카메라, 층 가시성, 시야 블렌드, `PlayerSight`, **캐릭터 시야 페이드** `CharacterSightFadeDriver`)는 시스템. 입력(`PlayerMovement` / Aim / Combat / Pointer)은 `PlayerPossessedInputHost`에 두고 본체 공용 API만 부른다. `PlayerController`를 `NpcSample`에 올리지 않는다. `PlayerSight`는 씬 시스템 리그: `PossessedTransformFollower`(위치) + `PlayerSightVisionBinder`(`CharacterVision` → Spot **시각 동기**/루트 **yaw**). 시야 **로직**(각·탐지·소실 부채꼴)은 본체 `CharacterVision` 공통 — NPC AI도 동일. 페이드 **표현**만 시스템 Driver/Host — [`SIGHT_FADE.md`](SIGHT_FADE.md).

인벤·기어 호스트는 본체 프리팹에 두고 **인스턴스마다** 컨테이너를 갖는다. 몸 그래프 입구는 `CharacterSessionHub` (NpcSample). NPC는 `BecomePlayer`를 부르지 않고 `Active`를 건드리지 않는다. Possessed만 허브가 `PlayerInventoryRuntime` Bind + Gear/Encumbrance/TimedMove `ClaimActive` + `GameplayData` + 상태 UI rebind를 한 번에 한다. 인벤 이동 게이지는 그 몸의 `CharacterActionHost`를 본다. possessed 몸은 `player-body`, 나머지는 `character-body-*` (레지스트리 충돌 방지). 살아 있는 NPC 몸은 Nearby 루트에 안 뜬다 (`IsAvailableToPlayer`). 쓰러진·고통 쇼크는 그 게이트를 연다 (`IsDefeated || IsPainShocked`).

## 필드

| 필드 | ActorSO 대응 | Dist |
|------|--------------|------|
| `id` | `Name` (Loc 키) | `Loc.Get(id)` |
| `displayName` | Display Name | 비어 있으면 `id` Loc |
| `portraitSprite` | spritePortrait | 저장만 |
| `faction` | (신규) | `CharacterFaction` SO 참조. 적대 판정은 `CharacterFactionCatalog` 관계표 |
| `alignment` | Status.Alignment | Vector2, 전용 에디터, 소비처 없음 |
| `attributes` | Status.Str..Cha | 기본 8 (`SkillGrowth.DefaultAttributeLevel`) |
| `skillOverrides` | Skill.Active* | BN `skillId` — 레거시 인덱스 아님 |
| `traits` | (신규) | 상시 패시브 id (`TraitIds`: survival, omniscience, omnivision). Apply → `CharacterTraitsHost` / possessed 시 `GameplayData.Traits` |
| `bodyMassKg`, 쓰리사이즈 | Body.Personalized | 저장만; 과적 미연동 |
| `partMasses` | (없음) | `BodyPartIds` 트리 노드 키. 절단 시 `CharacterAppearanceHost.RemainingMassKg`가 없는 부위 kg을 차감. **과적(encumbrance)은 미연동** — [`docs/body/BODY.md`](../body/BODY.md) |
| `prototypeSeed` | — | `CharacterBody.CreateHumanDefault` |
| `prefab` | — | Factory용 |
| `wearItemIds` | — | 스폰 즉시 Wear. 겹치면 이후 항목 스킵 |
| `wieldLoadout` | — | `itemId` + `WieldHand`. 양손 무기는 TwoHand. 실패 시 몸통 폴백 |
| `bodyItemSeeds` | — | 몸통 `AddItem`. 테스터 컨테이너 시드 대체 |
| `spotAngleDegrees` | — | 시야각 → `CharacterVision`. 탐지/유지 반경·청력 반경 → `CharacterSenseBlock` (`Senses`) |
| `walkSpeedMeters` | — | 걷기 속도(m/s) → `CharacterMotor` + possessed 시 `PlayerMovement` (달리기·관성 상한도 동일 비율 스케일). `0` = 프리팹·시스템 기본값 유지 |

**미이관:** HP 풀, Passive 슬롯, Equipment 인덱스, Dialogue int id, Bark 테이블.

## Apply 경계

```text
definition == null  → BodyHost/SkillsHost/TraitsHost 기존 시드 (CreateHumanDefault(8), CreateSeededSkills, empty traits)
NPC (UseGameplayData* = false):
  → CharacterBodyHost.BindBody + CharacterSkillsHost.BindSkills + CharacterTraitsHost.BindTraits (인스턴스 owned)
Possessed (CharacterSpawner → ConfigureAsGameplayDataOwner → Apply):
  → GameplayData.Stats / Body / RecipeMemory(reset) / Traits 직접 기록
  → Host Awake Ensure* 가 GameplayData alias
+ Appearance / Faction / CharacterVision / CharacterHearing / CharacterMotor walk speed (+ possessed `PlayerMovement`)
```

**Traits 읽기:** `GameplayData.Traits` (= `GameplayPlayerRuntime.Traits`). possess 후 `RegisterPossessedTraitsResolver`가 possessed `CharacterTraitsHost.Traits`를 우선 반환. **possess 전 읽기 금지** (fallback empty).

부위효과→숙련 수식(`BodySkillModifierAggregator`)은 `CharacterSkillsHost`만 붙인다. HUD ViewModel·`GameplayData` Body setter는 붙이지 않는다.

## 본체 vs 매니저

PC와 NPC는 **같은 본체 프리팹** (`NpcSample`: 모터·몸·Binder·공격·애니)을 쓴다. SO는 정보만 채운다. NPC 인스턴스에 `NpcCombatBehavior` / `NpcSenses` / `NpcSteerToPoint`를 Add하지 않는다.

| 경로 | 책임 |
|------|------|
| `CharacterMotor` | 공용 물리. possessed면 Player 채널, 아니면 World |
| `PlayerMovement` | 시스템(`PlayerPossessedInputHost`) 입력 드라이버. possessed 모터에 `ICharacterMotorDrive` 바인드. NPC에는 입력을 켜지 않음 |
| `NpcManager` | 비possessed 유닛을 행 단위 FSM으로 원격 틱 (2대 이상). `CharacterSenseContactResolver`로 Vision/Hearing 단일 Chase |
| `NpcSteer` | 조향 헬퍼 (MB 아님) |
| `CharacterActionHost` | 행위자 1줄 행동 큐(종류별)·게이지·CancelAll. [`ACTION.md`](ACTION.md) |
| `CharacterSessionHub` | 본체 세션 입구. possessed만 `BecomePlayer` |
| `CharacterMoodHost` | 기분·사고·Wander 양도. [`../mood/MOOD.md`](../mood/MOOD.md) |

`CharacterKind`로 PC/NPC를 나누지 않는다. 조종 여부는 `CharacterMotor.IsPossessed` (`PlayerManager.Possess`).

## NpcSample 본체 계층 (에디터 SSOT)

**SSOT:** `CharacterBodyPrefabOrganizeMenu` (`Dist/MCP/Character/Organize NpcSample Body Hierarchy` · `Ensure NpcSample Body Refs`).  
**Resolve:** 루트 `CharacterBodyRefs` — 스폰 시 1회 `ResolveFromHierarchy`, 소비자는 `GetBodyRefs()` / `GetInBody` (캐시 우선). **Update 등 hot path에서 `GetInBody` 금지** — Awake·바인드 시 캐시.

| GO | 역할 | 대표 컴포넌트 |
|----|------|----------------|
| **루트** | 물리·정의·애니·resolve 캐시 | `Rigidbody`, `CapsuleCollider`, `CharacterBodyRoot`, `CharacterBodyRefs`, `CharacterState`, `CharacterMotor`, `CharacterDefinitionBinder`, `CharacterLocomotionAnim`, `CharacterVaultHost`, `CharacterFootDustVfx` |
| **GameplayCore** | 몸 데이터·세션·전투·기분·기후 | `CharacterBodyHost`, `CharacterSessionHub`, `CharacterActionHost`, `CharacterArriveHost`, `CharacterAttacker`, `PlayerGearHost`, `PlayerInventoryHost`, … (`CharacterBodyPrefabOrganizeMenu.GameplayCoreTypes`) |
| **Senses** | 시야·청각·Presence | `CharacterVision`, `CharacterHearing`, `CharacterPresenceHost`, `CharacterSenseGizmo` |
| **Presentation** | 이모트·페이드·외형 | `CharacterSightFadeHost`, `CharacterAppearanceHost`, `CharacterEmoteHost` |

**MUST NOT:** 자식 GO에 `CharacterBodyRoot` / `CharacterBodyRefs` 중복. `PlayerController`를 NpcSample에 올리지 않음 (입력은 `PlayerPossessedInputHost`).

**Work Layer / Cell:** `CharacterTimedWorkPlayback` + `CharacterActionHost` Cell pipelines (`CharacterCellFarmPipeline` / Fish / Construction). Farm/Fish/Construction ActionHost MB·`CharacterFarmWorkHost` / `CharacterFishWorkHost` 삭제됨.

## 샘플

| 에셋 | 용도 |
|------|------|
| `CharacterDefinition.NpcParity.asset` | NpcSample 배선용 패리티 (능력치 8, undershirt + bat) |
| `CharacterDefinition.PlayerParity.asset` | 동일 패리티, prefab=`NpcSample`, plain + pistol(클립 탄) + 몸통 bag/egg |

## 검증

- Unity 컴파일 에러 없음
- Data Definitions → Characters/Definitions: NpcParity / PlayerParity, Alignment 위젯 (Inspector)
- NpcSample + NpcParity SO: Play 시 STR 8·기존 전투 시드 유지. 프리팹에 AI MB 없음
- IsoLand `CharacterSpawner`: Possessed 1 + Npc 2, 셀 서로 다름. 씬에 박힌 `>PlayerCharacter` / `NpcSample` 인스턴스는 비활성
- `NpcManager`: 스폰된 NPC만 틱. possessed는 건너뜀
- Binder/definition null: 기존과 동일 시드

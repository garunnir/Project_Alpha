# Combat Pipeline

세부: [`ACTION`](../character/ACTION.md) · [`GEAR`](GEAR.md) · [`WEAPON_VISUAL`](WEAPON_VISUAL.md) · [`DIG`](../map/DIG.md)

---

## 전체

```mermaid
flowchart TB
  subgraph L1["① 조준"]
    RMB[RMB] --> Sight[IAimSightProvider]
    Sight --> CS[CharacterState<br/>AimWorldPoint / InteractionDir]
    CS --> Resolve[TargetResolve<br/>Leaf·ResolveMode]
    Resolve --> Preview[ICombatTargetingPreview]
  end

  subgraph L2["② 시전"]
    LMB[LMB click / hold] --> Drv[ICombatPerformDriver]
    CS -.->|조준 상태| Drv
    Resolve -.->|동일 타겟 SSOT| Drv
    Drv --> TP[TryPerform Leaf]
  end

  subgraph L3["③ cue"]
    TP --> Att[CharacterAttacker]
    Att --> Cue[anim / cue]
    Cue --> H[IActionHandler]
  end

  H --> Hit[melee_hit]
  H --> Proj[spawn_projectile]
  H --> Dig[melee_block_target]
  H --> Chop[melee_plant_target]
  H --> Guard[raise_guard]
```

### Layer1 조준 3단

| 단 | 역할 | Excavate |
|----|------|----------|
| **Sight** | 시선·월드점. **입력**(`PlayerAimController` RMB) + `IAimSightProvider`. **Flatten Y**는 `CombatAimSightPolicy`. Dig LMB 잠금 시 `CombatAimSightHoldLock`(캐스트 스킵·SightDir=블록, 카메라 자유) | AimWorldPoint Y 유지 · 홀드 중 SightDir 고정 |
| **Resolve** | typed 타겟 + 사거리 clamp | `DigTileTargetResolver.TryResolveFromCombatAim` (발끝 transform→목표 셀 중심 월드 유클리드 ≤ `DigActionRangeCells × cellSize`) |
| **Preview** | 피드백 consumer | `MeleeBlockAimPreview` → `TilePresentationSystem.SetDigHighlight` |

`ICombatTargetingPreview`는 `ICombatPerformDriver`와 대칭 — `PlayerCombatController`가 perform Tick **이후** preview Tick. 원거리 Preview는 `UIAimPointer` + `TryPreviewRangedSpread` (후속 `RangedAimPreview` 이주 가능). Farm/건설 셀 세션은 입력·확정이 달라 합치지 않음.

---

## 이름 계층

```mermaid
flowchart LR
  Fam[Family<br/>Melee / Trigger / Etc] -->|UI 묶음| Leaf[Leaf<br/>Strike · Semi · Excavate…]
  Leaf -->|Presentation 한 줄| Entry[Entry]
  Entry --> Atk[WeaponAttack]
  Atk -->|logicId| Hand[Handler]
  Leaf -.->|연출만| Verb[AnimVerb<br/>Swing / Dig / Trigger…]
```

**동작 SSOT = logicId.** Leaf는 선택 칸, AnimVerb는 팔 클립.

---

## Leaf → 입력 → Handler

```mermaid
flowchart LR
  subgraph Melee
    S[Strike / Pierce] -->|click| MS[MeleeSwing]
    MS --> MH[melee_hit]
  end

  subgraph Trigger
    SB[Semi / Burst] -->|click| RT[RangedTrigger]
    AU[Auto] -->|hold| AH[AutoHold]
    RT --> SP[spawn_projectile]
    AH --> SP
  end

  subgraph Etc
    EX[Excavate] -->|조준+hold| EH[ExcavateHold]
    CH[Chop] -->|hold| CHD[ChopHold]
    EH --> MB[melee_block_target]
    CHD --> MP[melee_plant_target]
    MB --> DigP[CharacterDigPipeline]
    MP --> ChopP[CharacterChopPipeline]
  end
```

---

## Presentation 어디서 오나

```mermaid
flowchart TB
  Item[ItemData] --> Base{베이스 하나}
  Base -->|1| ByItem[By Item Id]
  Base -->|2 miss| BySkill[By Skill<br/>gun.skill]
  Base -->|3 miss| ByCat[By Category]
  Base -->|4 miss| Unarmed[Unarmed]

  ByItem --> Merge
  BySkill --> Merge
  ByCat --> Merge
  Unarmed --> Merge

  Item --> Q[qualities<br/>DIG / AXE…]
  Q --> Overlay[By Quality Id<br/>얇은 템플릿]
  Overlay --> Merge[합산: 없는 Leaf만 추가<br/>런타임 캐시 · bake 금지]

  Merge --> Pres[WeaponPresentation]
  Pres --> Att2[CharacterAttacker]
```

---

## 코드 위치

```text
Aim/          ① Sight (IAimSightProvider · CombatAimSightPolicy · CombatAimSightHoldLock)
Targeting/    ① Preview (ICombatTargetingPreview)
Perform/      ②  PlayerCombatController
Combat/       ③  CharacterAttacker · Handlers · Catalog
Map/Dig/      Resolve (DigTileTargetResolver · DigTileTargetAimPose) · Dig·Chop Pipeline
```

Data Definitions: **Combat** 루트 허브 → ① Catalog → ② Baselines/Quality → ③ Attacks → ④ Fallbacks. Catalog·허브에 Resolve 미리보기.

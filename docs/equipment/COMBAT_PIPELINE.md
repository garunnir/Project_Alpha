# Combat Pipeline

세부: [`ACTION`](../character/ACTION.md) · [`GEAR`](GEAR.md) · [`WEAPON_VISUAL`](WEAPON_VISUAL.md) · [`DIG`](../map/DIG.md)

---

## 전체

```mermaid
flowchart TB
  subgraph L1["① 조준"]
    RMB[RMB] --> Aim[IAimSightProvider]
    Aim --> CS[CharacterState<br/>AimWorldPoint / IsAiming]
  end

  subgraph L2["② 시전"]
    LMB[LMB click / hold] --> Drv[ICombatPerformDriver]
    CS -.->|조준 상태| Drv
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
Aim/          ①
Perform/      ②  PlayerCombatController
Combat/       ③  CharacterAttacker · Handlers · Catalog
Character/Cell/   Dig·Chop Pipeline
```

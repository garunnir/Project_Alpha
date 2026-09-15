# Mood (thoughts / break)

> LLM/에이전트용 Dist 기분·사고·정신붕괴 SSOT.
> 인덱스: `docs/README.md`
> **CharacterMoodHost / MoodSettings / 사고 HUD·붕괴 양도를 쓰거나 고치기 전에 이 문서를 읽는다.**

경로(호스트): `Assets/Dist/Scripts/Entity/Character/CharacterMoodHost.cs`  
경로(시뮬): `Assets/Dist/Scripts/Gameplay/Mood/`  
경로(HUD·산문): `Assets/Dist/Scripts/UI/PlayerStatus/`  
설정 SO: `Assets/Dist/SOData/Gameplay/Mood/MoodSettings.asset`  
상태 칩 HUD는 이 문서가 아님 — [`../needs/NEEDS.md`](../needs/NEEDS.md) · [`../body/BODY.md`](../body/BODY.md)

`MoodEntry` 상태 칩과 기분 수치는 별개다. 칩을 사고 합으로 바꾸지 않는다.

---

## 역할

| Type | Role |
|------|------|
| `MoodSettings` | 기준 기분·클램프·붕괴 문턱/확률·Wander·사고 표 SSOT |
| `CharacterMoodHost` | possessed 사고 합산 + 분 틱 + Wander 양도. `Active`. plain module (`CharacterBodyRefs`). Settings는 `MoodSettings` SO (`DefaultAssetPath`), 본체 Host Inspector 아님 |
| `MoodThought` | 런타임 한 줄 (`ThoughtId`, kind, offset, remainingMinutes) |
| `MoodSituationalCollector` | 고통·허기·갈증·체온·출혈·과적 재평가 |
| `MoodBreakRuntime` | Wander `NpcSteer`. 본체 AI MB 없음 |
| `MoodGameplayGate` | 붕괴 중 인벤/제작/손 작업 차단 |
| `MoodThoughtLabels` | 상태창·HUD 툴팁 문구 |

PC/NPC 스펙 분리는 없다. 조종은 `IsPossessed`. 붕괴는 **possess를 풀지 않는다.**

---

## 기분

`Mood = clamp(BaseMood + Σ thought.offset, MoodMin, MoodMax)`  
기본 `BaseMood=50`, `0..100`.

| Kind | 언제 | 지속 |
|------|------|------|
| Situational | 몸/니즈/체온/과적이 맞을 때 매 재계산 | 조건이 끝나면 사라짐 |
| Memory | 식사·구토·부패 섭취·붕괴 종료(Catharsis) | `durationMinutes` 월드분, 스택은 행 `stackLimit` |

식사 사고 offset은 `comestible.fun` (0이면 표 기본값). `Fun` 대사량은 Needs에 그대로 둔다.

---

## 붕괴

기분 ≥ `BreakThreshold`(35)이면 굴림 없음. 아래면 림월드식 **독립 분 틱 MTB**(누적 없음):

| 기분 | 밴드 | 평균 간격 |
|------|------|-----------|
| &lt; 35 | minor | `MinorBreakMtbDays` 10일 |
| &lt; 35×4/7 ≈ 20 | major | `MajorBreakMtbDays` 3일 |
| &lt; 35×1/7 = 5 | extreme | `ExtremeBreakMtbDays` 0.7일 |

분당 확률 = `1 / (mtbDays × WorldClock.MinutesPerDay)`. 성공 시 **Wander**. 문턱만 살짝 밑이면 평균 열흘이지, 몇십 분이 아니다.

시작:

1. `CharacterActionHost.CancelAll`
2. `PlayerPossessedInputHost.SetControlEnabled(false)` — 카메라·세션·니즈 유지
3. `AnyControlYielded` → 인벤·제작 창 닫기
4. `NpcSteer` 랜덤 포인트 배회 (`NpcCombatState`에 넣지 않음)

고통 쇼크면 Steer Stop. 기상 후 남은 분 이어감. Defeat면 신규 붕괴 없음.

종료: 지속 끝 → 입력 복귀 + `Catharsis` 기억.

후순위 Kind (구현 없음): Flee / Berserk / Catatonic. Tantrum은 벽 HP 없음.

### 창

| 연다 | 안 연다 |
|------|---------|
| 상태창(읽기) · 기분 칩 툴팁 · 메시지 로그 · 일시정지·설정 · 배속 | 인벤 / 루트 / 제작 / Wear·Wield·섭취 / 손 작업 컨텍스트 |

게이트: `MoodGameplayGate.IsBlocked`.

---

## HUD

요약 첫 슬롯 = 기분 아이콘(수치 밴드) + 툴팁(수치·사고 목록·붕괴 중이면 한 줄).  
상태창 Status 탭 바이탈 아래 같은 목록.

---

## Pending

| 항목 | 상태 |
|------|------|
| HUD 칩 수집 (`PlayerStatusMoodChipSlots`) | enum·로컬·카탈로그 자리만 — Tier A body emergency, hygiene, social, inspiration 등 Collect Pending |
| Situational 사고 수집 | `LowOxygen`·`PainShock`·`CapacityDown`·청결·소셜 등 `ThoughtId`·`MoodSettings` 행만 — `MoodSituationalCollector` Pending |
| Flee / Berserk / Catatonic | `MoodBreakKind`·HUD 라벨·`BeginKind` 자리 — 런타임 동작 Pending |
| 기분 DTO / 세이브 | 없음 |

카탈로그 SSOT: `MoodThoughtDefaults.Catalog` · 에디터 `Dist/MCP/Mood/Ensure Thought Catalog Rows`.

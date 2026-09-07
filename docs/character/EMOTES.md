# Character Emotes (world overhead)

> LLM/에이전트용 월드 이모트 SSOT.  
> 인덱스: [`docs/README.md`](../README.md)  
> 관련: [`DEFINITION.md`](DEFINITION.md) · [`ACTION.md`](ACTION.md) · [`../mood/MOOD.md`](../mood/MOOD.md) · [`SENSES.md`](SENSES.md)

경로(호스트): `Assets/Dist/Scripts/Entity/Character/CharacterEmoteHost.cs`  
경로(표현): `Assets/Dist/Scripts/UI/Character/UICharacterEmote.cs`  
카탈로그 SO: `Assets/Dist/SOData/Gameplay/Character/CharacterEmoteCatalog.asset`  
프리팹: `Assets/Dist/Visual/Prefabs/UIComponents/Character/Grp_CharacterEmote.prefab`  
Patch: `Dist/MCP/Character/Ensure Character Emote`

PlayerStatus HUD 칩·Mood 수치와 **별개** — 월드 머리 위 1슬롯 아이콘만 담당한다.

---

## 역할

| Type | Role |
|------|------|
| `EmoteId` | 감정·전투 ! 통합 카탈로그 키 (`MoodIconId`와 별개) |
| `CharacterEmoteCatalog` | sprite / tint / `ObserverOnly` SSOT |
| `CharacterEmoteHost` | 소스별 Request·우선순위·필터·가시성 게이트 (+ possessed Mood 구독, NPC combat alert API) |
| `UICharacterEmote` | WorldSpace Canvas + `WorldBillboard` presenter |

---

## 우선순위

동시에 **하나만** 표시:

```text
Combat > Dialogue(예약) > Mood
```

| Source | v1 | Priority |
|--------|-----|----------|
| `Combat` | NPC Alert/Chase | 높음 |
| `Dialogue` | 후속 Pixel Crushers | 중간 |
| `Mood` | possessed 기분 밴드 | 낮음 |

API: `Request(EmoteRequest)` · `Clear(EmoteSource)`  
`EmoteRequest`: `Id`, `Source`, optional `DurationSeconds`

---

## ObserverOnly (플레이어 본인 필터)

| Id | ObserverOnly | 의미 |
|----|--------------|------|
| `AlertSuspicious` | **true** | 청각 추적 — 노란 ! |
| `AlertSpotted` | **true** | 시야 Alert — 빨간 ! |
| Mood 밴드 | false | possessed 감정 표시 |

`ObserverOnly` 이모트는 `CharacterMotor.IsPossessed == true`일 때 Host가 **수락·표시하지 않음**.  
플레이어는 NPC 머리 위 전투 !만 관찰한다.

---

## Mood 매핑

`CharacterMoodEmoteMapper` — [`MoodThoughtLabels.ResolveMoodIcon`](../Assets/Dist/Scripts/UI/PlayerStatus/MoodThoughtLabels.cs) 밴드와 동일:

| Mood | EmoteId |
|------|---------|
| ≥80 | `MoodVeryHappy` |
| ≥65 | `MoodHappy` |
| ≥55 | `MoodSlightlyHappy` |
| ≥45 | `MoodNeutral` |
| ≥35 | `MoodSlightlySad` |
| ≥25 | `MoodSad` |
| ≥15 | `MoodVerySad` |
| &lt;15 | `MoodDepressed` |

v1: `CharacterMoodEmoteSource`는 possessed만 갱신. NPC Mood는 후속.

---

## NPC 전투 연동

`NpcManager` → `CharacterEmoteHost combat API` (UI 직접 참조 없음):

| 전이 | Emote |
|------|-------|
| `EnterAlert()` | `AlertSpotted` |
| `EnterChase()` + Hearing | `AlertSuspicious` |
| `EnterChase()` + Vision | combat clear |
| `ClearTarget` / `EnterDead` / `Release` | combat clear |

---

## 가시성 (Sight Fade)

- **possessed 본인:** fade gate 없음 — 이모트 항상 표시 가능
- **NPC:** `CharacterSightFadeHost.DisplayVisibility ≤ HiddenThreshold`이면 숨김  
  임계 SSOT: `CharacterEmoteSettings.HiddenThreshold` (= 청각 핑 기본값)

---

## 레이아웃

`CharacterEmoteLayout` — ActionGauge(`LocalY=2.2`) **위** `LocalY=2.55`, `sortingOrder=21`.

---

## Pixel Crushers Dialogue (후속)

v1 미구현. Host API만 예약:

- `EmoteSource.Dialogue`
- `SequencerCommandEmote(speaker, EmoteId, seconds)` (Dist 브리지)
- Entry custom field `Emote` / 대화 종료 시 `Clear(Dialogue)`

벤더 `Assets/Plugins/Pixel Crushers/**` 인플레이스 수정 금지. Legacy [`SequencerCommandActivate`](../Assets/Dist/Legacy/PixelCrushers/DialogueSystem/SequencerCommandUIActivate.cs) 패턴 참고.

---

## 검증

IsoLand Play:

1. possessed mood 변화 → 머리 위 감정 아이콘 (HUD 유지)
2. NPC 시야 Alert → 빨간 ! (플레이어 머리에는 없음)
3. NPC 청각 Chase → 노란 !
4. fade로 숨겨진 NPC → 이모트 없음
5. Runtime Debug **Emote** 탭: resolved id/source/hide reason

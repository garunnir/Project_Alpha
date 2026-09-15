# Character Sight Fade

**구현:** `CharacterSightFadeDriver` · `CharacterSightFadeHost` (plain module, `CharacterBodyRefs` 소유) · `CharacterSightFadeEvaluator`  
**시야 판정 SSOT (PC/NPC 공통):** `CharacterVision` — 각=`CharacterDefinition.spotAngleDegrees`, 탐지/유지 반경=`CharacterDefinition.Senses` → `EffectiveDetect/LoseRadius` (× HelmetVision)  
**청력·채널:** [`SENSES.md`](SENSES.md)  
**뷰(시스템만):** Driver/Host 페이드 · Spot 동기  
**페이드 전방:** `PlayerSight` 루트 forward (= Spot)

## 페이드 판정

1. 공통 `IsWithinConeXZ` (반경 + 시야각) — **Y 제한 없음**  
2. soft distance/angle fade  
3. **3D LOS** (옵션) — 눈높이 선분  
   - 벽: 시선 높이 + 발 층만 (전 층 밴드 아님)  
   - **Floor: gridY 교차 시** (위층이 수직으로 막음)

trait `omnivision` (만시): `CharacterSightFadeDriver`가 `EvaluateTarget` 없이 `SetTargetVisibility(1)` — 캐릭터 메시 사라짐 무효. 시야 cone/LOS 판정 자체는 그대로.

## 튜닝

| 위치 | 무엇을 |
|------|------|
| `CharacterDefinition` | Spot Angle Degrees |
| `CharacterDefinition.Senses` | sightDetect / sightLose / hearingRadius (기본값은 `CharacterVisionDefaults` / `CharacterHearingDefaults`) |
| `CharacterVisionDefaults` | Detect/Lose **폴백** (SenseBlock default 파생) |
| Driver Settings | FadeWidth, LOS, LosHeightOffset (시스템 Inspector). Host display 속도·epsilon은 `CharacterSightFadeSettings.DefaultUnity` — 본체 Host Inspector 아님 |

## See also

[`DEFINITION.md`](DEFINITION.md) · [`SENSES.md`](SENSES.md)

# Character Senses (Sight + Hearing)

**SSOT:** `CharacterDefinition.Senses` (`CharacterSenseBlock`)  
**시력:** `CharacterVision` (plain module, `CharacterBodyRefs` 소유) — XZ 부채꼴 (`IsWithinConeXZ`)  
**청력:** `CharacterHearing` (plain module) + `CharacterHearingEvaluator` — 3D 구형 (시야 API **미사용**)  
**채널:** `CharacterSenseContactResolver` — **Vision > Hearing**

## CharacterSenseBlock

| 필드 | 기본 SSOT | 런타임 |
|------|-----------|--------|
| `sightDetectMeters` | `CharacterVisionDefaults.DetectRadius` (10) | `CharacterVision.EffectiveDetectRadius` × HelmetVision |
| `sightLoseMeters` | `CharacterVisionDefaults.LoseRadius` (14) | `CharacterVision.EffectiveLoseRadius` × HelmetVision |
| `hearingRadiusMeters` | `CharacterHearingDefaults.BaseRadius` (8) | `CharacterHearing.EffectiveHearingRadius` |

`spotAngleDegrees`는 SenseBlock 밖 — Definition 필드 그대로 `CharacterVision`에 실시간 반영.

## Vision vs Hearing

| | 시력 | 청력 |
|---|------|------|
| 공간 | XZ 부채꼴 | 3D 구 |
| 거리 | `sqrt(dx²+dz²)` | `Vector3.Distance` |
| 방향 | forward + spotAngle | 없음 |
| 이동 | 불필요 | `CurrentSpeed ≥ MovementSpeedThreshold` |
| occlusion | SightFade 3D LOS (표현) | grid 벽 segment + \|ΔgridY\| 층 감쇠 + **대상 `Noise01`** |

## Presence (대상별 탐지 보정 — 가시성·소음 스탯)

**SSOT:** `CharacterPresenceHost` (`ICharacterPresence`, plain module) → `CharacterPresenceResolved.Evaluate`  
**튜닝:** `CharacterPresenceSettings.DefaultUnity` (코드 SSOT. 본체 Inspector 아님)

Listener(시력/청력 능력)와 분리 — `CharacterVision` / `CharacterHearing` 은 **탐지자**, Presence는 **대상** 스탯.

| 출력 | 범위 | NPC 소비 |
|------|------|----------|
| `Visibility01` | 0~1 | 시력 `EffectiveDetect/LoseRadius × Visibility01` (`NpcManager`) |
| `Noise01` | 0~1 | 청력 `audibility × Noise01` (`CharacterHearingEvaluator`) |

**v1 입력:** `IsStealthActive`, `CurrentSpeed`, `IsSprinting`.  
**v2 stub:** `BodyScale01`, `Transparency01` (=1).

`Noise01` 산식 (이동 중):  
`speedNorm × sprintFactor × stealthNoiseMultiplier` — 정지 시 0 (기존 `MovementSpeedThreshold`와 병행).

플레이어 토글: `PlayerStealthController` (C) → `CharacterState.IsStealth`.

소비 API: `CharacterPresenceHost.TryResolve` / `ResolveVisibility01` / `ResolveNoise01`.

## CharacterSenseContactResolver

한 틱·한 대상:

```text
Resolve(visionActive, hearingActive) → Vision 우선
```

| 채널 | NPC steer | Attack | Alert | 플레이어 핑 |
|------|-----------|--------|-------|------------|
| Vision | target transform | O | O | 없음 |
| Hearing | 적 grid 셀 | X | X (바로 Chase) | grid alpha (페이드 숨김) |
| None | — | — | ClearTarget | 없음 |

NPC: `NpcManager.RefreshTarget` + 단일 `Chase` + `ResolveSteerGoal`.  
플레이어: `CharacterHearingPingDriver` — `ShowsHearingPing` + `DisplayVisibility ≤ HiddenThreshold`.  
trait `omnivision` (만시): 핑 Driver가 overlay Clear 후 return — 청각 핑 무효.

## 기습 (Surprise)

**인지 SSOT:** `CombatSurprise.HasVisionOf` — **시력만**. 청력 Chase 중이어도 기습 가능.  
NPC가 이미 Vision 타깃이면 LoseRadius, 아니면 DetectRadius (`NpcManager.TryGetVisionLock`).  
피해: `ResolveCommittedHit`에서 배율. 근접 특수·애니: [`GEAR.md`](../equipment/GEAR.md) Melee connect.

## 청각 핑 (플레이어)

- **Driver:** `CharacterHearingPingDriver` (`IMapHearingPingDriver`)
- **Overlay:** `MapHearingPingOverlay` / `MapHearingPingRenderer` / `MapHearingPingHost`
- **튜닝:** `CharacterHearingPingSettings` (HiddenThreshold, MaxAlpha, Y offset, fade)
- 적 **메시는 SightFade** — 핑은 **바닥 quad** (셀 중심, audibility × MaxAlpha)
- 같은 셀 다수 적 → **max(audibility)** 1 quad
- trait `omnivision` → 핑 없음 (`GameplayData.Traits.Has(TraitIds.Omnivision)` — possessed resolver 경유)

## 맵 바인딩

- `CharacterHearing.BindMapCollision(MapTopologyLineCast)` — `MapGameplayBootstrap` (`CharacterBodyRefs.Hearing`, Start + 스폰 증분)
- 청각 핑 — `TileMapManager`가 `IMapHearingPingDriver.Init` + `MapHearingPingHost` Ensure

## 디버그

`Tools/Character Runtime Debug` → Combat 탭 → **Senses** (Definition base / effective radii). 스킬 목록에 넣지 않음.

Scene/Play 기즈모: `CharacterSenseGizmo.Draw` (`CharacterBodyRefs` OnDrawGizmos)

## See also

[`DEFINITION.md`](DEFINITION.md) · [`SIGHT_FADE.md`](SIGHT_FADE.md)

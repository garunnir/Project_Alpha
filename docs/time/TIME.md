# Game Time — 시간 레이어

> LLM/에이전트용 Dist 게임 시간 SSOT.
> 진입: `docs/README.md` · `docs/tech-stack.md` · 룰: `.cursor/rules/game-time.mdc` · 인덱스: `00-rules-index.mdc`
> **시간·배속·하루 시계 스크립트를 쓰거나 고치기 전에 이 문서를 읽는다.**

경로(코어): `Assets/Dist/Scripts/Gameplay/Time/` (`WorldClock` 등)  
경로(배속 SSOT): `Assets/Dist/Scripts/Static/Time/` — **Config** asmdef (`TimeScaleService` / `TimeScaleChannel`, Dist.Map·DistScript 공용)  
경로(UI): `Assets/Dist/Scripts/UI/Time/`  
경로(Editor): `Assets/Dist/Scripts/Editor/Time/`  
설정 SO: `Assets/Dist/SOData/Gameplay/Time/WorldClockSettings.asset`  
프리팹: `Assets/Dist/Visual/Prefabs/UIComponents/Time/Grp_TimeDisplay.prefab`

어셈블리: `TimeScale*` = `Config`; `WorldClock`/HUD = `DistScript` (별도 Time asmdef 없음)

---

## 왜 세 층인가

Unity `Time.timeScale`만 쓰면 **불릿타임(월드만 느리고 플레이어는 정상)** 이 깨진다.  
Dist는 `Time.timeScale`을 건드리지 않고, 아래 세 층을 분리한다.

| 레이어 | SSOT | 책임 | 비책임 |
|--------|------|------|--------|
| **TimeScaleService** | 채널 배율 + Push/Pop 스택 | 일시정지·배속·불릿타임 | 하루/시각, HUD 텍스트 |
| **WorldClock** | DayIndex + MinuteOfDay | 인게임 하루·시각 진행, 기간 이벤트 | 배속 정책, 입력 pause |
| **Time HUD** | Bridge → ViewModel → Panel | 시계 표시(읽기 전용) | 시뮬 진행 |

입력 메뉴 “정지”(`InputManager` 스코프)와는 **별개**다. 입력 정지는 플레이어 입력을 막고, 시간 레이어는 시뮬 배속을 담당한다.

```text
TimeScaleService
  Realtime (항상 1)     → UI 연출 / WaitForSecondsRealtime
  World                 → NPC·환경·WorldClock
  Player                → 플레이어 (불릿타임 면제 가능)
        │
        ▼ GetDelta(World)
WorldClock  →  MinuteChanged / DayChanged / PeriodChanged
        │
        ▼
Time HUD (Day N  HH:MM)
```

---

## TimeScaleService

호스트: 씬 배치 MonoBehaviour 싱글톤 (`DefaultExecutionOrder(-200)`, **Config** 어셈블리 — Map/DistScript가 모두 참조)

### 채널 (`TimeScaleChannel`)

| 채널 | 의미 |
|------|------|
| `Realtime` | 항상 scale `1`. Push 무시. UI/메뉴 연출용 |
| `World` | 월드·NPC·환경·`WorldClock` 진행 |
| `Player` | 플레이어 전용 delta (불릿타임 시 1 유지 가능) |

### API

```csharp
float scale = TimeScaleService.Instance.GetScale(TimeScaleChannel.World);
float dt    = TimeScaleService.Instance.GetDelta(TimeScaleChannel.World);
// GetDelta = Time.unscaledDeltaTime * GetScale(channel)

TimeScaleService.Instance.Push("bullet_time", TimeScaleChannel.World, 0.25f);
TimeScaleService.Instance.Push("bullet_time", TimeScaleChannel.Player, 1f);
// ...
TimeScaleService.Instance.Pop("bullet_time");  // 해당 키 전부 제거
```

기타: `HasModifier(key)`, `ClearAllModifiers()`, `Changed` 이벤트.

### Push / Pop (곱셈 스택)

배속을 **한 필드에 덮어쓰지 않는다.** 효과가 여러 시스템에서 겹칠 수 있으므로, 각각 **키로 등록**하고 끝날 때 **자기 키만 제거**한다.

- 같은 채널의 모디파이어는 **곱해서** 최종 scale을 만든다 (기준 `1`).
- `scale < 1` → 느림, `> 1` → 빠름, `0` → 정지.
- 슬로우 디버프가 여러 개 겹치면 더 느려지는 것과 같다.
- 일시정지(`0`)가 하나라도 있으면 `× 0`이라 그 채널은 멈춘다.
- 같은 키로 여러 채널에 Push 가능 → `Pop(key)` 한 번에 전부 해제.

예시:

| 스택 | World 최종 |
|------|------------|
| (없음) | `1` |
| bullet `0.25` | `0.25` |
| bullet `0.25` + pause `0` | `0` |
| pause Pop 후 | `0.25` (bullet 유지) |

권장 키 예: `"pause_menu"`, `"bullet_time"`, `"sleep_ff"`, `"gameplay_speed"`.

### 플레이어 HUD 배속 (`gameplay_speed`)

| 키 | 소유 | World+Player |
|----|------|----------------|
| `pause_menu` | 세팅 Overlay (`UISettingsController`) | `0` (세팅 열림만) |
| `gameplay_speed` | `GameplayTimeScale` (우상단 HUD) | Pause=`0`, 1x=Pop, 2x=`2`, Smart=작업 중 `10` |

`GameplayTimeScale`은 **키 소유 정책만** — `Pop` 후 `Push`로 독점. 곱 스택 중복 Push 금지.

**지능형(Smart):** `ReservedWorkHub`에 등록된 소스가 하나라도 `HasActiveWork`면 10x. 마지막 작업 종료 시 1x + 모드 Normal. 작업이 이미 켜진 채 Smart를 눌러도 종료 시 Normal로 돌아간다.

**예약 작업 소스 추가:** `IReservedWorkSource` 구현 → `ReservedWorkHub.Register`. Gear/제작 타입을 `GameplayTimeScale`에 `if`로 넣지 않음.

초기 소스: `PossessedActionReservedWorkSource` (`PlayerPossessSession.Current.ActionHost.HasCancellableWork` — 큐가 빌 때까지. 전투만 남은 상태는 제외), `GearReservedWorkSource`, `InventoryReservedWorkSource`, `CraftingReservedWorkSource`.

HUD: `Hud_TimeScale` (우상단, Summary 왼쪽 기본 앵커). Setup: `Dist/MCP/Time/Setup TimeScale HUD In Open Scene`.

세팅·Cancel: [`ui/SETTINGS.md`](../ui/SETTINGS.md).

### 소비 측 규칙 (연동 시)

- 월드/NPC·환경·맵 프레젠테이션: `TimeScaleService.Delta(World)` / `TimeNow(World)`
- 플레이어 이동·애니·카메라 추적: `Delta(Player)` / `FixedDelta(Player)` / `TimeNow(Player)`
- UI 타이머·메뉴 연출: `Delta(Realtime)` 또는 `WaitForSecondsRealtime`
- FixedUpdate: **`FixedDelta`** = `Time.fixedDeltaTime * scale` (레거시 FixedUpdate 패리티; `maximumDeltaTime`로 클램프). `fixedUnscaledDeltaTime` 베이스 금지(스파이크 시 Floor 터널링)
- **하지 말 것**: Dist 게임플레이에서 `Time.timeScale` / `Time.deltaTime` / `Time.fixedDeltaTime`을 배속 SSOT로 쓰기

정적 헬퍼 (Instance null 시 unscaled fallback): `Delta` / `FixedDelta` / `TimeNow`.

---

## WorldClock

호스트: `SceneSingleton<WorldClock>` (`DefaultExecutionOrder(-100)`)

### 상태

| 멤버 | 설명 |
|------|------|
| `DayIndex` | 인게임 일수 |
| `MinuteOfDay` | 하루 내 분 (int, SSOT) |
| `HourOfDay` / `MinuteOfHour` | 표시용 파생 (하루를 24등분) |
| `DayNormalized` | 하루 진행도 `[0, 1)` (분 + accumulator, 연출용) |
| `Period` | `Dawn` / `Day` / `Dusk` / `Night` |

이벤트: `MinuteChanged`, `DayChanged`, `PeriodChanged`.

`Period` 소비자: `CharacterClimateHost` → `WeatherExposure.Resolve(kind, period, outdoor)` (야외 Night/Dawn ambient 오프셋). 본문: [`docs/body/BODY.md`](../body/BODY.md).

진행식 (매 `Update`):

```text
AdvanceMinutes(
  TimeScaleService.GetDelta(World)
  × WorldClockSettings.WorldMinutesPerRealtimeSecond
)
```

`World` scale이 `0`이면 시계도 멈춘다. `Player` 채널과는 무관하다.

수동 설정: `SetTime(dayIndex, minuteOfDay)`, `SetSettings(so)`.

Play 디버그: `Tools/Environment Runtime Debug` (Odin). 구현: `EnvironmentRuntimeDebugWindow` / `EnvironmentRuntimeDebugModel` (`EnvironmentRuntimeDebugDomain` = Time / Weather / Outdoor).

| 입력 | API |
|------|-----|
| Day / Hour / Minute, Period 점프 | `WorldClock.SetTime` (기간 시작 분 = `WorldClockSettings`) |
| WeatherKind | `WorldWeatherHost.Instance.SetKind` (디버그 포워드: `PlayerGearHost.SetWeatherKind`) |
| 실내외 강제 | `CharacterClimateHost.DebugOutdoorOverride` (에디터 전용. Play 시작·종료 시 Map으로 리셋) |

배속은 HUD `GameplayTimeScale`. Period 라이팅·Kind VFX는 `WorldEnvironmentPresenter` ([`weather/WEATHER.md`](../weather/WEATHER.md)). 체온·습윤 직접 편집은 `Tools/Character Runtime Debug` Climate.

### WorldWeatherHost / Settings

메뉴: `Create → Dist/Time/World Weather Settings` · MCP `Dist/MCP/Time/Ensure World Weather*`.  
기본 에셋: `Assets/Dist/SOData/Gameplay/Time/WorldWeatherSettings.asset`.

| API | 의미 |
|-----|------|
| `CurrentKind` | 글로벌 Kind (Phase D: 플레이어 셀 샘플) |
| `TryGetKindAt(x,z)` | 공간 샘플 — 지금은 stub → `CurrentKind` |
| `SchedulerEnabled` | 계절 가중치 자동 전환 on/off |

SSOT: [`weather/WEATHER.md`](../weather/WEATHER.md).

### WorldClockSettings (SO)

메뉴: `Create → Dist/Time/World Clock Settings`  
기본 에셋: `Assets/Dist/SOData/Gameplay/Time/WorldClockSettings.asset`

| 필드 | 기본 | 의미 |
|------|------|------|
| `MinutesPerDay` | `1440` | 하루 길이(분) |
| `StartingDayIndex` | `1` | 시작 일 |
| `StartingMinuteOfDay` | `0` | 시작 시각(분) |
| `WorldMinutesPerRealtimeSecond` | `1` | World scale=1일 때 실시간 1초당 월드 분 |
| `DaysPerYear` | `364` | 파생 달력 연 길이. **DayIndex는 절대값이며 여기서 wrap하지 않는다.** |
| `DaysPerSeason` | `91` | 이름 있는 계절 길이 (`91 × 4 = 364`) |
| Dawn / Day / Dusk / Night start | 5:00 / 7:00 / 18:00 / 20:00 | `DayPeriod` 경계 (분, inclusive start). Night는 자정 wrap |

### Derived calendar (season)

계절은 `WorldClock` 상태가 아니다. `DayIndex`는 계속 절대 일수이고, `SetTime(dayIndex, minuteOfDay)` API는 그대로다.

파생은 `WorldCalendar` (`Gameplay/Time/WorldCalendar.cs`):

| 값 | 식 |
|----|----|
| `dayOfYear` | `DayIndex % DaysPerYear` (0 .. DaysPerYear-1, 음수 DayIndex는 양수 wrap) |
| `Season` | `dayOfYear / DaysPerSeason` → Spring / Summer / Autumn / Winter (index ≥3 은 Winter clamp) |

농사 서리(식물 고사)는 이 **Winter** 파생 + 야외 + 비온실만 본다. `CharacterClimateHost` frostbite와 별개다. 낮/밤 `Period`와도 별개다.

---

## Time HUD

PlayerStatus Summary와 동일 패턴:

```text
TimeUIBridge (Canvas)
  └─ TimeViewModel  ← WorldClock 이벤트 구독
        └─ UITimeDisplayController
              └─ UITimeDisplayPanel (HUD 레이어 Instantiate)
```

- 부모: `UICanvasLayerHost.GetLayerRoot(UICanvasLayer.HUD)`
- 앵커: **상단 중앙** (Summary 우상단·런처 좌상단과 충돌 회피)
- 표시 포맷 SSOT: `TimeDisplayFormat.DayTimePattern` → `Day {0}  {1:00}:{2:00}`
- TMP 폰트: Galmuri7 SDF (`TimeUIFactory.DefaultUIFontPath`)
- 창 크롬: Controller `Enable Drag Header` / `Enable Resize`(둘 다 기본 on) → `Area_Header`+`UIWindowDragHandler`(창 근처 시 표시) · 루트 `UIWindowResizeHandles`(Awake에서 8핸들 생성, 폭만 Inspector) + `UIWindowResizeProximity`(가장자리 근접 시 핸들). Inventory/Status는 Handles만(AlwaysHit, proximity 미사용).
- 접기/끄기: 공용 `UIWindowChromeBar` (`Area_Header`의 `Btn_Fold` / `Btn_Close`). 접기 = 헤더만 남김. 끄기 = 패널 `SetActive(false)` (이번 슬라이스 HUD 복원 런처 없음 — Play 재시작 전까지 숨음). 접힌 동안 헤더는 proximity hide를 끄고 시계 문자열은 `Txt_FoldedTitle`에 표시.

---

## Editor 메뉴 (Dist/MCP — Unity MCP / 에이전트용)

| 메뉴 | 역할 |
|------|------|
| `Dist/MCP/Time/Patch Display Resize Handles` | 구 `Area_ResizeHandle_*` 제거 + `UIWindowResizeHandles`/Proximity 부착 |
| `Dist/MCP/Time/Setup Canvas In Open Scene` | `System/Time`에 Service/Clock/Controller, Canvas에 Bridge, HUD 배선 (기존 `Hud_TimeDisplay` 로드만 — full bake 없음) |
| `Dist/MCP/Time/Ensure World Clock Settings Asset` | `WorldClockSettings` 에셋 Ensure |

Setup 전제: 열린 씬에 `Canvas` + `InputManager`(System 부모)가 있어야 한다.  
IsoLand에는 Setup이 적용·저장되어 있다. Full bake 정책: `.cursor/rules/ui-prefab-bake.mdc`.  
`Create → Dist/Time/World Clock Settings` 는 CreateAssetMenu(사람용)이며 `Dist/MCP` 와 별개다.

---

## Pending (의도적 미완)

| 항목 | 상태 |
|------|------|
| Dist `Scripts` 내 `Time.deltaTime`/`fixedDeltaTime`/`Time.time` 시뮬 경로 | **연동됨** (아래 Consumer 표) |
| 불릿타임 실전 콘텐츠(키 바인딩·연출) | 미구현 — API·소비 경로만 준비 |
| 저장/로드 | 런타임 메모리만 |
| 낮/밤 라이팅·월드 연출 | 없음 |
| Pixel Crushers `GameTime`/`DialogueTime` | Dist SSOT로 **채택하지 않음** |
| Plugins / Legacy / 벤더 코드의 `Time.*` | Dist 범위 밖 — 미개입 |

### Consumer 패리티 (Dist Scripts)

| 스크립트 | 채널 | API |
|----------|------|-----|
| `PlayerMovement` | Player | `FixedDelta` (`fixedDeltaTime * scale`, max clamp) |
| `CharacterFacingAnim` | Player (기본) / World (NPC) | `Delta` |
| `WorldBillboard` | Realtime (게이지 기본) / Inspector | `TimeNow` |
| `CameraFollowTargetDriver` | Player | `Delta` |
| `CharacterOcclusionDisplayDriver` | World | `Delta` |
| `TileMapChunkStreamer` (unload hysteresis) | Realtime | `TimeNow` |
| `GridCursor` (hold/repeat) | Realtime | `Delta` |
| Context menus | Realtime | `WaitForSecondsRealtime` (기존) |
| `PlayerGearHost` (Gear timed / `HelmetVision`) | World | `Delta` |
| `CharacterClimateHost` (BodyTemp / WearEnvExposure / frostbite·heat / env move) | World | `Delta` + `WorldClock.Period` |
| `CharacterHitStop` (로컬 히트스톱 지속) | Realtime | `Delta` (타이머만. World/Player Push 없음) |
| `WeatherExposure.Resolve` (ClimateHost) | WorldClock `Period` + World dt 틱 | `Resolve(kind, period, outdoor)` |
| `UICraftingWindow` (제작 대기) | WorldClock 게임 분 | `WorldClock.DeltaGameMinutes` |

구동작 계약: scale=1·모디파이어 없을 때 unscaled 기준이므로 Unity `timeScale==1`일 때와 동일. Dist는 `timeScale`을 쓰지 않으므로 기본 플레이 패리티 유지. 채널 Push 시에만 분기.

---

## 빠른 사용 예

**불릿타임**

```csharp
var t = TimeScaleService.Instance;
t.Push("bullet_time", TimeScaleChannel.World, 0.25f);
t.Push("bullet_time", TimeScaleChannel.Player, 1f);
// 해제
t.Pop("bullet_time");
```

**전역 일시정지 (시뮬만)**

```csharp
var t = TimeScaleService.Instance;
t.Push("pause_menu", TimeScaleChannel.World, 0f);
t.Push("pause_menu", TimeScaleChannel.Player, 0f);
// UI는 Realtime / WaitForSecondsRealtime
t.Pop("pause_menu");
```

**시계 읽기**

```csharp
var clock = WorldClock.Instance;
int day = clock.DayIndex;
int hour = clock.HourOfDay;
int minute = clock.MinuteOfHour;
float day01 = clock.DayNormalized; // [0, 1) 연출/회전체
DayPeriod period = clock.Period;
```

씬 HUD 바늘 등: `WorldClockNormalizedRotatorBinder`가 `DayNormalized` → `UINormalizedRotator.SetNormalized` (LateUpdate).

# Inventory UI 문서

> LLM/에이전트용 Dist 인벤토리 UI SSOT.
> 인덱스: `docs/README.md` · 룰: `.cursor/rules/inventory-ui.mdc`
> **인벤 UI 스크립트를 쓰거나 고치기 전에 이 문서를 읽는다.**

경로: `Assets/Dist/Scripts/UI/Inventory/`  
프리팹: `Assets/Dist/Visual/Prefabs/UIComponents/Inventory/`  
어셈블리: `Dist.Inventory.UI` (`Assets/Dist/Scripts/UI/Inventory/Dist.Inventory.UI.asmdef`)



---



## 절대 원칙 (의존성 분리)



- 인벤토리 UI는 일반 UI 시스템(`UIController`, `UIModel`, `UIStatus*`, `UIMap*`)에 의존하지 않는다.

- 일반 UI가 인벤토리 구현체를 직접 참조하지 않도록 `IInventoryOverlayController` 인터페이스 경계로 연결한다.

- 런타임 컨테이너 조회는 `InventoryContainerRegistry`의 `ContainerId` 경계로 수행한다.

- 인벤토리 런타임 데이터 접근은 `PlayerInventoryRuntime`, `InventorySession`, `InventoryContainer` 경계에서만 수행한다.

- 인벤토리 입력 정책은 `InputManager` SSOT만 사용하고, 별도 입력 라우터/채널을 만들지 않는다.

- 땜빵 금지 — 전역 `.cursor/rules/bug-fix-gate.mdc`. 증상 회피 분기보다 **상태 소유권/이벤트 경계** 먼저.

- 드래그 수명주기(`Begin/Commit/Cancel`)는 단일 상태 소유자에서 종료한다.

- `InventoryDragState.End()`는 `UIInventoryController.FinalizeItemDrag` / `CleanupIfNoWindowsOpen`에서만 호출한다. `OnDrop`은 이동·선택 해제만 수행하고, 종료는 `OnEndDrag` → `OnItemDragEnded` 한 경로로 모은다.
- 드래그 상태 SSOT: `InventoryDragState`는 DistScript (`Assets/Dist/Scripts/UI/`) — 창 간 Item DnD 공유.
- 창밖 바닥 투하: `UIOverlayWindowHitTest.ContainsScreenPoint`가 false이고 `WasConsumed`가 아닐 때만 `floor-loot`로 `MoveStacks`. 등록된 Dist 창(인벤·Character·Time 등) Rect 위에서는 투하 금지.
- 창 위 포인터 SSOT: `UIOverlayWindow` (Enable 등록) + `UIOverlayWindowHitTest`. Zoom/Aim 억제·바닥 투하 판정 동일 API.
- 창 레이어 내 활성화: `UIOverlayWindowActivate` (Canvas/`UICanvasLayerHost`) — 포인터 프레스 raycast → parent `UIOverlayWindow.BringToFront` (같은 레이어 sibling만).

- `LateUpdate`는 드래그 종료·고스트 위치가 아니라 창 위 포인터 캐시·Zoom/Aim 억제 전용이다.
- 드래그 고스트 SSOT: `UIItemDragGhostService` (UICanvas 컴포넌트, 인스턴스는 `UICanvasLayer.TopMost`). 특정 창/컨트롤러 비소유.
- 드래그 고스트 위치는 `OnBeginDrag` → `Show`, `OnDrag` → `SetScreenPosition` 이벤트 경로만 사용한다. 종료 시 `Hide` (`FinalizeItemDrag` / `CleanupIfNoWindowsOpen` 포함).
- 아이템 드래그는 스크롤 오버레이(`InventoryScrollDragOverlay`)를 켜지 않는다. 오버레이는 리스트 스크롤 드래그 전용이다.



---



## 창 구조



- `UIInventoryListWindow` 하나의 뷰를 플레이어창/루팅창에서 **동일하게 재사용**한다.

- 차이는 `InventoryWindowMode` 데이터 바인딩뿐이다.

  - `PlayerOnly`: 플레이어 컨테이너(`player-body`) 단일 리스트. 중첩 가방 탭 **또는 착용 storage 포켓**이 있으면 사이드바 표시, 없으면 숨김. 착용 포켓 SSOT: `WornPocketRules` + `EquipmentWearState` (`docs/equipment/GEAR.md` Phase B).

  - `NearbyOnly`: `NearbyContainerDetector`가 등록한 주변 컨테이너 전체를 사이드탭으로 표시 (플레이어 제외). `TrackLootContainer` 없음 — 반경 스캔만 사용. **합산 탭** (`loot-aggregate`, `LootAggregateHost`): 사이드바 **맨 위(index 0)** — 포착된 모든 루팅 소스의 물리 스택 참조를 한 리스트로 표시(UI는 `IItemStackDisplayEquivalence`로 동일 키 행 묶기). Session `TryAddSidebarContainer`에 등록하지 않음 — `GetSidebarContainersForMode`가 index 0에 주입. 선택 시 `LootProximityCoordinator` 생략 → `SetActiveContainer`만. **집어넣기(drop in) 불가** — 가상 보기·꺼내기 전용(S5 DnD 가드). `floor-loot` 및 그 중첩 탭은 사이드바 **맨 아래** 그룹. 바닥 안 휴대 컨테이너(Nested)는 Detector가 managed 월드 루트로 promote하고, 사이드바 탭은 PlayerOnly body 유도와 같이 floor 스택에서 유도한다.
  - 살아 있는 캐릭터 몸 인벤(`player-body` / `character-body-*`)은 Nearby에 넣지 않는다. possessed는 id 스킵, 그 외는 `PlayerInventoryHost.IsAvailableToPlayer`가 자기 몸만 true. **쓰러진·고통 쇼크 NPC 루팅**은 `IsDefeated || IsPainShocked`일 때 그 게이트를 열어 몸 컨테이너를 Nearby 탭으로 쓴다. `character-body-*` 탭 표시 SSOT: `PlayerInventoryHost.GetBodyLootDisplayKind` — 사망(`dead_body`) / 기절·무력(`unconscious_body`, 고통 쇼크·비사망 Defeat 포함). 라벨=캐릭터 이름, 아이콘=`ContainerIconCatalog` → 초상화 폴백 (`ContainerVisualPresenter`).
  - 감지 SSOT: 컨테이너 후보 판정은 `InventoryContainerRegistry` provider 목록 + `CharacterState.ResolveGridCell`(WorldGrid 기준) 단일 경로를 사용한다. `ContainerGridRegistry`는 Nearby 판단 경로에서 사용하지 않는다.

- 월드 컨테이너 표현은 **TilePresentationSystem** 단일 진입점 → `TileViewPresentationApplier`. UI는 Applier를 직접 호출하지 않는다.
- 루팅 파이프라인: `NearbyContainerDetector` → `LootProximityCoordinator` 이벤트 → `{ TilePresentationSystem, UIInventoryController }` 각각 구독. 컨테이너 TileView는 `SetSelected` → URP **RenderingLayer** 외곽선.
- `NearbyOnly`: 사이드탭이 없으면 아이템 리스트도 비움. 활성 탭 1개만 월드 하이라이트.
- 사이드탭 표현: `Normal` / `Selected` / `Dragging`. 중첩 가방 탭은 드래그 소스(컨테이너째), 고정 컨테이너 탭(`player-body` / `floor-loot` / 월드)은 내용물 전체 드래그(스택 순차 이동, 중량·부피 초과 시 중단). **`loot-aggregate` 합산 탭은 드롭 타겟·내용물 드래그 모두 불가** — 행 DnD OUT은 `(owner, stack)` fan-out 후 `InventoryTimedMoveHost.TryBeginMultiSourceSequentialUntilFull`로 **물리 스택 1개씩** 순차 이동(소스 컨테이너가 달라도 동일 큐). 나머지 탭은 드롭 타겟.
- Transfer duration: `InventoryTransferDuration` SSOT — MoveStacks / 퀵이동 / 창밖 투하 / 가방→Gear 인출에 적용 (`InventoryTimedMoveHost`). 소스 `draw_moves`→초(`CombatMath.MovesPerSecond`) **+** weight/volume/nest handling (storage-ml 가산 없음). **분할 가능 스택은 개수 1단위·물리 스택 단위로 큐에 넣어 순차**(각각 개별 딜레이→`MoveStackCount`). Nested·탄창 부착은 통째 이동. 가방 중량 SSOT=`ItemStack.TotalWeight`(Nested 내용물 포함; `TotalVolume`은 외형만). **진행 UI**: `ItemTimedNameProgress` → 리스트 행·중첩가방 탭 Name stretch fill(idle=내구도, busy=로딩; Gear Wear/Wield·HandWork act도 동일). `Dist/MCP/Inventory/Patch Row Name Status Bar`. 상세: [`docs/equipment/GEAR.md`](../equipment/GEAR.md).
- 섭취 손 파이프: `CharacterHandWork` — Unwield(든 것→body) → Wield(대상) → `ConsumeDuration` mealtime. ESC는 현재 단계까지(롤백 없음).
- 착용 포켓 탭: Wear 변경 시 `InventorySession.NotifySidebarLayoutChanged`로 사이드바 Sync. 착용 포켓은 고정 탭(컨테이너째 드래그 아님); 아이콘은 착용 아이템.
- `Area_List`·`Area_Sidebar`(`SlotRoot`)는 세로 스크롤바(`Scrollbar_Vertical`, AutoHideAndExpandViewport)를 프리팹에 내장한다. 창 전체 rebake 금지 — `Dist/MCP/Inventory/Patch Window Scrollbars`로만 패치 (`Area_InvInfo` 보존). 사이드바는 `InventorySidebarScrollRect`(탭 DnD 중 스크롤 드래그 무시); 사이드바 Viewport에는 `InventoryScrollDragHandler` 없음.
- 컨테이너 상호작용은 `Interactable`의 레거시 스프라이트 아웃라인을 사용하지 않는다. 포커스 시각효과는 타입별 전용 컴포넌트(`SpriteOutlineFocusVisual` 등)로 분리한다.
- `ContainerInteractable`는 런타임 `containerId` 충돌 시 자동으로 고유 suffix를 부여해 레지스트리 충돌을 방지한다.

- 프리팹 컴포넌트 배선은 프로젝트 공통 규칙(`.cursor/rules/collaboration-unity.mdc` §Prefab Component Wiring)을 따른다. 인벤 UI·월드 컨테이너 프리팹 모두 런타임 `AddComponent` 금지.

- 두 창은 **독립 open/close** (`TogglePrimaryWindow`, `ToggleLootWindow`). 레이아웃 프리팹은 **하나** (`Grp_InventoryListWindow`) — Primary/Loot는 인스턴스·모드·제목만 다름.
- `Area_InvInfo` (`Txt_Weight` / `Txt_Liter`): 선택 컨테이너 used/max 무게(kg)·부피(L).
- 창 위치는 **상단 헤더 드래그**로 자유 이동, **8방향 리사이즈 핸들**(상·하·좌·우 + 4모서리)로 크기 조절 (`UIWindowResizeHandles` + `UIWindowResizeHandler` / `WindowResizeEdge`). 핸들은 프리팹에 두지 않고 런타임 생성(폭만 Inspector).
- 크기 제한: 최소 폭은 `UIItemListRow` 프리팹 열+창 chrome(사이드바 켠 상태·세로 스크롤바)에서 계산, 높이 240. 최대 Canvas의 75%×78% (`InventoryListColumnLayout.MeasureMinRowWidth` / `InventoryWindowLayout.ComputeMinWidth`). 행 열 폭·이름 폰트는 `InventoryListColumnLineLayout`이 행 프리팹에 반영. **StatusFill Rect는 프리팹 SSOT** — 런타임이 위치·stretch를 덮지 않는다.

- 사이드탭 클릭 시 해당 컨테이너 아이템 리스트로 즉시 전환.
- **창 선택 SSOT:** `UIInventoryListWindow.SelectedContainer` — 활성 탭 하이라이트와 리스트 `Bind`는 `SetActiveContainer` 단일 경로로만 갱신한다. 사이드바/리스트 단독 갱신 금지.
- **루팅 월드 SSOT:** `LootProximityCoordinator` — 월드 컨테이너 하이라이트·감지 목록 SSOT. `NearbyOnly` 사이드탭 클릭은 **`SetActiveContainer` 직행**(창 선택 SSOT); 비합산 탭은 부가로 `RequestActiveContainer`로 월드 하이라이트만 동기화(coordinator가 이미 동일 id면 이벤트 없어도 창은 전환됨). **`loot-aggregate` 합산 탭**은 coordinator 미사용. 루팅창 기본 선택은 루팅 소스가 있으면 합산 탭 우선.
- **스택 수량:** 런타임 `MaxStack` 상한 없음 — 동일 `ItemMergeKey`는 컨테이너당 스택 1개·Count 무제한(용량은 weight/volume policy만). UI 표시 묶기도 MaxStack 미사용(`IItemStackDisplayEquivalence`).



---



## 프리팹



### UI 프리팹



| 프리팹 | 역할 |

|--------|------|

| `Grp_InventoryListWindow` | 리스트 + 사이드바 창 (상단 드래그 헤더 포함). 헤더 `UIWindowChromeBar` 접기(헤더만) / 끄기(`ClosePrimaryWindow` / `CloseLootWindow`). `Area_List/Viewport/Area_ColumnHeader` sticky 헤더 클릭으로 뷰 전용 정렬 (`UIItemListColumnHeader` → `UIItemListView.SetSort`). 컨테이너 스택 순서는 불변. |

| `Grp_ItemListRow` | 아이템 행 (LeanPool). 컬럼: Icon | Category | Name(flex) | Count | WeightValue | WeightUnit(kg) | VolumeValue | VolumeUnit(L). 폭 SSOT: `InventoryListColumnLayout`. |

| `Grp_ContainerSlot` | 사이드바 컨테이너 슬롯 — 아이콘·라벨 SSOT는 `ContainerVisualPresenter` (월드 타일 thumbnail → provider SpriteRenderer → 중첩 가방은 item icon; `floor-loot`·`loot-aggregate`·NPC 몸 `unconscious_body`/`dead_body`는 `ContainerIconCatalog`, 미할당 시 DefaultIcon·NPC 몸 라벨은 캐릭터 이름) |

| `Grp_InventoryDragGhost` | 드래그 고스트 비주얼 (`UIInventoryDragGhost`). 소유: Canvas `UIItemDragGhostService` → 런타임 TopMost. `Setup Canvas Overlays In Open Scene` |

| `InventoryScrollDragOverlay` | 스크롤/드래그 중 전체 캔버스 레이캐스트 차단 (`UICanvas` 하위) |

| `InventoryItemDetailPanel` | 리스트 행 호버 상세 보조창 (`UICanvas` TopMost via `UIHoverCanvasLayer`, `Setup Canvas Overlays In Open Scene`) |



폰트: 텍스트가 있는 행/슬롯 프리팹 TMP는 `Galmuri7 SDF` (`DistUiFont`) 사용.

아이콘 resolve (`ItemVisualPresenter.GetDisplayIcon`):

1. `ItemIconCatalog` 할당분 (`Assets/Dist/Resources/ItemIconCatalog.asset`) — GameData 커스텀·수동 오버라이드. 편집은 **Tools/Data Definitions** 아이템 상세 Override.
2. BN 타일셋(MSX++) — `StreamingAssets/BNData/tileset/item_sprites.json` + 아틀라스 PNG. item id와 타일 id가 같거나, BN 원본 `looks_like` / `copy-from` 체인이 타일에 닿으면 매핑. Bake: `python Tools/bn_converter/export_tileset_icons.py --bn-path <Cataclysm-BN> --items Assets/StreamingAssets/BNData/items.json --output Assets/StreamingAssets/BNData/tileset --tileset MSX++UnDeadPeopleEdition`
3. 기본 empty 아이콘 (`ui_icon_empty.png`)

`looks_like`는 Dist `ItemData`에 넣지 않는다. bake가 체인만 따라가 `item_sprites.json`에 펼친다.

프리팹 갱신: full bake 메뉴는 두지 않음 (`.cursor/rules/ui-prefab-bake.mdc`). 기존 창에 컬럼 헤더만: `Dist/MCP/Inventory/Patch Window Column Header`. 열 폭·행간격·패딩 SSOT 에셋 반영: `Dist/MCP/Inventory/Sync List Column Layout` (`Resources/Inventory/InventoryListColumnLayoutSettings`). 구 리사이즈 핸들 제거+`UIWindowResizeHandles` 부착: `Dist/MCP/Inventory/Patch Window Resize Handlers`. 헤더 접기/끄기: `Dist/MCP/WindowChrome/Patch Fold Close Buttons`. `Dist/MCP/*` 는 Unity MCP·에이전트용 Setup/Patch 메뉴다 (`Create → Dist/...` 와 별개).

캔버스 오버레이 배선: `Dist/MCP/Inventory/Setup Canvas Overlays In Open Scene` (IsoLand 등 씬 1회 실행).

**유의사항 (이주/하이브리드):** uGUI와 UI Toolkit 등 **다른 UI 경로를 한 화면에 섞거나** 리스트만 갈아끼울 때, 변경 전 동작 패리티 없이 기본 경로로 켜지 말 것. 반쯤 켠 채 증상별 재현으로 메우지 말 것 — `.cursor/rules/migration-parity.mdc`, `.claude/checklists/migration-parity.md`.

### 월드 컨테이너 프리팹 (인벤 관련)



| 프리팹 | 필수 컴포넌트(예) |

|--------|------------------|

| `Map/Furniture/Create.prefab` 등 | `ContainerInteractable`, `ContainerTileViewRegistrar`, (선택) `SpriteOutlineFocusVisual` |



- `_containerId`는 인스턴스마다 고유해야 한다. 동일 ID 복제 시 런타임 suffix가 붙지만, 프리팹 단계에서부터 중복을 피하는 것이 기준이다.



---



## DnD + 박스 선택 구조



- 다중 선택 SSOT: `InventoryListSelection`

- 드래그 상태 SSOT: `InventoryDragState` / `InventoryDragPayload` (DistScript UI; `ClearSelection`·`MarkConsumed`)

- 드래그 시작: `UIItemListRow` (`IBeginDragHandler`)

- 드롭 처리: 행 `UIItemListRow` (`IDropHandler`)가 먼저 — ammo/mag/gun이면 `WeaponAmmoDrop` → `WeaponAmmoService` (삽탄·장착/교체). 아니면 같은 컨테이너로 `InventoryDragDrop.TryApplyTo` (`MoveStacks`). 빈 리스트 영역은 `UIInventoryListDropZone` → `MoveStacks`. Unity drop은 bubble하지 않음.
- 리스트 행 더블클릭: 플레이어·루트 창이 **둘 다 열려 있을 때** 반대편 창 활성 탭으로 `InventoryDragDrop.TryQuickTransferBetweenWindows` → `MoveStacks` (드래그 후 반대 리스트 드롭과 동일). 선택에 클릭 스택이 없으면 단일 스택, 있으면 선택 전체.
- 리스트 행 호버 보조창: `UIItemListRow.Hovered` → `UIInventoryItemDetailPanel` (`UIHoverCanvasLayer` / `TopMost`) + `UIHoverPanelShell` / `UIPopupPositioner` keep-in-bounds ([`docs/ui/HOVER.md`](../ui/HOVER.md)). **VerticalLayoutGroup** 행별 표시 — description·분류·유형·수납·재질 등 아이템에 따라 `SetActive`로 행 추가/제거. 내구도는 `ItemData.has_durability` + `ItemDurabilityRules` + `ItemStack.DamageLevel`. 재질명은 `GameplayData.GetMaterial`. 숨김: exit·드래그 시작·창 닫힘·우클릭 메뉴. `raycastTarget=false`. 프리팹 description 행 추가 후 `Setup Canvas Overlays In Open Scene` 재실행.

- 게임 데이터: `BNData/`(참조) + `GameData/`(커스텀) 듀얼. `GameDataLoader` + `GameDataJson`(Newtonsoft). `ItemData`에 BN 게임 디테일 통합(description·armor·gun·tool·comestible 등). `GameplayData.GetItem` / `GetMaterial` — 커스텀 우선 → 참조 fallback. **아이템 표시명·설명·레시피 카테고리·도구 품질명**은 `item_names.json` (`names` / `descriptions` / `recipe_categories` / `qualities`) + `LocalizationBundle` — [`ITEM_NAMES.md`](ITEM_NAMES.md). BN 재생성: `python Tools/bn_converter/convert.py --bn-path <Cataclysm-BN> --output Assets/StreamingAssets/BNData` (산출에 네 섹션 포함; `--locale-only`는 로케일만). 필드 화이트리스트·미도입 목록: [`docs/equipment/BN_BAKE.md`](../equipment/BN_BAKE.md).

- 데이터 갱신: `InventorySession`이 `InventoryStacksChangeSet`(변경 컨테이너 + `SidebarAffected`)을 발행. 무 payload `StacksChanged` 금지.
  - `InventoryContainer.ContentVersion` — `NotifyContentsChanged`마다 증가.
  - `StacksChanged(changeSet)` → `UIInventoryController.OnInventoryDataChanged` → `UIInventoryListWindow.SyncFromChangeSet` — 창 관심 컨테이너만 List/Sidebar/Capacity 갱신.
  - `SidebarChanged` → `OnSessionChanged` → `SyncFromChangeSet(Full)` + 사이드바 Sync.
  - **드래그 중**: 사이드바 `Sync` / `ApplyModeLayout` 보류. `OnDrop`→`MoveStacks`는 드래그 중이라 리스트 `Bind`도 생략. 종료 후 `RefreshVisibleWindowsAfterDrag` → `SyncDeferredAfterDrag`(사이드바 + 선택 컨테이너 리스트 Bind).
  - 탭 클릭 / 컨테이너 선택 → `SetActiveContainer` / 리스트 `Bind`(version skip + 증분 Sync).
- `SetActiveContainer`: 드래그 중 리스트 Bind 생략.
- `InventoryDragDrop`: `ContainerTab`은 Source(부모)==리스트 타깃(body)이어도 early-out하지 않음(`MoveStacks` from==to가 no-op). Item/ContainerContents만 Source==target early-out.
- 사이드 탭 이동: 간이(중첩) = 컨테이너째 `MoveStacks`; 고정 탭 = 내용물 순차(용량 초과 시 중단). 소요 시간=`InventoryTransferDuration` — 스택당 순차 (`InventoryTimedMoveHost`).
- `ConfigureDragAndDrop`: 뷰포트 DnD 배선만. 리스트/사이드바 Bind는 `Initialize`·`SyncFromChangeSet` SSOT.
- `UIItemListView`: **고정 높이 가상화** — `_orderedStacks`(데이터)와 가시 행 LeanPool을 분리. Content 높이는 `N * stride`(+ sticky top/bottom pad, `InventoryListColumnLayout` SSOT). 스크롤·리사이즈 시 viewport+`RowOverscan` 윈도우만 Bind·배치. 프리팹 VLG/CSF는 런타임 비활성. 마퀴는 가시 GO가 아니라 인덱스 기하(`SelectRowsInRect`). 선택 SSOT는 비가시 스택도 유지. `ActiveRowCount`=가시 풀, `BoundStackCount`=데이터 수. 컨트롤러 Awake에서 `PrewarmRowPool`(`RowPoolPrewarmCount`≈viewport 상한). 패리티 계약·검증 게이트: 작업 플랜 `inventory_list_virtualization` + `.claude/checklists/migration-parity.md` §C.
- `UIInventoryController.LateUpdate`: `UIOverlayWindowHitTest`로 창 위 포인터 캐시 후 변경 시에만 `SuppressPlayerAction` 호출



### 배선 규칙



- `UIInventoryListDropZone`, `InventoryListMarqueeSelector`는 `Content`가 아니라 `ScrollRect.viewport`에 부착한다.

- `Viewport`는 투명 `Image(raycastTarget=true)`로 포인터/드롭 이벤트를 받는다.

- 베이크 시 `InventoryUIHierarchyBuilder`가 viewport 기준 배선을 생성해야 한다.



### 용량 검증

- 다중 이동은 개별 `CanAccept`만으로 판단하지 않는다.
- `InventorySession.MoveStacks(...)`에서 합산 무게/부피를 누적 계산해 검증한다.
- **하드 차원은 policy 플래그로 결정**한다 (`IContainerCapacityPolicy.EnforcesHardWeightLimit` / `EnforcesHardVolumeLimit`).
  - 상자·가방 등 `FixedContainerCapacityPolicy`: 무게·부피 모두 hard.
  - 플레이어 몸통 `PlayerCarryCapacityPolicy`: **무게 soft**(초과 적재 허용), **부피만 hard**.
- `GetMaxWeight`는 거절 한도가 아니라 **감당 가능 한도**(UI·과적 비율·추후 스킬 가산) SSOT다.

### 과적(Encumbrance)

- `PlayerEncumbranceHost`가 몸통 `used/max` 비율로 단계(None/Light/Medium/Heavy/Extreme)를 평가한다.
- Light~Heavy: 이동 감속·스탯 delta·상태 HUD 디버프 아이콘. **메시지 로그 없음**.
- Extreme: 이동 불가 + 디버프 아이콘 + `GameplayMessageLog` (`msg.status.encumbrance_immobile`, 이동 시도 시 Extreme 구간 1회).
- 부피 soft / 정리정돈 스킬 한도 상승은 **미구현**. provider 훅(`PlayerInventoryHost` Func)만 유지.
- BN `armor.encumbrance`와는 별개(착용 레이어 부담 데이터).

### 용량 UI

- `Area_InvInfo` `Txt_Weight`: `used/max`. `used > max`이면 `InventoryCapacityVisuals.OverweightColor` (정상 색은 프리팹 기본값 유지).


### 런타임 조회/시딩



- 각 Provider(`PlayerInventoryHost`, `ContainerInteractable`)는 `ContainerId`를 가진다.

- `InventoryContainerRegistry`가 `ContainerId -> InventoryContainer`를 관리한다.

- 캐릭터 몸통·Wear/Wield 시드는 `CharacterDefinition` (`CharacterSpawnGearApplier`, `SetActive` 직후). IsoLand 테스터는 바닥 소형 아이템만 (`InventoryRuntimeTestSetup._seedContainerOnStart` off).
- 시작 시 바닥 소형 아이템 스폰은 `InventoryRuntimeTestSetup`(런타임 테스트 전용)으로 수행한다 (`PlayerInventoryRuntime._seedDemoItemsOnStart` 기본값 `false` — 중복 시딩 방지).
- 월드 소형 아이템 부모 SSOT는 `Map/Items` (`SmallItemSpawner.ResolveWorldRoot`). 테스트 스폰·바닥 투하(`FloorLootHost`) 모두 여기. `InventoryRuntimeTestSetup._smallItemSpawnRoot`는 오버라이드(비우면 SSOT, 자기 자신은 무시).



---



## 런처 아이콘



- `InventoryWindowLauncher`를 `UICanvas` 하위 버튼에 부착한다. 컨트롤러가 open 상태를 `SetOpen`으로 push → 아이콘 색(활성/비활성).

- `LauncherTarget.Primary` → 플레이어창 토글

- `LauncherTarget.Loot` → 루팅창 토글

- `I` 키는 `InputManager.PlayerInventoryTogglePerformed` → 플레이어창 토글만 수행한다.



---



## 확인 체크리스트 (IsoLand)



1. `I`로 플레이어창 열기/닫기, 루팅 아이콘으로 루팅창 독립 토글

2. `E`로 루팅창 열기 + 해당 컨테이너 탭 점프 (탭 목록은 주변 전체 유지)

3. 사이드탭 클릭 시 해당 컨테이너 아이템 리스트 즉시 표시

4. 상단 드래그로 창 이동, 8방향 핸들로 리사이즈 후 DnD/스크롤 정상

5. 박스 드래그로 다중 선택, 플레이어창 ↔ 루팅창 간 드롭

6. 용량 초과 시 이동 실패, 실패 드롭 시 드래그 상태/고스트 정상 정리

---

## 아이템 우클릭 컨텍스트 메뉴

- 코드: `Assets/Dist/Scripts/UI/Inventory/ItemContextMenu/`
- **사람용 사용법:** [`ItemContextMenu-usage.md`](ItemContextMenu-usage.md)
- 기술 경계·항목 추가: [`ItemContextMenu.md`](ItemContextMenu.md)
- 프리팹: `ItemContextMenu.prefab` (HierarchyBuilder bake). 메뉴 항목 추가는 Contributor/Action만 — UI 프리팹 변경 불필요.
- 섭취: `ConsumeContextContributor` — Eat / Drink / Use. heal은 Use Group → 부위 Leaf. 라벨 `ItemContextMenu.Eat` · `.Drink` · `.Use` · `.Unwrap`. 실행은 `ConsumeService.TryBegin(..., partId?)` → `CharacterHandWork` ([`needs/NEEDS.md`](../needs/NEEDS.md)). HUD Status 실루엣 RMB: `BodyPartHealContextMenuBuilder`.
- 부패 병합: `ItemMergeKey.IsRotten`이 다르면 합치지 않는다 (`ItemMergePolicy`).



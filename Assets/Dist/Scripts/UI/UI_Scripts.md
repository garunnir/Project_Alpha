# UI 스크립트 문서

경로: `Assets/Dist/Scripts/UI/`

---

## 구조 개요

```
UI/
├── UI.cs                          # 추상 기반 클래스
├── CanvasGroupController.cs       # CanvasGroup 표시/숨김 컴포넌트
├── Controller/
│   ├── UIController.cs            # 추상 컨트롤러 기반
│   ├── UIStatusController.cs      # 캐릭터 스테이터스 컨트롤러
│   └── UIMapController.cs         # 맵 UI 컨트롤러
├── Model/
│   ├── UIModel.cs                 # 추상 데이터 모델
│   └── UIStatusPageHandler.cs     # 스테이터스 페이지 핸들러 + UICharModel
├── View/
│   ├── UIVerticalView.cs          # 세로 레이아웃 동적 뷰
│   ├── UIMapViewer.cs             # 맵 시각화 뷰
│   ├── UIActorView.cs             # 액터 정보 뷰 (미완성)
│   ├── UISelectBtnsPopup.cs       # 선택지 팝업
│   └── UISender.cs                # 사용자 입력 송신 컴포넌트
├── Container/
│   └── UIButtonContainer.cs      # 빈 껍데기 (미구현)
└── Font/
    └── Katuri (.otf/.ttf/SDF)    # 한글 폰트 리소스
```

---

## 파일별 상세

### UI.cs
**역할:** 모든 UI 요소의 추상 기반 클래스 (MonoBehaviour 상속)

**핵심 기능:**
- `Show()` / `Hide()` — 코루틴 기반 표시/숨김 (중복 실행 방지)
- `ShowForce()` / `HideForce()` — 즉각 실행 (코루틴 없이)
- `FadeIn()` / `FadeOut()` — DOTween 기반 알파 페이드
- 이벤트 훅: `Start`, `End`, `ShowStart`, `ShowEnd`, `HideStart`, `HideEnd`
- 추상 메서드: `AddShowAct()`, `AddHideAct()` — 하위 클래스에서 커스텀 애니메이션 정의

**효용:** UI 애니메이션 흐름을 표준화. 모든 UI가 동일한 Show/Hide 인터페이스를 갖도록 강제.

---

### CanvasGroupController.cs
**역할:** CanvasGroup을 통한 UI 패널 표시/숨김 관리 컴포넌트

**핵심 기능:**
- `Show()` / `Hide()` — 코루틴으로 AddShowAct/AddHideAct 실행 후 alpha 전환
- `ShowForce()` / `HideForce()` — 즉각 alpha 0/1 전환
- `AddShowAct` / `AddHideAct` — `Func<IEnumerator>` 방식으로 외부에서 애니메이션 주입 가능
- Canvas enabled 토글(선택적)로 Draw Call 최적화
- `isVisible` 프로퍼티로 현재 상태 노출
- 에디터 커스텀 인스펙터: Show/Hide 버튼 제공

**UI.cs와의 차이:** UI.cs는 상속 기반이고 이쪽은 컴포넌트 부착 방식. 애니메이션을 외부 주입(`Func<IEnumerator>`)으로 받아 더 유연함.

**효용:** 어떤 GameObject에도 부착 가능한 범용 Show/Hide 컴포넌트. 에디터에서 바로 테스트 가능.

---

## Controller/

### UIController.cs
**역할:** MVC 패턴에서 Controller의 추상 기반

**핵심 기능:**
- `UIModel _model` 참조 보유
- `Init()` 추상 메서드

**효용:** 모든 UI 컨트롤러가 UIModel을 갖도록 강제하는 최소한의 구조 정의.

---

### UIStatusController.cs
**역할:** 캐릭터 스테이터스 창 컨트롤러

**핵심 기능:**
- `UIStatusPageHandler`와 연동하여 페이지 네비게이션(다음/이전) 관리
- 여러 `IPage` 구현체를 리스트로 관리 — 페이지 경계에서 다음 IPage로 넘어가는 중첩 페이지 지원
- `GameManager.ResourceLoadDoneEvent` 구독 → 리소스 로드 완료 시 특정 캐릭터(id=5) 스테이터스 표시
- `ShowFieldData()` — 다중 Field 리스트를 페이지별로 표시

**현황:** 구버전 Callstatus 코드가 주석으로 남아있음 (UICharModel 리팩토링 전).

**효용:** 스테이터스 UI의 입력 처리와 페이지 전환 로직 담당.

---

### UIMapController.cs
**역할:** 맵 이동 및 맵 UI 전체 조율 컨트롤러

**핵심 기능:**
- `UIMapViewer`(View)의 MoveEvent/CreatedEvent 구독 → 맵 노드 생성 및 이동 처리
- `MoveTo(entryid)` — 연결 여부 확인 후 선택 위치 이동, 배경 이미지 교체
- `CreateOther(entryid)` — 이동한 노드의 인접 노드(상하좌우) 동적 생성
- `ShowBG(entry)` — 맵 엔트리의 배경 텍스처를 UIManager 배경에 적용 + 비율 조정
- **DialogueSystem 연동:** 현재 맵의 Actor 탐색 → 대화 버튼 활성화 → 선택지 자동 연결
- `Update()` — 마우스 클릭 시 UIMapViewer.CheckMoveable() 호출

**효용:** 맵 탐색 게임플레이의 핵심. 맵 이동, 시각화, 배경, NPC 대화를 하나로 연결.

---

## Model/

### UIModel.cs
**역할:** UI 데이터 모델의 추상 기반

**핵심 기능:**
- `Update(object args)` / `Init(object args)` 추상 메서드

**효용:** MVC에서 Model 레이어 정의. 현재 UICharModel이 유일한 구현체.

---

### UIStatusPageHandler.cs
**역할:** 캐릭터 스테이터스 페이지 핸들러 + 실제 렌더링 담당

UI.cs 상속 + IPage 인터페이스 구현

**핵심 기능:**
- UICharModel 목록과 (좌UIVerticalView, 우UIVerticalView) 쌍을 1:1로 딕셔너리 관리
- `CallStatus(int id)` / `Callstatus(string name)` / `Callstatus(Actor)` — 캐릭터를 찾아 렌더링 요청
- `Callstatus(List<List<Field>>)` — 다중 캐릭터 페이지 일괄 생성
- 페이지 순환: GetNextPage/GetPrevPage (경계에서 wrap-around)

**내부 클래스 UICharModel (UIModel 상속):**
- 표시할 필드 허용목록: `IsOpenedList` = `{Status.Exp, Pictures, Character.OuterAge, Character.FirstName}`
- 바 표현식: `BarExpression` = `{Status.Exp}`
- 이미지 표현식: `ImgExpression` = `{Pictures}`
- `Update(List<Field>, (UIVerticalView, UIVerticalView))` — 필드 타입에 따라 ShowBar/ShowImg/ShowText 분기, 좌우 높이 균형 배치
- `GetPicName()` — Pictures 필드 값에서 `[이름;]` 포맷 파싱

**IPage 인터페이스 (동 파일 정의):**
```csharp
int GetCurrentPage(); int GetMaxPage();
void GetNextPage(); void GetPrevPage();
void SetPage(int); void SetActive(bool);
```

**효용:** 캐릭터 데이터(Dialogue System Field)를 UI 컴포넌트로 실제 변환하는 핵심 렌더링 레이어.

---

## View/

### UIVerticalView.cs
**역할:** VerticalLayoutGroup 기반 동적 컴포넌트 생성 뷰

UI.cs 상속

**핵심 기능:**
- `ShowBar(title, fill, out ratio)` — 프리팹 인스턴스화, fillAmount 설정, 로컬라이즈된 텍스트 적용
- `ShowImg(tex, out ratio)` — 이미지 프리팹 생성, 텍스처 비율에 맞게 RectTransform 조정
- `ShowText(title, value, out ratio)` — 텍스트 프리팹 생성, 로컬라이즈된 키 + 값 표시
- `ClearComponents()` — 자식 오브젝트 전부 Destroy
- `SetOBJ()` — 생성된 오브젝트를 target(VerticalLayoutGroup)에 부착하고 너비 기반 비율 크기 설정

**프리팹 참조:** `prf_component`, `prf_text`, `prf_img`, `prf_bar`

**주의:** `AddHideAct` / `AddShowAct`는 `NotImplementedException` — Show/Hide 기능 미완성.

**효용:** 스테이터스 창의 실제 UI 요소 배치를 담당. UICharModel이 이 클래스를 통해 데이터를 화면에 그림.

---

### UIMapViewer.cs
**역할:** ScrollRect 기반 맵 노드 시각화 뷰

**핵심 기능:**
- `ShowMap(entryTitle, entryid)` — 시작 노드 기점 맵 Content 초기화
- `CreateSprite(title, id, pos)` — 맵 노드(RawImage) 동적 생성, 중복/한도 체크
- `GenerateBridge(point, CP)` — 노드 사이 연결선(RawImage) 생성 (상하/좌우 방향)
- `SelectPositionMoveTo(idx)` — 선택 표시 스프라이트를 대상 노드 위치로 이동
- `AlignCenter()` — 선택된 노드가 화면 중심에 오도록 Content 오프셋 조정
- `CheckMoveable()` — 마우스 위치와 모든 노드 RectTransform 겹침 검사 → MoveEvent 발생
- `MapToggle()` — 버튼으로 맵 창 3단계 크기 전환 (숨김 → 최소 → 최대)

**이벤트:**
- `MoveEvent(int entryid, bool ignoreBridge)` → UIMapController.MoveTo 연결
- `CreatedEvent(int entryid)` → UIMapController.CreateOther 연결

**효용:** 맵 데이터를 화면에 격자 형태로 렌더링. Controller의 지시에 따라 노드를 생성하고 이동 처리를 Controller에 위임.

---

### UIActorView.cs
**역할:** 액터(캐릭터) 정보 표시 뷰

**현황:** Start/Update만 있고 실제 로직 없음. 프로필 이미지, TMP 텍스트, 스탯 바 배열, 버프 레이아웃 그룹 레퍼런스만 선언됨.

**효용:** 미구현 상태. 향후 전투나 캐릭터 상세 화면에서 사용할 가능성이 있는 뷰 틀.

---

### UISelectBtnsPopup.cs
**역할:** 동적 선택지 버튼 팝업 UI

UI.cs 상속, ContentSizeFitter 필수

**핵심 기능:**
- `CreateBtns(params string[])` — LeanPool로 버튼 풀링 생성 (복수)
- `CreateBtns(string)` — 단일 버튼 생성 + TMP 텍스트 설정
- `ClearAll()` — 활성 버튼 전부 LeanPool.Despawn

**효용:** 대화 선택지나 인게임 팝업 메뉴를 풀링 방식으로 효율적으로 생성/해제. Dialogue System 응답 버튼 용도로 추정.

---

### UISender.cs
**역할:** 사용자 정보 입력 UI — 값을 수집해서 직렬화 문자열로 반환

**핵심 기능:**
- `Mode.inputfield` / `Mode.dropdown` 두 가지 모드
- `OnEnable()` — UILocalizationManager로 타이틀 텍스트 로컬라이징
- Gender 드롭다운 모드 시 Enum 목록 자동 생성 + 로컬라이즈
- `GetValue()` — 현재 입력값을 `Garunnir.Utillity.TextSerializeBuffer.TupleSingle()` 포맷으로 직렬화

**효용:** 캐릭터 생성(이름, 성별 등) 또는 설정 입력 화면에서 각 입력 필드가 자신의 값을 Controller에 전달하는 역할.

---

## Container/

### UIButtonContainer.cs
**역할:** 미구현 빈 클래스

**현황:** MonoBehaviour만 상속, 내용 없음.

**효용:** 현재 없음. 향후 버튼 그룹 컨테이너 용도로 예약된 것으로 보임.

---

## Font/

### Katuri (.otf / .ttf / SDF)
**역할:** 프로젝트 전용 한글 폰트

- `Katuri.otf` / `Katuri.ttf` — 원본 폰트 파일
- `Katuri SDF.asset` — TextMeshPro용 SDF 렌더링 에셋

**효용:** UIVerticalView, UISender 등에서 `UILocalizationManager.localizedFonts`를 통해 언어별 TMP 폰트로 사용됨.

---

## 정리: 효용가치 평가

| 파일 | 완성도 | 효용 |
|------|--------|------|
| UI.cs | 중 | 기반 인터페이스 역할은 하나 FadeIn/Out 중복 구현 존재 |
| CanvasGroupController.cs | 높음 | 실용적이고 에디터 지원도 있어 재사용성 높음 |
| UIController.cs | 낮음 | 너무 얇아 현재 실질적 역할 없음 |
| UIStatusController.cs | 중 | 페이지 네비게이션은 작동, 구버전 주석 정리 필요 |
| UIMapController.cs | 높음 | 맵 게임플레이의 핵심, 책임이 다소 과중 |
| UIModel.cs | 낮음 | 추상 기반이나 실제 구현체가 UICharModel 하나뿐 |
| UIStatusPageHandler.cs | 높음 | 스테이터스 렌더링의 핵심 로직 |
| UIVerticalView.cs | 중 | Show/Hide 미구현, 렌더링 자체는 작동 |
| UIMapViewer.cs | 높음 | 맵 시각화 잘 구현됨 |
| UIActorView.cs | 매우 낮음 | 껍데기만 존재, 미구현 |
| UISelectBtnsPopup.cs | 중 | 풀링 잘 활용, Show/Hide 미구현 |
| UISender.cs | 중 | 입력 수집 기능은 작동, 연결 대상(Controller) 주석 처리됨 |
| UIButtonContainer.cs | 매우 낮음 | 완전 미구현 |

---

## 종합 분석

### 잘 된 부분

- **`CanvasGroupController`** — 범용 Show/Hide 컴포넌트. 에디터 버튼 지원, 외부 애니메이션 주입(`Func<IEnumerator>`) 구조가 유연하고 재사용성 높음.
- **`UIMapController` + `UIMapViewer`** — View/Controller 분리가 이벤트(`MoveEvent`/`CreatedEvent`)로 명확하게 연결됨. 맵 이동, 시각화, 배경, NPC 대화를 하나의 흐름으로 잘 조율.
- **`UIStatusPageHandler` + `UICharModel`** — 필드 데이터(Dialogue System Field) → UI 컴포넌트(바/이미지/텍스트) 렌더링 파이프라인이 실질적으로 작동. 좌우 균형 배치 로직도 구현됨.

### 문제점

- **미구현 파일 다수** — `UIActorView`, `UIButtonContainer`는 껍데기만 존재. `UIVerticalView`와 `UISelectBtnsPopup`은 UI 기반 클래스 상속 후 `AddHideAct`/`AddShowAct`를 `NotImplementedException`으로 방치해 Show/Hide 기능이 런타임 오류 위험 있음.
- **Show/Hide 이중 구조** — `UI.cs`(상속 방식)와 `CanvasGroupController.cs`(컴포넌트 부착 방식)가 유사한 역할을 중복 구현. 프로젝트 내 혼용으로 일관성 부족.
- **Controller 연결 단절** — `UISender`의 Controller 이벤트 구독 코드가 주석 처리되어 현재 값을 능동적으로 전달하지 못하고, 외부에서 `GetValue()`를 직접 호출해야 함.
- **UIMapController 책임 과중** — 맵 이동, 배경 전환, NPC 탐색, 대화 연결, 노드 생성까지 한 클래스에 집중. 추후 유지보수 시 분리 고려 필요.
- **UIStatusController 미정리** — 구버전 `Callstatus` 로직이 100줄 가까이 주석으로 남아 있음. 삭제하거나 별도 브랜치로 이관 필요.

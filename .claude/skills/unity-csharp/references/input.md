# Input System Reference

이 프로젝트는 **Unity New Input System (이벤트 기반)** 만 사용한다.
`Input.GetKey`, `Input.GetMouseButton`, `Input.mousePosition` 등 레거시 API는 절대 사용하지 않는다.

---

## Action Map 구성

| Map | 활성 시점 | 전환 방법 |
|-----|---------|---------|
| `Player` | 게임플레이 중 | `InputManager.Instance.SwitchToPlayer()` |
| `UI` | UI 화면 | `InputManager.Instance.SwitchToUI()` |

**Player 액션:**
- `Move` (Vector2) — WASD
- `Interaction` (Button) — E
- `Run` (Button, Hold) — Left Shift

**UI 액션:**
- `Click`, `Point`, `Navigate`, `Submit`, `Cancel`, `ScrollWheel`, `Pagination`

---

## 표준 패턴: Callback (이벤트)

```csharp
// MonoBehaviour에서 — PlayerInput 컴포넌트가 자동 호출
public void OnInteract(InputAction.CallbackContext context)
{
    if (!context.performed) return;
    // 처리
}
```

메서드 이름은 `On{ActionName}` 형식, Inspector의 PlayerInput 컴포넌트에서 연결.

---

## 폴링 (매 프레임 값이 필요할 때만)

```csharp
// 이동처럼 지속적인 값이 필요한 경우에만 허용
Vector2 move = InputManager.Instance.Actions.Player.Move.ReadValue<Vector2>();
```

---

## 포인터 (마우스/터치 공용)

`Pointer`는 `Mouse`, `Pen`, `Touchscreen`의 공통 베이스. **`Mouse.current` 직접 참조 금지** — 모바일에서 null.

```csharp
using UnityEngine.InputSystem;

// 위치 (마우스/터치 공용)
Vector2 screenPos = Pointer.current?.position.ReadValue() ?? Vector2.zero;

// 클릭/탭 (이 프레임에 눌렸는지)
bool pressed = Pointer.current?.press.wasPressedThisFrame ?? false;

// 스크린 → 월드 레이
var ray = Camera.main.ScreenPointToRay(Pointer.current?.position.ReadValue() ?? Vector2.zero);
```

---

## 금지 API

```csharp
// ❌ 절대 사용 금지
Input.GetKey(KeyCode.E)
Input.GetKeyDown(KeyCode.Space)
Input.GetMouseButtonDown(0)
Input.mousePosition
Input.GetAxis("Horizontal")
```

---

## InputActions.cs 는 자동생성 파일

**직접 편집 금지.** 액션/바인딩 추가·변경은 반드시:
1. `Assets/Dist/Scripts/SerializedObject/ScriptableObject/InputActions.inputactions` 에셋을 Inspector에서 편집
2. Unity가 `InputActions.cs` 자동 재생성

---

## 새 액션 추가 절차

1. `.inputactions` 에셋 열기
2. 해당 Map에 액션 추가 + 바인딩 지정
3. `InputActions.cs` 재생성 확인
4. `On{ActionName}(InputAction.CallbackContext context)` 메서드로 처리

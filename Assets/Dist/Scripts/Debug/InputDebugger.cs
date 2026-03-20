using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

public class InputDebugger : MonoBehaviour, IBeginDragHandler, IDragHandler 
{
    [SerializeField] private float _dragThreshold = 5f;

    private InputSystemUIInputModule _inputModule;
    private Vector2 _pointerDownPos;
    private Vector2 _currentPos;
    private bool _isDragging;
    private bool _isPressed;

    private void Awake()
    {
        _inputModule = FindObjectOfType<InputSystemUIInputModule>();
        if (_inputModule == null)
        {
            Debug.LogError("[InputDebugger] InputSystemUIInputModule not found in the scene.");
            return;
        }

        _inputModule.leftClick.action.started   += OnLeftClickStarted;
        _inputModule.leftClick.action.canceled  += OnLeftClickCanceled;
        _inputModule.rightClick.action.started  += OnRightClickStarted;
        _inputModule.point.action.performed     += OnPoint;
        _inputModule.scrollWheel.action.performed += OnScroll;
    }

    private void OnDestroy()
    {
        if (_inputModule == null) return;

        _inputModule.leftClick.action.started   -= OnLeftClickStarted;
        _inputModule.leftClick.action.canceled  -= OnLeftClickCanceled;
        _inputModule.rightClick.action.started  -= OnRightClickStarted;
        _inputModule.point.action.performed     -= OnPoint;
        _inputModule.scrollWheel.action.performed -= OnScroll;
    }

    private void OnLeftClickStarted(InputAction.CallbackContext ctx)
    {
        _pointerDownPos = _currentPos;
        _isDragging = false;
        _isPressed = true;
        Debug.Log($"[Input] PointerDown  pos={_currentPos}");
    }

    private void OnLeftClickCanceled(InputAction.CallbackContext ctx)
    {
        if (_isDragging)
            Debug.Log($"[Input] DragEnd     pos={_currentPos}");
        else
            Debug.Log($"[Input] Click       pos={_currentPos}");

        _isDragging = false;
        _isPressed = false;
    }

    private void OnRightClickStarted(InputAction.CallbackContext ctx)
    {
        Debug.Log($"[Input] RightClick  pos={_currentPos}");
    }

    private void OnPoint(InputAction.CallbackContext ctx)
    {
        _currentPos = ctx.ReadValue<Vector2>();

        if (!_isPressed) return;

        Vector2 delta = _currentPos - _pointerDownPos;

        if (!_isDragging && delta.magnitude >= _dragThreshold)
        {
            _isDragging = true;
            Debug.Log($"[Input] DragStart   from={_pointerDownPos}");
        }

        if (_isDragging)
            Debug.Log($"[Input] Drag        pos={_currentPos}  delta={delta}");
    }

    private void OnScroll(InputAction.CallbackContext ctx)
    {
        Vector2 scroll = ctx.ReadValue<Vector2>();
        Debug.Log($"[Input] Scroll      delta={scroll}");
    }
        public void OnBeginDrag(PointerEventData eventData) => Debug.Log("드래그 시작됨!");
    public void OnDrag(PointerEventData eventData) => Debug.Log("드래그 중: " + eventData.delta);
}

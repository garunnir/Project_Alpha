using System;
using IsoTilemap;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridCursor : MonoBehaviour
{
    [SerializeField] TileMapController _controller;
    [SerializeField] TilePlacementState _placementState;
    [SerializeField] GameObject _cursorVisual;

    private Vector3Int _cursorPos;

    private Vector2 _heldDir;
    private float _holdTimer;
    private float _repeatTimer;

    const float HOLD_THRESHOLD = 1f;
    const float REPEAT_INTERVAL = 0.15f;

    void Start()
    {
        var actions = InputManager.Instance.Actions;
        actions.UI.Navigate.started  += OnNavigateStarted;
        actions.UI.Navigate.canceled += OnNavigateCanceled;
        actions.UI.Submit.performed  += OnSubmit;
    }

    void Update()
    {
        if (_heldDir == Vector2.zero) return;

        _holdTimer += Time.deltaTime;
        if (_holdTimer < HOLD_THRESHOLD) return;

        _repeatTimer += Time.deltaTime;
        if (_repeatTimer >= REPEAT_INTERVAL)
        {
            MoveCursor(_heldDir);
            _repeatTimer = 0f;
        }
    }

    void OnNavigateStarted(InputAction.CallbackContext ctx)
    {
        Vector2 dir = ctx.ReadValue<Vector2>();
        _heldDir = dir;
        _holdTimer = 0f;
        _repeatTimer = 0f;
        MoveCursor(dir);
    }

    void OnNavigateCanceled(InputAction.CallbackContext ctx)
    {
        _heldDir = Vector2.zero;
        _holdTimer = 0f;
        _repeatTimer = 0f;
    }

    void MoveCursor(Vector2 dir)
    {
        // 아이소메트릭 기준: 입력 x → grid x, 입력 y → grid z
        _cursorPos += new Vector3Int(
            Mathf.RoundToInt(dir.x),
            0,
            Mathf.RoundToInt(dir.y)
        );
        UpdateVisual();
    }

    void OnSubmit(InputAction.CallbackContext ctx)
    {
        if (_placementState.Selected == null) return;

        var def = _placementState.Selected;
        var tileData = new TileData
        {
            tileDefId = Guid.NewGuid(),
            state     = new TileState(),
            identity  = new TileIdentity
            {
                PrefabId  = def.prefabId,
                GridPos   = _cursorPos,
                sizeUnit  = Vector3Int.one,
                tileType  = 0,
            }
        };
        _controller.AddAndFlush(tileData);
    }

    void UpdateVisual()
    {
        if (_cursorVisual == null) return;
        _cursorVisual.transform.position = TileHelper.ConvertGridToWorldPos(_cursorPos);
    }

    public void SetActive(bool active)
    {
        enabled = active;
        if (_cursorVisual != null)
            _cursorVisual.SetActive(active);
    }

    void OnDestroy()
    {
        if (InputManager.Instance == null) return;
        var actions = InputManager.Instance.Actions;
        actions.UI.Navigate.started  -= OnNavigateStarted;
        actions.UI.Navigate.canceled -= OnNavigateCanceled;
        actions.UI.Submit.performed  -= OnSubmit;
    }
}

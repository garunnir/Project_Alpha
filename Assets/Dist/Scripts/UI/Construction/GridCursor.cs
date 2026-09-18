using System;
using IsoTilemap;
using UnityEngine;
using UnityEngine.InputSystem;

public class GridCursor : MonoBehaviour
{
    [SerializeField] TileMapController _controller;
    [SerializeField] TileMapManager _tileMapManager;
    [SerializeField] TilePlacementState _placementState;
    [SerializeField] GameObject _cursorVisual;
    [SerializeField] Camera _camera;

    private Vector3Int _cursorGridPos;
    IFarmCellTargetSession _targetSession;
    Renderer _cursorRenderer;
    Material _cursorMaterial;
    Color _targetTint = Color.white;
    bool _targetingActive;

    private Vector2 _heldDir;
    private float _holdTimer;
    private float _repeatTimer;

    const float HOLD_THRESHOLD = 1f;
    const float REPEAT_INTERVAL = 0.15f;

    Transform _visualOriginalParent;
    bool _visualReparented;

    public bool IsTargeting => _targetingActive;

    bool UsesWorldPointerSync =>
        _targetingActive || (_placementState != null && _placementState.Selected != null);

    void Start()
    {
        if (_camera == null) _camera = Camera.main;
        ResolveTileMapRefs();
        CacheCursorRenderer();

        InputManager input = InputManager.Instance;
        input.UiNavigateStarted += OnNavigateStarted;
        input.UiNavigateCanceled += OnNavigateCanceled;
        input.UiSubmitPerformed += OnSubmit;
    }

    void ResolveTileMapRefs()
    {
        if (_controller == null)
            _controller = FindFirstObjectByType<TileMapController>();
        if (_tileMapManager == null)
            _tileMapManager = FindFirstObjectByType<TileMapManager>();
    }

    void Update()
    {
        SyncFromPointer();
        UpdateHoldRepeat();

        InputManager input = InputManager.Instance;
        if (input != null &&
            input.TryReadPointerPressedThisFrame(out bool pressed) &&
            pressed)
            OnPrimaryClick();
    }

    /// <summary>매 프레임 포인터→셀 동기화. 부모 UI 비활성 시 FarmCellTargetSession이 호출.</summary>
    public void SyncFromPointer() => UpdateFromPointer();

    public void BeginTargeting(IFarmCellTargetSession session)
    {
        _targetSession = session;
        _targetingActive = true;
        enabled = true;
        EnsureCamera();
        CacheCursorRenderer();
        SyncCursorVisualParent();

        SyncFromPointer();
        NotifyHover();
    }

    public void EndTargeting()
    {
        _targetSession = null;
        _targetingActive = false;
        SyncCursorVisualParent();
        if (_placementState == null || _placementState.Selected == null)
            SetActive(false);
        ClearTargetTint();
    }

    public void SetTargetTint(Color tint)
    {
        _targetTint = tint;
        ApplyCursorTint();
    }

    public bool TryConfirmTargetingClick()
    {
        if (!_targetingActive || _targetSession == null)
            return false;

        return _targetSession.TryConfirm(_cursorGridPos);
    }

    void OnPrimaryClick()
    {
        if (_targetingActive && _targetSession != null)
        {
            TryConfirmTargetingClick();
            return;
        }

        TryPlace();
    }

    void UpdateFromPointer()
    {
        EnsureCamera();
        SyncCursorVisualParent();

        if (!TrySyncCellFromCameraRay(out Vector3Int grid, out Vector3 worldCenter))
            return;

        _cursorGridPos = grid;
        UpdateVisual(worldCenter);

        if (_targetingActive)
            NotifyHover();
    }

    bool TrySyncCellFromCameraRay(out Vector3Int cell, out Vector3 worldCenter) =>
        PlayerSightTarget.TryResolveOccupiedCellFromCameraRay(
            out cell,
            out worldCenter,
            ResolveCellSize(),
            _camera);

    void SyncCursorVisualParent() =>
        ReparentCursorVisualForTargeting(UsesWorldPointerSync);

    void NotifyHover()
    {
        if (!_targetingActive || _targetSession == null)
            return;

        bool canApply = _targetSession.CanApply(_cursorGridPos);
        _targetSession.OnCellHover(_cursorGridPos, canApply);
    }

    void UpdateHoldRepeat()
    {
        if (_heldDir == Vector2.zero) return;

        float dt = TimeScaleService.Delta(TimeScaleChannel.Realtime);
        _holdTimer += dt;
        if (_holdTimer < HOLD_THRESHOLD) return;

        _repeatTimer += dt;
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
        _cursorGridPos += new Vector3Int(
            Mathf.RoundToInt(dir.x),
            0,
            Mathf.RoundToInt(dir.y)
        );
        UpdateVisual();
        NotifyHover();
    }

    void OnSubmit(InputAction.CallbackContext ctx) => OnPrimaryClick();

    void TryPlace()
    {
        if (_targetingActive || _placementState == null || _placementState.Selected == null)
            return;

        ResolveTileMapRefs();
        if (_controller == null)
        {
            Debug.LogError("[GridCursor] TileMapController missing — cannot place.");
            return;
        }

        var def = _placementState.Selected;
        if (!TilePlaceUtil.CanPlaceOnBase(def, _cursorGridPos))
            return;
        if (!TilePlaceUtil.TryBuildTileData(def, _cursorGridPos, out TileData tileData))
            return;

        _controller.AddAndFlush(tileData);
    }

    void UpdateVisual()
    {
        UpdateVisual(TileHelper.ConvertGridToWorldPos(_cursorGridPos, ResolveCellSize()));
    }

    void UpdateVisual(Vector3 worldPos)
    {
        if (_cursorVisual == null) return;
        _cursorVisual.transform.position = worldPos;
        ApplyCursorTint();
    }

    public void SetActive(bool active)
    {
        if (_targetingActive && !active)
            return;

        if (!active)
            ReparentCursorVisualForTargeting(false);

        enabled = active;
        if (_cursorVisual != null)
            _cursorVisual.SetActive(active || UsesWorldPointerSync);
    }

    float ResolveCellSize() =>
        _tileMapManager?.WorldGrid != null ? _tileMapManager.WorldGrid.CellSize : 1f;

    void EnsureCamera()
    {
        // 픽·visual은 플레이 뷰(MainCamera)와 같아야 함. 씬에 IsoCam 등이 배선돼 있어도 무시.
        Camera main = Camera.main;
        if (main != null)
        {
            _camera = main;
            return;
        }

        if (_camera == null)
            _camera = FindFirstObjectByType<Camera>();
    }

    void ReparentCursorVisualForTargeting(bool targeting)
    {
        if (_cursorVisual == null)
            return;

        if (targeting)
        {
            if (_visualReparented)
                return;

            _visualOriginalParent = _cursorVisual.transform.parent;
            _cursorVisual.transform.SetParent(null, true);
            _visualReparented = true;
            _cursorVisual.SetActive(true);
            return;
        }

        if (!_visualReparented)
            return;

        if (_visualOriginalParent != null)
            _cursorVisual.transform.SetParent(_visualOriginalParent, true);

        _visualReparented = false;
        if (_placementState == null || _placementState.Selected == null)
            _cursorVisual.SetActive(false);
    }

    void CacheCursorRenderer()
    {
        if (_cursorVisual == null)
            return;

        _cursorRenderer = _cursorVisual.GetComponentInChildren<Renderer>();
        if (_cursorRenderer != null && _cursorRenderer.sharedMaterial != null)
            _cursorMaterial = _cursorRenderer.material;
    }

    void ApplyCursorTint()
    {
        if (_cursorMaterial == null)
            return;

        Color color = _targetingActive ? _targetTint : Color.white;
        if (_cursorMaterial.HasProperty("_BaseColor"))
            _cursorMaterial.SetColor("_BaseColor", color);
        else if (_cursorMaterial.HasProperty("_Color"))
            _cursorMaterial.SetColor("_Color", color);
    }

    void ClearTargetTint()
    {
        _targetTint = Color.white;
        ApplyCursorTint();
    }

    void OnDestroy()
    {
        InputManager input = InputManager.Instance;
        if (input == null)
            return;

        input.UiNavigateStarted -= OnNavigateStarted;
        input.UiNavigateCanceled -= OnNavigateCanceled;
        input.UiSubmitPerformed -= OnSubmit;
    }
}

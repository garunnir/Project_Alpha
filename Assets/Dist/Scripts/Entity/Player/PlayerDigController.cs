// ============================================================
// PlayerDigController — LMB 홀드 채굴 입력·타겟·진행·하이라이트
// ============================================================

using System;
using System.Collections.Generic;
using IsoTilemap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerDigController : MonoBehaviour
{
    [SerializeField] Camera _camera;
    [SerializeField] LayerMask _hitMask = ~0;

    CharacterState _characterState;
    readonly List<RaycastResult> _uiRaycastResults = new();

    Vector3Int _activeCell;
    DigTileTarget _activeTarget;
    float _holdProgress;
    Guid _highlightTileId = Guid.Empty;
    bool _inputEnabled = true;

    public void BindBody(CharacterState characterState) => _characterState = characterState;

    void Awake()
    {
        if (_characterState == null)
            _characterState = CharacterBodyResolve.GetInBody<CharacterState>(this);
    }

    public void SetEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!enabled)
            CancelDig();
    }

    void Update()
    {
        if (!_inputEnabled)
            return;

        if (ShouldSuppressDig())
        {
            CancelDig();
            return;
        }

        InputManager input = InputManager.Instance;
        if (input == null || !input.TryReadPointerHeld(out bool held) || !held)
        {
            CancelDig();
            return;
        }

        if (!TryResolveTarget(out DigTileTarget target))
        {
            CancelDig();
            return;
        }

        if (MapDigService.GetBlockedReason(target) != null)
        {
            CancelDig();
            return;
        }

        if (target.WalkableCell != _activeCell)
        {
            _activeCell = target.WalkableCell;
            _activeTarget = target;
            _holdProgress = 0f;
            SetHighlight(target.FaceTile.tileDefId);
        }

        _holdProgress += Time.deltaTime;
        if (_holdProgress < MapDigService.BaseBreakSeconds)
            return;

        if (MapDigService.TryBreak(_activeTarget))
        {
            _holdProgress = 0f;
            _activeCell = default;
            _activeTarget = default;
            ClearHighlight();
            return;
        }

        CancelDig();
    }

    bool ShouldSuppressDig()
    {
        if (_characterState != null && _characterState.IsAiming)
            return true;

        if (FarmCellTargetSession.IsActive ||
            ConstructionCellTargetSession.IsActive ||
            FishCellTargetSession.IsActive ||
            UIConstruction.IsOpen)
        {
            return true;
        }

        InputManager input = InputManager.Instance;
        if (input == null)
            return true;

        if (input.IsUiMenuInputActive)
            return true;

        return input.TryReadPointerScreenPosition(out Vector2 screenPos) &&
               IsPointerBlockedByUiAt(screenPos);
    }

    bool TryResolveTarget(out DigTileTarget target)
    {
        target = default;

        TileMapCacheHub hub = TileMapCacheHub.Runtime;
        if (hub == null)
            return false;

        Camera cam = _camera != null ? _camera : Camera.main;
        if (cam == null)
            return false;

        InputManager input = InputManager.Instance;
        if (input == null || !input.TryReadPointerScreenPosition(out Vector2 screenPos))
            return false;

        float cellSize = ResolveCellSize();
        TilePrefabDB prefabDb = ResolvePrefabDb();
        float feetY = CharacterFeetPose.GetFeetWorld(transform).y;

        return DigTileTargetResolver.TryResolve(
            cam,
            screenPos,
            hub,
            cellSize,
            MapDigConsts.MaxRayDistance,
            _hitMask,
            feetY,
            prefabDb,
            out target);
    }

    static float ResolveCellSize()
    {
        MapDigColumnHost digHost = MapDigColumnHost.Runtime;
        if (digHost != null)
            return digHost.CellSize;

        MapPlantHost plantHost = MapPlantHost.Runtime;
        return plantHost != null ? plantHost.CellSize : 1f;
    }

    static TilePrefabDB ResolvePrefabDb()
    {
        TileMapManager map = FindFirstObjectByType<TileMapManager>();
        return map != null ? map.PrefabDB : null;
    }

    void SetHighlight(Guid presentationTileId)
    {
        if (presentationTileId == Guid.Empty)
        {
            ClearHighlight();
            return;
        }

        if (_highlightTileId == presentationTileId)
            return;

        ClearHighlight();
        _highlightTileId = presentationTileId;
        TilePresentationSystem.Instance?.SetDigHighlight(presentationTileId, true);
    }

    void ClearHighlight()
    {
        if (_highlightTileId == Guid.Empty)
            return;

        TilePresentationSystem.Instance?.SetDigHighlight(_highlightTileId, false);
        _highlightTileId = Guid.Empty;
    }

    void CancelDig()
    {
        _holdProgress = 0f;
        _activeCell = default;
        _activeTarget = default;
        ClearHighlight();
    }

    bool IsPointerBlockedByUiAt(Vector2 screenPosition)
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        var pointerData = new PointerEventData(eventSystem) { position = screenPosition };
        _uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerData, _uiRaycastResults);

        for (int i = 0; i < _uiRaycastResults.Count; i++)
        {
            if (_uiRaycastResults[i].module is GraphicRaycaster)
                return true;
        }

        return false;
    }
}

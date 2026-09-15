// ============================================================
// TileObjectPointerController — 타일/캐릭터 호버 하이라이트 + RMB 클릭 메뉴
// ============================================================

using System;
using System.Collections.Generic;
using IsoTilemap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TileObjectPointerController : MonoBehaviour
{
    const int PhysicsHitBufferSize = 16;

    [SerializeField] Camera _refCam;
    [SerializeField] LayerMask _hitMask;
    [SerializeField] float _maxRayDistance = 200f;

    readonly List<RaycastResult> _uiRaycastResults = new();
    readonly RaycastHit[] _physicsHits = new RaycastHit[PhysicsHitBufferSize];

    TileObjectInteractionTarget _hoveredTile;
    PlayerInventoryHost _hoveredBody;
    bool _tileHoverOwned;
    bool _bodyHoverOwned;
    PlayerInventoryHost _cachedBodyContextHost;
    bool _cachedBodyHasContext;
    bool _connected;
    bool _inputEnabled = true;

    void Awake() => EnsurePointerPickMask();

    void Reset() => _hitMask = DistPhysicsLayerMasks.PointerWorldPick;

    void OnValidate() => EnsurePointerPickMask();

    void OnEnable()
    {
        EnsurePointerPickMask();
        if (_inputEnabled)
            Connect();
    }

    void Start()
    {
        if (_inputEnabled)
            Connect();
    }

    void EnsurePointerPickMask()
    {
        if (DistPhysicsLayerMasks.IncludesCharacter(_hitMask))
            return;

        _hitMask = DistPhysicsLayerMasks.WithCharacterPick(_hitMask);
    }

    void OnDisable()
    {
        Disconnect();
        ClearHover();
    }

    public void SetEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (enabled)
            Connect();
        else
        {
            Disconnect();
            ClearHover();
            ContextMenuHostEvents.RequestHide();
        }
    }

    void Connect()
    {
        InputManager input = InputManager.Instance;
        if (input == null || _connected)
            return;

        input.PlayerLookAtTapPerformed += OnLookAtTapPerformed;
        _connected = true;
    }

    void Disconnect()
    {
        InputManager input = InputManager.Instance;
        if (input != null && _connected)
            input.PlayerLookAtTapPerformed -= OnLookAtTapPerformed;

        _connected = false;
    }

    void LateUpdate()
    {
        if (!_inputEnabled)
            return;

        InputManager input = InputManager.Instance;
        if (input == null || !input.TryReadPointerScreenPosition(out Vector2 screenPos))
        {
            ClearHover();
            return;
        }

        if (IsPointerBlockedByUiAt(screenPos))
        {
            ClearHover();
            return;
        }

        if (!TryRaycastHits(
                screenPos,
                out TileObjectInteractionTarget tileTarget,
                out float tileDistance,
                out PlayerInventoryHost bodyTarget,
                out float bodyDistance))
        {
            ClearHover();
            return;
        }

        ApplyResolvedHover(tileTarget, tileDistance, bodyTarget, bodyDistance);
    }

    void OnLookAtTapPerformed(InputAction.CallbackContext context)
    {
        if (FishCellTargetSession.TryConsumeRightClick())
            return;

        if (ConstructionCellTargetSession.TryConsumeRightClick())
            return;

        if (FarmCellTargetSession.TryConsumeRightClick())
            return;

        InputManager input = InputManager.Instance;
        if (input == null || !input.TryReadPointerScreenPosition(out Vector2 screenPos))
            return;

        ProcessClick(screenPos);
    }

    void ProcessClick(Vector2 screenPosition)
    {
        if (IsPointerBlockedByUiAt(screenPosition))
            return;

        if (!TryRaycastPointerTargets(
                screenPosition,
                out TileObjectInteractionTarget tileTarget,
                out PlayerInventoryHost bodyLoot))
            return;

        if (bodyLoot != null && CharacterBodyContextMenuBuilder.TryShow(bodyLoot, screenPosition))
            return;

        TileObjectInteractionTarget target = tileTarget ?? _hoveredTile;
        if (target == null)
            return;

        ContextMenuModel model = target.BuildContextMenuModel();
        if (model.IsEmpty)
            return;

        if (!UIContextMenuHost.TryShow(model, screenPosition))
        {
            Debug.LogError(
                "[TileObjectPointerController] UIContextMenuHost failed to show.",
                this);
        }
    }

    void ApplyResolvedHover(
        TileObjectInteractionTarget tileTarget,
        float tileDistance,
        PlayerInventoryHost bodyTarget,
        float bodyDistance)
    {
        bool bodyEligible = bodyTarget != null && HasBodyHoverOutlineContext(bodyTarget);

        if (bodyTarget != null && tileTarget != null)
        {
            if (bodyDistance < tileDistance)
            {
                if (bodyEligible)
                    SetBodyHover(bodyTarget);
                else
                    SetTileHover(tileTarget);
                return;
            }

            SetTileHover(tileTarget);
            return;
        }

        if (bodyTarget != null)
        {
            if (bodyEligible)
                SetBodyHover(bodyTarget);
            else
                ClearHover();
            return;
        }

        if (tileTarget != null)
            SetTileHover(tileTarget);
        else
            ClearHover();
    }

    void SetTileHover(TileObjectInteractionTarget target)
    {
        if (_hoveredTile == target && _tileHoverOwned && _hoveredBody == null)
            return;

        ClearHover();
        _hoveredTile = target;
        ApplyTileHoverSelection(true);
    }

    void SetBodyHover(PlayerInventoryHost body)
    {
        if (_hoveredBody == body && _bodyHoverOwned && _hoveredTile == null)
            return;

        ClearHover();
        _hoveredBody = body;
        ApplyBodyHoverSelection(true);
    }

    bool HasBodyHoverOutlineContext(PlayerInventoryHost body)
    {
        if (body == null)
            return false;

        if (body == _cachedBodyContextHost)
            return _cachedBodyHasContext;

        _cachedBodyContextHost = body;
        _cachedBodyHasContext = CharacterBodyContextMenuBuilder.HasHoverOutlineContext(body);
        return _cachedBodyHasContext;
    }

    bool TryRaycastPointerTargets(
        Vector2 screenPos,
        out TileObjectInteractionTarget tileTarget,
        out PlayerInventoryHost bodyLoot)
    {
        tileTarget = null;
        bodyLoot = null;

        if (!TryRaycastHits(
                screenPos,
                out tileTarget,
                out float tileDistance,
                out bodyLoot,
                out float bodyDistance))
            return false;

        if (tileTarget != null && bodyLoot != null)
        {
            if (bodyDistance < tileDistance)
            {
                tileTarget = null;
                return true;
            }

            bodyLoot = null;
        }

        return tileTarget != null || bodyLoot != null;
    }

    bool TryRaycastHits(
        Vector2 screenPos,
        out TileObjectInteractionTarget tileTarget,
        out float tileDistance,
        out PlayerInventoryHost bodyTarget,
        out float bodyDistance)
    {
        tileTarget = null;
        bodyTarget = null;
        tileDistance = float.MaxValue;
        bodyDistance = float.MaxValue;

        Camera cam = _refCam != null ? _refCam : Camera.main;
        if (cam == null)
            return false;

        Ray ray = cam.ScreenPointToRay(screenPos);
        int hitCount = Physics.RaycastNonAlloc(
            ray,
            _physicsHits,
            _maxRayDistance,
            _hitMask,
            QueryTriggerInteraction.Collide);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _physicsHits[i];
            if (hit.collider == null)
                continue;

            TileObjectInteractionTarget tileCandidate =
                hit.collider.GetComponentInParent<TileObjectInteractionTarget>();
            if (tileCandidate != null && hit.distance < tileDistance)
            {
                tileDistance = hit.distance;
                tileTarget = tileCandidate;
            }

            PlayerInventoryHost bodyCandidate =
                CharacterBodyResolve.GetModule<PlayerInventoryHost>(hit.collider);
            if (bodyCandidate != null && hit.distance < bodyDistance)
            {
                bodyDistance = hit.distance;
                bodyTarget = bodyCandidate;
            }
        }

        return tileTarget != null || bodyTarget != null;
    }

    void ApplyTileHoverSelection(bool selected)
    {
        if (_hoveredTile == null)
            return;

        if (selected)
        {
            if (ShouldSkipHoverClearForLoot(_hoveredTile))
            {
                _tileHoverOwned = false;
                return;
            }

            _hoveredTile.SetHoverSelected(true);
            _tileHoverOwned = true;
            return;
        }

        if (!_tileHoverOwned)
            return;

        if (ShouldSkipHoverClearForLoot(_hoveredTile))
        {
            _tileHoverOwned = false;
            return;
        }

        _hoveredTile.SetHoverSelected(false);
        _tileHoverOwned = false;
    }

    void ApplyBodyHoverSelection(bool selected)
    {
        if (_hoveredBody == null)
            return;

        CharacterSelectionOutlineHost outlineHost =
            CharacterBodyResolve.GetModule<CharacterSelectionOutlineHost>(_hoveredBody.BodyRefs);
        if (outlineHost == null)
            return;

        outlineHost.SetHoverOutline(
            selected ? CharacterOutlineHoverState.Available : CharacterOutlineHoverState.None);
        _bodyHoverOwned = selected;
    }

    void ClearHover()
    {
        if (_hoveredTile != null)
            ApplyTileHoverSelection(false);

        if (_hoveredBody != null)
            ApplyBodyHoverSelection(false);

        _hoveredTile = null;
        _hoveredBody = null;
        _tileHoverOwned = false;
        _bodyHoverOwned = false;
        _cachedBodyContextHost = null;
        _cachedBodyHasContext = false;
    }

    static bool ShouldSkipHoverClearForLoot(TileObjectInteractionTarget target)
    {
        if (target == null)
            return false;

        Guid tileId = target.ResolvePresentationTileIdForLootGuard();
        if (tileId == Guid.Empty)
            return false;

        ITileLootHighlightSink sink = TilePresentationSystem.Instance;
        return sink != null && sink.IsLootHighlightActive(tileId);
    }

    /// <summary>
    /// GraphicRaycaster 히트만 UI로 본다. PhysicsRaycaster 월드 히트는 포인터 차단으로 치지 않는다.
    /// </summary>
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

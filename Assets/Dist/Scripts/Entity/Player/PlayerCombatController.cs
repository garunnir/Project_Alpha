// ============================================================
// PlayerCombatController — ICombatPerformDriver + TargetingPreview 라우트 (Layer2)
// ============================================================

using System.Collections.Generic;
using IsoTilemap;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class PlayerCombatController : MonoBehaviour
{
    CharacterAttacker _attacker;
    CharacterState _characterState;
    CharacterActionHost _actionHost;
    readonly List<RaycastResult> _uiRaycastResults = new();
    readonly ICombatPerformDriver[] _drivers =
    {
        new MeleeSwingPerformDriver(),
        new RangedTriggerPerformDriver(),
        new AutoHoldPerformDriver(),
        new ExcavateHoldPerformDriver(),
        new ChopHoldPerformDriver(),
    };
    readonly ICombatTargetingPreview[] _previewDrivers =
    {
        new MeleeBlockAimPreview(),
    };
    bool _connected;
    bool _inputEnabled = true;

    public void BindBody(
        CharacterAttacker attacker,
        CharacterState characterState,
        CharacterActionHost actionHost,
        Camera camera = null)
    {
        _attacker = attacker;
        _characterState = characterState;
        _actionHost = actionHost;
        // camera 인자는 하위 호환(구 ScreenPointToRay dig). AimWorldPoint 경로에서는 미사용.
        _ = camera;
    }

    void Awake()
    {
        _attacker = GetComponent<CharacterAttacker>();
        _characterState = GetComponent<CharacterState>();
        TryGetComponent(out _actionHost);
    }

    void OnDisable()
    {
        DisconnectInput();
        ClearDrivers();
    }

    /// <summary>PlayerController.SetControlEnabled 경로 — 조준/이동과 동일 소유권.</summary>
    public void SetEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (enabled)
            ConnectInput();
        else
        {
            DisconnectInput();
            ClearDrivers();
        }
    }

    void ConnectInput()
    {
        InputManager input = InputManager.Instance;
        if (input == null || _connected)
            return;

        input.PlayerCombatCyclePerformed += OnCombatCycle;
        input.PlayerCombatAttackPerformed += OnCombatAttack;
        _connected = true;
    }

    void DisconnectInput()
    {
        InputManager input = InputManager.Instance;
        if (input != null && _connected)
        {
            input.PlayerCombatCyclePerformed -= OnCombatCycle;
            input.PlayerCombatAttackPerformed -= OnCombatAttack;
        }

        _connected = false;
    }

    void Update()
    {
        CombatPerformContext ctx = BuildContext();
        for (int i = 0; i < _drivers.Length; i++)
            _drivers[i].Tick(ctx);

        // perform Clear(LMB up) 이후 같은 프레임에 RMB-only highlight 복구.
        for (int i = 0; i < _previewDrivers.Length; i++)
            _previewDrivers[i].TickPreview(ctx);
    }

    void OnCombatCycle(InputAction.CallbackContext context)
    {
        if (!context.performed)
            return;
        _attacker?.CycleSelectedLeaf();
    }

    void OnCombatAttack(InputAction.CallbackContext context)
    {
        if (!context.performed || _attacker == null)
            return;

        CombatPerformContext ctx = BuildContext();
        CombatLeaf leaf = _attacker.SelectedLeaf;
        for (int i = 0; i < _drivers.Length; i++)
        {
            if (!_drivers[i].MatchesLeaf(leaf))
                continue;
            if (_drivers[i].TryOnAttackPerformed(ctx))
                return;
        }
    }

    CombatPerformContext BuildContext() =>
        new(_attacker, _characterState, _actionHost, _inputEnabled, this);

    void ClearDrivers()
    {
        CombatPerformContext ctx = BuildContext();
        for (int i = 0; i < _drivers.Length; i++)
            _drivers[i].Clear(ctx);

        for (int i = 0; i < _previewDrivers.Length; i++)
            _previewDrivers[i].ClearPreview();
    }

    /// <summary>GraphicRaycaster UI가 포인터를 가로채면 시전·Excavate 차단.</summary>
    public bool IsAttackBlockedByUi()
    {
        InputManager input = InputManager.Instance;
        if (input == null || !input.TryReadPointerScreenPosition(out Vector2 screenPos))
            return false;
        return IsPointerBlockedByUiAt(screenPos);
    }

    /// <summary>
    /// CharacterState.AimWorldPoint → DigTileTarget (combat clamp SSOT).
    /// hold·preview 공용. 카메라 ScreenPointToRay 없음.
    /// </summary>
    public bool TryResolveDigTargetFromAim(out DigTileTarget target)
    {
        target = default;

        if (_characterState == null || !_characterState.IsAiming)
            return false;

        TileMapCacheHub hub = TileMapCacheHub.Runtime;
        if (hub == null)
            return false;

        float cellSize = ResolveCellSize();
        TilePrefabDB prefabDb = ResolvePrefabDb();
        Vector3 actorFeetWorld = CharacterFeetPose.GetFeetWorld(_characterState.transform);

        // lookOrigin = 발끝 — Dig 바깥면 facing 가점용 (Aim Y flatten과 분리).
        return DigTileTargetResolver.TryResolveFromCombatAim(
            _characterState.AimWorldPoint,
            _characterState.InteractionDir,
            actorFeetWorld,
            hub,
            cellSize,
            prefabDb,
            out target);
    }

    /// <summary>CharacterState.AimWorldPoint → ChopPlantTarget.</summary>
    public bool TryResolveChopTargetFromAim(out ChopPlantTarget target)
    {
        target = default;
        if (_characterState == null || !_characterState.IsAiming)
            return false;

        return ChopPlantTargetResolver.TryResolveFromWorldPoint(
            _characterState.AimWorldPoint,
            ResolveCellSize(),
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

    /// <summary>
    /// GraphicRaycaster 히트만 UI로 본다. PhysicsRaycaster 월드 히트는 차단하지 않는다.
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

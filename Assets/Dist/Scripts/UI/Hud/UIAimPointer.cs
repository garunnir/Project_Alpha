// ============================================================
// UIAimPointer — 원거리 RMB 조준 포인터 (센터 프리팹 고정 + SDF 링)
// ============================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public sealed class UIAimPointer : MonoBehaviour
{
    static readonly int ThicknessId = Shader.PropertyToID("_Thickness");
    static readonly int SoftFillId = Shader.PropertyToID("_SoftFill");

    [SerializeField] RectTransform _root;
    [SerializeField] CanvasGroup _canvasGroup;
    [SerializeField] RectTransform _ring;
    [SerializeField] Image _ringImage;
    [SerializeField] Material _ringMaterialTemplate;
    [SerializeField] RectTransform _center;
    [SerializeField] Canvas _rootCanvas;
    [SerializeField] Camera _worldCamera;
    [Tooltip("투영 반경 하한(스크린 픽셀). 프리팹 SSOT.")]
    [SerializeField] float _minRadiusPx = 6f;
    [Tooltip("투영 반경 상한(스크린 픽셀). 프리팹 SSOT.")]
    [SerializeField] float _maxRadiusPx = 240f;
    [Tooltip("링 선 굵기(스크린 픽셀). 반경과 무관. 프리팹 SSOT.")]
    [SerializeField] float _strokePx = 2f;
    [Tooltip("원 안 soft fill 알파. 프리팹 SSOT.")]
    [SerializeField] [Range(0f, 1f)] float _softFill = 0.12f;

    CharacterAttacker _attacker;
    CharacterState _characterState;
    Material _ringMat;
    readonly List<RaycastResult> _uiRaycastResults = new();
    bool _bound;
    bool _visible;
    bool _hidOsCursor;

    void Awake()
    {
        if (_root == null)
            _root = transform as RectTransform;
        if (_canvasGroup == null)
            TryGetComponent(out _canvasGroup);
        if (_rootCanvas == null)
            _rootCanvas = GetComponentInParent<Canvas>();
        if (_ringImage == null && _ring != null)
            _ring.TryGetComponent(out _ringImage);

        EnsureRingMaterial();
        SetVisible(false);
    }

    void OnDestroy()
    {
        if (_ringMat != null)
            Destroy(_ringMat);
    }

    void OnDisable()
    {
        SetVisible(false);
        RestoreOsCursor();
    }

    void LateUpdate()
    {
        if (!_bound)
            TryBind();

        if (!TryShouldShow(out Vector2 screenPos, out float radiusPx))
        {
            SetVisible(false);
            RestoreOsCursor();
            return;
        }

        SetVisible(true);
        HideOsCursor();
        PlaceAtScreen(screenPos);
        ApplyRing(screenPos, radiusPx);
    }

    void EnsureRingMaterial()
    {
        if (_ringImage == null || _ringMaterialTemplate == null)
            return;

        _ringMat = new Material(_ringMaterialTemplate);
        _ringImage.material = _ringMat;
        _ringMat.SetFloat(SoftFillId, _softFill);
    }

    void TryBind()
    {
        PlayerGearHost host = PlayerGearHost.Active;
        if (host == null)
        {
            if (_bound)
            {
                _attacker = null;
                _characterState = null;
                _bound = false;
            }
            return;
        }

        if (_bound &&
            _attacker != null &&
            CharacterBodyResolve.IsSameBodyRoot(_attacker, host))
            return;

        CharacterBodyRefs refs = host.GetBodyRefs();
        if (refs == null)
            return;

        CharacterAttacker attacker = refs.Attacker;
        CharacterState state = refs.State;
        if (attacker == null || state == null)
            return;

        _attacker = attacker;
        _characterState = state;
        _bound = true;
    }

    bool TryShouldShow(out Vector2 screenPos, out float radiusPx)
    {
        screenPos = default;
        radiusPx = 0f;

        if (_attacker == null || _characterState == null || !_characterState.IsAiming)
            return false;

        InputManager input = InputManager.Instance;
        if (input == null || !input.IsPlayerActionEnabled(PlayerAction.Aim))
            return false;

        if (!input.TryReadPointerScreenPosition(out screenPos))
            return false;

        if (IsPointerBlockedByUiAt(screenPos))
            return false;

        if (!_attacker.TryPreviewRangedSpread(out float effective))
            return false;

        Camera worldCam = ResolveWorldCamera();
        if (worldCam == null)
            return false;

        float range = _characterState.InteractionReach;
        if (range <= 1e-4f)
            return false;

        radiusPx = CombatMath.ProjectYawHalfWidthPixels(
            worldCam,
            _attacker.ResolveOrigin(),
            _attacker.ResolveFireDirection(),
            range,
            effective);

        if (_maxRadiusPx > _minRadiusPx)
            radiusPx = Mathf.Clamp(radiusPx, _minRadiusPx, _maxRadiusPx);
        else
            radiusPx = Mathf.Max(radiusPx, _minRadiusPx);

        return true;
    }

    void PlaceAtScreen(Vector2 screenPos)
    {
        if (_root == null || _rootCanvas == null)
            return;

        UIPopupPositioner.PlaceAtScreenPoint(_root, screenPos, _rootCanvas);
    }

    void ApplyRing(Vector2 screenPos, float radiusScreenPx)
    {
        if (_ring == null)
            return;

        float diameterLocal = radiusScreenPx * 2f;
        float strokeLocal = _strokePx;
        RectTransform parent = _root != null ? _root.parent as RectTransform : null;
        if (parent != null && _rootCanvas != null)
        {
            Camera uiCam = UIPopupPositioner.ResolveCamera(_rootCanvas);
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenPos, uiCam, out Vector2 localCenter) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    screenPos + new Vector2(radiusScreenPx, 0f),
                    uiCam,
                    out Vector2 localEdge))
            {
                float radiusLocal = Vector2.Distance(localCenter, localEdge);
                diameterLocal = radiusLocal * 2f;
            }

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent, screenPos, uiCam, out Vector2 a) &&
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parent,
                    screenPos + new Vector2(_strokePx, 0f),
                    uiCam,
                    out Vector2 b))
            {
                strokeLocal = Vector2.Distance(a, b);
            }
        }

        _ring.sizeDelta = new Vector2(diameterLocal, diameterLocal);

        if (_ringMat == null)
            return;

        // UV radius=1 at quad edge; thickness as fraction of that radius.
        float radiusLocalHalf = diameterLocal * 0.5f;
        float thicknessNorm = radiusLocalHalf > 1e-4f
            ? Mathf.Clamp(strokeLocal / radiusLocalHalf, 0.002f, 0.5f)
            : 0.04f;
        _ringMat.SetFloat(ThicknessId, thicknessNorm);
        _ringMat.SetFloat(SoftFillId, _softFill);
    }

    void SetVisible(bool visible)
    {
        if (_visible == visible)
            return;
        _visible = visible;
        if (_canvasGroup == null)
            return;
        _canvasGroup.alpha = visible ? 1f : 0f;
        _canvasGroup.blocksRaycasts = false;
        _canvasGroup.interactable = false;
    }

    void HideOsCursor()
    {
        if (_hidOsCursor)
            return;
        Cursor.visible = false;
        _hidOsCursor = true;
    }

    void RestoreOsCursor()
    {
        if (!_hidOsCursor)
            return;
        Cursor.visible = true;
        _hidOsCursor = false;
    }

    Camera ResolveWorldCamera()
    {
        if (_worldCamera != null)
            return _worldCamera;
        return Camera.main;
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

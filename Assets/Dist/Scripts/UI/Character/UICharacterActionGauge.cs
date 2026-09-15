// ============================================================
// UICharacterActionGauge — 행위자 Host 진행 fill / 자동이동 아이콘
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public sealed class UICharacterActionGauge : MonoBehaviour
{
    [SerializeField] CharacterActionHost _host;
    [SerializeField] Image _fill;
    [SerializeField] Canvas _canvas;
    [SerializeField] GameObject _autoProgressIcon;
    [SerializeField] CanvasGroup _canvasGroup;

    void Awake()
    {
        if (_host == null)
            _host = CharacterBodyResolve.GetInBody<CharacterActionHost>(this);
        if (_host == null)
            _host = GetComponentInParent<CharacterActionHost>();

        if (_fill == null)
        {
            Transform fill = transform.Find(CharacterActionGaugeLayout.FillName);
            if (fill != null)
                fill.TryGetComponent(out _fill);
        }

        if (_autoProgressIcon == null)
            _autoProgressIcon = ResolveAutoProgressIcon();

        if (_canvas == null)
            TryGetComponent(out _canvas);
        if (_canvas == null && transform.parent != null)
            transform.parent.TryGetComponent(out _canvas);

        if (_canvasGroup == null)
            TryGetComponent(out _canvasGroup);
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    void OnEnable()
    {
        // 공유 부모 Canvas는 끄지 않음(Emote 등과 공유). 표시는 CanvasGroup만.
        if (_canvas != null)
        {
            if (!_canvas.enabled)
                _canvas.enabled = true;
            if (_canvas.worldCamera == null)
                _canvas.worldCamera = Camera.main;
        }

        ApplyVisible(false);
        SetAutoProgressIcon(false);
    }

    void LateUpdate()
    {
        // Rule 6: fillAmount·alpha만. 할당 없음(Awake에서 CanvasGroup 확보).
        if (_host == null)
        {
            ApplyVisible(false);
            SetAutoProgressIcon(false);
            return;
        }

        bool show = _host.HasVisibleProgress;
        ApplyVisible(show);
        if (!show)
        {
            SetAutoProgressIcon(false);
            return;
        }

        EnsureFillActive();

        bool autoMove = _host.IsCellArriving;
        SetAutoProgressIcon(autoMove);

        if (_fill != null)
        {
            if (_fill.enabled == autoMove)
                _fill.enabled = !autoMove;
            if (!autoMove)
                _fill.fillAmount = _host.Progress01;
        }
    }

    GameObject ResolveAutoProgressIcon()
    {
        Transform icon = transform.Find(CharacterActionGaugeLayout.AutoProgressIconName);
        if (icon == null && transform.parent != null)
            icon = transform.parent.Find(CharacterActionGaugeLayout.AutoProgressIconName);
        return icon != null ? icon.gameObject : null;
    }

    void SetAutoProgressIcon(bool show)
    {
        if (_autoProgressIcon == null)
            return;
        if (_autoProgressIcon.activeSelf != show)
            _autoProgressIcon.SetActive(show);
    }

    void ApplyVisible(bool show)
    {
        if (_canvasGroup != null)
        {
            float alpha = show ? 1f : 0f;
            if (!Mathf.Approximately(_canvasGroup.alpha, alpha))
                _canvasGroup.alpha = alpha;
            if (_canvasGroup.blocksRaycasts != show)
                _canvasGroup.blocksRaycasts = show;
            if (_canvasGroup.interactable != show)
                _canvasGroup.interactable = show;
        }
        else if (gameObject.activeSelf != show)
        {
            gameObject.SetActive(show);
        }

        if (show)
            EnsureFillActive();
    }

    void EnsureFillActive()
    {
        if (_fill == null)
            return;
        if (!_fill.gameObject.activeSelf)
            _fill.gameObject.SetActive(true);
    }
}

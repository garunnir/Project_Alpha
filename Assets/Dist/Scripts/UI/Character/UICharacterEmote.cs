// ============================================================
// UICharacterEmote — CharacterEmoteHost resolved sprite / tint
// ============================================================

using UnityEngine;
using UnityEngine.UI;

public sealed class UICharacterEmote : MonoBehaviour
{
    CharacterEmoteHost _host;
    [SerializeField] Image _icon;
    [SerializeField] Canvas _canvas;

    void Awake()
    {
        if (_host == null)
            _host = this.GetBodyModule<CharacterEmoteHost>();

        if (_icon == null)
        {
            Transform icon = transform.Find(CharacterEmoteLayout.IconName);
            if (icon != null)
                icon.TryGetComponent(out _icon);
        }

        if (_canvas == null)
            TryGetComponent(out _canvas);
        if (_canvas == null && transform.parent != null)
            transform.parent.TryGetComponent(out _canvas);
    }

    void OnEnable()
    {
        if (_canvas != null && _canvas.worldCamera == null)
            _canvas.worldCamera = Camera.main;
        ApplyVisible(false);
    }

    void LateUpdate()
    {
        if (_host == null)
        {
            ApplyVisible(false);
            return;
        }

        bool show = _host.TryGetResolvedDisplay(out Sprite sprite, out Color tint);
        ApplyVisible(show);
        if (!show || _icon == null)
            return;

        if (_icon.sprite != sprite)
            _icon.sprite = sprite;
        if (_icon.color != tint)
            _icon.color = tint;
    }

    void ApplyVisible(bool show)
    {
        if (_canvas != null)
        {
            if (_canvas.enabled != show)
                _canvas.enabled = show;
            return;
        }

        if (gameObject.activeSelf != show)
            gameObject.SetActive(show);
    }
}

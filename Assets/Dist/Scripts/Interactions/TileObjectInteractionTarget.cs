// ============================================================
// TileObjectInteractionTarget — 타일 오브젝트 상호작용 액션 SSOT + 빠른 E
// ============================================================

using System;
using System.Collections.Generic;
using Interactions;
using IsoTilemap;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TileObjectInteractionTarget : MonoBehaviour, IInteractable
{
    [SerializeField] TileView _tileView;

    IInteractableFocusVisual _focusVisual;
    string _cachedHintText = string.Empty;

    public TileView TileView => _tileView;
    public Transform InteractTransform => transform;

    public string HintText =>
        string.IsNullOrEmpty(_cachedHintText) ? string.Empty : _cachedHintText;

    void Awake()
    {
        if (_tileView == null)
            _tileView = GetComponent<TileView>() ?? GetComponentInChildren<TileView>(true);

        CacheFocusVisual();
    }

    void CacheFocusVisual()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IInteractableFocusVisual visual)
            {
                _focusVisual = visual;
                break;
            }
        }
    }

    public ContextMenuModel BuildContextMenuModel() =>
        TileObjectContextMenuBuilder.Build(this, TileObjectContextMenuCatalog.All);

    public bool TryGetSingleExecutableLeaf(out ContextMenuEntry leaf)
    {
        leaf = null;
        ContextMenuModel model = BuildContextMenuModel();
        if (model.IsEmpty)
            return false;

        var leaves = new List<ContextMenuEntry>();
        ContextMenuQuery.CollectExecutableLeaves(model.Roots, leaves);
        if (leaves.Count != 1)
            return false;

        leaf = leaves[0];
        return true;
    }

    public bool CanInteract(GameObject interactor) =>
        TryGetSingleExecutableLeaf(out _);

    public void Interact(GameObject interactor)
    {
        if (!TryGetSingleExecutableLeaf(out ContextMenuEntry leaf) || leaf.Action == null)
            return;

        leaf.Action.Execute();

        if (TryGetSingleExecutableLeaf(out ContextMenuEntry next))
        {
            _cachedHintText = next.Label ?? string.Empty;
            UIEvents.RequestPopup(UIPopupType.InteractionHint, this);
        }
    }

    public void OnFocus(GameObject interactor)
    {
        if (TryGetSingleExecutableLeaf(out ContextMenuEntry leaf))
        {
            _cachedHintText = leaf.Label ?? string.Empty;
            UIEvents.RequestPopup(UIPopupType.InteractionHint, this);
        }
        else
        {
            _cachedHintText = string.Empty;
        }

        _focusVisual?.OnFocusVisual(interactor);
    }

    public void OnUnfocus(GameObject interactor)
    {
        UIEvents.RequestPopup(UIPopupType.none, this);
        _cachedHintText = string.Empty;
        _focusVisual?.OnUnfocusVisual(interactor);
    }

    public void BindTileView(TileView tileView)
    {
        if (tileView != null)
            _tileView = tileView;
    }

    public void SetHoverSelected(bool selected)
    {
        if (_tileView != null)
            _tileView.SetSelected(selected);
    }

    public Guid ResolvePresentationTileIdForLootGuard()
    {
        ContainerInteractable container = GetComponent<ContainerInteractable>();
        if (container != null)
            return container.PresentationTileId;

        return Guid.Empty;
    }

}

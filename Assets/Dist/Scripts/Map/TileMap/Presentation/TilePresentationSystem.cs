// ============================================================
// TilePresentationSystem — 월드 타일 표현 단일 진입점 (Map)
// ============================================================

using System;
using IsoTilemap;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TilePresentationSystem : MonoBehaviour, ITileLootHighlightSink
{
    public static TilePresentationSystem Instance { get; private set; }

    TileViewPresentationApplier _applier;
    Guid _activeLootTileId = Guid.Empty;
    Guid _activeDigTileId = Guid.Empty;

    public void Initialize(TileViewPresentationApplier applier) => _applier = applier;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[TilePresentationSystem] Duplicate instance ignored.", this);
            return;
        }

        Instance = this;
    }

    void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    void OnDisable()
    {
        ClearLootHighlight();
        ClearDigHighlight();
    }

    public void SetLootHighlight(Guid presentationTileId, bool highlighted) =>
        SetLootContainerHighlight(presentationTileId, highlighted);

    public void ClearLootHighlight() => ClearLootContainerHighlight();

    public bool IsLootHighlightActive(Guid presentationTileId) =>
        presentationTileId != Guid.Empty && _activeLootTileId == presentationTileId;

    public void SetLootContainerHighlight(Guid presentationTileId, bool highlighted)
    {
        if (_applier == null || presentationTileId == Guid.Empty)
            return;

        if (highlighted)
        {
            if (_activeLootTileId != Guid.Empty && _activeLootTileId != presentationTileId)
                _applier.SetSelected(_activeLootTileId, false);

            _applier.SetSelected(presentationTileId, true);
            _activeLootTileId = presentationTileId;
            return;
        }

        if (_activeLootTileId == presentationTileId)
            _activeLootTileId = Guid.Empty;

        _applier.SetSelected(presentationTileId, false);
    }

    public void ClearLootContainerHighlight()
    {
        if (_applier == null || _activeLootTileId == Guid.Empty)
        {
            _activeLootTileId = Guid.Empty;
            return;
        }

        _applier.SetSelected(_activeLootTileId, false);
        _activeLootTileId = Guid.Empty;
    }

    public void SetDigHighlight(Guid presentationTileId, bool highlighted)
    {
        if (_applier == null || presentationTileId == Guid.Empty)
            return;

        if (highlighted)
        {
            if (_activeDigTileId != Guid.Empty && _activeDigTileId != presentationTileId)
                _applier.SetSelected(_activeDigTileId, false);

            _applier.SetSelected(presentationTileId, true);
            _activeDigTileId = presentationTileId;
            return;
        }

        if (_activeDigTileId == presentationTileId)
            _activeDigTileId = Guid.Empty;

        _applier.SetSelected(presentationTileId, false);
    }

    public void ClearDigHighlight()
    {
        if (_applier == null || _activeDigTileId == Guid.Empty)
        {
            _activeDigTileId = Guid.Empty;
            return;
        }

        _applier.SetSelected(_activeDigTileId, false);
        _activeDigTileId = Guid.Empty;
    }
}

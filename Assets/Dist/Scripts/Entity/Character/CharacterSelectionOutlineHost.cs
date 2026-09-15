// ============================================================
// CharacterSelectionOutlineHost — 호버 시 ProPixelizer OutlineControl/_OutlineColor 토글 (plain module)
// ============================================================

using System.Collections.Generic;
using ProPixelizer;
using UnityEngine;

public sealed class CharacterSelectionOutlineHost
{
    static readonly int s_OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int s_ObjectId = Shader.PropertyToID("_ID");

    CharacterBodyRefs _refs;
    Transform _renderRoot;
    SelectionLayerConfig _selectionLayer;
    readonly List<OutlineControl> _outlineControls = new();
    bool _cached;
    CharacterOutlineHoverState _state = CharacterOutlineHoverState.None;

    public void Bind(CharacterBodyRefs refs)
    {
        _refs = refs;
        _renderRoot = refs != null ? refs.RenderRoot : null;
        _selectionLayer = refs != null ? refs.SelectionLayer : null;
        _cached = false;
    }

    public void SetHoverOutline(CharacterOutlineHoverState state)
    {
        if (_state == state && _cached)
            return;

        EnsureCache();
        if (_outlineControls.Count == 0 || _selectionLayer == null)
            return;

        Color color = ResolveOutlineColor(state);
        for (int i = 0; i < _outlineControls.Count; i++)
            ApplyOutlineColor(_outlineControls[i], color);

        _state = state;
    }

    public void Disable()
    {
        if (_state != CharacterOutlineHoverState.None)
            SetHoverOutline(CharacterOutlineHoverState.None);
    }

    Color ResolveOutlineColor(CharacterOutlineHoverState state) =>
        state switch
        {
            CharacterOutlineHoverState.Available => _selectionLayer.CharacterAvailableOutlineColor,
            CharacterOutlineHoverState.Unavailable => _selectionLayer.CharacterUnavailableOutlineColor,
            _ => _selectionLayer.CharacterIdleOutlineColor,
        };

    void ApplyOutlineColor(OutlineControl control, Color color)
    {
        if (control == null)
            return;

        control.UseRootColor = false;
        control.Color = color;

        Renderer renderer = control.GetComponent<Renderer>();
        if (renderer == null)
            return;

        float objectId = control.ID % 255f;
        Material[] materials = renderer.materials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || !material.HasProperty(s_OutlineColorId))
                continue;

            material.SetColor(s_OutlineColorId, color);
            if (material.HasProperty(s_ObjectId))
                material.SetFloat(s_ObjectId, objectId);
        }
    }

    void EnsureCache()
    {
        if (_cached)
            return;

        Transform searchRoot = _renderRoot;
        if (searchRoot == null && _refs != null)
        {
            Transform pivot = _refs.transform.Find(CharacterBodyRefs.RenderPivotChildName);
            searchRoot = pivot != null ? pivot : _refs.transform;
        }

        if (searchRoot == null)
            return;

        _outlineControls.Clear();

        OutlineControl[] existing = searchRoot.GetComponentsInChildren<OutlineControl>(true);
        for (int i = 0; i < existing.Length; i++)
        {
            if (existing[i] != null)
                _outlineControls.Add(existing[i]);
        }

        Renderer[] renderers = searchRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.GetComponent<OutlineControl>() != null)
                continue;

            if (!RendererSupportsProPixelizerOutline(renderer))
                continue;

            OutlineControl added = renderer.gameObject.AddComponent<OutlineControl>();
            added.UseRandomUID = false;
            added.UseRootUID = false;
            added.UseRootColor = false;
            _outlineControls.Add(added);
        }

        _cached = true;
    }

    static bool RendererSupportsProPixelizerOutline(Renderer renderer)
    {
        Material[] materials = renderer.sharedMaterials;
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material != null && material.HasProperty(s_OutlineColorId))
                return true;
        }

        return false;
    }
}

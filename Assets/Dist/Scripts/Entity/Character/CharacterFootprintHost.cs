// ============================================================
// CharacterFootprintHost — CharacterDefinition grid footprint 런타임 보관 (plain module)
// ============================================================

using IsoTilemap;
using UnityEngine;

public sealed class CharacterFootprintHost
{
    CharacterBodyRefs _refs;
    Vector3Int _gridFootprint = CharacterGridFootprintDefaults.Default;
    Vector3Int _resolvedFootprint = CharacterGridFootprintDefaults.Default;
    bool _resolvedInitialized;

    /// <summary>런타임 grid footprint. max(SO, CapsuleCollider-derived).</summary>
    public Vector3Int GridFootprint
    {
        get
        {
            if (!_resolvedInitialized)
                RebuildResolvedFootprint();
            return _resolvedFootprint;
        }
    }

    /// <summary>CharacterDefinition에서 적용한 기본 footprint (셀).</summary>
    public Vector3Int BaseGridFootprint => CharacterGridFootprintDefaults.Clamp(_gridFootprint);

    public void Bind(CharacterBodyRefs refs)
    {
        _refs = refs;
        RebuildResolvedFootprint();
#if UNITY_EDITOR
        ValidateFootprintAgainstCapsule();
#endif
    }

    public void ApplyFromDefinition(CharacterDefinition definition)
    {
        _gridFootprint = definition != null
            ? definition.GridFootprint
            : CharacterGridFootprintDefaults.Default;
        RebuildResolvedFootprint();
    }

    void RebuildResolvedFootprint()
    {
        Vector3Int baseFootprint = BaseGridFootprint;
        CapsuleCollider capsule = ResolveCapsule();
        if (capsule == null)
        {
            _resolvedFootprint = baseFootprint;
            _resolvedInitialized = true;
            return;
        }

        _resolvedFootprint = CharacterGridFootprintResolver.Resolve(
            capsule,
            ResolveCellSize(),
            baseFootprint);
        _resolvedInitialized = true;
    }

    CapsuleCollider ResolveCapsule()
    {
        if (_refs == null)
            return null;
        return _refs.Get<CapsuleCollider>();
    }

    static float ResolveCellSize()
    {
        TileMapManager manager = Object.FindFirstObjectByType<TileMapManager>();
        if (manager != null && manager.WorldGrid != null && manager.WorldGrid.CellSize > 0f)
            return manager.WorldGrid.CellSize;
        return 1f;
    }

#if UNITY_EDITOR
    const float FootprintDimensionEpsilonMeters = 0.05f;

    void ValidateFootprintAgainstCapsule()
    {
        CapsuleCollider capsule = ResolveCapsule();
        if (capsule == null)
            return;

        float cellSize = ResolveCellSize();
        Vector3Int baseFootprint = BaseGridFootprint;
        Vector3Int derived = CharacterGridFootprintResolver.DeriveFromCapsule(capsule, cellSize);
        Vector3Int resolved = CharacterGridFootprintResolver.Resolve(capsule, cellSize, baseFootprint);
        Object logContext = _refs;

        if (resolved != baseFootprint)
        {
            Debug.Log(
                $"[{nameof(CharacterFootprintHost)}] '{(_refs != null ? _refs.name : string.Empty)}' runtime footprint expands to " +
                $"{resolved} from base {baseFootprint} (capsule-derived {derived}, cellSize={cellSize:0.###}).",
                logContext);
        }

        float expectedHeight = baseFootprint.y * cellSize;
        float capsuleHeight = capsule.height * Mathf.Abs(capsule.transform.lossyScale.y);
        float heightTolerance = Mathf.Max(FootprintDimensionEpsilonMeters, expectedHeight * 0.1f);
        if (Mathf.Abs(expectedHeight - capsuleHeight) > heightTolerance)
        {
            Debug.LogWarning(
                $"[{nameof(CharacterFootprintHost)}] '{(_refs != null ? _refs.name : string.Empty)}' base footprint Y={baseFootprint.y} " +
                $"× cellSize={cellSize:0.###} → {expectedHeight:0.###} m, " +
                $"but CapsuleCollider.height×scale={capsuleHeight:0.###} m.",
                logContext);
        }

        float expectedWidth = baseFootprint.x * cellSize;
        float capsuleDiameter = capsule.radius * 2f *
            Mathf.Max(Mathf.Abs(capsule.transform.lossyScale.x), Mathf.Abs(capsule.transform.lossyScale.y));
        float widthTolerance = Mathf.Max(FootprintDimensionEpsilonMeters, expectedWidth * 0.1f);
        if (derived.x > baseFootprint.x && Mathf.Abs(expectedWidth - capsuleDiameter) > widthTolerance)
        {
            Debug.LogWarning(
                $"[{nameof(CharacterFootprintHost)}] '{(_refs != null ? _refs.name : string.Empty)}' base footprint X/Z={baseFootprint.x} " +
                $"× cellSize={cellSize:0.###} → {expectedWidth:0.###} m wide, " +
                $"but CapsuleCollider diameter×scale={capsuleDiameter:0.###} m " +
                $"(runtime uses {resolved.x} cells).",
                logContext);
        }
    }
#endif
}

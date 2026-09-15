// ============================================================
// DistPhysicsLayerMasks — Physics Layer / LayerMask SSOT (TagManager)
// ============================================================

using UnityEngine;

public static class DistPhysicsLayers
{
    public const int Default = 0;
    public const int TransparentFx = 1;
    public const int IgnoreRaycast = 2;
    public const int Water = 4;
    public const int Ui = 5;
    public const int Character = 6;
}

public static class DistPhysicsLayerMasks
{
    /// <summary>
    /// IsoLand <see cref="TileObjectPointerController"/> legacy serialized pick mask (Character 제외).
    /// </summary>
    public const int LegacyIsoLandPointerPickBits = 55;

    public const int CharacterLayerBit = 1 << DistPhysicsLayers.Character;

    /// <summary>월드 RMB/호버 pointer pick — legacy mask + Character 몸 캡슐.</summary>
    public static LayerMask PointerWorldPick =>
        LegacyIsoLandPointerPickBits | CharacterLayerBit;

    public static bool IncludesCharacter(LayerMask mask) =>
        (mask.value & CharacterLayerBit) != 0;

    public static LayerMask WithCharacterPick(LayerMask mask) =>
        mask.value | CharacterLayerBit;
}

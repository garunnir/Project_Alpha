// ============================================================
// CombatAimSightPolicy — Leaf/ResolveMode → Sight knobs (Layer1, 입력 밖)
// ============================================================

using Garunnir.Runtime.Gameplay.Data;

/// <summary>
/// 조준 월드점 해석 중 Leaf에 따라 바뀌는 항목.
/// Cast 반경·거리·마스크는 입력 컨트롤러 기본값.
/// </summary>
public readonly struct AimSightLeafPolicy
{
    public readonly bool FlattenAimYToPlayerHeight;

    public AimSightLeafPolicy(bool flattenAimYToPlayerHeight) =>
        FlattenAimYToPlayerHeight = flattenAimYToPlayerHeight;
}

/// <summary>
/// Sight 정책 SSOT. 입력(<see cref="PlayerAimController"/>)은 baseFlatten만 제공하고,
/// Mode/Leaf가 최종 flatten을 고른다.
/// </summary>
public static class CombatAimSightPolicy
{
    /// <summary>
    /// <see cref="WeaponResolveMode.MeleeBlock"/>(Excavate): Y 유지.
    /// 그 외: <paramref name="baseFlattenAimY"/> (컨트롤러 Inspector 기본).
    /// </summary>
    public static AimSightLeafPolicy Resolve(CombatLeaf leaf, bool baseFlattenAimY) =>
        Resolve(CombatLeafUtil.ResolveMode(leaf), baseFlattenAimY);

    public static AimSightLeafPolicy Resolve(WeaponResolveMode mode, bool baseFlattenAimY)
    {
        if (mode == WeaponResolveMode.MeleeBlock)
            return new AimSightLeafPolicy(flattenAimYToPlayerHeight: false);

        return new AimSightLeafPolicy(flattenAimYToPlayerHeight: baseFlattenAimY);
    }
}

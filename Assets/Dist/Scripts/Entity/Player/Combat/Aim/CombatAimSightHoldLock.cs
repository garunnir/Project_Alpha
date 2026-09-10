// ============================================================
// CombatAimSightHoldLock — Dig LMB 잠금 시 SphereCast 대신 SightDir 고정
// ============================================================

using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

/// <summary>
/// Excavate 홀드 잠금이면 AimWorldPoint/SightDir을 잠금 블록에 고정.
/// 마우스 SphereCast 스킵. 카메라는 Aim과 분리되어 자유.
/// </summary>
public static class CombatAimSightHoldLock
{
    public static bool TryApplyLockedSight(
        CharacterState state,
        Transform body,
        float castOriginYOffset,
        CharacterAttacker attacker,
        CharacterActionHost actionHost)
    {
        if (state == null || body == null || attacker == null || actionHost == null)
            return false;

        if (CombatLeafUtil.ResolveMode(attacker.SelectedLeaf) != WeaponResolveMode.MeleeBlock)
            return false;

        CharacterDigPipeline dig = actionHost.DigPipeline;
        if (dig == null || !dig.TryGetActiveTarget(out DigTileTarget target))
            return false;

        float cellSize = ResolveCellSize();
        Vector3 lookOrigin = CharacterFeetPose.GetFeetWorld(body);
        if (!DigTileTargetAimPose.TryGetSightWorldPoint(
                in target,
                cellSize,
                lookOrigin,
                out Vector3 aimPoint))
        {
            return false;
        }

        Vector3 origin = body.position + Vector3.up * castOriginYOffset;
        Vector3 sightFlat = aimPoint - origin;
        sightFlat.y = 0f;
        if (sightFlat.sqrMagnitude < 1e-4f)
            return false;

        state.SetAimDir(sightFlat.normalized, aimPoint, sightFlat.magnitude);
        return true;
    }

    static float ResolveCellSize()
    {
        MapDigColumnHost digHost = MapDigColumnHost.Runtime;
        if (digHost != null)
            return digHost.CellSize;

        MapPlantHost plantHost = MapPlantHost.Runtime;
        return plantHost != null ? plantHost.CellSize : 1f;
    }
}

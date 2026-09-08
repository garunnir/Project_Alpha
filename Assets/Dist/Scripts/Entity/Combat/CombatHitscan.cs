// ============================================================
// CombatHitscan — 원거리 cue 히트스캔 (origin overlap + 전구간 레이)
// ============================================================

using System;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 원거리 명중 기하 SSOT. 비행 DistProjectile과 분리.
/// EffectiveRange = max(무기 사거리, CameraGroundView.ReachFrom).
/// </summary>
public static class CombatHitscan
{
    public const int BufferSize = 16;
    public const float OriginOverlapRadius = 0.05f;
    const string UnderwaterGunFlag = "UNDERWATER_GUN";

    public static float EffectiveRange(
        ItemData item,
        CombatLeaf action,
        ItemData ammo,
        Vector3 shooterOrigin,
        Vector3? shooterFeetWorld = null)
    {
        float weapon = CombatMath.RangeMeters(item, action, ammo);
        Vector3 feet = shooterFeetWorld ?? shooterOrigin;
        if (HasItemFlag(item, UnderwaterGunFlag) && !MapFishService.IsShooterInWater(feet))
            return weapon * MapFishConsts.UnderwaterGunLandRangeMultiplier;

        float view = ResolveCameraViewReach(shooterOrigin);
        return Mathf.Max(weapon, view);
    }

    static bool HasItemFlag(ItemData item, string flag)
    {
        if (item?.flags == null || string.IsNullOrEmpty(flag))
            return false;

        for (int i = 0; i < item.flags.Count; i++)
        {
            string value = item.flags[i];
            if (!string.IsNullOrEmpty(value) &&
                value.Equals(flag, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    public static float ResolveCameraViewReach(Vector3 shooterOrigin)
    {
        Camera cam = Camera.main;
        CinemachineCamera cm = null;
        CameraZoomController zoom = CameraZoomController.Active;
        if (zoom != null)
            zoom.TryGetComponent(out cm);
        return CameraGroundView.ReachFrom(shooterOrigin, cam, cm);
    }

    /// <summary>
    /// 맵 topology 벽까지 사거리 클립. 조준 <see cref="PlayerAimController"/>와 같은 LineCast.
    /// 막힘 지점은 이후 벽 HP의 Obstructed 훅.
    /// </summary>
    public static bool TryClipMapRange(
        CharacterAttacker attacker,
        Vector3 direction,
        float maxDistance,
        out float hitDistance)
    {
        hitDistance = maxDistance;
        if (attacker == null || attacker.MapLineCast == null)
            return false;

        Vector3 feet = CharacterFeetPose.GetFeetWorld(attacker.transform);
        return attacker.MapLineCast.TryGetBlockingDistance(
            feet,
            direction,
            maxDistance,
            out hitDistance);
    }

    /// <summary>
    /// origin overlap → 전구간 RaycastNonAlloc 정렬 순회.
    /// 바디 히트는 hosts/impacts에 채우고, 월드·맵 막힘이면 obstructed=true.
    /// 아무 것도 없으면 missAtRangeEnd=true (impact = origin+dir*range).
    /// </summary>
    public static void Trace(
        CharacterAttacker attacker,
        Vector3 origin,
        Vector3 direction,
        float range,
        LayerMask mask,
        int pierce,
        RaycastHit[] hitBuffer,
        Collider[] overlapBuffer,
        CharacterBodyHost[] hosts,
        Vector3[] impacts,
        out int bodyHitCount,
        out bool obstructed,
        out Vector3 obstructImpact,
        out bool missAtRangeEnd,
        out Vector3 missImpact)
    {
        bodyHitCount = 0;
        obstructed = false;
        obstructImpact = origin;
        missAtRangeEnd = false;
        missImpact = origin;

        if (attacker == null ||
            direction.sqrMagnitude < 1e-8f ||
            range <= CharacterAttacker.MinRayDistance ||
            hitBuffer == null ||
            hosts == null ||
            impacts == null)
        {
            missAtRangeEnd = true;
            missImpact = origin;
            return;
        }

        Vector3 dir = direction.normalized;
        int pierceLeft = Mathf.Max(0, pierce);
        bool mapBlocked = TryClipMapRange(attacker, dir, range, out float mapDist);
        Vector3 mapImpact = origin + dir * mapDist;
        if (mapBlocked)
            range = mapDist;

        if (overlapBuffer != null &&
            TryResolveOriginOverlap(
                attacker, origin, mask, overlapBuffer,
                out CharacterBodyHost overlapBody,
                out bool overlapWorld,
                out Vector3 overlapPoint))
        {
            if (overlapWorld)
            {
                obstructed = true;
                obstructImpact = overlapPoint;
                return;
            }

            if (overlapBody != null)
            {
                RememberBody(overlapBody, origin, hosts, impacts, ref bodyHitCount);
                if (pierceLeft <= 0)
                    return;
                pierceLeft--;
            }
        }

        int hitCount = Physics.RaycastNonAlloc(
            origin,
            dir,
            hitBuffer,
            range,
            mask,
            QueryTriggerInteraction.Ignore);
        SortHitsByDistance(hitBuffer, hitCount);

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = hitBuffer[i];
            Collider col = hit.collider;
            if (col == null || attacker.IsOwnCollider(col))
                continue;

            if (TryResolveBody(col, out CharacterBodyHost host))
            {
                if (AlreadyListed(hosts, bodyHitCount, host))
                    continue;

                RememberBody(host, hit.point, hosts, impacts, ref bodyHitCount);
                if (pierceLeft <= 0)
                    return;
                pierceLeft--;
                continue;
            }

            obstructed = true;
            obstructImpact = hit.point;
            return;
        }

        if (mapBlocked)
        {
            obstructed = true;
            obstructImpact = mapImpact;
            return;
        }

        if (bodyHitCount == 0)
        {
            missAtRangeEnd = true;
            missImpact = origin + dir * range;
        }
    }

    static bool TryResolveOriginOverlap(
        CharacterAttacker attacker,
        Vector3 origin,
        LayerMask mask,
        Collider[] overlapBuffer,
        out CharacterBodyHost body,
        out bool worldBlock,
        out Vector3 point)
    {
        body = null;
        worldBlock = false;
        point = origin;

        int count = Physics.OverlapSphereNonAlloc(
            origin,
            OriginOverlapRadius,
            overlapBuffer,
            mask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < count; i++)
        {
            Collider col = overlapBuffer[i];
            if (col == null || attacker.IsOwnCollider(col))
                continue;

            if (TryResolveBody(col, out CharacterBodyHost host))
            {
                body = host;
                point = col.ClosestPoint(origin);
                return true;
            }

            worldBlock = true;
            point = col.ClosestPoint(origin);
            return true;
        }

        return false;
    }

    static bool TryResolveBody(Collider collider, out CharacterBodyHost host) =>
        CharacterBodyResolve.TryResolveBodyHost(collider, out host);

    static void RememberBody(
        CharacterBodyHost host,
        Vector3 impact,
        CharacterBodyHost[] hosts,
        Vector3[] impacts,
        ref int count)
    {
        if (count >= hosts.Length || count >= impacts.Length)
            return;
        hosts[count] = host;
        impacts[count] = impact;
        count++;
    }

    static bool AlreadyListed(CharacterBodyHost[] hosts, int count, CharacterBodyHost host)
    {
        for (int i = 0; i < count; i++)
        {
            if (ReferenceEquals(hosts[i], host))
                return true;
        }

        return false;
    }

    static void SortHitsByDistance(RaycastHit[] hits, int count)
    {
        for (int i = 1; i < count; i++)
        {
            RaycastHit key = hits[i];
            int j = i - 1;
            while (j >= 0 && hits[j].distance > key.distance)
            {
                hits[j + 1] = hits[j];
                j--;
            }

            hits[j + 1] = key;
        }
    }
}

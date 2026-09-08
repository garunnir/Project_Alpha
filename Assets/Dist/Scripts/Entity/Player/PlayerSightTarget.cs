// ============================================================
// PlayerSightTarget — 시선 월드점 해석 SSOT (IAimSightProvider / 농사·건설 레이)
// ============================================================

using IsoTilemap;
using UnityEngine;

public static class PlayerSightTarget
{
    public struct Settings
    {
        public float CastOriginYOffset;
        public float SphereRadius;
        public float MaxDistance;
        public bool FlattenAimYToPlayerHeight;
        public LayerMask ObstructionMask;
    }

    public static bool TryResolveWorldPoint(
        Transform body,
        Camera camera,
        MapTopologyLineCast topologyLineCast,
        in Settings settings,
        out Vector3 aimWorldPoint)
    {
        aimWorldPoint = default;
        if (body == null || camera == null)
            return false;

        Vector3 origin = body.position + Vector3.up * settings.CastOriginYOffset;

        if (!ScreenRaycaster.TryGetMouseWorldPosition(camera, origin.y, out Vector3 mousePlanePos))
            return false;

        Vector3 flatTarget = mousePlanePos;
        flatTarget.y = origin.y;

        Vector3 toTarget = flatTarget - origin;
        toTarget.y = 0f;
        float maxDist = Mathf.Min(toTarget.magnitude, settings.MaxDistance);
        if (maxDist < 1e-4f)
            return false;

        Vector3 dir = toTarget.normalized;

        if (topologyLineCast != null)
        {
            Vector3 feetWorld = CharacterFeetPose.GetFeetWorld(body);
            if (topologyLineCast.TryGetBlockingDistance(feetWorld, dir, maxDist, out float blockDist))
                maxDist = Mathf.Min(maxDist, blockDist);
        }

        bool hasHit = Physics.SphereCast(
            origin,
            settings.SphereRadius,
            dir,
            out RaycastHit hit,
            maxDist,
            settings.ObstructionMask,
            QueryTriggerInteraction.Ignore);

        aimWorldPoint = hasHit ? hit.point : origin + dir * maxDist;

        if (settings.FlattenAimYToPlayerHeight)
            aimWorldPoint.y = body.position.y + settings.CastOriginYOffset;

        return true;
    }

    /// <summary>
    /// 카메라 스크린 레이 → 월드점(Physics hit, 없으면 발 높이 수평면). 농사 셀 타겟팅용.
    /// </summary>
    public static bool TryResolveWorldPointFromCameraRay(
        Camera camera,
        out Vector3 worldPoint,
        float maxDistance = 200f,
        LayerMask obstructionMask = default)
    {
        worldPoint = default;
        if (camera == null)
            camera = Camera.main;
        if (camera == null)
            return false;

        InputManager input = InputManager.Instance;
        if (input == null || !input.TryReadPointerScreenPosition(out Vector2 screenPos))
            return false;

        if (obstructionMask.value == 0)
            obstructionMask = ~0;

        Ray ray = camera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                maxDistance,
                obstructionMask,
                QueryTriggerInteraction.Ignore))
        {
            worldPoint = hit.point;
            return true;
        }

        float planeY = ResolveFallbackPlaneY();
        return ScreenRaycaster.TryGetMouseWorldPosition(camera, planeY, out worldPoint);
    }

    /// <summary>
    /// 카메라 스크린 레이 → walkable 셀. GridCursor(건설·농사 타겟팅) SSOT.
    /// </summary>
    public static bool TryResolveOccupiedCellFromCameraRay(
        out Vector3Int cell,
        out Vector3 worldCenter,
        float cellSize,
        Camera camera = null)
    {
        cell = default;
        worldCenter = default;

        if (camera == null)
            camera = Camera.main;
        if (camera == null)
            return false;

        if (!TryResolveWorldPointFromCameraRay(camera, out Vector3 world))
            return false;

        MapPlantHost host = MapPlantHost.Runtime;
        cellSize = Mathf.Max(1e-4f, cellSize);
        cell = host != null
            ? host.ResolveCellFromWorld(world)
            : TileHelper.ConvertWorldToGrid(world, cellSize);
        worldCenter = TileHelper.ConvertGridToWorldPos(cell, host != null ? host.CellSize : cellSize);
        return true;
    }

    static float ResolveFallbackPlaneY()
    {
        PlayerPossessedInputHost input =
            Object.FindFirstObjectByType<PlayerPossessedInputHost>();
        if (input?.BodyTransform != null)
            return CharacterFeetPose.GetFeetWorld(input.BodyTransform).y;

        PlayerGearHost gear = PlayerGearHost.Active;
        if (gear != null)
            return CharacterFeetPose.GetFeetWorld(gear.transform).y;

        return 0f;
    }
}

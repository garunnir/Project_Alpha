// ============================================================
// MapSaveLayerCarryOver — 저장 시 비-타일 레이어(액체·혈흔·시계)를 잃지 않게 채우는 SSOT
// ============================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 씬 타일 스냅샷(<see cref="IMapMapper.FromPrepared"/>)은 타일·벽·바닥만 복원한다.
    /// 액체·혈흔·시계는 씬에 표현이 없어 새 DTO에서 항상 빈 값이므로, 여기서 다시 채워야 한다.
    /// </summary>
    /// <remarks>
    /// 레이어마다 진실원이 둘이다 — 런타임 호스트(Play 중 살아 있는 상태)와 디스크(이전 저장).
    /// 호스트가 있으면 호스트가 이기고, 없으면(편집 모드는 <c>Awake</c>가 안 돌아 항상 null) 디스크를 계승한다.
    /// 이 구분을 안 하면 편집 모드 저장 한 번이 물·혈흔·시각을 전부 지운다.
    /// </remarks>
    public static class MapSaveLayerCarryOver
    {
        /// <summary>DistScript <see cref="PlayerProgressSaveBridge"/>가 등록. possessed 없을 때 디스크 계승.</summary>
        public static Action<MapSaveJsonDto, MapSaveJsonDto> MergePlayerProgress;
        /// <summary>
        /// 덮어쓸 대상 파일을 미리 읽는다. 파일이 없거나 비어 있으면 계승할 것이 없으므로
        /// <paramref name="existing"/> null + true(최초 저장은 정상).
        /// </summary>
        /// <returns>
        /// false면 **저장을 중단해야 한다** — 파일은 있는데 읽거나 파싱하지 못한 경우다.
        /// 계승할 값을 모르는 채로 쓰면 그대로 유실이라, 덮어쓰기보다 실패가 안전하다.
        /// </returns>
        public static bool TryReadExisting(string fullPath, out MapSaveJsonDto existing)
        {
            existing = null;
            if (string.IsNullOrEmpty(fullPath) || !File.Exists(fullPath))
                return true;

            try
            {
                string json = File.ReadAllText(fullPath);
                if (string.IsNullOrWhiteSpace(json))
                    return true;

                existing = JsonUtility.FromJson<MapSaveJsonDto>(json);
            }
            catch (System.Exception e)
            {
                Debug.LogError(
                    $"[MapSaveLayerCarryOver] 기존 맵을 읽지 못해 저장을 중단합니다 — 액체·혈흔·시계가 유실될 수 있습니다: {fullPath}\n{e}");
                return false;
            }

            if (existing != null)
                return true;

            Debug.LogError(
                $"[MapSaveLayerCarryOver] 기존 맵 JSON 파싱 결과가 null이라 저장을 중단합니다: {fullPath}");
            return false;
        }

        /// <summary>
        /// <paramref name="target"/>의 비-타일 레이어를 호스트(우선) 또는 <paramref name="existing"/>(폴백)으로 채운다.
        /// </summary>
        public static void Apply(
            MapSaveJsonDto target,
            MapSaveJsonDto existing,
            MapLiquidHost liquidHost,
            MapBloodHost bloodHost,
            MapPlantHost plantHost,
            MapDigColumnHost digHost = null)
        {
            if (target == null)
                return;

            // 물 저작 면은 씬 마커에서만 나온다. null = 호출부가 씬을 읽지 않았다는 뜻이므로 디스크를 계승한다
            // (Play 저장 경로). 빈 리스트는 "씬에 물이 없다"는 확정이라 그대로 둔다.
            CarryLiquidAuthoring(target, existing);

            // Unity fake-null 때문에 ?. 대신 != null — 파괴된 컴포넌트에 WriteToDto를 걸지 않는다.
            // 액체 우선순위: Play 호스트 → 에디터 물 저작 면 bake → 디스크 계승.
            if (liquidHost != null)
                liquidHost.WriteToDto(target);
            else if (MapLiquidAuthoringBake.TryBakeFromAuthoringFaces(target, out int baked) && baked > 0)
            {
                Debug.Log(
                    $"[MapSaveLayerCarryOver] 물 저작 면 → liquidCells 베이크 ({baked} cells).");
            }
            else
            {
                CarryLiquid(target, existing);
                if (existing?.hasLiquidSnapshot == true)
                {
                    Debug.Log(
                        $"[MapSaveLayerCarryOver] 물 저작 면 없음 — 기존 liquidCells 계승 " +
                        $"({target.liquidCells?.Count ?? 0} cells). 물 프리팹을 새로 깔았다면 " +
                        "LiquidAuthoringView.prefabId / TilePrefabDB 등록을 확인하세요.");
                }
            }

            if (bloodHost != null)
                bloodHost.WriteToDto(target);
            else
                CarryBlood(target, existing);

            // plantCells는 OccupiedCell 타일로 이주 완료 — 타일 스냅샷이 복원하므로 계승 대상이 아니다.
            if (plantHost != null)
                plantHost.WriteToDto(target);

            MapDigColumnHost digRuntime = digHost != null ? digHost : MapDigColumnHost.Runtime;
            if (digRuntime != null)
                digRuntime.WriteToDto(target);
            else
                CarryStratumSeed(target, existing);

            // 훅이 없으면 WriteToDto가 hasClockSnapshot을 false로 눕힌다. 그때만 디스크 시각을 되살린다.
            MapClockSnapshot.WriteToDto(target);
            if (!target.hasClockSnapshot)
                CarryClock(target, existing);

            if (MergePlayerProgress != null)
                MergePlayerProgress.Invoke(target, existing);
            else
                CarryPlayerProgress(target, existing);
        }

        static void CarryLiquidAuthoring(MapSaveJsonDto target, MapSaveJsonDto existing)
        {
            if (target.liquidAuthoringFaces != null)
                return;

            if (existing == null)
            {
                target.liquidAuthoringFaces = new List<FloorFaceSaveData>();
                return;
            }

            // 여기서 읽은 existing은 TileMapSerializer를 거치지 않아 구 JSON 물이 floorFaces에 남아 있다.
            MapLiquidAuthoringBake.PromoteLegacyFloorFaces(existing);
            target.liquidAuthoringFaces = existing.liquidAuthoringFaces ?? new List<FloorFaceSaveData>();
        }

        static void CarryLiquid(MapSaveJsonDto target, MapSaveJsonDto existing)
        {
            if (existing == null)
                return;

            target.liquidCells = existing.liquidCells ?? new List<MapLiquidCellSaveData>();
            target.hasLiquidSnapshot = existing.hasLiquidSnapshot;
            target.hasLiquidTemperature = existing.hasLiquidTemperature;
        }

        static void CarryBlood(MapSaveJsonDto target, MapSaveJsonDto existing)
        {
            if (existing == null)
                return;

            target.bloodStamps = existing.bloodStamps ?? new List<BloodStampSaveData>();
        }

        static void CarryClock(MapSaveJsonDto target, MapSaveJsonDto existing)
        {
            if (existing == null || !existing.hasClockSnapshot)
                return;

            target.hasClockSnapshot = true;
            target.dayIndex = existing.dayIndex;
            target.minuteOfDay = existing.minuteOfDay;
        }

        static void CarryPlayerProgress(MapSaveJsonDto target, MapSaveJsonDto existing)
        {
            if (existing == null || !existing.hasPlayerProgressSnapshot)
            {
                target.hasPlayerProgressSnapshot = false;
                target.playerProgressJson = null;
                return;
            }

            target.hasPlayerProgressSnapshot = true;
            target.playerProgressJson = existing.playerProgressJson;
        }

        static void CarryStratumSeed(MapSaveJsonDto target, MapSaveJsonDto existing)
        {
            if (existing == null)
                return;

            target.stratumSeed = existing.stratumSeed;
        }
    }
}

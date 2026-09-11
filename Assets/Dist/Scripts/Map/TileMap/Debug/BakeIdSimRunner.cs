// ============================================================
// [BakeIdSimRunner] — 직렬화 타일·step·rule로 bake ID 시뮬레이션 실행
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// Host / EditMode Sim 테스트 공통. NUnit Assert 없이 실패 메시지 목록을 반환한다.
    /// </summary>
    public static class BakeIdSimRunner
    {
        public readonly struct FloorIds
        {
            public readonly int BuildingId;
            public readonly int RoomId;
            public readonly int SpaceId;
            public readonly bool IsOutdoor;

            public FloorIds(int buildingId, int roomId, int spaceId, bool isOutdoor)
            {
                BuildingId = buildingId;
                RoomId = roomId;
                SpaceId = spaceId;
                IsOutdoor = isOutdoor;
            }

            public override string ToString() =>
                $"b={BuildingId} r={RoomId} s={SpaceId} outdoor={IsOutdoor}";
        }

        public sealed class Result
        {
            public readonly List<string> Failures = new();
            public TileMapCacheHub Hub;
            public bool Ok => Failures.Count == 0;
        }

        public static Result Run(
            IReadOnlyList<BakeIdSimTileEntry> tiles,
            IReadOnlyList<BakeIdSimProbe> probes,
            IReadOnlyList<BakeIdSimRule> rules,
            IReadOnlyList<BakeIdSimStep> steps = null)
        {
            var result = new Result();
            var model = new TileMapModel();
            var registry = new BuildingGroupRegistry();
            var hub = TileMapCacheHub.Create(model, registry);
            model.SetMapCacheHub(hub);
            var builder = new BuildingGroupBuilder(model, hub);
            model.SetBuildingGroupBuilder(builder);
            result.Hub = hub;

            List<TileData> initial = BakeIdSimTileUtil.ToTileDataList(tiles, out _, "[BakeIdSimRunner]");
            for (int i = 0; i < initial.Count; i++)
                model.SetTile(initial[i]);

            registry.ReplaceOutdoorFloorCells(BakeIdPlaygroundLayout.MasterPlayground.OutdoorFloorCells);
            // 시드 타일은 buildingId=0 — 연결 remesh. AssignAll은 양수 하드 파티션 보존용.
            builder.RebakeAllBuildingPartitions();

            if (steps != null)
            {
                for (int i = 0; i < steps.Count; i++)
                {
                    if (!TryApplyStep(model, probes, steps[i], out string stepError))
                    {
                        result.Failures.Add(stepError);
                        return result;
                    }
                }
            }

            if (rules == null || rules.Count == 0)
                return result;

            var probeMap = BuildProbeMap(probes, result.Failures);
            if (result.Failures.Count > 0)
                return result;

            for (int i = 0; i < rules.Count; i++)
                EvaluateRule(hub, probeMap, rules[i], result.Failures);

            return result;
        }

        /// <summary>Host Preview — 초기 rebake된 model에 steps만 순서 적용.</summary>
        public static bool TryApplySteps(
            TileMapModel model,
            IReadOnlyList<BakeIdSimProbe> probes,
            IReadOnlyList<BakeIdSimStep> steps,
            out string firstError)
        {
            firstError = null;
            if (steps == null || steps.Count == 0)
                return true;

            for (int i = 0; i < steps.Count; i++)
            {
                if (!TryApplyStep(model, probes, steps[i], out string stepError))
                {
                    firstError = stepError;
                    return false;
                }
            }

            return true;
        }

        static Dictionary<string, Vector3Int> BuildProbeMap(
            IReadOnlyList<BakeIdSimProbe> probes,
            List<string> failures)
        {
            var map = new Dictionary<string, Vector3Int>();
            if (probes == null)
                return map;

            for (int i = 0; i < probes.Count; i++)
            {
                BakeIdSimProbe p = probes[i];
                if (string.IsNullOrEmpty(p.name))
                {
                    failures.Add($"Probe[{i}] has empty name");
                    continue;
                }

                if (map.ContainsKey(p.name))
                {
                    failures.Add($"Duplicate probe name '{p.name}'");
                    continue;
                }

                map[p.name] = p.cell;
            }

            return map;
        }

        static bool TryApplyStep(
            TileMapModel model,
            IReadOnlyList<BakeIdSimProbe> probes,
            BakeIdSimStep step,
            out string error)
        {
            error = null;
            if (step.kind == BakeIdSimStepKind.None)
                return true;

            if (!TryResolveProbe(probes, step.probeA, out Vector3Int a))
            {
                error = $"Step {step.kind}: probeA '{step.probeA}' not found";
                return false;
            }

            switch (step.kind)
            {
                case BakeIdSimStepKind.AddFloorAtProbe:
                    model.SetTile(BakeIdSyntheticTiles.Floor(a));
                    return true;

                case BakeIdSimStepKind.RemoveFloorAtProbe:
                {
                    if (!TryFindFloorAt(model, a, out TileData floor))
                    {
                        error = $"Step RemoveFloorAtProbe: no floor at {a}";
                        return false;
                    }

                    model.RemoveTile(floor);
                    return true;
                }

                case BakeIdSimStepKind.RemoveThinWallBetweenProbes:
                {
                    if (!TryResolveProbe(probes, step.probeB, out Vector3Int b))
                    {
                        error = $"Step RemoveThinWallBetweenProbes: probeB '{step.probeB}' not found";
                        return false;
                    }

                    if (!TryFindThinWallBetween(model, a, b, out TileData wall))
                    {
                        error = $"Step RemoveThinWallBetweenProbes: no wall between {a} and {b}";
                        return false;
                    }

                    model.RemoveTile(wall);
                    return true;
                }

                case BakeIdSimStepKind.AddCubeAtProbe:
                    model.SetTile(BakeIdSyntheticTiles.Cube(a));
                    return true;

                case BakeIdSimStepKind.RemoveCubeAtProbe:
                {
                    if (!TryFindCubeAt(model, a, out TileData cube))
                    {
                        error = $"Step RemoveCubeAtProbe: no cube at {a}";
                        return false;
                    }

                    model.RemoveTile(cube);
                    return true;
                }

                default:
                    error = $"Unknown step kind {step.kind}";
                    return false;
            }
        }

        static bool TryResolveProbe(
            IReadOnlyList<BakeIdSimProbe> probes,
            string name,
            out Vector3Int cell)
        {
            cell = default;
            if (probes == null || string.IsNullOrEmpty(name))
                return false;

            for (int i = 0; i < probes.Count; i++)
            {
                if (probes[i].name != name)
                    continue;
                cell = probes[i].cell;
                return true;
            }

            return false;
        }

        static bool TryFindFloorAt(TileMapModel model, Vector3Int cell, out TileData tile)
        {
            tile = default;
            foreach (TileData candidate in model.TilesSnapshot)
            {
                if (!TileIdentityUtil.IsHorizontalFace(candidate.identity))
                    continue;
                if (candidate.identity.GridPos != cell)
                    continue;
                tile = candidate;
                return true;
            }

            return false;
        }

        static bool TryFindCubeAt(TileMapModel model, Vector3Int cell, out TileData tile)
        {
            tile = default;
            foreach (TileData candidate in model.TilesSnapshot)
            {
                if (!TileIdentityUtil.IsOccupiedCell(candidate.identity))
                    continue;
                if (candidate.identity.GridPos != cell)
                    continue;
                tile = candidate;
                return true;
            }

            return false;
        }

        static bool TryFindThinWallBetween(
            TileMapModel model,
            Vector3Int a,
            Vector3Int b,
            out TileData tile)
        {
            tile = default;
            foreach (TileData candidate in model.TilesSnapshot)
            {
                if (!BakeIdSyntheticTiles.IsThinWallBetween(candidate, a, b))
                    continue;
                tile = candidate;
                return true;
            }

            return false;
        }

        static void EvaluateRule(
            TileMapCacheHub hub,
            Dictionary<string, Vector3Int> probes,
            BakeIdSimRule rule,
            List<string> failures)
        {
            switch (rule.kind)
            {
                case BakeIdSimRuleKind.SameIds:
                {
                    if (!RequirePair(probes, rule, failures, out Vector3Int a, out Vector3Int b))
                        return;
                    if (!TryRead(hub, a, out FloorIds ia) || !TryRead(hub, b, out FloorIds ib))
                    {
                        failures.Add($"SameIds: missing floor at {a} or {b}");
                        return;
                    }

                    if (ia.BuildingId != ib.BuildingId ||
                        ia.RoomId != ib.RoomId ||
                        ia.SpaceId != ib.SpaceId ||
                        ia.IsOutdoor != ib.IsOutdoor)
                    {
                        failures.Add(
                            $"SameIds '{rule.probeA}'@{a} ({ia}) vs '{rule.probeB}'@{b} ({ib})");
                    }

                    return;
                }

                case BakeIdSimRuleKind.Differ:
                {
                    if (!RequirePair(probes, rule, failures, out Vector3Int a, out Vector3Int b))
                        return;
                    if (!TryRead(hub, a, out FloorIds ia) || !TryRead(hub, b, out FloorIds ib))
                    {
                        failures.Add($"Differ: missing floor at {a} or {b}");
                        return;
                    }

                    if (FieldEquals(ia, ib, rule.differField))
                    {
                        failures.Add(
                            $"Differ({rule.differField}) '{rule.probeA}'@{a} ({ia}) vs '{rule.probeB}'@{b} ({ib}) — expected different");
                    }

                    return;
                }

                case BakeIdSimRuleKind.SameBuilding:
                {
                    if (!RequirePair(probes, rule, failures, out Vector3Int a, out Vector3Int b))
                        return;
                    if (!TryRead(hub, a, out FloorIds ia) || !TryRead(hub, b, out FloorIds ib))
                    {
                        failures.Add($"SameBuilding: missing floor at {a} or {b}");
                        return;
                    }

                    if (ia.BuildingId != ib.BuildingId)
                    {
                        failures.Add(
                            $"SameBuilding '{rule.probeA}'@{a} ({ia}) vs '{rule.probeB}'@{b} ({ib})");
                    }

                    return;
                }

                case BakeIdSimRuleKind.IsOutdoor:
                {
                    if (!probes.TryGetValue(rule.probeA, out Vector3Int cell))
                    {
                        failures.Add($"IsOutdoor: probe '{rule.probeA}' not found");
                        return;
                    }

                    // Floor 있으면 bake Space/plaza; 없으면 IsOutdoorEvaluation 선택 A (empty→true).
                    bool gotOutdoor;
                    string detail;
                    if (TryRead(hub, cell, out FloorIds ids))
                    {
                        gotOutdoor = ids.IsOutdoor;
                        detail = ids.ToString();
                    }
                    else
                    {
                        gotOutdoor = hub.IsOutdoorEvaluation(cell.y, cell.x, cell.z);
                        detail = "empty/no-floor IsOutdoorEvaluation";
                    }

                    if (gotOutdoor != rule.outdoorExpected)
                    {
                        failures.Add(
                            $"IsOutdoor '{rule.probeA}'@{cell} got={gotOutdoor} expected={rule.outdoorExpected} ({detail})");
                    }

                    return;
                }

                default:
                    failures.Add($"Unknown rule kind {rule.kind}");
                    break;
            }
        }

        static bool RequirePair(
            Dictionary<string, Vector3Int> probes,
            BakeIdSimRule rule,
            List<string> failures,
            out Vector3Int a,
            out Vector3Int b)
        {
            a = default;
            b = default;
            if (!probes.TryGetValue(rule.probeA, out a))
            {
                failures.Add($"Rule {rule.kind}: probeA '{rule.probeA}' not found");
                return false;
            }

            if (!probes.TryGetValue(rule.probeB, out b))
            {
                failures.Add($"Rule {rule.kind}: probeB '{rule.probeB}' not found");
                return false;
            }

            return true;
        }

        public static bool TryRead(TileMapCacheHub hub, Vector3Int walkableCell, out FloorIds ids)
        {
            ids = default;
            if (hub == null)
                return false;

            if (!hub.TryGetFloorFaceForWalkableCell(
                    walkableCell.x, walkableCell.y, walkableCell.z, out TileData face))
            {
                return false;
            }

            int buildingId = face.identity.buildingId;
            int roomId = face.identity.roomId;
            int spaceId = 0;
            bool isOutdoor = false;

            if (hub.Spaces.TryGetSpaceAtFloorCell(walkableCell, out int sid))
            {
                spaceId = sid;
                isOutdoor = hub.Spaces.IsOutdoorSpace(sid);
            }
            else if (buildingId == TileIdentity.BuildingIdOutdoor)
            {
                isOutdoor = true;
            }

            ids = new FloorIds(buildingId, roomId, spaceId, isOutdoor);
            return true;
        }

        static bool FieldEquals(in FloorIds a, in FloorIds b, BakeIdSimIdField field) =>
            field switch
            {
                BakeIdSimIdField.BuildingId => a.BuildingId == b.BuildingId,
                BakeIdSimIdField.RoomId => a.RoomId == b.RoomId,
                BakeIdSimIdField.SpaceId => a.SpaceId == b.SpaceId,
                BakeIdSimIdField.IsOutdoor => a.IsOutdoor == b.IsOutdoor,
                _ => a.BuildingId == b.BuildingId &&
                     a.RoomId == b.RoomId &&
                     a.SpaceId == b.SpaceId &&
                     a.IsOutdoor == b.IsOutdoor,
            };
    }
}

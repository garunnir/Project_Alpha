// ============================================================
// WallOcclusionFinder — BFS flood-fill로 플레이어 기준 가려야 할 Wall 타일(면 EdgeWall 포함)을 탐색
// ============================================================
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace IsoTilemap
{
    public struct OcclusionMaskOptions
    {
        public bool Enabled { get; }
        public int MinStartDepthTiles { get; }
        public int StartLeftTiles { get; }
        public int StartRightTiles { get; }
        public int SideExpandStepTiles { get; }
        public int LeftTiles { get; }
        public int RightTiles { get; }
        public int DownTiles { get; }
        public int ReleaseMarginTiles { get; }
        public Vector3Int DownAxis { get; }
        public Vector3Int RightAxis { get; }

        public OcclusionMaskOptions(
            bool enabled,
            int minStartDepthTiles,
            int startLeftTiles,
            int startRightTiles,
            int sideExpandStepTiles,
            int leftTiles,
            int rightTiles,
            int downTiles,
            int releaseMarginTiles,
            Vector3Int downAxis,
            Vector3Int rightAxis)
        {
            Enabled = enabled;
            MinStartDepthTiles = Mathf.Max(0, minStartDepthTiles);
            StartLeftTiles = Mathf.Max(0, startLeftTiles);
            StartRightTiles = Mathf.Max(0, startRightTiles);
            SideExpandStepTiles = Mathf.Max(1, sideExpandStepTiles);
            LeftTiles = Mathf.Max(0, leftTiles);
            RightTiles = Mathf.Max(0, rightTiles);
            DownTiles = Mathf.Max(0, downTiles);
            ReleaseMarginTiles = Mathf.Max(0, releaseMarginTiles);
            DownAxis = downAxis;
            RightAxis = rightAxis;
        }

        public static OcclusionMaskOptions Default => new OcclusionMaskOptions(
            enabled: true,
            minStartDepthTiles: 0, 
            startLeftTiles: 0,
            startRightTiles: 0,
            sideExpandStepTiles: 2,
            leftTiles: 1,
            rightTiles: 1,
            downTiles: 2, 
            releaseMarginTiles: 0,
            downAxis: new Vector3Int(1, 0, -1),  // +x, -z
            rightAxis: new Vector3Int(1, 0, 1)); // +x, +z
    } 

    public sealed class OcclusionSelection
    {
        public List<TileData> BaseOccluding { get; }
        public List<TileData> ExtraOccludingByPlayer { get; }
        // Backward-compatible alias. Semantics changed to additive player layer.
        public List<TileData> MaskedOutByPlayer => ExtraOccludingByPlayer;
        public List<TileData> FinalOccluding { get; }
        public List<TileData> Occluding => FinalOccluding;

        public OcclusionSelection(
            List<TileData> baseOccluding,
            List<TileData> extraOccludingByPlayer,
            List<TileData> finalOccluding)
        {
            BaseOccluding = baseOccluding ?? new List<TileData>();
            ExtraOccludingByPlayer = extraOccludingByPlayer ?? new List<TileData>();
            FinalOccluding = finalOccluding ?? new List<TileData>();
        }
    }

    internal sealed class MaskApplicationResult
    {
        public List<TileData> ExtraOccludingByPlayer { get; }

        public MaskApplicationResult(List<TileData> extraOccludingByPlayer)
        {
            ExtraOccludingByPlayer = extraOccludingByPlayer ?? new List<TileData>();
        }
    }

    public class WallOcclusionFinder
    {
        private static readonly Vector3Int[] CardinalNeighbors =
        {
            Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward
        };

        private static readonly Vector3Int[] BottomOcclusionDirections =
        {
            Vector3Int.right, Vector3Int.back
        };

        private static readonly Dictionary<WallEdgeKey, TileData> EmptyEdges = new Dictionary<WallEdgeKey, TileData>();

        private readonly Dictionary<Vector3Int, List<TileData>> _tiles;
        private readonly IReadOnlyDictionary<WallEdgeKey, TileData> _edges;
        public OcclusionMaskOptions MaskOptions { get; set; } = OcclusionMaskOptions.Default;

        /// <param name="edges"><see cref="TileEdgeBinder"/> 등 면 벽 레지스트리 인덱스(셀 리스트와 분리).</param>
        public WallOcclusionFinder(Dictionary<Vector3Int, List<TileData>> tiles, IReadOnlyDictionary<WallEdgeKey, TileData> edges)
        {
            _tiles = tiles;
            _edges = edges ?? EmptyEdges;
        }

        public List<TileData> Find(Vector3Int playerCellPos) =>
            FindOcclusion(playerCellPos).Occluding;

        /// <summary>
        /// Flood-fill 진행 중 +X/-Z 방향에서 막힌 셀 벽과 엣지 벽을 숨김 후보로 반환합니다.
        /// </summary>
        public OcclusionSelection FindOcclusion(Vector3Int playerCellPos)
        {
            Vector3Int start = playerCellPos;
            var belowCellSet = new HashSet<TileData>();
            var topCellSet = new HashSet<TileData>();
            var belowEdgeSet = new HashSet<TileData>();
            // Top edges are classified for diagnostics and symmetry, but are not hidden.
            var topEdgeSet = new HashSet<TileData>();

            if (_tiles.TryGetValue(start, out var startList))
            {
                bool hasBlocking = startList.Any(t => IsSolidCellWall((TileView.TileType)t.identity.tileType));

                if (hasBlocking)
                {
                    bool found = false;
                    foreach (var d in CardinalNeighbors)
                    {
                        var n = new Vector3Int(start.x + d.x, start.y, start.z + d.z);
                        if (_tiles.TryGetValue(n, out var nList) &&
                            !nList.Any(x => IsSolidCellWall((TileView.TileType)x.identity.tileType)))
                        {
                            start = n;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        var adjTiles = CollectAdjacentWalls(start);
                        var adjEdges = CollectAdjacentWallEdges(start);
                        var adjMerged = new List<TileData>(adjTiles.Count + adjEdges.Count);
                        adjMerged.AddRange(adjTiles);
                        adjMerged.AddRange(adjEdges);
                        return new OcclusionSelection(adjMerged, new List<TileData>(), adjMerged);
                    }
                }
            }

            if (_tiles.TryGetValue(start, out var stlist))
            {
                if (stlist == null || stlist.Count == 0)
                {
                    if (Config.DebugMode.FloorAlgorithm) Debug.LogWarning("내 위치에 아무것도 없음." + start);
                    return new OcclusionSelection(new List<TileData>(), new List<TileData>(), new List<TileData>());
                }
            }

            var visited = new HashSet<Vector3Int>();
            var q = new Queue<Vector3Int>();
            var floorChecked = new HashSet<Vector3Int>();
            var wallCellChecked = new HashSet<Vector3Int>();
            visited.Add(start);
            q.Enqueue(start);
            floorChecked.Add(start);

            int safetyLimit = 200000;
            int steps = 0;
            while (q.Count > 0)
            {
                if (++steps > safetyLimit) break;
                var cur = q.Dequeue();

                foreach (var d in CardinalNeighbors)
                {
                    var nx = new Vector3Int(cur.x + d.x, playerCellPos.y, cur.z + d.z);
                    bool isBottomDir = IsBottomOcclusionDirection(d);

                    if (WallEdgeKey.TryBetween(cur, nx, out var edgeKey) && _edges.TryGetValue(edgeKey, out TileData edgeWall))
                    {
                        AddByDirection(edgeWall, isBottomDir, belowEdgeSet, topEdgeSet);
                        // Edge walls block this step only. The cell beyond may still be reachable from another route.
                        continue;
                    }

                    if (visited.Contains(nx)) continue;

                    bool isFloor = false;
                    if (_tiles.TryGetValue(nx, out var list))
                    {
                        bool hasSolidWall = false;
                        foreach (var t in list)
                        {
                            if (IsSolidCellWall((TileView.TileType)t.identity.tileType))
                            {
                                AddByDirection(t, isBottomDir, belowCellSet, topCellSet);
                                hasSolidWall = true;
                                continue;
                            }
                            else if ((TileView.TileType)t.identity.tileType == TileView.TileType.Floor)
                            {
                                isFloor = true;
                            }
                        }
                        if (hasSolidWall)
                        {
                            wallCellChecked.Add(nx);
                            isFloor = false;
                        }
                        if (isFloor)
                        {
                            visited.Add(nx);
                            q.Enqueue(nx);
                            floorChecked.Add(nx);
                        }
                    }
                    else
                    {
                        if (Config.DebugMode.FloorAlgorithm)
                            Debug.Log("빈셀발견 확장" + nx);
                    }
                }
            }

            if (Config.DebugMode.FloorAlgorithm)
                Debug.Log("Hideable WallDetect " + (belowCellSet.Count + topCellSet.Count) + " tiles, " + (belowEdgeSet.Count + topEdgeSet.Count) + " edges, " + visited.Count + " visited");

            var cornerSeedSet = new HashSet<TileData>(belowCellSet);
            cornerSeedSet.UnionWith(topCellSet);
            var cornerSeedWalls = cornerSeedSet.ToList();
            var cornerExtras = CollectNewWallsAdjacentToMultipleTopWalls(cornerSeedWalls, wallCellChecked);
            var tileResult = new List<TileData>(belowCellSet.Count + cornerExtras.Count);
            tileResult.AddRange(belowCellSet);
            tileResult.AddRange(cornerExtras);

            var belowEdges = belowEdgeSet.ToList();
            var merged = new List<TileData>(tileResult.Count + belowEdges.Count);
            merged.AddRange(tileResult);
            merged.AddRange(belowEdges);
            var playerCandidates = CollectPenetratingPlayerCandidates();
            var maskResult = CollectAdditionalPlayerOccluding(playerCandidates, playerCellPos, MaskOptions);
            var extraOccludingByPlayer = maskResult.ExtraOccludingByPlayer;
            var finalOccluding = UnionByTileId(merged, extraOccludingByPlayer);

#if UNITY_EDITOR
            if (Config.DebugMode.FloorAlgorithm)
            {
                var wallCheckedSnapshot = wallCellChecked;
                var startSnapshot = start;
                var belowEdgesSnapshot = belowEdges;
                var cornerExtrasSnapshot = cornerExtras;
                Action action = () => 
                {
                    var finalOcclusionCells = finalOccluding
                        .Select(t => t.identity.GridPos)
                        .ToHashSet();
                    var extraByPlayerCells = extraOccludingByPlayer
                        .Select(t => t.identity.GridPos)
                        .ToHashSet();
                    DebugGizmos(floorChecked, 0, Color.green);
                    DebugGizmos(wallCheckedSnapshot, 0.05f, Color.red);
                    DebugGizmos(new HashSet<Vector3Int> { startSnapshot }, 0.01f, Color.cyan);
                    DebugGizmos(finalOcclusionCells, 0.02f, Color.yellow);
                    DebugEdgeGizmos(belowEdgesSnapshot, 0.02f, Color.yellow);
                    DebugGizmos(cornerExtrasSnapshot.Select(t => t.identity.GridPos).ToHashSet(), 0.02f, Color.blue);
                    DebugGizmos(extraByPlayerCells, 0.03f, Color.magenta);
                };
                StateRunner.Instance.ChangeState(new DebugTileRunner(action));
            }
#endif

            return new OcclusionSelection(merged, extraOccludingByPlayer, finalOccluding);
        }

        private static MaskApplicationResult CollectAdditionalPlayerOccluding(
            List<TileData> source,
            Vector3Int playerCellPos,
            OcclusionMaskOptions options)
        {
            if (!options.Enabled || source.Count == 0)
                return new MaskApplicationResult(new List<TileData>());

            int downScale = AxisScale(options.DownAxis);
            int rightScale = AxisScale(options.RightAxis);
            if (downScale <= 0 || rightScale <= 0)
                return new MaskApplicationResult(new List<TileData>());

            var extraOccluding = new List<TileData>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                var tile = source[i];
                if (!IsTileInBottomVisibilityBand(tile, playerCellPos, options, downScale))
                    continue;
                if (IsTileInsideMask(tile, playerCellPos, options, downScale, rightScale))
                    extraOccluding.Add(tile);
            }

            return new MaskApplicationResult(extraOccluding);
        }

        private static bool IsTileInBottomVisibilityBand(
            TileData tile,
            Vector3Int playerCellPos,
            OcclusionMaskOptions options,
            int downScale)
        {
            int minDown = options.MinStartDepthTiles * downScale;
            int maxDown = options.DownTiles * downScale;

            if ((TileView.TileType)tile.identity.tileType == TileView.TileType.EdgeWall)
            {
                WallEdgeKey key = WallEdgeKey.FromEdgeTileIdentity(tile.identity);
                int downA = DotXZ(new Vector3Int(key.CellA.x - playerCellPos.x, 0, key.CellA.z - playerCellPos.z), options.DownAxis);
                int downB = DotXZ(new Vector3Int(key.CellB.x - playerCellPos.x, 0, key.CellB.z - playerCellPos.z), options.DownAxis);
                // EdgeWall은 상단 간섭 방지를 위해 양쪽 셀이 모두 하단 밴드 안에 있어야 후보로 인정.
                return downA >= minDown && downA <= maxDown && downB >= minDown && downB <= maxDown;
            }

            int down = DotXZ(
                new Vector3Int(tile.identity.GridPos.x - playerCellPos.x, 0, tile.identity.GridPos.z - playerCellPos.z),
                options.DownAxis);
            return down >= minDown && down <= maxDown;
        }

        private static List<TileData> UnionByTileId(List<TileData> baseOccluding, List<TileData> extraOccluding)
        {
            if (extraOccluding == null || extraOccluding.Count == 0)
                return baseOccluding;

            var union = new List<TileData>(baseOccluding.Count + extraOccluding.Count);
            var seenIds = new HashSet<Guid>();
            for (int i = 0; i < baseOccluding.Count; i++)
            {
                union.Add(baseOccluding[i]);
                seenIds.Add(baseOccluding[i].tileDefId);
            }

            for (int i = 0; i < extraOccluding.Count; i++)
            {
                TileData tile = extraOccluding[i];
                if (seenIds.Add(tile.tileDefId))
                    union.Add(tile);
            }

            return union;
        }

        /// <summary>
        /// 플레이어 가시성 레이어용 후보를 BFS와 무관하게 전역 벽 인덱스에서 수집합니다.
        /// </summary>
        private List<TileData> CollectPenetratingPlayerCandidates()
        {
            var result = new List<TileData>();

            foreach (var cellTiles in _tiles.Values)
            {
                for (int i = 0; i < cellTiles.Count; i++)
                {
                    TileData tile = cellTiles[i];
                    if (IsSolidCellWall((TileView.TileType)tile.identity.tileType))
                        result.Add(tile);
                }
            }

            foreach (var edgeTile in _edges.Values)
            {
                result.Add(edgeTile);
            }

            return result;
        }

        private static bool IsTileInsideMask(
            TileData tile,
            Vector3Int playerCellPos,
            OcclusionMaskOptions options,
            int downScale,
            int rightScale)
        {
            if ((TileView.TileType)tile.identity.tileType == TileView.TileType.EdgeWall)
            {
                WallEdgeKey key = WallEdgeKey.FromEdgeTileIdentity(tile.identity);
                if (!IsEdgeFaceInBottomHemisphere(key, playerCellPos, options.DownAxis))
                    return false;
                // 상단 누수 방지를 위해 EdgeWall은 양 끝 셀이 모두 마스크 안일 때만 포함.
                return IsCellInsideMask(key.CellA, playerCellPos, options, downScale, rightScale) &&
                       IsCellInsideMask(key.CellB, playerCellPos, options, downScale, rightScale);
            }

            return IsCellInsideMask(tile.identity.GridPos, playerCellPos, options, downScale, rightScale);
        }

        private static bool IsEdgeFaceInBottomHemisphere(
            WallEdgeKey key,
            Vector3Int playerCellPos,
            Vector3Int downAxis)
        {
            // 2배 스케일 중심 좌표(정수)로 비교해서 부동소수 오차를 피한다.
            int centerX2 = key.CellA.x + key.CellB.x;
            int centerZ2 = key.CellA.z + key.CellB.z;
            int playerX2 = playerCellPos.x * 2;
            int playerZ2 = playerCellPos.z * 2;

            if (key.Face == WallFace.PosX)
            {
                if (downAxis.x >= 0)
                    return centerX2 >= playerX2;
                return centerX2 <= playerX2;
            }

            if (downAxis.z >= 0)
                return centerZ2 >= playerZ2;
            return centerZ2 <= playerZ2;
        }

        private static bool IsCellInsideMask(
            Vector3Int cellPos,
            Vector3Int playerCellPos,
            OcclusionMaskOptions options,
            int downScale,
            int rightScale)
        { 
            Vector3Int delta = new Vector3Int(cellPos.x - playerCellPos.x, 0, cellPos.z - playerCellPos.z);
            int downProjection = DotXZ(delta, options.DownAxis);
            int rightProjection = DotXZ(delta, options.RightAxis);

            // Player visibility layer must not affect top walls.
            if (downProjection < 0 || downProjection > options.DownTiles * downScale)
                return false;

            int depth = downProjection / downScale;
            if (depth < options.MinStartDepthTiles)
                return false;

            // +X/-Z 진행축 기준 삼각형 마스크: 깊어질수록 좌우 허용폭 1타일씩 증가.
            int depthFromStart = (depth - options.MinStartDepthTiles) + 1;
            int allowedLeftTiles = Mathf.Min(options.LeftTiles, depthFromStart);
            int allowedRightTiles = Mathf.Min(options.RightTiles, depthFromStart);

            if (rightProjection < -allowedLeftTiles * rightScale || rightProjection > allowedRightTiles * rightScale)
                return false;

            return true;
        }

        private static int AxisScale(Vector3Int axis)
        {
            int scale = Mathf.Abs(axis.x) + Mathf.Abs(axis.z);
            return Mathf.Max(0, scale);
        }

        private static int DotXZ(Vector3Int a, Vector3Int b)
        {
            return (a.x * b.x) + (a.z * b.z);
        }

        private List<TileData> CollectAdjacentWallEdges(Vector3Int center)
        {
            var result = new List<TileData>();
            foreach (var d in BottomOcclusionDirections)
            {
                var n = center + d;
                if (WallEdgeKey.TryBetween(center, n, out var key) && _edges.TryGetValue(key, out TileData e))
                    result.Add(e);
            }
            return result;
        }

        private List<TileData> CollectAdjacentWalls(Vector3Int center)
        {
            var result = new List<TileData>();
            foreach (var d in BottomOcclusionDirections)
            {
                if (_tiles.TryGetValue(center + d, out var list))
                {
                    foreach (var t in list)
                    {
                        if (IsSolidCellWall((TileView.TileType)t.identity.tileType))
                            result.Add(t);
                    }
                }
            }
            return result;
        }

        private List<TileData> CollectNewWallsAdjacentToMultipleTopWalls(
            List<TileData> topWalls,
            HashSet<Vector3Int> wallChecked)
        {
            var neighborHitCount = new Dictionary<Vector3Int, int>();
            foreach (var tw in topWalls)
            {
                var p = tw.identity.GridPos;
                foreach (var d in CardinalNeighbors)
                {
                    var np = new Vector3Int(p.x + d.x, p.y, p.z + d.z);
                    // 코너 후보는 빨간 벽 셀 자체가 아닌, 그 주변의 비-빨간 벽 셀만 본다.
                    if (wallChecked.Contains(np))
                        continue;
                    if (!CellHasOccludableWall(np))
                        continue;
                    int redNeighborHits = 0;
                    foreach (var rd in CardinalNeighbors)
                    {
                        if (wallChecked.Contains(np + rd))
                            redNeighborHits++;
                    }
                    // 빨간 벽과 2방향 이상 맞닿는 경우만 코너 후보로 카운트
                    if (redNeighborHits < 2)
                        continue;
                    neighborHitCount.TryGetValue(np, out var c);
                    neighborHitCount[np] = c + 1;
                }
            }

            var extra = new List<TileData>();
            foreach (var kv in neighborHitCount)
            {
                if (kv.Value < 2)
                    continue;
                if (!_tiles.TryGetValue(kv.Key, out var list))
                    continue;
                foreach (var t in list)
                {
                    if (IsSolidCellWall((TileView.TileType)t.identity.tileType))
                    {
                        extra.Add(t);
                        break;
                    }
                }
            }

            return extra;
        }

        private static bool IsBottomOcclusionDirection(Vector3Int direction) =>
            direction == Vector3Int.right || direction == Vector3Int.back;

        private static void AddByDirection(
            TileData wall,
            bool isBottomDirection,
            HashSet<TileData> belowSet,
            HashSet<TileData> topSet)
        {
            if (isBottomDirection)
                belowSet.Add(wall);
            else
                topSet.Add(wall);
        }

        private bool CellHasOccludableWall(Vector3Int cell)
        {
            if (_tiles.TryGetValue(cell, out var list))
            {
                foreach (var t in list)
                {
                    if (IsSolidCellWall((TileView.TileType)t.identity.tileType))
                        return true;
                }
            }

            return false;
        }

        private static bool IsSolidCellWall(TileView.TileType type) =>
            type == TileView.TileType.Wall || type == TileView.TileType.Obstacle;

#if UNITY_EDITOR
        private void DebugGizmos(HashSet<Vector3Int> occupiedCells, float offset = 0f, Color color = default)
        {
            var occupiedCellList = occupiedCells.ToList();
            foreach (var cell in occupiedCellList)
            {
                foreach (var direction in CardinalNeighbors)
                {
                    var adjacentCell = new Vector3Int(cell.x + direction.x, cell.y, cell.z + direction.z);
                    if (!occupiedCells.Contains(adjacentCell))
                    {
                        Vector3 cellToAdjacentDir = adjacentCell - cell;
                        Vector3 perpendicularDir = new Vector3(-cellToAdjacentDir.z, 0, cellToAdjacentDir.x).normalized;
                        Vector3 edgeCenter = new Vector3(
                            (cell.x + adjacentCell.x) * 0.5f,
                            cell.y,
                            (cell.z + adjacentCell.z) * 0.5f);

                        Vector3 edgeLineStart = TileHelper.ConvertGridToWorldPos(
                            edgeCenter - perpendicularDir * 0.5f + cellToAdjacentDir * offset, 1f);
                        Vector3 edgeLineEnd = TileHelper.ConvertGridToWorldPos(
                            edgeCenter + perpendicularDir * 0.5f + cellToAdjacentDir * offset, 1f);

                        Debug.DrawLine(edgeLineStart, edgeLineEnd, color);
                    }
                }
            }
        }

        private void DebugEdgeGizmos(List<TileData> edgeTiles, float offset = 0f, Color color = default)
        {
            for (int i = 0; i < edgeTiles.Count; i++)
            {
                var key = WallEdgeKey.FromEdgeTileIdentity(edgeTiles[i].identity);
                Vector3Int neighbor = key.CellB;
                Vector3 cellToNeighbor = neighbor - key.CellA;
                Vector3 perpendicularDir = new Vector3(-cellToNeighbor.z, 0, cellToNeighbor.x).normalized;
                Vector3 edgeCenter = new Vector3(
                    (key.CellA.x + neighbor.x) * 0.5f,
                    key.CellA.y,
                    (key.CellA.z + neighbor.z) * 0.5f);

                Vector3 edgeLineStart = TileHelper.ConvertGridToWorldPos(
                    edgeCenter - perpendicularDir * 0.5f + cellToNeighbor * offset, 1f);
                Vector3 edgeLineEnd = TileHelper.ConvertGridToWorldPos(
                    edgeCenter + perpendicularDir * 0.5f + cellToNeighbor * offset, 1f);

                Debug.DrawLine(edgeLineStart, edgeLineEnd, color);
            }
        }
#endif
    }
}

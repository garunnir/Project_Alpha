// ============================================================
// WallOcclusionFinder — BFS flood-fill로 플레이어 기준 가려야 할 Wall 타일(면 EdgeWall 포함)을 탐색
// ============================================================
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace IsoTilemap
{
    public sealed class OcclusionSelection
    {
        public List<TileData> Occluding { get; }

        public OcclusionSelection(List<TileData> occluding)
        {
            Occluding = occluding;
        }
    }

    public class WallOcclusionFinder
    {
        private static readonly Vector3Int[] CardinalNeighbors =
        {
            Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward
        };

        private static readonly Dictionary<WallEdgeKey, TileData> EmptyEdges = new Dictionary<WallEdgeKey, TileData>();

        private readonly Dictionary<Vector3Int, List<TileData>> _tiles;
        private readonly IReadOnlyDictionary<WallEdgeKey, TileData> _edges;

        /// <param name="edges"><see cref="TileEdgeBinder"/> 등 면 벽 레지스트리 인덱스(셀 리스트와 분리).</param>
        public WallOcclusionFinder(Dictionary<Vector3Int, List<TileData>> tiles, IReadOnlyDictionary<WallEdgeKey, TileData> edges)
        {
            _tiles = tiles;
            _edges = edges ?? EmptyEdges;
        }

        public List<TileData> Find(Vector3Int playerCellPos) =>
            FindOcclusion(playerCellPos).Occluding;

        public OcclusionSelection FindOcclusion(Vector3Int playerCellPos)
        {
            Vector3Int start = playerCellPos;
            var resultSet = new HashSet<TileData>();
            var resultEdgeSet = new HashSet<TileData>();

            if (_tiles.TryGetValue(start, out var startList))
            {
                bool hasBlocking = startList.Any(t => IsSolidCellWall((TileView.TileType)t.identity.tileType));

                if (hasBlocking)
                {
                    var tryDirs = new Vector3Int[] { Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward };
                    bool found = false;
                    foreach (var d in tryDirs)
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
                        return new OcclusionSelection(adjMerged);
                    }
                }
            }

            if (_tiles.TryGetValue(start, out var stlist))
            {
                if (stlist == null || stlist.Count == 0)
                {
                    if (Config.DebugMode.FloorAlgorithm) Debug.LogWarning("내 위치에 아무것도 없음." + start);
                    return new OcclusionSelection(new List<TileData>());
                }
            }

            var visited = new HashSet<Vector3Int>();
            var q = new Queue<Vector3Int>();
            var floorChecked = new HashSet<Vector3Int>();
            var wallChecked = new HashSet<Vector3Int>();
            visited.Add(start);
            q.Enqueue(start);
            floorChecked.Add(start);

            int safetyLimit = 200000;
            int steps = 0;
            var neighbors = new Vector3Int[] { Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward };

            while (q.Count > 0)
            {
                if (++steps > safetyLimit) break;
                var cur = q.Dequeue();

                foreach (var d in neighbors)
                {
                    var nx = new Vector3Int(cur.x + d.x, playerCellPos.y, cur.z + d.z);
                    if (!visited.Add(nx)) continue;

                    if (WallEdgeKey.TryBetween(cur, nx, out var edgeKey) && _edges.TryGetValue(edgeKey, out TileData edgeWall))
                    {
                        resultEdgeSet.Add(edgeWall);
                        wallChecked.Add(nx);
                        continue;
                    }

                    bool isFloor = false;
                    if (_tiles.TryGetValue(nx, out var list))
                    {
                        foreach (var t in list)
                        {
                            if (IsSolidCellWall((TileView.TileType)t.identity.tileType))
                            {
                                resultSet.Add(t);
                                wallChecked.Add(nx);
                                isFloor = false;
                                break;
                            }
                            else if ((TileView.TileType)t.identity.tileType == TileView.TileType.Floor)
                            {
                                isFloor = true;
                            }
                        }
                        if (isFloor)
                        {
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
                Debug.Log("Hideable WallDetect" + resultSet.Count + " tiles, " + resultEdgeSet.Count + " edges, " + visited.Count + " visited");

            var belowList = GetBelowWalls(floorChecked, resultSet);
            var topWallsForCorners = GetTopWalls(floorChecked, resultSet);
            var cornerExtras =
                CollectNewWallsAdjacentToMultipleTopWalls(topWallsForCorners, wallChecked);

            var tileResult = new List<TileData>(belowList.Count + cornerExtras.Count);
            tileResult.AddRange(belowList);
            tileResult.AddRange(cornerExtras);

            var belowEdges = GetBelowWallEdges(floorChecked, resultEdgeSet);
            var merged = new List<TileData>(tileResult.Count + belowEdges.Count);
            merged.AddRange(tileResult);
            merged.AddRange(belowEdges);

#if UNITY_EDITOR
            if (Config.DebugMode.FloorAlgorithm)
            {
                var wallCheckedSnapshot = wallChecked;
                var startSnapshot = start;
                var belowSnapshot = belowList;
                var topWallsSnapshot = topWallsForCorners;
                var cornerExtrasSnapshot = cornerExtras;
                Action action = () =>
                {
                    DebugGizmos(floorChecked, 0, Color.green);
                    DebugGizmos(wallCheckedSnapshot, 0.05f, Color.red);
                    DebugGizmos(new HashSet<Vector3Int> { startSnapshot }, 0.01f, Color.cyan);
                    DebugGizmos(belowSnapshot.Select(t => t.identity.GridPos).ToHashSet(), 0.02f, Color.yellow);
                    DebugGizmos(topWallsSnapshot.Select(t => t.identity.GridPos).ToHashSet(), 0.02f, Color.white);
                    DebugGizmos(cornerExtrasSnapshot.Select(t => t.identity.GridPos).ToHashSet(), 0.02f, Color.blue);
                };
                StateRunner.Instance.ChangeState(new DebugTileRunner(action));
            }
#endif

            return new OcclusionSelection(merged);
        }

        private List<TileData> CollectAdjacentWallEdges(Vector3Int center)
        {
            var result = new List<TileData>();
            var checkDirs = new Vector3Int[] { Vector3Int.right, Vector3Int.back };
            foreach (var d in checkDirs)
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
            var checkDirs = new Vector3Int[] { Vector3Int.right, Vector3Int.back };
            foreach (var d in checkDirs)
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

        private List<TileData> GetBelowWallEdges(HashSet<Vector3Int> visitedFloor, HashSet<TileData> edges)
        {
            var result = new List<TileData>();
            foreach (var wall in edges)
            {
                var anchor = wall.identity.GridPos;
                if (visitedFloor.Contains(anchor + Vector3Int.forward) ||
                    visitedFloor.Contains(anchor + Vector3Int.left))
                    result.Add(wall);
            }
            return result;
        }

        private List<TileData> GetBelowWalls(HashSet<Vector3Int> visitedFloor, HashSet<TileData> walls)
        {
            var result = new List<TileData>();
            foreach (var wall in walls)
            {
                if (visitedFloor.Contains(wall.identity.GridPos + Vector3Int.forward) ||
                    visitedFloor.Contains(wall.identity.GridPos + Vector3Int.left))
                    result.Add(wall);
            }
            return result;
        }

        private List<TileData> GetTopWalls(HashSet<Vector3Int> visitedFloor, HashSet<TileData> walls)
        {
            var result = new List<TileData>();
            foreach (var wall in walls)
            {
                if (visitedFloor.Contains(wall.identity.GridPos + Vector3Int.back) ||
                    visitedFloor.Contains(wall.identity.GridPos + Vector3Int.right))
                    result.Add(wall);
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
                    if (wallChecked.Contains(np))
                        continue;
                    if (!CellHasOccludableWallOrEdge(np))
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

        private bool CellHasOccludableWallOrEdge(Vector3Int cell)
        {
            if (CellHasOccludableWall(cell))
                return true;
            foreach (var d in CardinalNeighbors)
            {
                var n = cell + d;
                if (WallEdgeKey.TryBetween(cell, n, out var k) && _edges.ContainsKey(k))
                    return true;
            }
            return false;
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

            foreach (var d in CardinalNeighbors)
            {
                var n = new Vector3Int(cell.x + d.x, cell.y, cell.z + d.z);
                if (WallEdgeKey.TryBetween(cell, n, out var k) && _edges.ContainsKey(k))
                    return true;
            }

            return false;
        }

        private static bool IsSolidCellWall(TileView.TileType type) =>
            type == TileView.TileType.Wall || type == TileView.TileType.Obstacle;

#if UNITY_EDITOR
        private void DebugGizmos(HashSet<Vector3Int> occupiedCells, float offset = 0f, Color color = default)
        {
            var occupiedCellList = occupiedCells.ToList();
            var cardinalDirections = new Vector3Int[]
            {
                Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward
            };

            foreach (var cell in occupiedCellList)
            {
                foreach (var direction in cardinalDirections)
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
#endif
    }
}

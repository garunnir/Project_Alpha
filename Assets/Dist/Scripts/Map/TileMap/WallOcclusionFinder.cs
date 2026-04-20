// ============================================================
// WallOcclusionFinder — BFS flood-fill로 플레이어 기준 가려야 할 Wall 타일을 탐색
// ============================================================
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

namespace IsoTilemap
{
    public class WallOcclusionFinder
    {
        private readonly Dictionary<Vector3Int, List<TileData>> _tiles;

        public WallOcclusionFinder(Dictionary<Vector3Int, List<TileData>> tiles)
        {
            _tiles = tiles;
        }

        // 플레이어 셀 위치를 기준으로 XZ 평면 BFS를 수행하여
        // 가려야 할 Wall/Obstacle 타일 목록을 반환한다.
        public List<TileData> Find(Vector3Int playerCellPos)
        {
            Vector3Int start = playerCellPos;
            var resultSet = new HashSet<TileData>();

            // 시작 셀이 벽이면 인접한 빈 셀로 시작점을 옮긴다
            if (_tiles.TryGetValue(start, out var startList))
            {
                bool hasBlocking = startList.Any(t => IsWallEligibleForHiding((TileView.TileType)t.identity.tileType));

                if (hasBlocking)
                {
                    var tryDirs = new Vector3Int[] { Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward };
                    bool found = false;
                    foreach (var d in tryDirs)
                    {
                        var n = new Vector3Int(start.x + d.x, start.y, start.z + d.z);
                        if (_tiles.TryGetValue(n, out var nList) &&
                            !nList.Any(x => IsWallEligibleForHiding((TileView.TileType)x.identity.tileType)))
                        {
                            start = n;
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return CollectAdjacentWalls(start);
                    }
                }
            }

            if (_tiles.TryGetValue(start, out var stlist))
            {
                if (stlist == null || stlist.Count == 0)
                {
                    if (Config.DebugMode.FloorAlgorithm) Debug.LogWarning("내 위치에 아무것도 없음." + start);
                    return new List<TileData>();
                }
            }

            // BFS flood-fill (XZ plane, Y 고정)
            var visited = new HashSet<Vector3Int>();
            var q = new Queue<Vector3Int>();
            var floorChecked = new HashSet<Vector3Int>();
            var wallChecked = new HashSet<Vector3Int>();
            visited.Add(start);
            q.Enqueue(start);

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

                    bool isFloor = false;
                    if (_tiles.TryGetValue(nx, out var list))
                    {
                        foreach (var t in list)
                        {
                            if (IsWallEligibleForHiding((TileView.TileType)t.identity.tileType))
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
                        if(Config.DebugMode.FloorAlgorithm)
                            Debug.Log("빈셀발견 확장" + nx);
                    }
                }
            }

            if (Config.DebugMode.FloorAlgorithm)
                Debug.Log("Hideable WallDetect" + resultSet.Count + " tiles" + visited.Count);

            var result = GetBelowWalls(floorChecked, resultSet);

#if UNITY_EDITOR
            if (Config.DebugMode.FloorAlgorithm)
            {
                var wallCheckedSnapshot = wallChecked;
                var startSnapshot = start;
                var resultSnapshot = result;
                Action action = () =>
                {
                    DebugGizmos(floorChecked, 0, Color.green);
                    DebugGizmos(wallCheckedSnapshot, 0.05f, Color.red);
                    DebugGizmos(new HashSet<Vector3Int> { startSnapshot }, 0.01f, Color.cyan);
                    DebugGizmos(resultSnapshot.Select(t => t.identity.GridPos).ToHashSet(), 0.02f, Color.yellow);
                };
                StateRunner.Instance.ChangeState(new DebugTileRunner(action));
            }
#endif

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
                        if (IsWallEligibleForHiding((TileView.TileType)t.identity.tileType))
                            result.Add(t);
                    }
                }
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

        private bool IsWallEligibleForHiding(TileView.TileType type)
        {
            return type == TileView.TileType.Wall || type == TileView.TileType.Obstacle;
        }

#if UNITY_EDITOR
        // TODO: 타일이 1x1이 아닌 경우 정상 동작하지 않음 → 예외 처리 필요
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

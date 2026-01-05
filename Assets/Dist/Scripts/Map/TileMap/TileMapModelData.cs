using System;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace IsoTilemap
{
    public class TileMapModelData:IMapModel
    {
        public List<TileData> GetOccludingWalls(Vector3Int playerCellPos,Dictionary<Vector3Int, List<TileData>> alltiles)
        {
            // 주어진 플레이어 셀 위치(playerCellPos)를 기준으로
            // 2D(XZ) flood-fill을 수행하여 플레이어가 닿을 수 있는 빈 공간을 찾습니다.
            // 그 과정에서 빈 공간과 접한 Wall 타입의 타일을 수집하여 반환합니다.
            // 반환값: 숨겨야 할 Wall 타일들의 리스트(중복 제거)

            Vector3Int start = playerCellPos;
            var resultSet = new HashSet<TileData>();

            // 시작 셀이 점유되어 있고, 그 점유물이 Wall/Obstacle이면 인접한 빈칸을 찾아 시작점으로 삼음
            if (alltiles.TryGetValue(start, out var startList))
            {
                bool hasBlocking = false;
                foreach (var t in startList)
                {
                    if (t == null) continue;
                    if (IsWallEligibleForHiding((TileInfo.TileType)t.identity.tileType))
                    { hasBlocking = true; break; }
                }

                if (hasBlocking)
                {
                    // 우하향 우선 탐색: +X, +Z, -X, -Z

                    var tryDirs = new Vector3Int[] { Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward };
                    bool found = false;
                    foreach (var d in tryDirs)
                    {
                        var n = new Vector3Int(start.x + d.x, start.y, start.z + d.z);
                        //벽 타일이나 오브젝트 타일이 없는 빈칸을 찾음
                        if (!alltiles[n].Any(x => IsWallEligibleForHiding((TileInfo.TileType)x.identity.tileType))) { start = n; found = true; break; }
                    }
                    if (!found)
                    {
                        alltiles.TryGetValue(start + Vector3Int.right, out var rightlist);
                        alltiles.TryGetValue(start + Vector3Int.back, out var backlist);
                        var hidelist = new List<TileData>();
                        if (rightlist != null)
                        {
                            foreach (var t in rightlist)
                            {
                                if (IsWallEligibleForHiding((TileInfo.TileType)t.identity.tileType))
                                {
                                    hidelist.Add(t);
                                }
                            }
                        }
                        if (backlist != null)
                        {
                            foreach (var t in backlist)
                            {
                                if (IsWallEligibleForHiding((TileInfo.TileType)t.identity.tileType))
                                {
                                    hidelist.Add(t);
                                }
                            }
                        }
                        // 인접 빈칸이 없으면 우측과 아래 벽을 반환
                        return hidelist;
                    }
                }
            }

            //자신 위치를 확인하고 아무것도 없으면 무시
            if (alltiles.TryGetValue(start, out var stlist))
            {
                if (stlist == null || stlist.Count == 0)
                {
                    if (Config.DebugMode.FloorAlgorithm) Debug.LogWarning("내 위치에 아무것도 없음." + start);
                    return new List<TileData>();
                }
            }


            // BFS flood-fill (XZ plane only) - Y층은 playerCellPos.y로 고정



            var visited = new HashSet<Vector3Int>();
            var q = new Queue<Vector3Int>();
            var floorChecked = new HashSet<Vector3Int>();
            var wallChecked = new HashSet<Vector3Int>();
            visited.Add(start);
            q.Enqueue(start);

            int safetyLimit = 200000; // 무한루프 방지
            int steps = 0;

            var neighbors = new Vector3Int[] { Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward };

            while (q.Count > 0)
            {
                if (++steps > safetyLimit) break;
                var cur = q.Dequeue();

                foreach (var d in neighbors)
                {
                    var nx = new Vector3Int(cur.x + d.x, playerCellPos.y, cur.z + d.z);
                    // 'Add'로 방문 체크를 한 번에 처리
                    // (이미 방문했으면 false → continue)
                    if (!visited.Add(nx))
                        continue;
                    bool isFloor = false;

                    //해당 그리드타일에 벽 타일과 바닥 타일을 구분
                    if (alltiles.TryGetValue(nx, out var list))
                    {
                        // 점유된 셀: 해당 그리드좌표에 포함된 타일들 중 Wall인 경우가 결과에 추가
                        foreach (var t in list)
                        {
                            if (t == null) continue;
                            if (IsWallEligibleForHiding((TileInfo.TileType)t.identity.tileType))
                            {
                                resultSet.Add(t);
                                wallChecked.Add(nx);
                                //벽타일이면 확장하지 않음
                                isFloor = false;
                                break;
                            }
                            else if ((TileInfo.TileType)t.identity.tileType == TileInfo.TileType.Floor)
                            {
                                //바닥 타일이면 확장
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
                        // 비어있는 셀: 확장
                        Debug.LogError("빈셀발견 확장" + nx);
                    }
                }
            }
            List<TileData> result = new List<TileData>(resultSet);
            if (Config.DebugMode.FloorAlgorithm) Debug.Log("Hideable WallDetect" + result.Count + " tiles" + visited.Count);
            result = GetBelowWall(floorChecked, result.ToHashSet());

#if UNITY_EDITOR
            if (Config.DebugMode.FloorAlgorithm)
            {
                Action action = () =>
{
    DebugGizmos(floorChecked, 0, Color.green);
    DebugGizmos(wallChecked, 0.1f, Color.red);
    DebugGizmos(new() { start }, 0, Color.skyBlue);
    DebugGizmos(result.Select(t => t.identity.GridPos).ToHashSet(), 0.01f, Color.skyBlue);
};
                StateRunner.Instance.ChangeState(new DebugTileRunner(action));
            }
#endif


            return result;
        }

        private bool IsWallEligibleForHiding(TileInfo.TileType type)
        {
            return type == TileInfo.TileType.Wall || type == TileInfo.TileType.Obstacle;
        }
#if UNITY_EDITOR
        // TODO: 타일이 1x1이 아닌 경우 정상 동작하지 않음 → 예외 처리 필요
        private void DebugGizmos(
            HashSet<Vector3Int> occupiedCells,
            float offset = 0f,
            Color color = default)
        {
            // 경계 판별을 위해 리스트로 변환
            var occupiedCellList = occupiedCells.ToList();

            // 상하좌우(카디널) 방향
            var cardinalDirections = new Vector3Int[]
            {
        Vector3Int.right,
        Vector3Int.back,
        Vector3Int.left,
        Vector3Int.forward
            };

            for (int i = 0; i < occupiedCellList.Count; i++)
            {
                var cell = occupiedCellList[i];

                foreach (var direction in cardinalDirections)
                {
                    var adjacentCell = new Vector3Int(
                        cell.x + direction.x,
                        cell.y,
                        cell.z + direction.z
                    );

                    // 인접 셀이 없으면 외곽 경계
                    if (!occupiedCells.Contains(adjacentCell))
                    {
                        // 현재 셀 → 인접 셀 방향
                        Vector3 cellToAdjacentDir = adjacentCell - cell;

                        // 경계선용 수직 벡터
                        Vector3 perpendicularDir =
                            new Vector3(-cellToAdjacentDir.z, 0, cellToAdjacentDir.x).normalized;

                        // 두 셀 사이 경계의 중심
                        Vector3 edgeCenter = new Vector3(
                            (cell.x + adjacentCell.x) * 0.5f,
                            cell.y,
                            (cell.z + adjacentCell.z) * 0.5f
                        );

                        Vector3 edgeLineStart = edgeCenter - perpendicularDir * 0.5f;
                        Vector3 edgeLineEnd = edgeCenter + perpendicularDir * 0.5f;

                        // 오프셋 적용 후 월드 좌표 변환
                        edgeLineStart = TileHelper.ConvertGridToWorldPos(
                            edgeLineStart + cellToAdjacentDir * offset, 1f);

                        edgeLineEnd = TileHelper.ConvertGridToWorldPos(
                            edgeLineEnd + cellToAdjacentDir * offset, 1f);

                        Debug.DrawLine(edgeLineStart, edgeLineEnd, color);
                    }
                }
            }
        }
#endif

        private List<TileData> GetBelowWall(HashSet<Vector3Int> visitedGridPositions, HashSet<TileData> walls)
        {
            //방을 구성하는 벽들을 가져온다.
            //하단의 벽을 구분해서 가져온다.
            List<TileData> belowWalls = new List<TileData>();
            foreach (var wall in walls)
            {
                //벽 자신 기준으로 위와 왼쪽의 공간이 방의 구성요소일경우 아래에 있는 벽으로 간주한다.
                if (visitedGridPositions.Contains(wall.identity.GridPos + Vector3Int.forward) || visitedGridPositions.Contains(wall.identity.GridPos + Vector3Int.left))
                    belowWalls.Add(wall);
            }
            return belowWalls;
        }

        public IEnumerable<TileCellSnapshot> Tiles()
        {
            throw new NotImplementedException();
        }
    }
}
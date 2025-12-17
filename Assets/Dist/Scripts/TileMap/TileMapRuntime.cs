using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
namespace IsoTilemap
{

    //타일 데이터를 관리한다
    //컨트롤러가 뷰잉을 개시한다.
    //런타임에만 사용된다.
    //타일이 생성 삭제될때 반드시 갱신되어야 함
    // 맵 전체 데이터
    public class TileMapRuntime : MonoBehaviour
    {

        public TileMapRuntimeData GetRuntimeData() { return _runtimeData; }
        private TileMapRuntimeData _runtimeData;
        private void Awake()
        {
        }
        private void OnEnable()
        {
            //타일맵이 빌드될때마다 데이터 업데이트를 수행한다.
        }
        private void OnDisable()
        {
        }
        public void UpdateRuntimeData(TileMapRuntimeData runtimeData)
        {
            _runtimeData = runtimeData;
        }
        public void UpdateRuntimeData(Dictionary<Vector3Int, List<TileData>> keyValuePairs)
        {
            if (_runtimeData == null) _runtimeData = new TileMapRuntimeData();
            _runtimeData.tiles = keyValuePairs;
        }

        private HashSet<Vector3Int> _cachedCurrentRoomID;//이미 계산된 타일이면 건너뜀.
        private List<TileData> _cachedtiles;
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }
        private bool IsWallEligibleForHiding(TileInfo.TileType type)
        {
            return type == TileInfo.TileType.Wall || type == TileInfo.TileType.Obstacle;
        }
#if UNITY_EDITOR
        //TODO 타일이 1x1이 아닌경우 정상적 동작이 안됨 예외 처리 필요
        private void DebugGizmos(HashSet<Vector3Int> visitedCells, float offset = 0f, Color color = default)
        {

            //이웃하지 않는 셀을 표시
            var cellList = visitedCells.ToList();
            var tryDirs = new Vector3Int[] { Vector3Int.right, Vector3Int.back, Vector3Int.left, Vector3Int.forward };
            for (int i = 0; i < cellList.Count; i++)
            {
                var target = cellList[i];
                foreach (var dir in tryDirs)
                {
                    var neighbor = new Vector3Int(target.x + dir.x, target.y, target.z + dir.z);
                    if (!visitedCells.Contains(neighbor))
                    {
                        //다음블록과의 중심을 기준으로 표시선을 그린다
                        Vector3 dira = neighbor - target;
                        Vector3 dirv = new Vector3(-dira.z, 0, dira.x).normalized;//수직 벡터
                        Vector3 midPoint = new Vector3((target.x + neighbor.x) * 0.5f, target.y, (target.z + neighbor.z) * 0.5f);
                        Vector3 startline = midPoint - dirv * 0.5f;
                        Vector3 endline = midPoint + dirv * 0.5f;

                        startline = TileHelper.ConvertGridToWorldPos(startline + (dira) * offset, 1f);
                        endline = TileHelper.ConvertGridToWorldPos(endline + (dira) * offset, 1f);
                        Debug.DrawLine(startline, endline, color);
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
                if (visitedGridPositions.Contains(wall.tileInfo.gridPos + Vector3Int.forward) || visitedGridPositions.Contains(wall.tileInfo.gridPos + Vector3Int.left))
                    belowWalls.Add(wall);
            }
            return belowWalls;
        }
        //TODO 타일이 1x1이 아닌경우 정상적 동작이 안됨 예외 처리 필요
        public List<TileData> GetOccludingWalls(Vector3Int playerCellPos)
        {
            // 주어진 플레이어 셀 위치(playerCellPos)를 기준으로
            // 2D(XZ) flood-fill을 수행하여 플레이어가 닿을 수 있는 빈 공간을 찾습니다.
            // 그 과정에서 빈 공간과 접한 Wall 타입의 타일을 수집하여 반환합니다.
            // 반환값: 숨겨야 할 Wall 타일들의 리스트(중복 제거)

            if (_runtimeData == null || _runtimeData.tiles == null)
                return new List<TileData>();

            var alltiles = _runtimeData.tiles;
            Vector3Int start = playerCellPos;
            //이미 계산된구역이면 재계산하지 않음. 이 효력은 다른 구역으로 이동하면 사라짐
            if (_cachedCurrentRoomID != null && _cachedCurrentRoomID.Contains(start))
            {
                return _cachedtiles;
            }
            var resultSet = new HashSet<TileData>();

            // 시작 셀이 점유되어 있고, 그 점유물이 Wall/Obstacle이면 인접한 빈칸을 찾아 시작점으로 삼음
            if (alltiles.TryGetValue(start, out var startList))
            {
                bool hasBlocking = false;
                foreach (var t in startList)
                {
                    if (t == null) continue;
                    if (IsWallEligibleForHiding(t.tileInfo.tileType))
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
                        if (!alltiles[n].Any(x => IsWallEligibleForHiding(x.tileInfo.tileType))) { start = n; found = true; break; }
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
                                if (IsWallEligibleForHiding(t.tileInfo.tileType))
                                {
                                    hidelist.Add(t);
                                }
                            }
                        }
                        if (backlist != null)
                        {
                            foreach (var t in backlist)
                            {
                                if (IsWallEligibleForHiding(t.tileInfo.tileType))
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
                            if (IsWallEligibleForHiding(t.tileInfo.tileType))
                            {
                                resultSet.Add(t);
                                wallChecked.Add(nx);
                                //벽타일이면 확장하지 않음
                                isFloor = false;
                                break;
                            }
                            else if (t.tileInfo.tileType == TileInfo.TileType.Floor)
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
                    DebugGizmos(result.Select(t => t.tileInfo.gridPos).ToHashSet(), 0.01f, Color.skyBlue);
};
                StateRunner.Instance.ChangeState(new DebugTileRunner(action));
            }
#endif
            //_cachedCurrentRoomID = visited.ToHashSet();
            //_cachedtiles = result; 

            return result;
        }


    }
    public class DebugTileRunner : IFrameState
    {
        Action _action;
        public void Enter()
        {
        }

        public void Exit()
        {
        }

        public void Tick(float dt)
        {
            // Debug.Log("DebugRunner Tick: " + dt);
            _action?.Invoke();
        }
        public DebugTileRunner(Action action)
        {
            _action = action;
        }
    }
}
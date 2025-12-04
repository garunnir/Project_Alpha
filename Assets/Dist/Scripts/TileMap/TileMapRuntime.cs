using UnityEngine;
using System.Collections.Generic;
using System.Linq;
namespace IsoTilemap
{

    //타일 데이터를 관리한다
    //컨트롤러가 뷰잉을 개시한다.
    //런타임에만 사용된다.
    //타일이 생성 삭제될때 반드시 갱신되어야 함
    // 맵 전체 데이터
    public class TileMapRuntime : MonoBehaviour
    {
        private TileMapRuntimeData _runtimeData;
        public TileMapRuntimeData RuntimeData
        {
            get { return _runtimeData; }
            set { _runtimeData = value; }
        }
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {

        }
        private bool IsWallEligibleForHiding(TileInfo.TileType type)
        {
            return type == TileInfo.TileType.Wall || type == TileInfo.TileType.Obstacle;
        }
        private void DebugGizmos(HashSet<Vector3Int> visitedCells)
        {

            //이웃하지 않는 셀을 표시
            var cellList = visitedCells.ToList();
            var tryDirs = new Vector3Int[] { Vector3Int.right,Vector3Int.back, Vector3Int.left,Vector3Int.forward };
            for(int i = 0; i < cellList.Count; i++)
            {
                var target=cellList[i];
                foreach(var dir in tryDirs)
                {
                    var neighbor = new Vector3Int(target.x + dir.x, target.y, target.z + dir.z);
                    if(!visitedCells.Contains(neighbor))
                    {
                        //다음블록과의 중심을 기준으로 표시선을 그린다
                        Vector3 dirv= neighbor - target;
                        dirv=new Vector3(-dirv.z,0,dirv.x).normalized;//수직 벡터
                        Vector3 midPoint = new Vector3((target.x + neighbor.x) * 0.5f, target.y, (target.z + neighbor.z) * 0.5f);
                        Vector3 startline= midPoint - dirv * 0.5f;
                        Vector3 endline= midPoint + dirv * 0.5f;
                        Debug.DrawLine(startline,endline, Color.red);
                    }
                }
            }
        }
      
      //TODO 타일이 1x1이 아닌경우 정상적 동작이 안됨 예외 처리 필요
        public List<TileInfo> GetHideWallTiles(Vector3Int playerCellPos)
        {
            // 주어진 플레이어 셀 위치(playerCellPos)를 기준으로
            // 2D(XZ) flood-fill을 수행하여 플레이어가 닿을 수 있는 빈 공간을 찾습니다.
            // 그 과정에서 빈 공간과 접한 Wall 타입의 타일을 수집하여 반환합니다.
            // 반환값: 숨겨야 할 Wall 타일들의 리스트(중복 제거)

            if (_runtimeData == null || _runtimeData.tiles == null)
                return new List<TileInfo>();

            var occupancy = _runtimeData.tiles;
            Vector3Int start = playerCellPos;

            var resultSet = new HashSet<TileInfo>();

            // 시작 셀이 점유되어 있고, 그 점유물이 Wall/Obstacle이면 인접한 빈칸을 찾아 시작점으로 삼음
            if (occupancy.TryGetValue(start, out var startList))
            {
                bool hasBlocking = false;
                foreach (var t in startList)
                {
                    if (t == null) continue;
                    if (t.tileType == TileInfo.TileType.Wall || t.tileType == TileInfo.TileType.Obstacle)
                    { hasBlocking = true; break; }
                }

                if (hasBlocking)
                {
                    // 우하향 우선 탐색: +X, +Z, -X, -Z
                    
                    var tryDirs = new Vector3Int[] { Vector3Int.right,Vector3Int.back, Vector3Int.left,Vector3Int.forward };
                    bool found = false;
                    foreach (var d in tryDirs)
                    {
                        var n = new Vector3Int(start.x + d.x, start.y, start.z + d.z);
                        //벽 타일이나 오브젝트 타일이 없는 빈칸을 찾음
                        if (!occupancy[n].Any(x=>IsWallEligibleForHiding(x.tileType))) { start = n; found = true; break; }
                    }
                    if (!found)
                    {
                        occupancy.TryGetValue(start+Vector3Int.right, out var rightlist);
                        occupancy.TryGetValue(start+Vector3Int.back, out var backlist);
                        var hidelist = new List<TileInfo>();
                        if(rightlist!=null)
                        {
                            foreach(var t in rightlist)
                            {
                                if(IsWallEligibleForHiding(t.tileType))
                                {
                                    hidelist.Add(t);
                                }
                            }
                        }
                        if(backlist!=null)
                        {
                            foreach(var t in backlist)
                            {
                                if(IsWallEligibleForHiding(t.tileType))
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

            // BFS flood-fill (XZ plane only) - Y층은 playerCellPos.y로 고정
            var visited = new HashSet<Vector3Int>();
            var q = new Queue<Vector3Int>();
            visited.Add(start);
            q.Enqueue(start);

            int safetyLimit = 200000; // 무한루프 방지
            int steps = 0;

            var neighbors = new Vector3Int[] { Vector3Int.right,Vector3Int.back, Vector3Int.left,Vector3Int.forward };

            while (q.Count > 0)
            {
                if (++steps > safetyLimit) break;
                var cur = q.Dequeue();

                foreach (var d in neighbors)
                {
                    var nx = new Vector3Int(cur.x + d.x, playerCellPos.y, cur.z + d.z);
                    if (visited.Contains(nx)) continue;

                    if (occupancy.TryGetValue(nx, out var list))
                    {
                        // 점유된 셀: 포함된 타일들 중 Wall인 경우 결과에 추가
                        foreach (var t in list)
                        {
                            if (t == null) continue;
                            if (IsWallEligibleForHiding(t.tileType))
                                resultSet.Add(t);
                        }
                        // 이 쪽은 통과 불가 (벽/오브젝트가 점유)
                    }
                    else
                    {
                        // 비어있으면 확장
                        visited.Add(nx);
                        q.Enqueue(nx);
                    }
                }
            }
                        DebugGizmos(visited);

            return new List<TileInfo>(resultSet);
        }
    }
}
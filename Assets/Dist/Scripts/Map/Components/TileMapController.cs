using System;
using UnityEngine;
namespace IsoTilemap
{
    public class TileMapController : MonoBehaviour
    {
        [SerializeField] private TileMapVisualizer _visualizer;
        [SerializeField] private TileMapContext _context;


        void ClearExistingTiles()
        {
            // FindObjectsByType: include inactive so we match the previous behavior of FindObjectsOfType(true).
            var tileInfos = UnityEngine.Object.FindObjectsByType<TileInfo>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.None);

            // 타일만 날린다고 가정 (이 스크립트가 붙은 오브젝트는 남김)
            foreach (var info in tileInfos)
            {
                if (info != null)
                {
                    // 본인 자신(TileMapSerializer의 GameObject) 밑에 있는지만 보고 날릴 수도 있음
                    DestroyImmediate(info.gameObject);
                }
            }
        }

        HashSet<Vector3Int> dirty = new();

        //public ref TileState GetOrCreate(Vector3Int cell) { /* ... */ }

        public void MarkDirty(Vector3Int cell) => dirty.Add(cell);

        public void FlushDirty()
        {
            foreach (var cell in dirty)
                RefreshCell(cell); // 그 셀만 갱신
            dirty.Clear();
        }
        void Update()
        {
            FlushDirty();
        }
        private void RefreshCell(Vector3Int cellPos)
        {
            // 해당 셀만 갱신
            List<TileData> datas=_do.GetRuntimeData().tiles[cellPos];
            foreach (var data in datas)
            {
                RefreshObj(data);
            }
        }
        private void RefreshObj(TileData obj)
        {
            TileInfo info = _tileDefInstance[obj.tileDefId];
            info.gameObject.SetActive(!obj.state.isHiddenCharacter);
        }
        public void UpdateCell(Vector3Int cellPos, TileData state)
        {
            MarkDirty(cellPos);
        }
        
    }
}

// ============================================================
// MapDigColumnHost — 굴착 시 상층 바닥 제거·하층 노출·지층 생성
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    [DisallowMultipleComponent]
    public sealed class MapDigColumnHost : MonoBehaviour
    {
        public static MapDigColumnHost Runtime { get; private set; }

        readonly Dictionary<Vector2Int, int> _columnDepthByXz = new();

        TileMapCacheHub _hub;
        TileMapController _controller;
        TilePrefabDB _prefabDb;
        float _cellSize = 1f;
        int _stratumSeed;

        [SerializeField] StratumProfile _stratumProfile;

        public int StratumSeed => _stratumSeed;
        public float CellSize => _cellSize;

        void Awake()
        {
            Runtime = this;
        }

        void OnDestroy()
        {
            if (ReferenceEquals(Runtime, this))
                Runtime = null;
        }

        public void BindMapContext(
            TileMapCacheHub hub,
            float cellSize,
            TilePrefabDB prefabDb,
            TileMapController controller = null,
            StratumProfile stratumProfile = null)
        {
            _hub = hub;
            _cellSize = Mathf.Max(1e-4f, cellSize);
            _prefabDb = prefabDb;
            _controller = controller;
            if (stratumProfile != null)
                _stratumProfile = stratumProfile;
        }

        public void LoadFromDto(MapSaveJsonDto dto)
        {
            _columnDepthByXz.Clear();
            _stratumSeed = dto != null ? dto.stratumSeed : 0;
            if (_stratumSeed == 0)
                _stratumSeed = Random.Range(1, int.MaxValue);
        }

        public void WriteToDto(MapSaveJsonDto dto)
        {
            if (dto == null)
                return;

            dto.stratumSeed = _stratumSeed;
        }

        public bool TryBreakFloor(Vector3Int walkableCell, out string brokenPrefabId)
        {
            brokenPrefabId = null;
            if (_controller == null || _hub == null)
                return false;

            Vector3Int cellBelow = walkableCell + Vector3Int.down;
            if (!_hub.TryGetHorizontalFaceBetween(cellBelow, walkableCell, out TileData faceTile))
                return false;

            TileDefinition definition = ResolveDefinition(faceTile.identity.PrefabId);
            if (!TileFlags.IsDiggableTarget(definition))
                return false;

            brokenPrefabId = faceTile.identity.PrefabId;
            _controller.RemoveAndFlush(faceTile);

            Vector3Int belowWalkable = walkableCell + Vector3Int.down;
            if (!_hub.TryGetFloorFaceForWalkableCell(
                    belowWalkable.x,
                    belowWalkable.y,
                    belowWalkable.z,
                    out _))
            {
                int depth = IncrementColumnDepth(walkableCell.x, walkableCell.z);
                string generatedPrefabId = StratumGenerator.PickFloorPrefabId(
                    _stratumSeed,
                    walkableCell.x,
                    walkableCell.z,
                    depth,
                    _stratumProfile);

                if (TryGetDefinition(generatedPrefabId, out TileDefinition floorDef))
                    _controller.TryReplaceFloorMaterial(belowWalkable, floorDef);
            }

            return true;
        }

        public Vector3 CellWorld(Vector3Int walkableCell) =>
            TileHelper.ConvertGridToWorldPos(walkableCell, _cellSize);

        int IncrementColumnDepth(int x, int z)
        {
            var key = new Vector2Int(x, z);
            if (!_columnDepthByXz.TryGetValue(key, out int depth))
                depth = 0;

            depth++;
            _columnDepthByXz[key] = depth;
            return depth;
        }

        TileDefinition ResolveDefinition(string prefabId)
        {
            if (TryGetDefinition(prefabId, out TileDefinition def))
                return def;
            return null;
        }

        bool TryGetDefinition(string prefabId, out TileDefinition def)
        {
            def = null;
            if (_prefabDb != null && _prefabDb.TryGetDefinition(prefabId, out def))
                return def != null;

            return TilePrefabDB.TryResolveDefinition(prefabId, out def);
        }
    }
}

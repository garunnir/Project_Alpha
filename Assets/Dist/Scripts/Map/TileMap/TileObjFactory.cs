namespace IsoTilemap
{
    using UnityEngine;
    public class TileObjFactory
    {
        private readonly TileMapContext _context;
        private readonly TileMapVisualizer _wallPrefab;

        public TileObjFactory(TileMapContext context, TileMapVisualizer prefab)
        {
            _context = context;
            _wallPrefab = prefab;
        }

        public void CreateWall(Vector3 position)
        {
            // 1. 생성
            TileInfo newWall = Object.Instantiate(_wallPrefab, position, Quaternion.identity);

            // 2. 바인딩 (컨텍스트나 컨트롤러에 등록)
            _context.RegisterTileObject(newWall);

            // 3. 초기 데이터 주입
            newWall.Initialize(_context.Runtime.GetDefaultState());
        }
    }
}

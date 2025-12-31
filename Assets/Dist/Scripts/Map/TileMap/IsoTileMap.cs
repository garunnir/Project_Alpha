using System.Collections.Generic;
using UnityEngine.Tilemaps;
using UnityEngine;

public class IsoTile
{
	public int x, y;         // 타일 좌표
	public int layer;        // 층 정보 (1, 2, 3 ...)
	public string type;      // 타일 종류 (grass, dirt, house 등)
	public GameObject obj;   // 해당 타일에 배치된 오브젝트(있다면)
}

public class IsoTileMap : MonoBehaviour
{
	// [층][(x, y)]로 타일 관리
	public Dictionary<int, Dictionary<(int, int), IsoTile>> layers = new();

	// 층별 Tilemap 컴포넌트 연결
	public Dictionary<int, Tilemap> tilemaps = new();

	// Tilemap 연결 메서드
	public void SetTilemap(int layer, Tilemap tilemap)
	{
		tilemaps[layer] = tilemap;
	}

	// 타일 추가 시 Tilemap에도 반영
	public void AddTile(int x, int y, int layer, string type, GameObject obj = null)
	{
		if (!layers.ContainsKey(layer))
			layers[layer] = new Dictionary<(int, int), IsoTile>();
		layers[layer][(x, y)] = new IsoTile { x = x, y = y, layer = layer, type = type, obj = obj };

		// Tilemap에 타일 정보 전달
		if (tilemaps.ContainsKey(layer))
		{
			// type에 따라 타일 에셋을 가져오는 로직 필요 (예시: Resources.Load)
			TileBase tileAsset = Resources.Load<TileBase>(type);
			if (tileAsset != null)
			{
				tilemaps[layer].SetTile(new Vector3Int(x, y, 0), tileAsset);
			}
		}
	}

	// ...existing code...

	// 특정 위치의 모든 층 타일 조회
	public List<IsoTile> GetTiles(int x, int y)
	{
		var result = new List<IsoTile>();
		foreach (var kv in layers)
			if (kv.Value.ContainsKey((x, y)))
				result.Add(kv.Value[(x, y)]);
		return result;
	}

	// 예시: 특정 층의 모든 타일 반환
	public List<IsoTile> GetLayerTiles(int layer)
	{
		if (layers.ContainsKey(layer))
			return new List<IsoTile>(layers[layer].Values);
		return new List<IsoTile>();
	}
}

using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>MapSaveJsonDto prefabId 빈도 + 스트리밍 피크로 타입별 풀 cap 산출.</summary>
    public static class TilePoolBudgetBuilder
    {
        public static Dictionary<string, int> Build(
            MapSaveJsonDto dto,
            TilePoolSettings settings,
            TilePoolStreamEstimate streamEstimate)
        {
            var counts = new Dictionary<string, int>();
            if (dto == null)
                return counts;

            AccumulateTiles(dto.tiles, counts);
            AccumulateWallEdges(dto.wallEdges, counts);

            int totalTiles = 0;
            foreach (var kv in counts)
                totalTiles += kv.Value;

            if (totalTiles == 0)
                return counts;

            int budget = settings.MaxPooledInstances;
            if (settings.MaxPoolMemoryMb > 0f)
            {
                int fromMemory = MbToInstances(settings.MaxPoolMemoryMb, settings.EstimatedBytesPerInstance);
                budget = Mathf.Min(budget, fromMemory);
            }

            budget = Mathf.RoundToInt(budget * (1f - settings.ReserveRatio));

            int streamingPeak = settings.StreamingPeakOverride > 0
                ? settings.StreamingPeakOverride
                : EstimateStreamingPeak(dto, streamEstimate, totalTiles);
            budget = Mathf.Max(budget, streamingPeak);

            var caps = new Dictionary<string, int>(counts.Count);
            var sorted = new List<KeyValuePair<string, int>>(counts);
            sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

            for (int i = 0; i < sorted.Count; i++)
            {
                string prefabId = sorted[i].Key;
                int count = sorted[i].Value;
                float share = (float)count / totalTiles;
                int cap = Mathf.Clamp(
                    Mathf.RoundToInt(budget * share),
                    settings.MinPoolPerPrefab,
                    settings.MaxPoolPerPrefab);
                caps[prefabId] = cap;
            }

            return caps;
        }

        private static void AccumulateTiles(List<TileSaveData> tiles, Dictionary<string, int> counts)
        {
            if (tiles == null)
                return;

            for (int i = 0; i < tiles.Count; i++)
            {
                TileSaveData tile = tiles[i];
                if (tile == null || string.IsNullOrEmpty(tile.prefabId))
                    continue;

                counts.TryGetValue(tile.prefabId, out int n);
                counts[tile.prefabId] = n + 1;
            }
        }

        private static void AccumulateWallEdges(List<WallEdgeSaveData> edges, Dictionary<string, int> counts)
        {
            if (edges == null)
                return;

            for (int i = 0; i < edges.Count; i++)
            {
                WallEdgeSaveData edge = edges[i];
                if (edge == null || string.IsNullOrEmpty(edge.prefabId))
                    continue;

                counts.TryGetValue(edge.prefabId, out int n);
                counts[edge.prefabId] = n + 1;
            }
        }

        private static int EstimateStreamingPeak(
            MapSaveJsonDto dto,
            TilePoolStreamEstimate estimate,
            int totalTiles)
        {
            int occupiedChunks = CountOccupiedChunks(dto, estimate.ChunkSize);
            int tilesPerChunk = occupiedChunks > 0
                ? Mathf.Max(1, Mathf.CeilToInt((float)totalTiles / occupiedChunks))
                : totalTiles;

            int camRadius = TileViewportBounds.ComputeCameraChunkRadius(
                estimate.MaxOrthographicSize,
                estimate.CameraAspect,
                estimate.CellSize,
                estimate.ChunkSize,
                estimate.CameraChunkMargin);
            int camSide = camRadius * 2 + 1;
            int loadedChunkCount = camSide * camSide;
            return Mathf.CeilToInt(loadedChunkCount * tilesPerChunk * 1.1f);
        }

        private static int CountOccupiedChunks(MapSaveJsonDto dto, int chunkSize)
        {
            var chunks = new HashSet<Vector2Int>();

            if (dto.tiles != null)
            {
                for (int i = 0; i < dto.tiles.Count; i++)
                {
                    TileSaveData tile = dto.tiles[i];
                    if (tile == null)
                        continue;

                    chunks.Add(TileChunkCoord.FromCell(new Vector3Int(tile.x, tile.y, tile.z), chunkSize));
                }
            }

            if (dto.wallEdges != null)
            {
                for (int i = 0; i < dto.wallEdges.Count; i++)
                {
                    WallEdgeSaveData edge = dto.wallEdges[i];
                    if (edge == null)
                        continue;

                    chunks.Add(TileChunkCoord.FromCell(new Vector3Int(edge.x, edge.y, edge.z), chunkSize));
                }
            }

            return chunks.Count;
        }

        private static int MbToInstances(float megabytes, int bytesPerInstance) =>
            Mathf.Max(0, Mathf.FloorToInt(megabytes * 1024f * 1024f / bytesPerInstance));
    }
}

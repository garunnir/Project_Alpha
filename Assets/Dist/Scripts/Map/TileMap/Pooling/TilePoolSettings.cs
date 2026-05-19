namespace IsoTilemap
{
    /// <summary>맵 로드 1회 타일 풀 예산 설정.</summary>
    public readonly struct TilePoolSettings
    {
        public readonly int MaxPooledInstances;
        public readonly float MaxPoolMemoryMb;
        public readonly int EstimatedBytesPerInstance;
        public readonly float ReserveRatio;
        public readonly int MinPoolPerPrefab;
        public readonly int MaxPoolPerPrefab;
        public readonly int StreamingPeakOverride;

        public TilePoolSettings(
            int maxPooledInstances,
            float maxPoolMemoryMb,
            int estimatedBytesPerInstance,
            float reserveRatio,
            int minPoolPerPrefab,
            int maxPoolPerPrefab,
            int streamingPeakOverride)
        {
            MaxPooledInstances = UnityEngine.Mathf.Max(0, maxPooledInstances);
            MaxPoolMemoryMb = UnityEngine.Mathf.Max(0f, maxPoolMemoryMb);
            EstimatedBytesPerInstance = UnityEngine.Mathf.Max(1024, estimatedBytesPerInstance);
            ReserveRatio = UnityEngine.Mathf.Clamp(reserveRatio, 0f, 0.5f);
            MinPoolPerPrefab = UnityEngine.Mathf.Max(0, minPoolPerPrefab);
            MaxPoolPerPrefab = UnityEngine.Mathf.Max(1, maxPoolPerPrefab);
            StreamingPeakOverride = UnityEngine.Mathf.Max(0, streamingPeakOverride);
        }
    }

    /// <summary>스트리밍 피크 인스턴스 추정용 청크 파라미터.</summary>
    public readonly struct TilePoolStreamEstimate
    {
        public readonly int ChunkSize;
        public readonly int CameraChunkMargin;
        public readonly float MaxOrthographicSize;
        public readonly float CameraAspect;
        public readonly float CellSize;

        public TilePoolStreamEstimate(
            int chunkSize,
            int cameraChunkMargin,
            float maxOrthographicSize,
            float cameraAspect,
            float cellSize)
        {
            ChunkSize = UnityEngine.Mathf.Max(1, chunkSize);
            CameraChunkMargin = UnityEngine.Mathf.Max(0, cameraChunkMargin);
            MaxOrthographicSize = UnityEngine.Mathf.Max(0.01f, maxOrthographicSize);
            CameraAspect = UnityEngine.Mathf.Max(0.01f, cameraAspect);
            CellSize = UnityEngine.Mathf.Max(1e-4f, cellSize);
        }
    }
}

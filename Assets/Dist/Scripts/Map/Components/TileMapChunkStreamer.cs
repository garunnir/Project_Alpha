// ============================================================
// TileMapChunkStreamer — 카메라∪플레이어 → desired 청크 → StreamingVisualizer
// ============================================================

using System.Collections.Generic;
using IsoTilemap;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 청크 스트리밍 런타임 틱. <see cref="TileMapStreamingVisualizer"/>에 desired 청크를 전달합니다.
/// Inspector 설정은 본 컴포넌트, 조립은 <see cref="TileMapManager"/>가 Attach로 연결합니다.
/// </summary>
[DisallowMultipleComponent]
public class TileMapChunkStreamer : MonoBehaviour
{
    [Header("Chunk")]
    [SerializeField, Min(1)] private int _chunkSize = 16;
    [SerializeField, Min(0)] private int _playerChunkRadius = 2;
    [SerializeField, Min(0)] private int _cameraChunkMargin = 2;
    [Tooltip("desired에서 벗어난 청크를 언로드하기 전 대기 시간(초). 0이면 즉시 언로드.")]
    [SerializeField, Min(0f)] private float _unloadHysteresisSeconds = 0.35f;

    [Header("References")]
    [Tooltip("비어 있으면 Attach 시 TileMapManager의 Character State를 사용합니다.")]
    [SerializeField] private CharacterState _player;
    [SerializeField] private Camera _gameCamera;
    [SerializeField] private CinemachineCamera _cinemachineCamera;

    private TileMapStreamingVisualizer _visualizer;
    private float _cellSize;
    private CharacterState _activePlayer;
    private bool _isAttached;

    private readonly HashSet<Vector2Int> _desiredChunks = new();
    private readonly HashSet<Vector2Int> _effectiveChunks = new();
    private readonly Dictionary<Vector2Int, float> _unloadAfter = new();
    private readonly List<Vector2Int> _unloadKeysScratch = new();

    public int ChunkSize => Mathf.Max(1, _chunkSize);
    public int PlayerChunkRadius => Mathf.Max(0, _playerChunkRadius);
    public int CameraChunkMargin => Mathf.Max(0, _cameraChunkMargin);

    void Awake() => enabled = false;

    /// <summary>런타임에 생성된 스트리밍 visualizer를 연결하고 틱을 시작합니다.</summary>
    public void Attach(TileMapStreamingVisualizer visualizer, float cellSize, CharacterState playerFallback = null)
    {
        _visualizer = visualizer;
        _cellSize = Mathf.Max(1e-4f, cellSize);
        _activePlayer = _player != null ? _player : playerFallback;
        _isAttached = visualizer != null;
        _unloadAfter.Clear();
        enabled = _isAttached;
    }

    public void Shutdown()
    {
        _visualizer = null;
        _activePlayer = null;
        _isAttached = false;
        _unloadAfter.Clear();
        enabled = false;
    }

    public void SyncNow() => ApplyDesiredChunks();

    private void LateUpdate() => ApplyDesiredChunks();

    private void ApplyDesiredChunks()
    {
        if (!_isAttached || _visualizer == null)
            return;

        _desiredChunks.Clear();
        Camera cam = ResolveCamera();
        float groundY = _activePlayer != null ? _activePlayer.BodyWorldPoint.y : 0f;

        TileViewportBounds.AppendCameraChunks(
            _desiredChunks,
            cam,
            _cinemachineCamera,
            _cellSize,
            ChunkSize,
            CameraChunkMargin,
            groundY);

        if (_activePlayer != null)
        {
            TileViewportBounds.AppendPlayerChunks(
                _desiredChunks,
                _activePlayer.GridPos,
                ChunkSize,
                PlayerChunkRadius);
        }

        BuildEffectiveChunks();
        _visualizer.SyncDesiredChunks(_effectiveChunks);
    }

    private void BuildEffectiveChunks()
    {
        _effectiveChunks.Clear();
        _effectiveChunks.UnionWith(_desiredChunks);

        if (_unloadHysteresisSeconds <= 0f)
        {
            _unloadAfter.Clear();
            return;
        }

        float now = Time.time;
        foreach (Vector2Int chunk in _visualizer.LoadedChunks)
        {
            if (_desiredChunks.Contains(chunk))
            {
                _unloadAfter.Remove(chunk);
                continue;
            }

            if (!_unloadAfter.ContainsKey(chunk))
                _unloadAfter[chunk] = now + _unloadHysteresisSeconds;
        }

        _unloadKeysScratch.Clear();
        foreach (var kv in _unloadAfter)
        {
            if (_desiredChunks.Contains(kv.Key))
                continue;

            if (now < kv.Value)
                _effectiveChunks.Add(kv.Key);
            else
                _unloadKeysScratch.Add(kv.Key);
        }

        for (int i = 0; i < _unloadKeysScratch.Count; i++)
            _unloadAfter.Remove(_unloadKeysScratch[i]);
    }

    private Camera ResolveCamera()
    {
        if (_gameCamera != null)
            return _gameCamera;

        if (_cinemachineCamera != null &&
            _cinemachineCamera.TryGetComponent(out Camera cam))
            return cam;

        return Camera.main;
    }
}

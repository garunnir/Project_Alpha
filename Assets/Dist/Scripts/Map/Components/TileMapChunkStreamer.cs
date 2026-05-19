// ============================================================

// TileMapChunkStreamer — 스트리밍 런타임 틱(카메라∪플레이어 → desired 청크)

// 설정·조립은 TileMapManager, 본 컴포넌트는 LateUpdate 동기화만 담당합니다.

// ============================================================

using System.Collections.Generic;

using IsoTilemap;

using Unity.Cinemachine;

using UnityEngine;



/// <summary>TileMapManager가 주입하는 스트리밍 런타임 설정(Inspector 단일 진실원).</summary>

public readonly struct TileChunkStreamSettings

{

    public readonly float CellSize;

    public readonly int ChunkSize;

    public readonly int PlayerChunkRadius;

    public readonly int CameraChunkMargin;

    public readonly float UnloadHysteresisSeconds;

    public readonly Camera GameCamera;

    public readonly CinemachineCamera CinemachineCamera;

    public readonly CharacterState Player;



    public TileChunkStreamSettings(

        float cellSize,

        int chunkSize,

        int playerChunkRadius,

        int cameraChunkMargin,

        float unloadHysteresisSeconds,

        Camera gameCamera,

        CinemachineCamera cinemachineCamera,

        CharacterState player)

    {

        CellSize = Mathf.Max(1e-4f, cellSize);

        ChunkSize = Mathf.Max(1, chunkSize);

        PlayerChunkRadius = Mathf.Max(0, playerChunkRadius);

        CameraChunkMargin = Mathf.Max(0, cameraChunkMargin);

        UnloadHysteresisSeconds = Mathf.Max(0f, unloadHysteresisSeconds);

        GameCamera = gameCamera;

        CinemachineCamera = cinemachineCamera;

        Player = player;

    }

}



[DisallowMultipleComponent]

public class TileMapChunkStreamer : MonoBehaviour

{
    [Header("청크단위로 타일을 스트리밍 하는 컴포넌트입니다.")]
    private TileMapStreamingVisualizer _visualizer;

    private TileChunkStreamSettings _settings;

    private bool _isConfigured;

    private readonly HashSet<Vector2Int> _desiredChunks = new();

    private readonly HashSet<Vector2Int> _effectiveChunks = new();

    private readonly Dictionary<Vector2Int, float> _unloadAfter = new();

    private readonly List<Vector2Int> _unloadKeysScratch = new();



    public void Configure(TileMapStreamingVisualizer visualizer, TileChunkStreamSettings settings)

    {

        _visualizer = visualizer;

        _settings = settings;

        _isConfigured = visualizer != null;

        _unloadAfter.Clear();

        enabled = _isConfigured;

    }



    public void Shutdown()

    {

        _visualizer = null;

        _isConfigured = false;

        _unloadAfter.Clear();

        enabled = false;

    }



    public void SyncNow() => ApplyDesiredChunks();



    private void LateUpdate() => ApplyDesiredChunks();



    private void ApplyDesiredChunks()

    {

        if (!_isConfigured || _visualizer == null)

            return;



        _desiredChunks.Clear();

        Camera cam = ResolveCamera();

        float groundY = _settings.Player != null ? _settings.Player.BodyWorldPoint.y : 0f;



        TileViewportBounds.AppendCameraChunks(

            _desiredChunks,

            cam,

            _settings.CinemachineCamera,

            _settings.CellSize,

            _settings.ChunkSize,

            _settings.CameraChunkMargin,

            groundY);



        if (_settings.Player != null)

        {

            TileViewportBounds.AppendPlayerChunks(

                _desiredChunks,

                _settings.Player.GridPos,

                _settings.ChunkSize,

                _settings.PlayerChunkRadius);

        }



        BuildEffectiveChunks();

        _visualizer.SyncDesiredChunks(_effectiveChunks);

    }



    private void BuildEffectiveChunks()

    {

        _effectiveChunks.Clear();

        _effectiveChunks.UnionWith(_desiredChunks);



        if (_settings.UnloadHysteresisSeconds <= 0f)
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

                _unloadAfter[chunk] = now + _settings.UnloadHysteresisSeconds;

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

        if (_settings.GameCamera != null)

            return _settings.GameCamera;



        if (_settings.CinemachineCamera != null &&

            _settings.CinemachineCamera.TryGetComponent(out Camera cam))

            return cam;



        return Camera.main;

    }

}



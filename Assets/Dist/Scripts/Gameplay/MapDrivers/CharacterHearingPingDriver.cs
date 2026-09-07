// ============================================================
// CharacterHearingPingDriver — possessed 청각 핑 (Vision 우선, 페이드 숨김 gate)
// ============================================================

using System.Collections.Generic;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEngine;

[DefaultExecutionOrder(52)]
[DisallowMultipleComponent]
public sealed class CharacterHearingPingDriver : MonoBehaviour, IMapHearingPingDriver
{
    [SerializeField] CharacterState _playerState;
    [SerializeField] Transform _playerBody;
    [SerializeField] TileMapManager _tileMapManager;
    [SerializeField] CharacterHearingPingSettings _settings = CharacterHearingPingSettings.DefaultUnity;
    [SerializeField] bool _drawPlayGizmos = true;

    MapHearingPingHost _pingHost;
    CharacterVision _playerVision;
    CharacterHearing _playerHearing;
    CharacterFactionHost _playerFaction;
    bool _isActive;

    readonly System.Collections.Generic.Dictionary<Vector3Int, float> _displayAlpha = new(16);
    readonly System.Collections.Generic.List<Vector3Int> _scratchCells = new(16);

    public void SetPlayerState(CharacterState playerState)
    {
        _playerState = playerState;
        _playerVision = null;
        _playerHearing = null;
        _playerFaction = null;
        if (_playerState != null)
        {
            _playerVision = CharacterBodyResolve.GetInBody<CharacterVision>(_playerState);
            _playerHearing = CharacterBodyResolve.GetInBody<CharacterHearing>(_playerState);
            _playerFaction = CharacterBodyResolve.GetInBody<CharacterFactionHost>(_playerState);
        }
    }

    public void SetPlayerBody(Transform playerBody) => _playerBody = playerBody;

    public void Init(TileMapManager map)
    {
        _tileMapManager = map;
        _pingHost = map != null ? map.GetComponent<MapHearingPingHost>() : null;
        if (_pingHost == null && map != null)
            _pingHost = map.gameObject.AddComponent<MapHearingPingHost>();

        float cellSize = map != null && map.WorldGrid != null
            ? map.WorldGrid.CellSize
            : 1f;
        _pingHost?.BindMapContext(cellSize);
        _pingHost?.ConfigureDraw(
            _settings.QuadSizeMeters,
            _settings.YOffsetMeters,
            _settings.MaxAlpha);
        _isActive = _pingHost != null;
    }

    public void Shutdown()
    {
        _isActive = false;
        _displayAlpha.Clear();
        _pingHost?.Overlay.Clear();
    }

    void LateUpdate()
    {
        if (!_isActive || _pingHost == null || _playerState == null || _playerBody == null)
            return;

        EnsurePlayerComponents();
        if (_playerHearing == null || _playerVision == null)
            return;

        MapHearingPingOverlay overlay = _pingHost.Overlay;
        overlay.Clear();
        _scratchCells.Clear();

        if (GameplayData.Traits != null && GameplayData.Traits.Has(TraitIds.Omnivision))
            return;

        float dt = TimeScaleService.Delta(TimeScaleChannel.World);
        float cellSize = _pingHost.CellSize;
        Vector3 listenerFeet = CharacterFeetPose.GetFeetWorld(_playerBody);
        Vector3 forward = CharacterSightForward.ResolveXZ(_playerState, _playerBody);

        GameObject possessedGo = _playerBody.gameObject;
        float hiddenThreshold = Mathf.Max(0f, _settings.HiddenThreshold);
        float fadeSpeed = Mathf.Max(0f, _settings.DisplayFadePerSecond);

        for (int i = 0; i < CharacterBodyHost.ActiveCount; i++)
        {
            CharacterBodyHost bodyHost = CharacterBodyHost.GetActive(i);
            if (bodyHost == null || bodyHost.gameObject == possessedGo)
                continue;
            if (!IsPreferredHostile(bodyHost))
                continue;
            CharacterSightFadeHost fadeHost = CharacterBodyResolve.GetInBody<CharacterSightFadeHost>(bodyHost);
            if (fadeHost == null)
                continue;
            if (!CharacterBodyResolve.TryGetInBody(bodyHost, out CharacterMotor targetMotor))
                continue;

            Vector3 targetFeet = CharacterFeetPose.GetFeetWorld(bodyHost.transform);
            bool visionActive = _playerVision.CanDetect(listenerFeet, forward, targetFeet)
                || _playerVision.CanKeepTarget(listenerFeet, forward, targetFeet);
            bool hearingActive = _playerHearing.TryEvaluateAudibility(
                listenerFeet,
                targetFeet,
                targetMotor,
                out float audibility);

            if (!CharacterSenseContactResolver.ShowsHearingPing(visionActive, hearingActive))
                continue;
            if (fadeHost.DisplayVisibility > hiddenThreshold)
                continue;

            Vector3Int cell = TileHelper.ConvertWorldToGrid(targetFeet, cellSize);
            Vector3 worldPos = TileHelper.ConvertGridToWorldPos(cell, cellSize);
            overlay.AddOrMax(cell, worldPos, audibility);
            _scratchCells.Add(cell);
        }

        FadeDisplayAlphas(dt, fadeSpeed, overlay);
    }

    void FadeDisplayAlphas(float dt, float fadeSpeed, MapHearingPingOverlay overlay)
    {
        for (int i = 0; i < overlay.Count; i++)
        {
            HearingPingEntry entry = overlay.Entries[i];
            float target = entry.Alpha;
            if (!_displayAlpha.TryGetValue(entry.Cell, out float display))
                display = target;

            if (fadeSpeed <= 0f || dt <= 0f)
                display = target;
            else
                display = Mathf.MoveTowards(display, target, fadeSpeed * dt);

            _displayAlpha[entry.Cell] = display;
            overlay.AddOrMax(entry.Cell, entry.WorldPos, display);
        }

        for (int i = _scratchCells.Count - 1; i >= 0; i--)
        {
            Vector3Int cell = _scratchCells[i];
            bool stillActive = false;
            for (int j = 0; j < overlay.Count; j++)
            {
                if (overlay.Entries[j].Cell == cell)
                {
                    stillActive = true;
                    break;
                }
            }

            if (stillActive)
                continue;

            if (!_displayAlpha.TryGetValue(cell, out float display))
                continue;

            if (fadeSpeed <= 0f || dt <= 0f)
            {
                _displayAlpha.Remove(cell);
                continue;
            }

            display = Mathf.MoveTowards(display, 0f, fadeSpeed * dt);
            if (display <= 0f)
            {
                _displayAlpha.Remove(cell);
                continue;
            }

            _displayAlpha[cell] = display;
            Vector3 worldPos = TileHelper.ConvertGridToWorldPos(cell, _pingHost.CellSize);
            overlay.AddOrMax(cell, worldPos, display);
        }
    }

    void EnsurePlayerComponents()
    {
        if (_playerVision == null && _playerState != null)
            _playerVision = CharacterBodyResolve.GetInBody<CharacterVision>(_playerState);
        if (_playerHearing == null && _playerState != null)
            _playerHearing = CharacterBodyResolve.GetInBody<CharacterHearing>(_playerState);
        if (_playerFaction == null && _playerState != null)
            _playerFaction = CharacterBodyResolve.GetInBody<CharacterFactionHost>(_playerState);
    }

    bool IsPreferredHostile(CharacterBodyHost host)
    {
        if (host == null || _playerFaction == null)
            return false;
        if (!CharacterBodyResolve.TryGetInBody(host, out CharacterFactionHost otherFaction))
            return false;
        return CharacterHostility.IsHostile(_playerFaction, otherFaction);
    }

    void OnDrawGizmos()
    {
        if (!Application.isPlaying || !_drawPlayGizmos || _pingHost == null)
            return;

        IReadOnlyList<HearingPingEntry> entries = _pingHost.Overlay.Entries;
        float cellSize = _pingHost.CellSize;
        float half = cellSize * 0.45f;
        for (int i = 0; i < entries.Count; i++)
        {
            HearingPingEntry entry = entries[i];
            if (entry.Alpha <= 0f)
                continue;

            var center = entry.WorldPos;
            center.y += _settings.YOffsetMeters;
            Gizmos.color = new Color(0.45f, 0.75f, 1f, entry.Alpha * _settings.MaxAlpha);
            Gizmos.DrawWireCube(center, new Vector3(half * 2f, 0.05f, half * 2f));
        }
    }
}

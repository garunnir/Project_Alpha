using IsoTilemap;
using UnityEngine;

[RequireComponent(typeof(CharacterState))]
public class CharacterVisibilityBroadcaster : MonoBehaviour
{
    private CharacterState _characterState;

    [SerializeField] private TileMapManager _tileMapManager;

    [SerializeField] private OcclusionProximitySettings _occlusionSettings =
        OcclusionProximitySettings.DefaultUnity;

    private void Awake()
    {
        _characterState = GetComponent<CharacterState>();
        if (_tileMapManager == null)
            Debug.LogWarning("CharacterVisibilityBroadcaster: TileMapManager 참조 없음.");
    }

    private void OnEnable()
    {
        if (_characterState != null)
        {
            _characterState.WorldPoseChanged += BroadcastOcclusionFromWorldPose;
            SyncSettingsCellFromCharacterIfNeeded();
        }
    }

    private void OnDisable()
    {
        if (_characterState != null)
            _characterState.WorldPoseChanged -= BroadcastOcclusionFromWorldPose;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_characterState == null)
            _characterState = GetComponent<CharacterState>();

        SyncSettingsCellFromCharacterIfNeeded();
    }
#endif

    private void SyncSettingsCellFromCharacterIfNeeded()
    {
        if (_characterState == null) return;

        OcclusionProximitySettings settings = _occlusionSettings;
        settings.CellSize = _characterState.GridCellSize;
        _occlusionSettings = settings;
    }

    /// <summary>월드 포즈만 넘김 → 모델이 그리드/BFS 및 거리 매핑.</summary>
    private void BroadcastOcclusionFromWorldPose(Vector3 worldPosition)
    {
        SyncSettingsCellFromCharacterIfNeeded();

        OcclusionProximitySettings settings = _occlusionSettings;

        settings.CellSize = Mathf.Max(1e-4f, _characterState.GridCellSize);
        NormalizeSettings(ref settings);
        _occlusionSettings = settings;

        _tileMapManager.Model?.UpdateOcclusionFromPlayerWorld(worldPosition, settings);
    }

    private static void NormalizeSettings(ref OcclusionProximitySettings s)
    {
        if (s.OcclusionFullWithinDistance > s.OcclusionNoneBeyondDistance)
        {
            float t = s.OcclusionFullWithinDistance;
            s.OcclusionFullWithinDistance = s.OcclusionNoneBeyondDistance;
            s.OcclusionNoneBeyondDistance = t;
        }

        if (Mathf.Abs(s.OcclusionNoneBeyondDistance - s.OcclusionFullWithinDistance) < 1e-4f)
            s.OcclusionNoneBeyondDistance = s.OcclusionFullWithinDistance + 1e-3f;
    }
}

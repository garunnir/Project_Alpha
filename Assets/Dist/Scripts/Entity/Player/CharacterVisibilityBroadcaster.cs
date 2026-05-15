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
            _characterState.WorldPoseChanged += OnWorldPoseChanged;
            _characterState.AimWorldPointChanged += OnAimWorldPointChanged;
            SyncSettingsCellFromCharacterIfNeeded();
        }
    }

    private void OnDisable()
    {
        if (_characterState != null)
        {
            _characterState.WorldPoseChanged -= OnWorldPoseChanged;
            _characterState.AimWorldPointChanged -= OnAimWorldPointChanged;
        }
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

    private void OnWorldPoseChanged(Vector3 _) => BroadcastOcclusion();

    private void OnAimWorldPointChanged(Vector3 _) => BroadcastOcclusion();

    /// <summary>조준 중에는 <see cref="CharacterState.AimWorldPoint"/>, 아니면 <see cref="CharacterState.BodyWorldPoint"/>로 BFS·거리 오클루전 갱신.</summary>
    private void BroadcastOcclusion()
    {
        if (_characterState == null) return;

        SyncSettingsCellFromCharacterIfNeeded();

        OcclusionProximitySettings settings = _occlusionSettings;

        settings.CellSize = Mathf.Max(1e-4f, _characterState.GridCellSize);
        NormalizeSettings(ref settings);
        _occlusionSettings = settings;

        Vector3 occlusionWorld = _characterState.IsAiming
            ? _characterState.AimWorldPoint
            : _characterState.BodyWorldPoint;

        _tileMapManager.Model?.UpdateOcclusionFromPlayerWorld(occlusionWorld, settings);
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

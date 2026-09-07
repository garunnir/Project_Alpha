// ============================================================
// FarmWorkClipCatalog — 농사 Work 모션 클립·대기시간 SSOT
// ============================================================

using IsoTilemap;
using UnityEngine;

[CreateAssetMenu(fileName = "FarmWorkClipCatalog", menuName = "Dist/Farming/Work Clip Catalog")]
public sealed class FarmWorkClipCatalog : ScriptableObject
{
    public const string DefaultAssetPath =
        "Assets/Dist/SOData/Gameplay/Farming/FarmWorkClipCatalog.asset";

    [SerializeField] AnimationClip _plant;
    [SerializeField] AnimationClip _till;
    [SerializeField] AnimationClip _harvest;
    [SerializeField] AnimationClip _chop;
    [SerializeField, Min(0f)] float _plantDurationSeconds = MapPlantConsts.PlantWorkDurationSeconds;
    [SerializeField, Min(0f)] float _tillDurationSeconds = MapPlantConsts.TillWorkDurationSeconds;
    [SerializeField, Min(0f)] float _chopDurationSeconds = MapPlantConsts.ChopWorkDurationSeconds;

    /// <summary>MapGameplayBootstrap이 주입. Resources 폴백 없음.</summary>
    public static FarmWorkClipCatalog Runtime { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void DomainReset() => Runtime = null;

    public static void BindRuntime(FarmWorkClipCatalog catalog) => Runtime = catalog;

    public AnimationClip Resolve(FarmCellActionKind kind)
    {
        switch (kind)
        {
            case FarmCellActionKind.Plant:
                return _plant;
            case FarmCellActionKind.Till:
                return _till;
            case FarmCellActionKind.Harvest:
                return _harvest;
            case FarmCellActionKind.Chop:
                return _chop;
            default:
                return null;
        }
    }

    public float ResolveDuration(FarmCellActionKind kind)
    {
        switch (kind)
        {
            case FarmCellActionKind.Plant:
                return Mathf.Max(0f, _plantDurationSeconds);
            case FarmCellActionKind.Till:
                return Mathf.Max(0f, _tillDurationSeconds);
            case FarmCellActionKind.Chop:
                return Mathf.Max(0f, _chopDurationSeconds);
            default:
                return 0f;
        }
    }
}

#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Work 클립 카탈로그 변경 알림. S6: Mecanim Work Layer 상태 자동 동기화는 재생 계약이 아님
/// (Animancer Play(clip)). Rebuild/Ensure Work Layer는 S7 전 remnant 유지용으로 수동·MCP만.
/// </summary>
public sealed class WorkLayerCatalogPostprocessor : AssetPostprocessor
{
    static readonly string[] CatalogSuffixes =
    {
        "/VaultClipCatalog.asset",
        "/FarmWorkClipCatalog.asset",
        "/FishWorkClipCatalog.asset",
    };

    static void OnPostprocessAllAssets(
        string[] importedAssets,
        string[] deletedAssets,
        string[] movedAssets,
        string[] movedFromAssetPaths)
    {
        if (!ShouldRefresh(importedAssets) && !ShouldRefresh(movedAssets))
            return;

        // S6: playback no longer requires controller states named after clips.
        // Remnant Ensure stays available via Dist/MCP/Ensure Work Layer until S7.
    }

    static bool ShouldRefresh(string[] paths)
    {
        if (paths == null)
            return false;

        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];
            if (string.IsNullOrEmpty(path))
                continue;

            for (int s = 0; s < CatalogSuffixes.Length; s++)
            {
                if (path.EndsWith(CatalogSuffixes[s]))
                    return true;
            }
        }

        return false;
    }
}

/// <summary>
/// Play 진입 전 Work 계약 검사 — S6: Animancer Work 능력(카탈로그 에셋 존재).
/// Mecanim Work Layer state machine / clip.name 상태 일치는 재생 필수 조건이 아니다.
/// </summary>
[InitializeOnLoad]
static class WorkLayerPlayModeContractGuard
{
    static WorkLayerPlayModeContractGuard() =>
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;

    static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change != PlayModeStateChange.ExitingEditMode)
            return;

        ValidateAnimancerWorkContract();
    }

    static void ValidateAnimancerWorkContract()
    {
        List<string> missing = new();
        RequireCatalog<VaultClipCatalog>(VaultClipCatalog.DefaultAssetPath, "VaultClipCatalog", missing);
        RequireCatalog<FarmWorkClipCatalog>(FarmWorkClipCatalog.DefaultAssetPath, "FarmWorkClipCatalog", missing);
        RequireCatalog<FishWorkClipCatalog>(FishWorkClipCatalog.DefaultAssetPath, "FishWorkClipCatalog", missing);

        if (missing.Count > 0)
        {
            Debug.LogError(
                "[WorkLayerContract] Work catalog asset(s) missing (Animancer Play needs clip catalogs): " +
                string.Join(", ", missing));
            return;
        }

        // Replacement of Mecanim-only Work Layer SM checks:
        // Playback = CharacterWorkLayerAnim → Hybrid Layers[Work] Play(clip).
        // Controller Work Layer states / IK Pass / Ensure Work Layer are remnant until S7 —
        // not required for TryPlay when Hybrid + AnimationClip are wired.
    }

    static void RequireCatalog<T>(string path, string label, List<string> missing)
        where T : ScriptableObject
    {
        if (AssetDatabase.LoadAssetAtPath<T>(path) == null)
            missing.Add($"{label} @ {path}");
    }
}
#endif

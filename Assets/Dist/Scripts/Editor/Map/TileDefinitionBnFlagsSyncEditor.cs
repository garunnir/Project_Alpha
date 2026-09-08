#if UNITY_EDITOR
// ============================================================
// TileDefinitionBnFlagsSyncEditor — BN terrain MINEABLE/DIGGABLE → TileDefinition
// ============================================================

using System;
using System.Collections.Generic;
using System.Text;
using BnTerrainData = Garunnir.Runtime.Gameplay.Data.TerrainData;
using Garunnir.Runtime.Gameplay.Data;
using IsoTilemap;
using UnityEditor;
using UnityEngine;

namespace IsoTilemap.EditorTools
{
    public static class TileDefinitionBnFlagsSyncEditor
    {
        static readonly string[] SyncFlags = { TileFlags.Mineable, TileFlags.Diggable };

        /// <summary>
        /// Dist-owned prefabId → BN terrain id. Heuristic <c>t_</c>+snake는 Dist 경로
        /// (<c>Floor/Floor</c>→<c>t_floor</c>)에 오매칭하므로 alias만 승격한다.
        /// </summary>
        static readonly (string PrefabId, string BnTerrainId)[] DistPrefabBnAliases =
        {
            ("Floor/GrassFloor", "t_grass"),
        };

        /// <summary>
        /// Dist-authored dig floors: skip BN flag sync so empty BN (e.g. <c>t_floor</c>)
        /// cannot strip MINEABLE / Dist combat authoring.
        /// </summary>
        static readonly string[] DistAuthoredDigPrefabIds =
        {
            "Floor/Floor",
            "Floor/Tilled",
        };

        [MenuItem("Tools/Map/Sync TileDefinition flags from BN")]
        static void SyncTileDefinitionFlagsFromBn()
        {
            var dbs = Resources.FindObjectsOfTypeAll<TilePrefabDB>();
            int updated = 0;
            int skipped = 0;

            for (int d = 0; d < dbs.Length; d++)
            {
                TilePrefabDB db = dbs[d];
                if (db?.entries == null)
                    continue;

                for (int i = 0; i < db.entries.Count; i++)
                {
                    TileDefinition def = db.entries[i];
                    if (def == null || string.IsNullOrEmpty(def.prefabId))
                    {
                        skipped++;
                        continue;
                    }

                    if (IsDistAuthoredDigPrefab(def.prefabId))
                    {
                        skipped++;
                        continue;
                    }

                    if (!TryResolveTerrain(def, out BnTerrainData terrain))
                    {
                        skipped++;
                        continue;
                    }

                    if (def.flags == null)
                        def.flags = new List<string>();

                    bool changed = false;
                    for (int f = 0; f < SyncFlags.Length; f++)
                    {
                        string flag = SyncFlags[f];
                        bool want = TerrainHasFlag(terrain, flag);
                        bool have = TileFlags.HasFlag(def, flag);
                        if (want == have)
                            continue;

                        if (want)
                            def.flags.Add(flag);
                        else
                            RemoveFlagIgnoreCase(def.flags, flag);

                        changed = true;
                    }

                    if (!changed)
                        continue;

                    EditorUtility.SetDirty(def);
                    updated++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[TileDefinitionBnFlagsSync] 완료: 갱신 {updated}개, 스킵 {skipped}개");
        }

        static bool IsDistAuthoredDigPrefab(string prefabId)
        {
            for (int i = 0; i < DistAuthoredDigPrefabIds.Length; i++)
            {
                if (string.Equals(prefabId, DistAuthoredDigPrefabIds[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        static bool TryResolveTerrain(TileDefinition def, out BnTerrainData terrain)
        {
            terrain = null;
            foreach (string candidate in CandidateBnIds(def))
            {
                terrain = GameplayData.GetTerrain(candidate);
                if (terrain != null)
                    return true;
            }

            return false;
        }

        static IEnumerable<string> CandidateBnIds(TileDefinition def)
        {
            if (!string.IsNullOrEmpty(def.prefabId))
            {
                // Dist path aliases first (authoritative for Dist-owned floors).
                for (int a = 0; a < DistPrefabBnAliases.Length; a++)
                {
                    if (string.Equals(def.prefabId, DistPrefabBnAliases[a].PrefabId, StringComparison.Ordinal))
                        yield return DistPrefabBnAliases[a].BnTerrainId;
                }

                yield return def.prefabId;

                // BN ids are typically `t_*` / bare tokens without Dist category slash.
                // Do not invent `t_`+snake from `Floor/Floor` — that false-matches `t_floor`.
                if (def.prefabId.IndexOf('/') < 0)
                {
                    yield return "t_" + ToSnakeCase(def.prefabId);
                }
            }

            if (!string.IsNullOrEmpty(def.name) &&
                (string.IsNullOrEmpty(def.prefabId) ||
                 !string.Equals(def.name, def.prefabId, StringComparison.Ordinal)))
            {
                if (def.name.IndexOf('/') < 0 &&
                    def.name.StartsWith("t_", StringComparison.OrdinalIgnoreCase))
                    yield return def.name;
            }
        }

        static string ToSnakeCase(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            var sb = new StringBuilder(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (char.IsUpper(c) && i > 0)
                    sb.Append('_');
                sb.Append(char.ToLowerInvariant(c));
            }

            return sb.ToString();
        }

        static bool TerrainHasFlag(BnTerrainData terrain, string flag)
        {
            if (terrain?.flags == null || string.IsNullOrEmpty(flag))
                return false;

            for (int i = 0; i < terrain.flags.Count; i++)
            {
                string value = terrain.flags[i];
                if (!string.IsNullOrEmpty(value) &&
                    value.Equals(flag, StringComparison.OrdinalIgnoreCase))
                    return true;
            }

            return false;
        }

        static bool RemoveFlagIgnoreCase(List<string> flags, string flag)
        {
            for (int i = flags.Count - 1; i >= 0; i--)
            {
                string value = flags[i];
                if (!string.IsNullOrEmpty(value) &&
                    value.Equals(flag, StringComparison.OrdinalIgnoreCase))
                {
                    flags.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
    }
}
#endif

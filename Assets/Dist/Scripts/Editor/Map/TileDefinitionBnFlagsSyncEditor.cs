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
                yield return def.prefabId;
                string tail = def.prefabId;
                int slash = def.prefabId.LastIndexOf('/');
                if (slash >= 0)
                    tail = def.prefabId.Substring(slash + 1);

                yield return tail;
                yield return "t_" + ToSnakeCase(tail);
                yield return def.prefabId.Replace('/', '_');
            }

            if (def.name != null)
            {
                yield return def.name;
                yield return "t_" + ToSnakeCase(def.name);
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

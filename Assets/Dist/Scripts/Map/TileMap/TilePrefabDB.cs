using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace IsoTilemap
{
    // id → prefab
    [CreateAssetMenu(menuName = "Iso/Tile Prefab DB")]
    public class TilePrefabDB : ScriptableObject
    {
        public List<TileDefinition> entries = new List<TileDefinition>();

        private Dictionary<string, GameObject> _cache;

        static readonly List<TilePrefabDB> RuntimeDbs = new();
        static TilePrefabDB[] _findFallbackCache;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticState()
        {
            RuntimeDbs.Clear();
            _findFallbackCache = null;
        }

        void OnEnable()
        {
            BuildCache();
            RegisterRuntimeInstance(this);
        }

        void OnDisable()
        {
            RuntimeDbs.Remove(this);
        }

        /// <summary>씬/맵 부트스트랩에서 SO OnEnable 이전 호출 시 레지스트리 보강.</summary>
        public static void RegisterRuntime(TilePrefabDB db) => RegisterRuntimeInstance(db);

        static void RegisterRuntimeInstance(TilePrefabDB db)
        {
            if (db == null || RuntimeDbs.Contains(db))
                return;

            RuntimeDbs.Add(db);
        }

        static IReadOnlyList<TilePrefabDB> GetRuntimeDbs()
        {
            if (RuntimeDbs.Count > 0)
                return RuntimeDbs;

            if (_findFallbackCache == null)
                _findFallbackCache = Resources.FindObjectsOfTypeAll<TilePrefabDB>();

            for (int i = 0; i < _findFallbackCache.Length; i++)
                RegisterRuntimeInstance(_findFallbackCache[i]);

            return RuntimeDbs;
        }

        void BuildCache()
        {
            _cache = new Dictionary<string, GameObject>();
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (!string.IsNullOrEmpty(e.prefabId) && e.prefab != null)
                    _cache[e.prefabId] = e.prefab;
            }
        }

        public GameObject GetPrefab(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (_cache == null) BuildCache();
            if (_cache != null && _cache.TryGetValue(id, out var prefab))
                return prefab;
            // Visual/Prefab/Map 등에서 파일명만 id로 저장된 경우(예: SlimWall_WN)에 대한 폴백
            foreach (var e in entries)
            {
                if (e == null || e.prefab == null || string.IsNullOrEmpty(e.prefabId)) continue;
                if (e.prefab.name == id)
                    return e.prefab;
            }
            return null;
        }

        public bool TryGetDefinition(string id, out TileDefinition def)
        {
            def = null;
            if (string.IsNullOrEmpty(id))
                return false;

            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e == null)
                    continue;

                if (!string.Equals(e.prefabId, id, StringComparison.Ordinal))
                    continue;

                def = e;
                return true;
            }

            return false;
        }

        public TileDefinition GetDefinitionOrLogError(string prefabId)
        {
            if (TryGetDefinition(prefabId, out var def))
                return def;

            Debug.LogError($"[TilePrefabDB] Definition not found for prefabId='{prefabId}'. All tiles must be registered in TilePrefabDB.");
            return null;
        }

        public bool TryGetDefinitionSize(string id, out Vector3Int size)
        {
            size = Vector3Int.one;
            if (!TryGetDefinition(id, out var def) || def == null)
                return false;

            size = new Vector3Int(
                Mathf.Max(1, def.size.x),
                Mathf.Max(1, def.size.y),
                Mathf.Max(1, def.size.z));
            return true;
        }

        public static bool TryResolveDefinition(string id, out TileDefinition def)
        {
            def = null;
            if (string.IsNullOrEmpty(id))
                return false;

            IReadOnlyList<TilePrefabDB> dbs = GetRuntimeDbs();
            for (int i = 0; i < dbs.Count; i++)
            {
                TilePrefabDB db = dbs[i];
                if (db == null)
                    continue;

                if (db.TryGetDefinition(id, out def))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// 씬 인스턴스/변형 프리팹 에셋으로 TileDefinition.prefabId를 역조회한다.
        /// Floor 변형(ShallowWater 등)처럼 TileView.prefabId가 비어 있을 때 Save Map To JSON용.
        /// </summary>
        public static bool TryResolvePrefabIdByPrefab(GameObject prefabOrInstance, out string prefabId)
        {
            prefabId = null;
            if (prefabOrInstance == null)
                return false;

#if UNITY_EDITOR
            GameObject asset = PrefabUtility.GetCorrespondingObjectFromSource(prefabOrInstance);
            if (asset == null && AssetDatabase.Contains(prefabOrInstance))
                asset = prefabOrInstance;
#else
            GameObject asset = prefabOrInstance;
#endif
            if (asset == null)
                return false;

            IReadOnlyList<TilePrefabDB> dbs = GetRuntimeDbs();
            for (int i = 0; i < dbs.Count; i++)
            {
                TilePrefabDB db = dbs[i];
                if (db?.entries == null)
                    continue;

                for (int e = 0; e < db.entries.Count; e++)
                {
                    TileDefinition def = db.entries[e];
                    if (def == null || def.prefab == null || string.IsNullOrEmpty(def.prefabId))
                        continue;

                    if (def.prefab == asset || def.prefab == prefabOrInstance)
                    {
                        prefabId = def.prefabId;
                        return true;
                    }

#if UNITY_EDITOR
                    GameObject defAsset = PrefabUtility.GetCorrespondingObjectFromSource(def.prefab);
                    if (defAsset != null && defAsset == asset)
                    {
                        prefabId = def.prefabId;
                        return true;
                    }
#endif
                }
            }

            return false;
        }

        public static bool TryResolveDefinitionSize(string id, out Vector3Int size)
        {
            size = Vector3Int.one;
            if (!TryResolveDefinition(id, out var def) || def == null)
                return false;

            size = new Vector3Int(
                Mathf.Max(1, def.size.x),
                Mathf.Max(1, def.size.y),
                Mathf.Max(1, def.size.z));
            return true;
        }
        //public GameObject GetPrefab(int id)
        //{
        //    return entries[id].prefab;
        //}
#if UNITY_EDITOR
        void OnValidate()
        {
            AutoSetPrefabId();
        }

        void AutoSetPrefabId()
        {
            foreach (var e in entries)
            {
                if (e == null) continue;
                if (string.IsNullOrEmpty(e.prefabId) && e.prefab != null)
                {
                    e.prefabId = UnityEditor.Tile.PrefabDBExtensions.GetTilePrefabName(e.prefab);
                    UnityEditor.EditorUtility.SetDirty(e);
                }
            }
        }
#endif
    }

}
#if UNITY_EDITOR
    namespace UnityEditor.Tile
    {
        public static class PrefabDBExtensions
        {
            public static string GetTilePrefabName(GameObject objOrPrefab)
            {
                // 1) 이 오브젝트가 "에셋 안에 있는 프리팹"인지,
                //    아니면 "씬에 깔린 인스턴스"인지 먼저 구분
                UnityEngine.Object asset = null;

                if (AssetDatabase.Contains(objOrPrefab))
                {
                    // 프로젝트창의 .prefab 자체가 넘어온 경우
                    asset = objOrPrefab;
                }
                else
                {
                    // 씬 인스턴스인 경우 → 이 인스턴스의 소스 프리팹(베리언트 포함)을 가져옴
                    asset = PrefabUtility.GetCorrespondingObjectFromSource(objOrPrefab);
                    if (asset == null)
                    {
                        // 프리팹이 아닌 완전한 씬 전용 오브젝트일 수도 있음
                        asset = objOrPrefab;
                    }
                }

                string fullPath = AssetDatabase.GetAssetPath(asset);
                if (string.IsNullOrEmpty(fullPath))
                {
                    // 에셋 경로를 못 찾으면 그냥 이름으로 fallback
                    return objOrPrefab.name;
                }

                const string root = "Assets/Dist/Resources/Prefab/Map/";

                // 확장자 제거한 전체 경로
                string noExt = System.IO.Path.ChangeExtension(fullPath, null);

                if (fullPath.StartsWith(root, StringComparison.Ordinal))
                {
                    // 루트 이하 상대 경로만 사용: "Wall/Wall_1x2" 같은 형태
                    return noExt.Substring(root.Length);
                }
                else
                {
                    // 루트 밖에 있으면 파일명만 사용
                    return System.IO.Path.GetFileNameWithoutExtension(fullPath);
                }
            }
        }
    }
#endif
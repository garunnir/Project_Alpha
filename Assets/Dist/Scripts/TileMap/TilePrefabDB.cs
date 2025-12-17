using System;
using System.Collections.Generic;
using UnityEngine;
namespace IsoTilemap
{
    [Serializable]
    public class TilePrefabEntry
    {
        public string prefabId;
        public GameObject prefab;
    }

    // id → prefab
    [CreateAssetMenu(menuName = "Iso/Tile Prefab DB")]
    public class TilePrefabDB : ScriptableObject
    {
        public List<TilePrefabEntry> entries = new List<TilePrefabEntry>();

        private Dictionary<string, GameObject> _cache;

        void OnEnable()
        {
            BuildCache();
        }

        void BuildCache()
        {
            _cache = new Dictionary<string, GameObject>();
            foreach (var e in entries)
            {
                if (!string.IsNullOrEmpty(e.prefabId) && e.prefab != null)
                {
                    _cache[e.prefabId] = e.prefab;
                }
            }
        }

        public GameObject GetPrefab(string id)
        {
            if (_cache == null) BuildCache();
            return _cache != null && _cache.TryGetValue(id, out var prefab) ? prefab : null;
        }
        public GameObject GetPrefab(int id)
        {
            return entries[id].prefab;
        }
#if UNITY_EDITOR
        void OnValidate()
        {
            AutoSetPrefabId();
        }

        void AutoSetPrefabId()
        {

            foreach (var e in entries)
            {
                if (string.IsNullOrEmpty(e.prefabId) && e.prefab != null)
                {
                    e.prefabId = UnityEditor.Tile.PrefabDBExtensions.GetTilePrefabName(e.prefab);
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
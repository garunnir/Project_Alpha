using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>prefabId별 TileView 스택. cap 초과분은 Release 시 Destroy.</summary>
    public sealed class TileViewPoolRegistry
    {
        private readonly Transform _parent;
        private readonly TilePrefabDB _prefabDB;
        private readonly Dictionary<string, Stack<TileView>> _stacks = new();
        private readonly Dictionary<string, int> _caps = new();

        public TileViewPoolRegistry(Transform parent, TilePrefabDB prefabDB)
        {
            _parent = parent;
            _prefabDB = prefabDB;
        }

        public void RegisterCap(string prefabId, int cap)
        {
            if (string.IsNullOrEmpty(prefabId))
                return;

            _caps[prefabId] = Mathf.Max(0, cap);
        }

        public TileView Get(string prefabId)
        {
            if (string.IsNullOrEmpty(prefabId))
                return null;

            if (_stacks.TryGetValue(prefabId, out Stack<TileView> stack) && stack.Count > 0)
            {
                TileView view = stack.Pop();
                view.gameObject.SetActive(true);
                return view;
            }

            GameObject prefab = _prefabDB?.GetPrefab(prefabId);
            if (prefab == null)
            {
                Debug.LogWarning($"[TileViewPool] No prefab for id: {prefabId}");
                return null;
            }

            var go = Object.Instantiate(prefab, Vector3.zero, Quaternion.identity, _parent);
            TileView created = go.GetComponent<TileView>();
            if (created == null)
                created = go.AddComponent<TileView>();

            return created;
        }

        public void Release(TileView view)
        {
            if (view == null)
                return;

            string prefabId = view.prefabId;
            view.ResetForPool();

            if (string.IsNullOrEmpty(prefabId) ||
                !_caps.TryGetValue(prefabId, out int cap) ||
                cap <= 0)
            {
                Object.Destroy(view.gameObject);
                return;
            }

            if (!_stacks.TryGetValue(prefabId, out Stack<TileView> stack))
            {
                stack = new Stack<TileView>();
                _stacks[prefabId] = stack;
            }

            if (stack.Count >= cap)
            {
                Object.Destroy(view.gameObject);
                return;
            }

            view.gameObject.SetActive(false);
            stack.Push(view);
        }
    }
}

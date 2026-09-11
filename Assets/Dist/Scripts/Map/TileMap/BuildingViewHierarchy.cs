// ============================================================
// BuildingViewHierarchy — Outdoor/ + Buildings/Building_{id} 타일 뷰 부모
// ============================================================
// 청크는 load-set만. 타일 부모는 Building/Outdoor. 런타임 좌표는 월드
// (SetParent worldPositionStays=true). Building 루트 피벗 = min xyz * cellSize.
using System.Collections.Generic;
using UnityEngine;

namespace IsoTilemap
{
    /// <summary>
    /// 타일 컨테이너 아래 <c>Outdoor/</c>·<c>Buildings/Building_{id}</c> 뷰 하이어라키.
    /// </summary>
    public sealed class BuildingViewHierarchy
    {
        public const string OutdoorName = "Outdoor";
        public const string BuildingsName = "Buildings";
        public const string BuildingNamePrefix = "Building_";

        readonly Transform _tileContainer;
        readonly Dictionary<int, Transform> _buildingRoots = new();
        readonly List<Vector3> _pivotChildPosScratch = new();
        readonly List<Quaternion> _pivotChildRotScratch = new();

        Transform _outdoor;
        Transform _buildings;
        BuildingGroupRegistry _registry;
        float _cellSize;

        public Transform TileContainer => _tileContainer;
        public Transform OutdoorRoot => _outdoor;
        public Transform BuildingsRoot => _buildings;

        BuildingViewHierarchy(Transform tileContainer, float cellSize)
        {
            _tileContainer = tileContainer;
            _cellSize = Mathf.Max(1e-4f, cellSize);
            EnsureScaffold();
        }

        /// <summary>컨테이너에 Outdoor/Buildings 스캐폴드를 보장하고 반환합니다.</summary>
        public static BuildingViewHierarchy EnsureUnder(Transform tileContainer, float cellSize)
        {
            if (tileContainer == null)
                throw new System.ArgumentNullException(nameof(tileContainer));

            return new BuildingViewHierarchy(tileContainer, cellSize);
        }

        public void SetCellSize(float cellSize) =>
            _cellSize = Mathf.Max(1e-4f, cellSize);

        public void BindRegistry(BuildingGroupRegistry registry) =>
            _registry = registry;

        /// <summary>부모 체인이 Outdoor 루트 아래이면 true.</summary>
        public static bool IsUnderOutdoorRoot(Transform tileParent)
        {
            Transform t = tileParent;
            while (t != null)
            {
                if (t.name == OutdoorName)
                    return true;
                if (t.name == BuildingsName)
                    return false;
                t = t.parent;
            }

            return false;
        }

        /// <summary>Building_{id} 부모에서 id 파싱.</summary>
        public static bool TryParseBuildingIdFromParent(Transform tileParent, out int buildingId)
        {
            buildingId = 0;
            Transform t = tileParent;
            while (t != null)
            {
                string name = t.name;
                if (name.StartsWith(BuildingNamePrefix) &&
                    int.TryParse(name.Substring(BuildingNamePrefix.Length), out buildingId) &&
                    buildingId > 0)
                    return true;
                if (t.name == OutdoorName || t.name == BuildingsName)
                    return false;
                t = t.parent;
            }

            return false;
        }

        /// <summary><see cref="TileIdentity.BuildingIdOutdoor"/> (−1) → Outdoor/, 그 외 → Building_{id}.</summary>
        public static bool BelongsUnderOutdoor(int buildingId) =>
            buildingId == TileIdentity.BuildingIdOutdoor;

        public static string BuildingRootName(int buildingId) =>
            BuildingNamePrefix + buildingId;

        /// <summary>
        /// save buildings[] 로컬 원점과 동일: 피벗 월드 = (MinX, MinOccupiedY, MinZ) * cellSize.
        /// </summary>
        public static Vector3 PivotWorldFromExtent(in BuildingExtent extent, float cellSize)
        {
            cellSize = Mathf.Max(1e-4f, cellSize);
            return new Vector3(
                extent.MinX * cellSize,
                extent.MinOccupiedY * cellSize,
                extent.MinZ * cellSize);
        }

        /// <summary>
        /// 타일을 올바른 Outdoor/Building 부모에 붙입니다. 월드 좌표 유지.
        /// </summary>
        public void AttachTile(TileView view, in TileData tileData)
        {
            if (view == null)
                return;

            EnsureScaffold();

            Transform previous = view.transform.parent;
            Transform parent = ResolveParent(tileData.identity.buildingId);
            if (view.transform.parent != parent)
                view.transform.SetParent(parent, worldPositionStays: true);

            TryPruneBuildingRoot(previous);
        }

        /// <summary>풀/파괴 전 Building 부모에서 떼어 컨테이너로 되돌립니다.</summary>
        public void DetachTile(TileView view)
        {
            if (view == null)
                return;

            Transform previous = view.transform.parent;
            if (_tileContainer != null && view.transform.parent != _tileContainer)
                view.transform.SetParent(_tileContainer, worldPositionStays: true);

            TryPruneBuildingRoot(previous);
        }

        public Transform ResolveParent(int buildingId)
        {
            EnsureScaffold();
            if (BelongsUnderOutdoor(buildingId))
                return _outdoor;

            return GetOrCreateBuildingRoot(buildingId);
        }

        Transform GetOrCreateBuildingRoot(int buildingId)
        {
            if (_buildingRoots.TryGetValue(buildingId, out Transform existing) && existing != null)
            {
                SyncBuildingPivot(buildingId, existing);
                return existing;
            }

            string name = BuildingRootName(buildingId);
            Transform found = _buildings.Find(name);
            if (found != null)
            {
                _buildingRoots[buildingId] = found;
                SyncBuildingPivot(buildingId, found);
                return found;
            }

            var go = new GameObject(name);
            Transform root = go.transform;
            root.SetParent(_buildings, false);
            _buildingRoots[buildingId] = root;
            SyncBuildingPivot(buildingId, root);
            return root;
        }

        void SyncBuildingPivot(int buildingId, Transform root)
        {
            if (root == null || _registry == null)
                return;

            if (!_registry.TryGetBuildingExtent(buildingId, out BuildingExtent extent) || !extent.HasBounds)
                return;

            Vector3 target = PivotWorldFromExtent(extent, _cellSize);
            if ((root.position - target).sqrMagnitude < 1e-12f)
                return;

            // 피벗만 옮기고 자식 TileView 월드 좌표는 유지 (런타임은 월드 SSOT).
            int childCount = root.childCount;
            if (childCount == 0)
            {
                root.position = target;
                return;
            }

            _pivotChildPosScratch.Clear();
            _pivotChildRotScratch.Clear();
            for (int i = 0; i < childCount; i++)
            {
                Transform child = root.GetChild(i);
                _pivotChildPosScratch.Add(child.position);
                _pivotChildRotScratch.Add(child.rotation);
            }

            root.position = target;

            for (int i = 0; i < childCount; i++)
                root.GetChild(i).SetPositionAndRotation(
                    _pivotChildPosScratch[i],
                    _pivotChildRotScratch[i]);
        }

        void EnsureScaffold()
        {
            if (_outdoor == null)
                _outdoor = GetOrCreateChild(_tileContainer, OutdoorName);

            if (_buildings == null)
                _buildings = GetOrCreateChild(_tileContainer, BuildingsName);
        }

        static Transform GetOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
                return existing;

            var go = new GameObject(name);
            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = Vector3.zero;
            t.localRotation = Quaternion.identity;
            t.localScale = Vector3.one;
            return t;
        }

        void TryPruneBuildingRoot(Transform candidate)
        {
            if (candidate == null || _buildings == null)
                return;

            if (candidate.parent != _buildings)
                return;

            if (candidate.childCount > 0)
                return;

            string name = candidate.name;
            if (!name.StartsWith(BuildingNamePrefix))
                return;

            if (int.TryParse(name.Substring(BuildingNamePrefix.Length), out int id))
                _buildingRoots.Remove(id);

            DestroyHierarchyObject(candidate.gameObject);
        }

        static void DestroyHierarchyObject(GameObject go)
        {
            if (go == null)
                return;

            if (Application.isPlaying)
                Object.Destroy(go);
            else
                Object.DestroyImmediate(go);
        }
    }
}

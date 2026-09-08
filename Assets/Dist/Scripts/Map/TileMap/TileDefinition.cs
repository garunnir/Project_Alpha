// ============================================================
// TileDefinition — Prefab/placement/collision bake plus BN-style gameplay flags
// ============================================================
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;

namespace IsoTilemap
{
    [CreateAssetMenu(fileName = "TileDefinition", menuName = "Iso/Tile Definition")]
    public class TileDefinition : ScriptableObject
    {
        [HorizontalGroup("Row", Width = 90)]
        [PreviewField(90, ObjectFieldAlignment.Left), HideLabel]
        public Sprite thumbnail;

        [HorizontalGroup("Row"), VerticalGroup("Row/Info"), LabelWidth(70)]
        public string prefabId;

        [VerticalGroup("Row/Info"), LabelWidth(70)]
        public GameObject prefab;

        [VerticalGroup("Row/Info"), LabelWidth(70)]
        public string category;

        [VerticalGroup("Row/Info"), LabelWidth(70)]
        public Vector3Int size = Vector3Int.one;

        [VerticalGroup("Row/Info"), LabelWidth(70)]
        public TilePlacementSlot placementSlot = TilePlacementSlot.None;

        [FoldoutGroup("Flags", expanded: true)]
        [LabelText("플래그")]
        [Tooltip("BN-style gameplay flags (TileFlags SSOT). Not used for collision or occlusion.")]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = false)]
        public List<string> flags = new List<string>();

        [FoldoutGroup("Combat", expanded: false)]
        [LabelText("재질")]
        [Tooltip("BN MaterialData id 목록. 피해 채널(bash/cut)별 resist — Wear·구조물과 동일 SSOT.")]
        [ListDrawerSettings(DraggableItems = true, ShowFoldout = false)]
        public List<string> materials = new List<string>();

        [FoldoutGroup("Combat")]
        [LabelText("Break Durability")]
        [Tooltip("파괴 내구도 HP. 0이면 MapDigConsts.DefaultDigDurability.")]
        [Min(0)]
        public int breakDurability;

        [FoldoutGroup("Combat")]
        [LabelText("Material Thickness")]
        [Tooltip("재질 두께. WearCombatDefense.ArmorRating과 동일 스케일.")]
        [Min(0)]
        public int materialThickness;

#if UNITY_EDITOR
        [FoldoutGroup("충돌·오클루전", expanded: true)]
        [PropertyOrder(-10)]
        [OnInspectorGUI]
        void DrawUnsavedChangesBanner()
        {
            if (!HasUnsavedChanges)
                return;

            UnityEditor.EditorGUILayout.HelpBox(
                "저장되지 않은 변경사항이 있습니다.",
                UnityEditor.MessageType.Warning);
        }

        [FoldoutGroup("충돌·오클루전")]
        [PropertyOrder(-9)]
        [Button("저장"), HorizontalGroup("충돌·오클루전/SaveBar", Width = 72)]
        [EnableIf(nameof(HasUnsavedChanges))]
        void SaveCollisionSettings() => SaveAssetToDisk();
#endif

        [FoldoutGroup("충돌·오클루전")]
        [PropertyOrder(0)]
        [InlineProperty, HideLabel]
        [OnValueChanged(nameof(MarkDirty))]
        public TileOccupiedCellCollision occupied;

        [FoldoutGroup("충돌·오클루전")]
        [PropertyOrder(1)]
        [InlineProperty, HideLabel]
        [OnValueChanged(nameof(MarkDirty))]
        public TileEdgeCollision edge;

        [FoldoutGroup("충돌·오클루전")]
        [Button("Floor"), HorizontalGroup("충돌·오클루전/Presets")]
        void ApplyFloorPreset() => ApplyPreset(
            placement: TilePlacementSlot.HorizontalFace,
            occupied: new TileOccupiedCellCollision { providesLogicalFloor = true },
            edge: default);

        [FoldoutGroup("충돌·오클루전")]
        [Button("Wall"), HorizontalGroup("충돌·오클루전/Presets")]
        void ApplyWallPreset() => ApplyPreset(
            placement: TilePlacementSlot.OccupiedCell,
            occupied: new TileOccupiedCellCollision { blocksPassageAndOcclusion = true },
            edge: default);

        [FoldoutGroup("충돌·오클루전")]
        [Button("EdgeWall"), HorizontalGroup("충돌·오클루전/Presets")]
        void ApplyEdgeWallPreset() => ApplyPreset(
            placement: TilePlacementSlot.VerticalFace,
            occupied: default,
            edge: new TileEdgeCollision { blocksPassageAndOcclusion = true });

        [FoldoutGroup("충돌·오클루전")]
        [Button("Slope/Physics"), HorizontalGroup("충돌·오클루전/Presets")]
        void ApplyPhysicsPreset() => ApplyPreset(
            placement: TilePlacementSlot.OccupiedCell,
            occupied: new TileOccupiedCellCollision { usePhysicsCollider = true },
            edge: default);

        [FoldoutGroup("충돌·오클루전")]
        [Button("Water"), HorizontalGroup("충돌·오클루전/Presets")]
        void ApplyWaterPreset()
        {
            ApplyPreset(TilePlacementSlot.HorizontalFace, default, default);
            category = TileDefinitionCategories.Liquid;
            flags ??= new List<string>();
            flags.Clear();
            flags.Add(TileFlags.Water);
            flags.Add(TileFlags.Fishable);
            MarkDirty();
        }

        [FoldoutGroup("충돌·오클루전")]
        [Button("None"), HorizontalGroup("충돌·오클루전/Presets")]
        void ApplyNonePreset() => ApplyPreset(TilePlacementSlot.OccupiedCell, default, default);

        void ApplyPreset(
            TilePlacementSlot placement,
            TileOccupiedCellCollision occupied,
            TileEdgeCollision edge)
        {
            placementSlot = placement;
            this.occupied = occupied;
            this.edge = edge;
            MarkDirty();
        }

        void MarkDirty()
        {
#if UNITY_EDITOR
            if (this == null)
                return;

            UnityEditor.Undo.RecordObject(this, "TileDefinition 변경");
            UnityEditor.EditorUtility.SetDirty(this);
            RequestInspectorRepaint();
#endif
        }

#if UNITY_EDITOR
        bool HasUnsavedChanges => UnityEditor.EditorUtility.IsDirty(this);

        static void RequestInspectorRepaint()
        {
            UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
        }

        void SaveAssetToDisk()
        {
            if (this == null)
                return;

            UnityEditor.Undo.FlushUndoRecordObjects();
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssetIfDirty(this);
            RequestInspectorRepaint();
        }
#endif
    }
}

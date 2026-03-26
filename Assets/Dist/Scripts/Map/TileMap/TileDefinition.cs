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
    }
}

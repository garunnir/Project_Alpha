using UnityEngine;

namespace IsoTilemap
{
    [CreateAssetMenu(fileName = "TileDefinition", menuName = "Iso/Tile Definition")]
    public class TileDefinition : ScriptableObject
    {
        public string prefabId;
        public GameObject prefab;
        public Sprite thumbnail;
    }
}

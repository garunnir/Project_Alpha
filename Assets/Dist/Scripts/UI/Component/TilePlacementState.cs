using IsoTilemap;
using UnityEngine;

public class TilePlacementState : MonoBehaviour
{
    public TileDefinition Selected { get; private set; }

    public void Select(TileDefinition def)
    {
        Selected = def;
    }

    public void Clear()
    {
        Selected = null;
    }
}

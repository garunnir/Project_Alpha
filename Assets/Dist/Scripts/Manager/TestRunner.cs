using IsoTilemap;
using UnityEngine;

public class TestRunner : MonoBehaviour
{
    [SerializeField] TileMapLoader _loader;
    void Start()
    {
        _loader.LoadMapRuntime("TestMap");
        Debug.Log("TestRunner started.");
    }
}

using IsoTilemap;
using UnityEngine;

public class TestRunner : MonoBehaviour
{
    [SerializeField] TileMapLoader _serializer;
    void Start()
    {
        _serializer.LoadMapRuntime();
        Debug.Log("TestRunner started.");
    }
}

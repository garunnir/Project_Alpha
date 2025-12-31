using IsoTilemap;
using UnityEngine;

public class TestRunner : MonoBehaviour
{
    [SerializeField] TileMapLoader _serializer;
    void Start()
    {
        _serializer.LoadMap();
        Debug.Log("TestRunner started.");
    }
}

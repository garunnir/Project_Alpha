using IsoTilemap;
using UnityEngine;

public class TestRunner : MonoBehaviour
{
    [SerializeField] TileMapSerializer _serializer;
    void Start()
    {
        _serializer.LoadMap();
        Debug.Log("TestRunner started.");
    }
}

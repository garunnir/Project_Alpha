using IsoTilemap;
using UnityEngine;

public class TestRunner : MonoBehaviour
{
    [SerializeField] TileMapManager _manager;
    void Start()
    {
        _manager.Load();
        Debug.Log("TestRunner started.");
    }
}

using UnityEngine;

public class DebugLogController : MonoBehaviour
{
    [SerializeField] bool isDebugMode = false;
    [SerializeField] bool floorAlgorithm = false;
    [SerializeField] bool player = false;
    [SerializeField] bool playerInteraction = false;
    [SerializeField] bool playerMovement = false;
    [SerializeField] bool playerPosUpdate = false;
    [SerializeField] bool tileMapRuntime = false;
    [SerializeField] bool playerSight = false;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Config.DebugMode.FloorAlgorithm = floorAlgorithm;
        Config.DebugMode.Player = player;
        Config.DebugMode.PlayerInteraction = playerInteraction;
        Config.DebugMode.PlayerMovement = playerMovement;
        Config.DebugMode.PlayerPosUpdate = playerPosUpdate;
        Config.DebugMode.TileMapRuntime = tileMapRuntime;
        Config.DebugMode.PlayerSight = playerSight;
    }

}

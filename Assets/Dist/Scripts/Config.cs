
public static class Config
{
    public static class DebugMode
    {
        public static bool Player=false;
        public static bool PlayerInteraction=false&&Player;
        public static bool PlayerMovement=false&&Player;
        public static bool PlayerSight = false&&Player;
        public static bool PlayerPosUpdate=false&&Player;
        public static bool TileMapRuntime=true;
        public static bool FloorAlgorithm=true&&TileMapRuntime;

    }
}

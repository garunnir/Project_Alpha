
public static class Config
{
    public static class DebugMode
    {
        public static bool Player=false;
        public static bool PlayerInteraction=false||Player;
        public static bool PlayerMovement=false||Player;
        public static bool TileMapRuntime=false;
        public static bool FloorAlgorithm=true||TileMapRuntime;
    }
}

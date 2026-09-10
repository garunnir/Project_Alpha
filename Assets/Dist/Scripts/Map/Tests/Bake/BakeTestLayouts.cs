// ============================================================
// BakeTestLayouts — Dist.Map BakeIdPlaygroundLayout 위임 (테스트 호환)
// ============================================================
using IsoTilemap;

namespace IsoTilemap.Tests
{
    /// <summary>SSOT는 <see cref="BakeIdPlaygroundLayout"/> — 테스트는 기존 이름을 유지.</summary>
    public static class BakeTestLayouts
    {
        public static class MasterPlayground
        {
            public static readonly UnityEngine.Vector3Int OpenPair_A =
                BakeIdPlaygroundLayout.MasterPlayground.OpenPair_A;
            public static readonly UnityEngine.Vector3Int OpenPair_B =
                BakeIdPlaygroundLayout.MasterPlayground.OpenPair_B;
            public static readonly UnityEngine.Vector3Int ThinWall_Left =
                BakeIdPlaygroundLayout.MasterPlayground.ThinWall_Left;
            public static readonly UnityEngine.Vector3Int ThinWall_Right =
                BakeIdPlaygroundLayout.MasterPlayground.ThinWall_Right;
            public static readonly UnityEngine.Vector3Int CubeWall_Left =
                BakeIdPlaygroundLayout.MasterPlayground.CubeWall_Left;
            public static readonly UnityEngine.Vector3Int CubeWall_Right =
                BakeIdPlaygroundLayout.MasterPlayground.CubeWall_Right;
            public static readonly UnityEngine.Vector3Int Bridge_B1 =
                BakeIdPlaygroundLayout.MasterPlayground.Bridge_B1;
            public static readonly UnityEngine.Vector3Int Bridge_B1b =
                BakeIdPlaygroundLayout.MasterPlayground.Bridge_B1b;
            public static readonly UnityEngine.Vector3Int Bridge_GapCell =
                BakeIdPlaygroundLayout.MasterPlayground.Bridge_GapCell;
            public static readonly UnityEngine.Vector3Int Bridge_B2 =
                BakeIdPlaygroundLayout.MasterPlayground.Bridge_B2;
            public static readonly UnityEngine.Vector3Int Bridge_B2b =
                BakeIdPlaygroundLayout.MasterPlayground.Bridge_B2b;
            public static readonly UnityEngine.Vector3Int Column_Lower =
                BakeIdPlaygroundLayout.MasterPlayground.Column_Lower;
            public static readonly UnityEngine.Vector3Int Column_Upper =
                BakeIdPlaygroundLayout.MasterPlayground.Column_Upper;
            public static readonly UnityEngine.Vector3Int GShape_A =
                BakeIdPlaygroundLayout.MasterPlayground.GShape_A;
            public static readonly UnityEngine.Vector3Int GShape_B =
                BakeIdPlaygroundLayout.MasterPlayground.GShape_B;
            public static readonly UnityEngine.Vector3Int GShape_C =
                BakeIdPlaygroundLayout.MasterPlayground.GShape_C;
            public static readonly UnityEngine.Vector3Int Balcony_Floor =
                BakeIdPlaygroundLayout.MasterPlayground.Balcony_Floor;
            public static readonly UnityEngine.Vector3Int Plaza_Floor =
                BakeIdPlaygroundLayout.MasterPlayground.Plaza_Floor;
            public static readonly UnityEngine.Vector3Int Dig_Floor =
                BakeIdPlaygroundLayout.MasterPlayground.Dig_Floor;
            public static readonly UnityEngine.Vector3Int IndoorProbe =
                BakeIdPlaygroundLayout.MasterPlayground.IndoorProbe;
            public static readonly UnityEngine.Vector3Int OutdoorShedProbe =
                BakeIdPlaygroundLayout.MasterPlayground.OutdoorShedProbe;

            public static System.Collections.Generic.IReadOnlyList<TileData> Tiles =>
                BakeIdPlaygroundLayout.MasterPlayground.Tiles;

            public static TileData MakeBridgeFloorTile() =>
                BakeIdPlaygroundLayout.MasterPlayground.MakeBridgeFloorTile();

            public static TileData MakeOpenPairSeparatorWall() =>
                BakeIdPlaygroundLayout.MasterPlayground.MakeOpenPairSeparatorWall();

            public static bool IsThinWallSplitTile(TileData tile) =>
                BakeIdPlaygroundLayout.MasterPlayground.IsThinWallSplitTile(tile);

            public static bool IsCubeWallSplitTile(TileData tile) =>
                BakeIdPlaygroundLayout.MasterPlayground.IsCubeWallSplitTile(tile);

            public static bool IsDigFloorTile(TileData tile) =>
                BakeIdPlaygroundLayout.MasterPlayground.IsDigFloorTile(tile);
        }
    }
}

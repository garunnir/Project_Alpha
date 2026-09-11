// ============================================================
// [BakeIdSimDefaults] — Unit Layout에서 Sim 씬 초기 타일·probe·rule 시드
// ============================================================

using System.Collections.Generic;

namespace IsoTilemap
{
    /// <summary>Host Import용 시드 — <see cref="BakeIdPlaygroundLayout"/> → 씬 직렬화.</summary>
    public static class BakeIdSimDefaults
    {
        public static void FillFromUnitMasterPlayground(
            List<BakeIdSimTileEntry> tiles,
            List<BakeIdSimProbe> probes,
            List<BakeIdSimRule> rules)
        {
            tiles.Clear();
            probes.Clear();
            rules.Clear();

            tiles.AddRange(BakeIdSimTileUtil.FromTileDataList(
                BakeIdPlaygroundLayout.MasterPlayground.Tiles));

            probes.Add(new BakeIdSimProbe("OpenPair_A", BakeIdPlaygroundLayout.MasterPlayground.OpenPair_A));
            probes.Add(new BakeIdSimProbe("OpenPair_B", BakeIdPlaygroundLayout.MasterPlayground.OpenPair_B));
            probes.Add(new BakeIdSimProbe("ThinWall_Left", BakeIdPlaygroundLayout.MasterPlayground.ThinWall_Left));
            probes.Add(new BakeIdSimProbe("ThinWall_Right", BakeIdPlaygroundLayout.MasterPlayground.ThinWall_Right));
            probes.Add(new BakeIdSimProbe("IndoorProbe", BakeIdPlaygroundLayout.MasterPlayground.IndoorProbe));
            probes.Add(new BakeIdSimProbe(
                "OutdoorShedProbe", BakeIdPlaygroundLayout.MasterPlayground.OutdoorShedProbe));
            probes.Add(new BakeIdSimProbe("CubeWall_Left", BakeIdPlaygroundLayout.MasterPlayground.CubeWall_Left));
            probes.Add(new BakeIdSimProbe("CubeWall_Right", BakeIdPlaygroundLayout.MasterPlayground.CubeWall_Right));
            probes.Add(new BakeIdSimProbe("Bridge_B1", BakeIdPlaygroundLayout.MasterPlayground.Bridge_B1));
            probes.Add(new BakeIdSimProbe("Bridge_B2", BakeIdPlaygroundLayout.MasterPlayground.Bridge_B2));
            probes.Add(new BakeIdSimProbe(
                "Column_HFloor_Lower", BakeIdPlaygroundLayout.MasterPlayground.Column_HFloor_Lower));
            probes.Add(new BakeIdSimProbe(
                "Column_HFloor_Upper", BakeIdPlaygroundLayout.MasterPlayground.Column_HFloor_Upper));
            probes.Add(new BakeIdSimProbe("GShape_A", BakeIdPlaygroundLayout.MasterPlayground.GShape_A));
            probes.Add(new BakeIdSimProbe("Balcony_Floor", BakeIdPlaygroundLayout.MasterPlayground.Balcony_Floor));
            probes.Add(new BakeIdSimProbe("Plaza_Floor", BakeIdPlaygroundLayout.MasterPlayground.Plaza_Floor));
            probes.Add(new BakeIdSimProbe("Dig_Floor", BakeIdPlaygroundLayout.MasterPlayground.Dig_Floor));
            probes.Add(new BakeIdSimProbe(
                "EmptyOutdoorProbe", BakeIdPlaygroundLayout.MasterPlayground.EmptyOutdoorProbe));

            // Same room open pair
            rules.Add(BakeIdSimRule.SameIds("OpenPair_A", "OpenPair_B"));

            // Structural + ThinWall connect → same building; SeparatesRoom → different room
            rules.Add(BakeIdSimRule.SameBuilding("ThinWall_Left", "ThinWall_Right"));
            rules.Add(BakeIdSimRule.Differ(
                "ThinWall_Left", "ThinWall_Right", BakeIdSimIdField.RoomId));

            // Enclosed house indoor vs open shed / empty choice A
            rules.Add(BakeIdSimRule.IsOutdoor("IndoorProbe", false));
            rules.Add(BakeIdSimRule.IsOutdoor("OutdoorShedProbe", true));
            rules.Add(BakeIdSimRule.IsOutdoor("EmptyOutdoorProbe", true));

            // Cube structural bridge → same building; bridge gap → different buildings
            rules.Add(BakeIdSimRule.SameBuilding("CubeWall_Left", "CubeWall_Right"));
            rules.Add(BakeIdSimRule.Differ(
                "Bridge_B1", "Bridge_B2", BakeIdSimIdField.BuildingId));

            // AABB volume Space: stacked floors share SpaceId within building AABB
            rules.Add(BakeIdSimRule.SameIds("Column_HFloor_Lower", "Column_HFloor_Upper"));
        }
    }
}

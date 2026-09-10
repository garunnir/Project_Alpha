// ============================================================
// BakeIdScenarioTests — smoke + Region별 EDIT 템플릿
// ============================================================
// 사용자 수정: // --- EDIT --- 블록만. BakeTestScenario + Expect + MasterPlayground만 사용.
// ============================================================
using NUnit.Framework;
using UnityEngine;

namespace IsoTilemap.Tests
{
    using static BakeTestLayouts;

    public sealed class BakeIdScenarioTests
    {
        [Test]
        public void Smoke_MasterPlayground_AssignAll_ReadsIds()
        {
            BakeTestScenario s = BakeTestScenario
                .FromLayout(MasterPlayground.Tiles)
                .Bake();

            Assert.IsNotNull(s.Reader);

            // OpenPair: 밀폐 집 왼쪽 방 — 벽 없이 인접 → room/space/building 동일
            Expect.SameIds(s.Reader, MasterPlayground.OpenPair_A, MasterPlayground.OpenPair_B);

            // ThinWallSplit: 내부 칸막이 — buildingId(및 spaceId) 분리
            Expect.Differ(
                s.Reader,
                MasterPlayground.ThinWall_Left,
                MasterPlayground.ThinWall_Right,
                FloorIdField.BuildingId);
            Expect.Differ(
                s.Reader,
                MasterPlayground.ThinWall_Left,
                MasterPlayground.ThinWall_Right,
                FloorIdField.SpaceId);

            // 밀폐 집 → 실내 space; 지붕 없는 shed → 야외 space
            Expect.IsOutdoor(s.Reader, MasterPlayground.IndoorProbe, false);
            Expect.IsOutdoor(s.Reader, MasterPlayground.OutdoorShedProbe, true);
        }

        /*
         * ========== Region EDIT 템플릿 (복제해 [Test]로 전환) ==========
         *
         * --- EDIT Region: ThinWallSplit_Merge ---
         * [Test]
         * public void ThinWallSplit_RemoveWall_MergesIds()
         * {
         *     var s = BakeTestScenario.FromLayout(MasterPlayground.Tiles).Bake();
         *     var wall = s.FindTile(MasterPlayground.IsThinWallSplitTile);
         *     s.Remove(wall);
         *     // 벽 제거 후 incremental/full bake → Left/Right SameIds (building·room·space)
         *     Expect.SameIds(s.Reader, MasterPlayground.ThinWall_Left, MasterPlayground.ThinWall_Right);
         * }
         *
         * --- EDIT Region: CubeWallSplit ---
         * // Cube 제거 + gap에 floor 추가 → CubeWall_Left/Right SameIds
         * // var cube = s.FindTile(MasterPlayground.IsCubeWallSplitTile);
         * // s.Remove(cube);
         * // s.Set(BakeTileFactory.Floor(2, 1, 1));
         * // Expect.SameIds(s.Reader, MasterPlayground.CubeWall_Left, MasterPlayground.CubeWall_Right);
         *
         * --- EDIT Region: BridgeGap_BuildingMerge ---
         * // s.Set(MasterPlayground.MakeBridgeFloorTile());
         * // Expect.SameBuilding(s.Reader, MasterPlayground.Bridge_B1, MasterPlayground.Bridge_B2);
         *
         * --- EDIT Region: ColumnStack_Space ---
         * // Expect.SameIds 중 SpaceId만 / Differ SpaceId 없이
         * // Expect.Differ 대신 Space 동일:
         * // Assert SpaceId equal via SameIds if column shares space, or Differ SpaceId if not.
         * // Expect.SameIds(s.Reader, MasterPlayground.Column_Lower, MasterPlayground.Column_Upper);
         * // 또는 floor ids 전체 동일하지 않을 수 있음(room은 slice-local) → Differ RoomId + Space same:
         * //   reader.TryRead → SpaceId 비교는 Expect.Differ(field: SpaceId) 의 반대로 SameIds 부분 필요 시
         * //   Expect에 SameSpace 없으면 Differ 역으로 수동, 또는 SameIds가 room까지 같길 기대하지 말고
         * //   별도 assert 추가 가능.
         *
         * --- EDIT Region: OpenPair_Sever ---
         * // s.Set(MasterPlayground.MakeOpenPairSeparatorWall());
         * // Expect.Differ(s.Reader, MasterPlayground.OpenPair_A, MasterPlayground.OpenPair_B, FloorIdField.RoomId);
         *
         * --- EDIT Region: GShape ---
         * // Expect.SameIds(s.Reader, MasterPlayground.GShape_A, MasterPlayground.GShape_B);
         * // Expect.SameIds(s.Reader, MasterPlayground.GShape_B, MasterPlayground.GShape_C);
         *
         * --- EDIT Region: Balcony_Outdoor ---
         * // Expect.IsOutdoor(s.Reader, MasterPlayground.Balcony_Floor, true);
         *
         * --- EDIT Region: PlazaStrip ---
         * // plaza buildingId == -1 (Outdoor). 관계 assert 예:
         * // Assert.IsTrue(s.Reader.TryRead(MasterPlayground.Plaza_Floor, out var p));
         * // Assert.AreEqual(TileIdentity.BuildingIdOutdoor, p.BuildingId);
         * // (절대 번호는 plaza 상수 -1만 예외 — TileIdentity.BuildingIdOutdoor SSOT)
         *
         * --- EDIT Region: DigTarget ---
         * // var before = ... TryRead Dig_Floor buildingId
         * // s.Remove(s.FindTile(MasterPlayground.IsDigFloorTile));
         * // // 이웃 buildingId 유지 검증은 인접 probe로 SameBuilding
         *
         * --- EDIT Region: Golden ---
         * // ... incremental edits ...
         * // Expect.SnapshotEquals(s.GoldenRebake(), s.Reader);
         *
         * ========== END EDIT templates ==========
         */
    }
}

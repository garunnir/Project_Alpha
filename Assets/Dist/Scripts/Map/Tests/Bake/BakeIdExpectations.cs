// ============================================================
// BakeIdExpectations — 관계·동치 assert (IBakeIdReader만 입력)
// ============================================================
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace IsoTilemap.Tests
{
    /// <summary>시나리오 테스트용 Expect API. 절대 id 번호 assert 금지.</summary>
    public static class Expect
    {
        public static void SameIds(IBakeIdReader reader, Vector3Int probe, Vector3Int reference)
        {
            FloorIdSnapshot a = Require(reader, probe);
            FloorIdSnapshot b = Require(reader, reference);
            Assert.AreEqual(
                b, a,
                FormatPair("SameIds", probe, a, reference, b));
        }

        public static void Differ(IBakeIdReader reader, Vector3Int a, Vector3Int b, FloorIdField field)
        {
            FloorIdSnapshot sa = Require(reader, a);
            FloorIdSnapshot sb = Require(reader, b);
            bool same = FieldEquals(sa, sb, field);
            Assert.IsFalse(
                same,
                FormatPair($"Differ({field})", a, sa, b, sb) + " — expected different values");
        }

        public static void SameBuilding(IBakeIdReader reader, Vector3Int a, Vector3Int b)
        {
            FloorIdSnapshot sa = Require(reader, a);
            FloorIdSnapshot sb = Require(reader, b);
            Assert.AreEqual(
                sb.BuildingId, sa.BuildingId,
                FormatPair("SameBuilding", a, sa, b, sb));
        }

        public static void IsOutdoor(IBakeIdReader reader, Vector3Int cell, bool expected)
        {
            FloorIdSnapshot snap = Require(reader, cell);
            Assert.AreEqual(
                expected, snap.IsOutdoor,
                $"{cell} IsOutdoor got={snap.IsOutdoor} expected={expected} ({snap})");
        }

        public static void SnapshotEquals(IBakeIdReader golden, IBakeIdReader actual)
        {
            IReadOnlyDictionary<Vector3Int, FloorIdSnapshot> g = golden.CaptureAllFloors();
            IReadOnlyDictionary<Vector3Int, FloorIdSnapshot> a = actual.CaptureAllFloors();

            var sb = new StringBuilder();
            foreach (var kv in g)
            {
                if (!a.TryGetValue(kv.Key, out FloorIdSnapshot got) || !got.Equals(kv.Value))
                {
                    a.TryGetValue(kv.Key, out FloorIdSnapshot missing);
                    sb.AppendLine($"  {kv.Key}: golden={kv.Value} actual={missing}");
                }
            }

            foreach (var kv in a)
            {
                if (!g.ContainsKey(kv.Key))
                    sb.AppendLine($"  {kv.Key}: golden=missing actual={kv.Value}");
            }

            Assert.IsTrue(
                sb.Length == 0,
                "SnapshotEquals mismatch:\n" + sb);
        }

        static FloorIdSnapshot Require(IBakeIdReader reader, Vector3Int cell)
        {
            Assert.IsNotNull(reader, "IBakeIdReader is null");
            Assert.IsTrue(
                reader.TryRead(cell, out FloorIdSnapshot snap),
                $"No floor id snapshot at {cell}");
            return snap;
        }

        static bool FieldEquals(FloorIdSnapshot a, FloorIdSnapshot b, FloorIdField field) =>
            field switch
            {
                FloorIdField.BuildingId => a.BuildingId == b.BuildingId,
                FloorIdField.RoomId => a.RoomId == b.RoomId,
                FloorIdField.SpaceId => a.SpaceId == b.SpaceId,
                FloorIdField.IsOutdoor => a.IsOutdoor == b.IsOutdoor,
                _ => a.Equals(b),
            };

        static string FormatPair(
            string label,
            Vector3Int probe, FloorIdSnapshot probeSnap,
            Vector3Int reference, FloorIdSnapshot refSnap) =>
            $"{label}: probe {probe} ({probeSnap}) vs reference {reference} ({refSnap})";
    }
}

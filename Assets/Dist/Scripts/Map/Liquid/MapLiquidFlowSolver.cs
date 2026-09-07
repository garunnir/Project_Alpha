// ============================================================
// MapLiquidFlowSolver — compressible-water CA equalize + 재귀 수직 탈출
// ============================================================
// docs/map/LIQUID.md SSOT. 순수 반응형(정적 셀 무연산 보증 §2) —
// dirty 큐 pop 외의 순회(전체 overlay 스캔 등)를 절대 하지 않는다.

using UnityEngine;

namespace IsoTilemap
{
    public sealed class MapLiquidFlowSolver
    {
        static readonly Vector3Int[] HorizontalDirs =
        {
            new Vector3Int(1, 0, 0),
            new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 0, 1),
            new Vector3Int(0, 0, -1),
        };

        readonly MapLiquidOverlay _overlay;
        TileMapCacheHub _hub;

        public MapLiquidFlowSolver(MapLiquidOverlay overlay) => _overlay = overlay;

        public void BindMapContext(TileMapCacheHub hub) => _hub = hub;

        /// <summary>
        /// WorldClock.MinuteChanged 1회당 호출. dirty 큐가 비었으면 즉시 반환(비용 0) —
        /// 정지된 바다는 맵 크기와 무관하게 이 호출 자체가 no-op.
        /// </summary>
        public void ProcessDirty(int maxUpdates)
        {
            if (_hub == null)
                return;

            int processed = 0;
            while (processed < maxUpdates && _overlay.TryPopDirty(out Vector3Int cell))
            {
                ProcessCell(cell);
                processed++;
            }
        }

        void ProcessCell(Vector3Int self)
        {
            if (!_overlay.TryGetCell(self, out MapLiquidCell selfCell) || selfCell.IsEmpty)
                return;

            // 고체는 흐르지 않는다. 해동 시 ThermalSolver가 상 교차에서 다시 dirty로 넣어 준다.
            if (selfCell.IsSolid)
                return;

            string typeId = selfCell.TypeId;
            int capMl = MapLiquidConsts.DefaultMaxVolumeMl;

            // OutOfMap — 저장 mapBounds 밖(XZ) 또는 y<minY.
            if (!_hub.IsInMapBounds(self.x, self.y, self.z))
            {
                int outMl = _overlay.GetEffectiveMl(self);
                if (outMl > 0)
                    _overlay.AddEffectiveMl(self, -outMl, typeId);
                return;
            }

            // 1) 중력 — AirGap(점유 없음) transit. below는 bounds 안이어야 한다.
            Vector3Int below = self + Vector3Int.down;
            if (IsVerticalOpen(self)
                && _hub.IsInMapBounds(below.x, below.y, below.z)
                && CanHoldLiquid(below))
            {
                int totalMl = _overlay.GetEffectiveMl(self) + _overlay.GetEffectiveMl(below);
                int belowTarget = StableBelowMl(totalMl, capMl, MapLiquidConsts.OverCompressMl);
                int currentBelow = _overlay.GetEffectiveMl(below);
                int moveDown = Mathf.Min(belowTarget - currentBelow, _overlay.GetEffectiveMl(self));
                if (moveDown > MapLiquidConsts.MinFlowMl)
                {
                    _overlay.AddEffectiveMl(below, moveDown, typeId);
                    _overlay.AddEffectiveMl(self, -moveDown, typeId);
                    _overlay.MarkDirty(below);
                }
            }

            if (_overlay.GetEffectiveMl(self) <= 0)
                return; // 전량 낙하 — 더 처리할 것 없음

            // 2) 수평 equalize (diff/4, MinFlowMl 게이트)
            bool anyRoomSideways = false;
            for (int i = 0; i < HorizontalDirs.Length; i++)
            {
                Vector3Int neighbor = self + HorizontalDirs[i];
                if (!IsHorizontalOpen(self, neighbor) || !IsHorizontalFlowTarget(neighbor))
                    continue;

                int selfMl = _overlay.GetEffectiveMl(self);
                int neighborMl = _overlay.GetEffectiveMl(neighbor);
                int diff = selfMl - neighborMl;
                if (diff <= MapLiquidConsts.MinFlowMl)
                    continue;

                anyRoomSideways = true;
                int flow = Mathf.Clamp(diff / 4, 0, selfMl);
                if (flow <= 0)
                    continue;

                _overlay.AddEffectiveMl(self, -flow, typeId);
                _overlay.AddEffectiveMl(neighbor, flow, typeId);
                _overlay.MarkDirty(neighbor);
            }

            // 3) 옆이 전부 막힘/포화인데 여전히 압축 상한 초과 → 위 칸을 실제로 채움(재귀는 MarkDirty(above)가 담당)
            //
            // 임계가 capMl이 아니라 stableCapMl(= capMl + OverCompressMl)인 이유: 1)의 중력 목표가 아래 칸에
            // 최대 capMl + OverCompressMl까지 허용하므로, 여기서 capMl을 임계로 쓰면 1)이 올린 압축분을 3)이
            // 즉시 내리고 1)이 다시 올리는 무한 왕복이 된다. 두 단계의 상한을 같은 값으로 맞춰야 고정점이
            // 존재한다 — MinFlowMl 게이트에 의존하지 않는 구조적 정지 보증(docs/map/LIQUID.md 확산의 유한성).
            if (!anyRoomSideways)
            {
                int stableCapMl = capMl + MapLiquidConsts.OverCompressMl;
                int remainingMl = _overlay.GetEffectiveMl(self);
                if (remainingMl > stableCapMl)
                {
                    Vector3Int above = self + Vector3Int.up;
                    if (IsVerticalOpen(above) && IsHorizontalFlowTarget(above))
                    {
                        int room = Mathf.Max(0, capMl - _overlay.GetEffectiveMl(above));
                        int overflow = remainingMl - stableCapMl;
                        int moveUp = Mathf.Min(overflow, room);
                        if (moveUp > 0)
                        {
                            _overlay.AddEffectiveMl(above, moveUp, typeId);
                            _overlay.AddEffectiveMl(self, -moveUp, typeId);
                            _overlay.MarkDirty(above);
                        }
                    }
                    // 위도 막힘 = 완전 밀폐. 오픈 지형에서는 사실상 발생하지 않음(컨테이너 맥락은 MlBridge 책임).
                }
            }
        }

        /// <summary>
        /// 2-셀 stable state에서 아래 칸이 담아야 할 ml. 상한은 <c>capMl + overCompressMl</c>다.
        /// </summary>
        /// <remarks>
        /// 원전(W-Shadow "Simple Fluid Simulation With Cellular Automata")은 cap을 1.0으로 정규화한 float이라
        /// 중간 분기를 <c>(cap*cap + total*comp) / (cap + comp)</c>로 쓴다. cap이 ml 단위면 <c>cap*cap</c>가
        /// 10^12로 int를 넘기므로, 대수적으로 동등한 <c>cap + comp*(total-cap) / (cap+comp)</c>를 쓴다.
        /// 이 분기에서 <c>total > cap</c>이 보장되어 더하는 항이 음수가 아니고 cap이 정확한 정수이므로
        /// 정수 나눗셈 결과까지 원식과 일치한다. 교과서 형태로 "되돌리지" 말 것.
        /// 중간 곱의 상한은 <c>comp * (cap + comp)</c> ≈ 1.96e8 — terrain별 capMl을 도입해 cap이
        /// 약 3.3e6 ml을 넘기면 그때는 long 승격이 필요하다.
        /// </remarks>
        static int StableBelowMl(int totalMl, int capMl, int overCompressMl)
        {
            if (totalMl <= capMl)
                return totalMl;
            if (totalMl < 2 * capMl + overCompressMl)
                return capMl + overCompressMl * (totalMl - capMl) / (capMl + overCompressMl);
            return (totalMl + overCompressMl) / 2;
        }

        /// <summary>
        /// 수평·상향 equalize 대상 — mapBounds XZ 안 + topology 점유 + 비고체 + 논리 벽 아님.
        /// AirGap(점유 없음)으로는 옆/위 확산하지 않는다.
        /// </summary>
        bool IsHorizontalFlowTarget(Vector3Int cell) =>
            _hub.IsInMapBoundsXZ(cell.x, cell.z)
            && _hub.CellHasOccupancy(cell.x, cell.z, cell.y)
            && CanHoldLiquid(cell);

        /// <summary>액체 ml이 들어갈 수 있는 셀 — AirGap transit OK, 얼음·<see cref="TileCollisionFlags.BlocksOccupiedCells"/> 제외.</summary>
        bool CanHoldLiquid(Vector3Int cell)
        {
            if (IsSolidAt(cell))
                return false;

            if (!_hub.CellHasOccupancy(cell.x, cell.z, cell.y))
                return true;

            return !_hub.CellHasSolidWall(cell.x, cell.z, cell.y);
        }

        bool IsSolidAt(Vector3Int cell) =>
            _overlay.TryGetCell(cell, out MapLiquidCell c) && !c.IsEmpty && c.IsSolid;

        /// <summary>
        /// from→to 한 스텝 카드널 경계 — <see cref="MapTopologyGridSegment"/>와 동일(점유 벽 + EdgeWall).
        /// </summary>
        bool IsHorizontalOpen(Vector3Int from, Vector3Int to) =>
            !CellOrEdgeBlocks(from, to);

        bool CellOrEdgeBlocks(Vector3Int from, Vector3Int to)
        {
            if (_hub.CellHasSolidWall(to.x, to.z, to.y))
                return true;

            return _hub.TryGetEdgeBetween(from, to, out TileData edge)
                && TileCollisionFlagsUtil.EdgeBlocksPassage(edge);
        }

        /// <summary>(하단, upper) 경계가 열려 있는지 — upper 셀에 바닥이 없으면 개방.</summary>
        bool IsVerticalOpen(Vector3Int upper) =>
            !_hub.CellHasFloor(upper.x, upper.y, upper.z);
    }
}

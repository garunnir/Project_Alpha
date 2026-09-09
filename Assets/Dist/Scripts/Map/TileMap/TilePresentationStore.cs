using System;
using System.Collections.Generic;

namespace IsoTilemap
{
    /// <summary>런타임 맵 모델에 기록하지 않는 타일 선택·강조 오버레이 상태.</summary>
    internal sealed class TilePresentationStore
    {
        private readonly HashSet<Guid> _selected = new HashSet<Guid>();
        private readonly Dictionary<Guid, float> _vividEmphasis = new Dictionary<Guid, float>();

        public bool IsSelected(Guid tileId) => _selected.Contains(tileId);

        public void SetSelected(Guid tileId, bool selected)
        {
            if (selected)
                _selected.Add(tileId);
            else
                _selected.Remove(tileId);
        }

        public float GetVividEmphasis(Guid tileId) =>
            _vividEmphasis.TryGetValue(tileId, out float amount) ? amount : 0f;

        public void SetVividEmphasis(Guid tileId, float amount)
        {
            if (amount <= 0f)
                _vividEmphasis.Remove(tileId);
            else
                _vividEmphasis[tileId] = amount;
        }
    }
}

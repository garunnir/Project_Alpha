using UnityEngine;
using UnityEngine.Serialization;

// ============================================================
// SelectionLayerConfig
// 타일 URP RenderingLayer 비트 + 캐릭터 ProPixelizer outline 색 SSOT.
// ============================================================
[CreateAssetMenu(fileName = "SelectionLayerConfig", menuName = "Project/Outline/Selection Layer Config", order = 0)]
public class SelectionLayerConfig : ScriptableObject
{
    [FormerlySerializedAs("_renderingLayerMask")]
    [Tooltip("타일/스프라이트 Selected 오버레이 비트. URP GlobalSettings 'Selection' 슬롯(기본 bit 1).")]
    [SerializeField] private uint _tileRenderingLayerMask = 1u << 1;

    [Header("Character (ProPixelizer OutlineControl)")]
    [SerializeField] private Color _characterAvailableOutlineColor = new Color(0.1886576f, 1f, 0.015686274f, 1f);
    [SerializeField] private Color _characterUnavailableOutlineColor = new Color(1f, 0.2f, 0.15f, 1f);
    [SerializeField] private Color _characterIdleOutlineColor = new Color(0f, 0f, 0f, 0f);

    public uint TileRenderingLayerMask => _tileRenderingLayerMask;

    public Color CharacterAvailableOutlineColor => _characterAvailableOutlineColor;

    public Color CharacterUnavailableOutlineColor => _characterUnavailableOutlineColor;

    public Color CharacterIdleOutlineColor => _characterIdleOutlineColor;

    /// <summary>타일 SSOT 별칭 (기존 API 호환).</summary>
    public uint RenderingLayerMask => _tileRenderingLayerMask;
}

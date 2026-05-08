using UnityEngine;

// ============================================================
// SelectionLayerConfig
// Selected 오버레이용 URP RenderingLayer 비트의 단일 진실원.
// TileView와 SelectionOutlineRendererFeature가 동일 SO를 참조하여
// 비트 인덱스 어긋남을 방지한다.
// ============================================================
[CreateAssetMenu(fileName = "SelectionLayerConfig", menuName = "Project/Outline/Selection Layer Config", order = 0)]
public class SelectionLayerConfig : ScriptableObject
{
    [Tooltip("Selected 상태 오브젝트가 켜는 URP Rendering Layer 비트 마스크. 기본은 1번 비트(URPGlobalSettings의 'Selection' 슬롯).")]
    [SerializeField] private uint _renderingLayerMask = 1u << 1;

    public uint RenderingLayerMask => _renderingLayerMask;
}

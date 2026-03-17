using UnityEngine;

public class RhombusTileMarker : MonoBehaviour
{
    /// <summary>베이킹 후 이 마커가 속한 직사각형 그룹 ID (-1 = 미할당)</summary>
    [HideInInspector] public int groupId = -1;

#if UNITY_EDITOR
    [ContextMenu("Bake My Group")]
    void BakeMyGroup()
    {
        if (groupId < 0)
        {
            Debug.LogWarning("[RhombusTileMarker] groupId 가 할당되지 않았습니다. BakeChunk 를 먼저 실행하세요.");
            return;
        }
        var baker = GetComponentInParent<RhombusChunkBaker>();
        if (baker == null)
        {
            Debug.LogWarning("[RhombusTileMarker] 부모 계층에서 RhombusChunkBaker 를 찾을 수 없습니다.");
            return;
        }
        baker.BakeGroup(groupId);
    }
#endif
}

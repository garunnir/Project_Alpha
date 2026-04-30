using UnityEngine;

// ============================================================
// ShaderController - 셰이더 프로퍼티 제어를 위한 공통 베이스
// ============================================================
public abstract class ShaderController : MonoBehaviour
{
    protected Material Mat { get; private set; } = null;
    [SerializeField] private Renderer _renderer;

    protected virtual void Awake()
    {
        InitializeRendererAndMaterial(useSharedMaterial: false);
        CachePropertyIDs();
    }

    private void OnValidate()
    {
        InitializeRendererAndMaterial(useSharedMaterial: true);
        CachePropertyIDs();
    }

    private void InitializeRendererAndMaterial(bool useSharedMaterial)
    {
        if (_renderer == null)
        {
            _renderer = GetComponent<Renderer>();
        }

        if (_renderer != null)
        {
            Mat = useSharedMaterial ? _renderer.sharedMaterial : _renderer.material;
        }
    }

    // 각 셰이더별 컨트롤러가 ID를 선언하고 구현
    protected abstract void CachePropertyIDs();
}
using UnityEngine;

// ============================================================
// ShaderController - 셰이더 프로퍼티 제어를 위한 공통 베이스
// ============================================================
public abstract class ShaderController : MonoBehaviour
{
    protected Material Mat { get; private set; } = null;
    [SerializeField] private Renderer _renderer;
    public Renderer CachedRenderer => _renderer;

    protected virtual void Awake()
    {
        RebindMaterial();
        CachePropertyIDs();
    }

    private void OnValidate()
    {
        RebindMaterial(useSharedMaterial: true);
        CachePropertyIDs();
    }

    /// <summary>
    /// 렌더러 머티리얼이 교체된 뒤 Emphasis 등이 같은 인스턴스를 보도록 재바인딩.
    /// 씬 런타임 인스턴스만 <c>renderer.material</c>. 프리팹 에셋·에디터는 shared.
    /// </summary>
    public void RebindMaterial(bool useSharedMaterial = false)
    {
        if (_renderer == null)
            _renderer = GetComponent<Renderer>();

        if (_renderer == null)
        {
            Mat = null;
            return;
        }

        bool shared = useSharedMaterial || !CanInstantiateRendererMaterial(_renderer);
        Mat = shared ? _renderer.sharedMaterial : _renderer.material;
    }

    static bool CanInstantiateRendererMaterial(Renderer renderer)
    {
        if (renderer == null || !Application.isPlaying)
            return false;

        return renderer.gameObject.scene.IsValid();
    }

    // 각 셰이더별 컨트롤러가 ID를 선언하고 구현
    protected abstract void CachePropertyIDs();
}
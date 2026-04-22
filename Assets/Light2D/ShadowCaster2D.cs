using UnityEngine;
using UnityEngine.Rendering.Universal;

// ============================================================
// ShadowCaster2DRef — URP ShadowCaster2D를 안전하게 제어하는 래퍼 컴포넌트
// ============================================================
[AddComponentMenu("Rendering/2D/Shadow Caster 2D (Proxy)")]
[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.Rendering.Universal.ShadowCaster2D))]
public sealed class ShadowCaster2DRef : MonoBehaviour
{
    [SerializeField] private Camera _target;
    [SerializeField] private bool _autoCacheOnValidate = true;
    [SerializeField] private bool _hideUnderlyingShadowCasterInInspector = false;

    private UnityEngine.Rendering.Universal.ShadowCaster2D _shadowCaster;

    public UnityEngine.Rendering.Universal.ShadowCaster2D ShadowCaster => _shadowCaster;

    private void Awake()
    {
        Cache();
        if(_target==null){
            Debug.LogWarning("정확한 라이팅을 위해서, 카메라 타겟이 필요합니다.");
            _target=Camera.main;
        }
        transform.rotation=_target.transform.rotation;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (_autoCacheOnValidate && !Application.isPlaying)
            Cache();
    }
#endif

    [ContextMenu("Cache ShadowCaster2D")]
    public void Cache()
    {
        TryGetComponent(out _shadowCaster);

#if UNITY_EDITOR
        if (_hideUnderlyingShadowCasterInInspector && _shadowCaster != null)
            _shadowCaster.hideFlags |= HideFlags.HideInInspector;
#endif
    }

    public bool CastsShadows
    {
        get => _shadowCaster != null && _shadowCaster.castsShadows;
        set
        {
            if (_shadowCaster == null)
                Cache();
            if (_shadowCaster != null)
                _shadowCaster.castsShadows = value;
        }
    }

    public bool SelfShadows
    {
        get => _shadowCaster != null && _shadowCaster.selfShadows;
        set
        {
            if (_shadowCaster == null)
                Cache();
            if (_shadowCaster != null)
                _shadowCaster.selfShadows = value;
        }
    }

    public ShadowCaster2D.ShadowCastingOptions CastingOption
    {
        get => _shadowCaster != null ? _shadowCaster.castingOption : ShadowCaster2D.ShadowCastingOptions.CastShadow;
        set
        {
            if (_shadowCaster == null)
                Cache();
            if (_shadowCaster != null)
                _shadowCaster.castingOption = value;
        }
    }

    public float AlphaCutoff
    {
        get => _shadowCaster != null ? _shadowCaster.alphaCutoff : 0f;
        set
        {
            if (_shadowCaster == null)
                Cache();
            if (_shadowCaster != null)
                _shadowCaster.alphaCutoff = value;
        }
    }

    public float TrimEdge
    {
        get => _shadowCaster != null ? _shadowCaster.trimEdge : 0f;
        set
        {
            if (_shadowCaster == null)
                Cache();
            if (_shadowCaster != null)
                _shadowCaster.trimEdge = value;
        }
    }

    public Mesh Mesh => _shadowCaster != null ? _shadowCaster.mesh : null;

    public BoundingSphere BoundingSphere => _shadowCaster != null ? _shadowCaster.boundingSphere : default;

    public Vector3[] ShapePath => _shadowCaster != null ? _shadowCaster.shapePath : null;
}

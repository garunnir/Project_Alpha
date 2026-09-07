// ============================================================
// CharacterSightFadeHost — 시야 페이드 display → ProPixelizer dither / renderer
// ============================================================

using UnityEngine;

[DisallowMultipleComponent]
public sealed class CharacterSightFadeHost : MonoBehaviour
{
    const string UseAlphaKeyword = "USE_ALPHA_ON";
    const string UseAlphaFloat = "USE_ALPHA";
    static readonly int AlphaClipThresholdId = Shader.PropertyToID("_AlphaClipThreshold");
    static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] Transform _renderRoot;
    [SerializeField] CharacterSightFadeSettings _settings = CharacterSightFadeSettings.DefaultUnity;

    CharacterState _state;
    Renderer[] _renderers;
    Material[][] _materials;
    float[][] _baseThresholds;
    float[][] _baseColorAlphas;
    bool[][] _hadUseAlpha;
    bool _cached;

    float _target = 1f;
    float _display = 1f;
    bool _possessedSkip;

    public float TargetVisibility => _target;
    public float DisplayVisibility => _display;
    public CharacterSightFadeSettings Settings => _settings;

    public void ConfigureSettings(in CharacterSightFadeSettings settings) => _settings = settings;

    public void SetPossessedSkip(bool skip)
    {
        _possessedSkip = skip;
        if (skip)
        {
            _target = 1f;
            _display = 1f;
            ApplyDisplay(1f, force: true);
        }
    }

    public void SetTargetVisibility(float target01) =>
        _target = _possessedSkip ? 1f : Mathf.Clamp01(target01);

    public void TickDisplay(float deltaTime)
    {
        if (_possessedSkip)
            return;

        EnsureCache();
        float speed = Mathf.Max(0f, _settings.DisplayFadePerSecond);
        if (speed <= 0f || deltaTime <= 0f)
            _display = _target;
        else
            _display = Mathf.MoveTowards(_display, _target, speed * deltaTime);

        ApplyDisplay(_display, force: false);
    }

    void Awake()
    {
        _state = CharacterBodyResolve.GetInBody<CharacterState>(this);
        EnsureCache();
    }

    void OnDisable()
    {
        if (_cached)
            ApplyDisplay(1f, force: true);
    }

    void EnsureCache()
    {
        if (_cached)
            return;

        if (_renderRoot == null)
        {
            Transform pivot = transform.Find("3DRenderPivot");
            _renderRoot = pivot != null ? pivot : transform;
        }

        _renderers = _renderRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (_renderers == null || _renderers.Length == 0)
            _renderers = _renderRoot.GetComponentsInChildren<Renderer>(true);

        int n = _renderers.Length;
        _materials = new Material[n][];
        _baseThresholds = new float[n][];
        _baseColorAlphas = new float[n][];
        _hadUseAlpha = new bool[n][];

        for (int i = 0; i < n; i++)
        {
            Renderer r = _renderers[i];
            if (r == null)
                continue;

            Material[] mats = r.materials;
            _materials[i] = mats;
            _baseThresholds[i] = new float[mats.Length];
            _baseColorAlphas[i] = new float[mats.Length];
            _hadUseAlpha[i] = new bool[mats.Length];

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null)
                    continue;

                _baseThresholds[i][m] = mat.HasProperty(AlphaClipThresholdId)
                    ? mat.GetFloat(AlphaClipThresholdId)
                    : 0.5f;
                _baseColorAlphas[i][m] = mat.HasProperty(BaseColorId)
                    ? mat.GetColor(BaseColorId).a
                    : 1f;
                _hadUseAlpha[i][m] = mat.IsKeywordEnabled(UseAlphaKeyword);
            }
        }

        _cached = true;
    }

    void ApplyDisplay(float visibility01, bool force)
    {
        EnsureCache();
        float eps = Mathf.Max(0f, _settings.FullHideEpsilon);
        bool hide = visibility01 <= eps;
        bool fullyVisible = visibility01 >= 1f - eps;

        for (int i = 0; i < _renderers.Length; i++)
        {
            Renderer r = _renderers[i];
            if (r == null)
                continue;

            if (hide)
            {
                if (r.enabled || force)
                    r.enabled = false;
                continue;
            }

            if (!r.enabled)
                r.enabled = true;

            Material[] mats = _materials[i];
            if (mats == null)
                continue;

            for (int m = 0; m < mats.Length; m++)
            {
                Material mat = mats[m];
                if (mat == null)
                    continue;

                if (fullyVisible)
                {
                    if (_hadUseAlpha[i][m])
                    {
                        mat.EnableKeyword(UseAlphaKeyword);
                        mat.SetFloat(UseAlphaFloat, 1f);
                    }
                    else
                    {
                        mat.DisableKeyword(UseAlphaKeyword);
                        mat.SetFloat(UseAlphaFloat, 0f);
                    }

                    if (mat.HasProperty(AlphaClipThresholdId))
                        mat.SetFloat(AlphaClipThresholdId, _baseThresholds[i][m]);
                    if (mat.HasProperty(BaseColorId))
                    {
                        Color c = mat.GetColor(BaseColorId);
                        c.a = _baseColorAlphas[i][m];
                        mat.SetColor(BaseColorId, c);
                    }
                }
                else
                {
                    mat.EnableKeyword(UseAlphaKeyword);
                    mat.SetFloat(UseAlphaFloat, 1f);
                    if (mat.HasProperty(AlphaClipThresholdId))
                    {
                        float threshold = Mathf.Lerp(1f, _baseThresholds[i][m], visibility01);
                        mat.SetFloat(AlphaClipThresholdId, threshold);
                    }

                    if (mat.HasProperty(BaseColorId))
                    {
                        Color c = mat.GetColor(BaseColorId);
                        c.a = Mathf.Lerp(0f, Mathf.Max(_baseColorAlphas[i][m], 1e-3f), visibility01);
                        mat.SetColor(BaseColorId, c);
                    }
                }
            }
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmos()
    {
        CharacterSightFadeSettings settings = _settings;
        if (!settings.DrawEditorGizmos)
            return;

        Vector3 pos = Application.isPlaying && _state != null && _state.BodyWorldPoint.sqrMagnitude > 1e-8f
            ? _state.BodyWorldPoint
            : transform.position;

        Gizmos.color = CharacterSightFadeGizmoColors.ForVisibility(
            Application.isPlaying ? _display : 1f,
            settings.FullHideEpsilon,
            _possessedSkip);
        Gizmos.DrawWireSphere(pos, CharacterSightFadeGizmoColors.DefaultMarkerRadius);
    }
#endif
}

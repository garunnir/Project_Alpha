using UnityEngine;

// ============================================================
// MeshTextureOverride - Applies per-renderer texture override with MaterialPropertyBlock
// ============================================================
[ExecuteAlways]
[DisallowMultipleComponent]
public sealed class MeshTextureOverride : MonoBehaviour
{
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private string _texturePropertyName = "_BaseMap";
    [SerializeField] private Texture _texture;
    [SerializeField, Min(0)] private int _materialIndex;

    private MaterialPropertyBlock _propertyBlock;
    private int _cachedPropertyId;
    private string _cachedPropertyName;

    private void Reset()
    {
        _meshRenderer = GetComponent<MeshRenderer>();
    }

    private void Awake()
    {
        CacheReferences();
        Apply();
    }

    private void OnEnable()
    {
        CacheReferences();
        Apply();
    }

    private void OnValidate()
    {
        CacheReferences();
        Apply();
    }

    [ContextMenu("Apply Override")]
    public void Apply()
    {
        if (_meshRenderer == null || string.IsNullOrWhiteSpace(_texturePropertyName))
        {
            return;
        }

        if (_materialIndex >= _meshRenderer.sharedMaterials.Length)
        {
            return;
        }

        _meshRenderer.GetPropertyBlock(_propertyBlock, _materialIndex);
        _propertyBlock.SetTexture(_cachedPropertyId, _texture);
        _meshRenderer.SetPropertyBlock(_propertyBlock, _materialIndex);
    }

    [ContextMenu("Clear Override")]
    public void Clear()
    {
        if (_meshRenderer == null)
        {
            return;
        }

        _meshRenderer.SetPropertyBlock(null, _materialIndex);
    }

    private void CacheReferences()
    {
        if (_meshRenderer == null)
        {
            _meshRenderer = GetComponent<MeshRenderer>();
        }

        if (_propertyBlock == null)
        {
            _propertyBlock = new MaterialPropertyBlock();
        }

        if (_cachedPropertyName != _texturePropertyName)
        {
            _cachedPropertyName = _texturePropertyName;
            _cachedPropertyId = Shader.PropertyToID(_cachedPropertyName);
        }
    }
}

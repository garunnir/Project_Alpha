using UnityEngine;

using UnityEngine.Rendering;

using UnityEngine.Rendering.Universal;

using UnityEngine.Serialization;



// ============================================================

// SelectionOutlineRendererFeature

// URP RendererFeature: 타일 스프라이트 SelectionLayer RenderingLayer → 화면공간 외곽선.

// 캐릭터는 ProPixelizer OutlineControl(CharacterSelectionOutlineHost module) 사용.

// ============================================================

public class SelectionOutlineRendererFeature : ScriptableRendererFeature

{

    [SerializeField] private SelectionLayerConfig _layerConfig;

    [FormerlySerializedAs("_maskShader")]

    [SerializeField] private Shader _tileMaskShader;

    [SerializeField] private Shader _outlineShader;

    [SerializeField] private Color _outlineColor = Color.yellow;

    [SerializeField, Range(1, 8)] private int _outlineThicknessPx = 2;

    [SerializeField] private RenderPassEvent _passEvent = RenderPassEvent.AfterRenderingTransparents;



    Material _outlineMaterial;

    SelectionOutlinePass _pass;



    public override void Create()

    {

        DisposeMaterials();



        if (_tileMaskShader == null || _outlineShader == null || _layerConfig == null)

            return;



        _outlineMaterial = CoreUtils.CreateEngineMaterial(_outlineShader);



        _pass = new SelectionOutlinePass(

            _tileMaskShader,

            _layerConfig.TileRenderingLayerMask,

            _outlineMaterial,

            _outlineColor,

            _outlineThicknessPx)

        {

            renderPassEvent = _passEvent,

        };

    }



    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)

    {

        if (_pass == null)

            return;

        if (renderingData.cameraData.cameraType != CameraType.Game)

            return;



        renderer.EnqueuePass(_pass);

    }



    protected override void Dispose(bool disposing)

    {

        DisposeMaterials();

        _pass = null;

    }



    void DisposeMaterials()

    {

        if (_outlineMaterial != null)

        {

            CoreUtils.Destroy(_outlineMaterial);

            _outlineMaterial = null;

        }

    }

}



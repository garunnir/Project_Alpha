// Editor/TextureImportRules.cs
using UnityEditor;
using UnityEngine;

class TextureImportRules : AssetPostprocessor {
  void OnPreprocessTexture() {
    var ti = (TextureImporter)assetImporter;
    if (!assetPath.StartsWith("Assets/Resources_moved/Sprites/")) return;

    ti.textureType = TextureImporterType.Sprite;
    ti.mipmapEnabled = false;
    ti.alphaIsTransparency = true;
    ti.sRGBTexture = true;
    ti.isReadable = false;

    // 픽셀아트 감지: 경로/이름에 "Pixel" 포함 시
    bool isPixel = assetPath.Contains("/Pixel/") || assetPath.ToLower().Contains("_px_");
    bool isSingle= assetPath.Contains("/Single/");
    ti.spriteImportMode = isSingle? SpriteImportMode.Single : SpriteImportMode.Multiple; // 시트 기준
    ti.filterMode = isPixel ? FilterMode.Point : FilterMode.Bilinear;
    ti.textureCompression = isPixel ? TextureImporterCompression.Uncompressed : TextureImporterCompression.CompressedHQ;

    // 플랫폼별
    var android = new TextureImporterPlatformSettings { name="Android", overridden=true, maxTextureSize=1024, format=TextureImporterFormat.ASTC_6x6 };
    var ios     = new TextureImporterPlatformSettings { name="iPhone",  overridden=true, maxTextureSize=1024, format=TextureImporterFormat.ASTC_6x6 };
    if (isPixel) { android.format = ios.format = TextureImporterFormat.RGBA32; }
    ti.SetPlatformTextureSettings(android);
    ti.SetPlatformTextureSettings(ios);
  }
}

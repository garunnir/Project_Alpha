using System;
using System.IO;
using UnityEngine;

namespace Garunnir.Utillity
{
    public static class TextureIO
    {
        /// <summary>
        /// 파일에서 Texture2D 로드. 실패 시 null.
        /// </summary>
        public static Texture2D LoadImage(string path)
        {
            if (!File.Exists(path)) return null;

            byte[] bytes = File.ReadAllBytes(path);
            if (bytes == null) return null;

            var tex = new Texture2D(1, 1, TextureFormat.ARGB32, false);
            return tex.LoadImage(bytes) ? tex : null;
        }

        /// <summary>
        /// non-readable 텍스처를 임시 RenderTexture로 복사해 바이트로 인코딩.
        /// </summary>
        public static byte[] GetTextureBytesFromCopy(Texture2D texture, bool isJpeg = false)
        {
            Debug.LogWarning("Saving non-readable textures is slower than saving readable textures");

            Texture2D readable = null;
            RenderTexture rt = RenderTexture.GetTemporary(texture.width, texture.height);
            RenderTexture activeRT = RenderTexture.active;

            try
            {
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;

                readable = new Texture2D(
                    texture.width,
                    texture.height,
                    isJpeg ? TextureFormat.RGB24 : TextureFormat.RGBA32,
                    false);

                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0, false);
                readable.Apply(false, false);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                UnityEngine.Object.DestroyImmediate(readable);
                return null;
            }
            finally
            {
                RenderTexture.active = activeRT;
                RenderTexture.ReleaseTemporary(rt);
            }

            try
            {
                return isJpeg ? readable.EncodeToJPG(100) : readable.EncodeToPNG();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                return null;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(readable);
            }
        }
    }
}
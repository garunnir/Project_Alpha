using System.IO;
using System.Linq;
using UnityEngine;
namespace Garunnir.Utillity
{
    public static class PathUtility
    {
        /// <summary>
        /// 여러 경로 문자열을 '/'로 결합 (Unity 리소스/URL 스타일).
        /// </summary>
        public static string CombinePath(params string[] parts)
        {
            // 기존 구현 유지(동작 동일). 필요하면 string.Join로 더 깔끔하게 교체 가능.
            string str = string.Empty;
            foreach (var item in parts)
            {
                str += item;
                if (parts.Last() != item)
                    str += "/";
            }
            return str;
        }

        /// <summary>
        /// 디렉토리 존재 보장. isOnlyDir=false면 파일 경로에서 디렉토리만 추출.
        /// </summary>
        public static string EnsureDirectory(string path, bool isOnlyDir = true, bool logPath = true)
        {
            if (logPath) Debug.LogWarning(path);

            if (!isOnlyDir)
                path = Path.GetDirectoryName(path);

            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);

            return path;
        }
    }
}
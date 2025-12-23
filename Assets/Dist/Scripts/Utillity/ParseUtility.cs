using System;
using System.Globalization;
namespace Garunnir.Utillity
{
    public static class ParseUtility
    {
        /// <summary>
        /// 문자열을 지정 타입으로 변환.
        /// 지원: bool, float, int, 그 외는 string 그대로.
        /// </summary>
        public static T ObjectParser<T>(string str)
        {
            if (typeof(T) == typeof(bool))
                return (T)(object)bool.Parse(str);

            if (typeof(T) == typeof(float))
                return (T)(object)float.Parse(str, CultureInfo.InvariantCulture);

            if (typeof(T) == typeof(int))
                return (T)(object)int.Parse(str, CultureInfo.InvariantCulture);

            return (T)(object)str;
        }
    }
}
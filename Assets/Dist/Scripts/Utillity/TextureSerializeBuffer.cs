using System.Collections.Generic;
using System.Linq;
using System.Text;
namespace Garunnir.Utillity
{
    public static class TextSerializeBuffer
    {
        public const string Divider = "==";
        public const string LF = "\n";

        // 전역 버퍼: 기존 코드 유지 목적. (동시성/재진입 위험 있음)
        public static readonly StringBuilder SB = new StringBuilder();

        public static void Clear() => SB.Clear();

        /// <summary> Head:obj1,obj2,obj3\n </summary>
        public static void AppendValues(string head, params object[] objects)
        {
            SB.Append($"{head}:");

            foreach (var obj in objects)
            {
                SB.Append(obj);
                if (obj != objects.Last())
                    SB.Append(",");
            }

            SB.Append(LF);
        }

        /// <summary> head/List:a,b,c\n </summary>
        public static void AppendList(string head, List<string> list)
        {
            SB.Append($"{head}/List:");

            foreach (var obj in list)
            {
                SB.Append(obj);
                if (obj != list.Last())
                    SB.Append(",");
            }

            SB.Append(LF);
        }

        /// <summary> head/Dic:k=v,k=v\n </summary>
        public static void AppendDictionary(string head, Dictionary<string, float> dic)
        {
            SB.Append($"{head}/Dic:");

            foreach (var kv in dic)
            {
                SB.Append(kv.Key);
                SB.Append("=");
                SB.Append(kv.Value);

                if (kv.Key != dic.Last().Key)
                    SB.Append(",");
            }

            SB.Append(LF);
        }

        /// <summary> head/Dic:k=v,k=v\n </summary>
        public static void AppendDictionary(string head, Dictionary<string, object> dic)
        {
            SB.Append($"{head}/Dic:");

            foreach (var kv in dic)
            {
                SB.Append(kv.Key);
                SB.Append("=");
                SB.Append(kv.Value);

                if (kv.Key != dic.Last().Key)
                    SB.Append(",");
            }

            SB.Append(LF);
        }

        public static string TupleSingle(string key, bool flag, object value)
        {
            if (value != null && value.ToString() == string.Empty)
                value = "Null";

            return string.Format("{0}={1}|{2}", key, flag, value);
        }

        public static void AppendTupleDictionary(string head, Dictionary<string, (bool, object)> tupledic)
        {
            SB.Append($"{head}/Dic:");

            foreach (var kv in tupledic)
            {
                SB.Append(TupleSingle(kv.Key, kv.Value.Item1, kv.Value.Item2));
                if (kv.Key != tupledic.Last().Key)
                    SB.Append(",");
            }

            SB.Append(LF);
        }
    }
}
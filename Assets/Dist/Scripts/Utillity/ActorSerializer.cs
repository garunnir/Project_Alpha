using Garunnir;
using PixelCrushers.DialogueSystem;
namespace Garunnir.Utillity
{

    public static class ActorSerializer
    {
        /// <summary>
        /// Actor 데이터를 프로젝트 전용 텍스트 포맷으로 직렬화.
        /// </summary>
        public static string Serialize(Actor character)
        {
            TextSerializeBuffer.Clear();

            TextSerializeBuffer.SB.Append(
                $"{TextSerializeBuffer.LF}{GameManager.Instance.GetFormDic(Form0.character, Form.id)}:");

            TextSerializeBuffer.SB.Append(character.id);
            TextSerializeBuffer.SB.Append(TextSerializeBuffer.LF);

            // 필요하면 여기서 fields/bodyCore 등 확장
            // TextSerializeBuffer.AppendTupleDictionary(...)

            TextSerializeBuffer.SB.Append(TextSerializeBuffer.Divider);
            TextSerializeBuffer.SB.Append(TextSerializeBuffer.LF);

            return TextSerializeBuffer.SB.ToString();
        }
    }
}
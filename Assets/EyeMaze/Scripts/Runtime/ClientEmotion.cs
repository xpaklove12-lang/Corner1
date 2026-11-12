using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 내담자의 지금 감정. 수치가 아닌 상태 하나로 두며, 대사 CSV의 emotion 칸으로 바뀐다.
    /// </summary>
    public enum ClientEmotion
    {
        [InspectorName("평온")] Calm,
        [InspectorName("경계")] Guarded,
        [InspectorName("불안")] Anxious,
        [InspectorName("슬픔")] Sad,
        [InspectorName("안도")] Relieved,
    }

    /// <summary>
    /// 대사 CSV의 emotion 칸 글과 ClientEmotion을 서로 바꾼다.
    /// </summary>
    public static class ClientEmotionCsv
    {
        /// <summary>emotion 칸에 쓸 수 있는 값 목록 (오류 문구용)</summary>
        public const string VALUES = "calm, guarded, anxious, sad, relieved";

        /// <summary>
        /// emotion 칸 글을 감정으로 읽는다. 대소문자는 가리지 않는다.
        /// text를 사용하며, 정해진 값이면 true를 반환하고 emotion에 감정을 쓴다.
        /// </summary>
        public static bool TryParse(string text, out ClientEmotion emotion)
        {
            switch ((text ?? "").Trim().ToLowerInvariant())
            {
                case "calm": emotion = ClientEmotion.Calm; return true;
                case "guarded": emotion = ClientEmotion.Guarded; return true;
                case "anxious": emotion = ClientEmotion.Anxious; return true;
                case "sad": emotion = ClientEmotion.Sad; return true;
                case "relieved": emotion = ClientEmotion.Relieved; return true;
                default: emotion = ClientEmotion.Calm; return false;
            }
        }
    }
}

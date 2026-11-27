using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 게임 전역 설정 매니저. 씬에 하나 두며, 다른 스크립트보다 먼저 초기화된다.
    /// 지금은 언어 설정을 받아 시작 언어를 정한다 (스펙 LOC-01, LOC-02).
    /// 현재 언어 코드는 어디서나 GameSettings.Language로 읽고, 바꿀 때는 GameSettings.SetLanguage를 부른다.
    /// 값은 정적 Loc에 보관하므로 이 오브젝트를 찾지 않아도 되고, 에디터에서도 읽을 수 있다.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class GameSettings : MonoBehaviour
    {
        [Header("언어")]
        [Tooltip("언어 설정 (Data/Loc/LocConfig.asset). 지원 언어와 시작 언어 규칙")]
        [SerializeField] private LocConfig _locConfig;

        /// <summary>현재 언어 코드 (예: ko, en, ja)</summary>
        public static string Language => Loc.Current;

        void Awake() => Loc.Init(_locConfig);

        /// <summary>
        /// 플레이어가 고른 언어로 바꾸고 저장한다.
        /// code를 사용하며, 목록에 있는 언어면 현재 언어를 바꾸고 true를 반환한다.
        /// </summary>
        public static bool SetLanguage(string code) => Loc.SetLanguage(code);
    }
}

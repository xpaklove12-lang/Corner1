using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace EyeMaze
{
    /// <summary>
    /// 플레이어가 조작하지 않는 구간(시작 독백, 문 앞 혼잣말, 손님 입장·퇴장 컷신)을 키를 누르고 있는 동안 빨리 감는다.
    /// 연출은 Time.timeScale로 배속하고, 글자 찍기는 바로 끝내며, 대사는 짧은 간격으로 자동으로 넘긴다.
    /// </summary>
    public class CutsceneFastForward : MonoBehaviour
    {
        private static CutsceneFastForward _current;

        [Header("연결")]
        [SerializeField] private GameFlow _flow;
        [SerializeField] private OpeningMonologue _monologue;

        [Header("입력")]
        [Tooltip("누르고 있는 동안 빨리 감는 키보드 키")]
        [SerializeField] private Key _key = Key.LeftCtrl;
        [Tooltip("누르고 있는 동안 빨리 감는 게임패드 버튼")]
        [SerializeField] private GamepadButton _padButton = GamepadButton.RightShoulder;

        [Header("빨리감기")]
        [Tooltip("빨리감기 중 연출 배속")]
        [SerializeField] private float _timeScale = 4f;
        [Tooltip("한 줄이 끝까지 보인 뒤 자동으로 넘기기까지의 실제 시간 (초)")]
        [SerializeField] private float _autoAdvanceInterval = 0.15f;

        [Header("안내")]
        [Tooltip("컷신 중 필드 조작 안내 자리에 함께 띄우는 문구. Strings.csv의 hint.fast_forward가 있으면 그걸 쓴다")]
        [SerializeField] private string _hintText = "Ctrl  빨리감기";

        private bool _active;
        private float _advanceTimer;

        /// <summary>빨리감기가 적용 중인지.</summary>
        public static bool IsFastForwarding => _current != null && _current._active;

        /// <summary>필드 컷신 중이면 빨리감기 안내 문구, 그 밖에는 null.</summary>
        public static string Hint => _current != null && _current.isActiveAndEnabled && FieldCutscene.IsPlaying
                                     ? Strings.Get("hint.fast_forward", _current._hintText) : null;

        void OnEnable() => _current = this;

        void OnDisable()
        {
            SetActive(false);
            if (_current == this) _current = null;
        }

        void OnValidate()
        {
            _timeScale = Mathf.Max(1f, _timeScale);
            _autoAdvanceInterval = Mathf.Max(0f, _autoAdvanceInterval);
        }

        void Update()
        {
            bool inSegment = OpeningMonologue.IsPlaying || FieldCutscene.IsPlaying;
            SetActive(inSegment && Held());
            if (!_active) return;

            Typewriter.SkipAll();
            AutoAdvance(Time.unscaledDeltaTime);
        }

        /// <summary>
        /// 빨리감기 키나 버튼이 눌려 있는지 확인한다.
        /// _key, _padButton을 사용하며, 눌려 있으면 true를 반환한다.
        /// </summary>
        private bool Held()
        {
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (kb != null && kb[_key].isPressed) return true;
            if (pad != null && pad[_padButton].isPressed) return true;
            return false;
        }

        /// <summary>
        /// 빨리감기를 켜거나 끈다. 켜면 timeScale을 배속으로, 끄면 1로 되돌린다.
        /// on과 _timeScale을 사용하며, _active, _advanceTimer, Time.timeScale을 바꾼다.
        /// </summary>
        private void SetActive(bool on)
        {
            if (on) Time.timeScale = _timeScale;
            if (on == _active) return;
            _active = on;
            _advanceTimer = 0f;
            if (!on) Time.timeScale = 1f;
        }

        /// <summary>
        /// 한 줄이 끝까지 보인 채 간격이 지나면 그 줄을 넘긴다. 독백 중이면 독백을, 아니면 필드 대사창을 넘긴다.
        /// 실제 경과 시간 dt와 _autoAdvanceInterval을 사용하며, _advanceTimer와 대사 진행 상태를 바꾼다.
        /// </summary>
        private void AutoAdvance(float dt)
        {
            bool monologue = OpeningMonologue.IsPlaying;
            bool ready = monologue ? _monologue.CanAdvance : _flow.ShowingText;
            // 줄 사이 공백이나 이동만 진행 중일 때는 다음 줄이 보인 뒤부터 다시 센다.
            if (!ready) { _advanceTimer = 0f; return; }

            _advanceTimer += dt;
            if (_advanceTimer < _autoAdvanceInterval) return;
            _advanceTimer = 0f;
            if (monologue) _monologue.Advance();
            else _flow.AdvanceText();
        }
    }
}

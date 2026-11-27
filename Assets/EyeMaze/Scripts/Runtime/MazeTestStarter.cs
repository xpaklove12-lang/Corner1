using UnityEngine;
using UnityEngine.InputSystem;

namespace EyeMaze
{
    /// <summary>
    /// 눈 미로 테스트 씬의 시작과 진행을 맡는다.
    /// Play하면 필드와 대화 없이 바로 만남 화면에 들어가 눈을 들여다보고, 리셋 뒤에도 다시 들여다본다.
    /// 동공 한가운데에 닿으면 엔딩 대신 탈출 안내와 걸린 시간을 보여 주고, R로 처음부터 다시 한다.
    /// 대화가 없으므로 T로 손님의 의심이 풀린 상태(상처가 보이는지)를 켜고 끄고, [ ]로 상처 단계를 앞뒤로 옮긴다.
    /// </summary>
    public class MazeTestStarter : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameFlow _flow;
        [SerializeField] private EyeMazeDirector _director;

        [Header("테스트 조건")]
        [Tooltip("신뢰나 문 플래그 없이 모든 층 문을 열어 둔다")]
        [SerializeField] private bool _openAllGates = true;
        [Tooltip("들여다보는 동안 부담감이 차지 않게 한다")]
        [SerializeField] private bool _pauseBurden = true;
        [Tooltip("시작할 때와 리셋한 뒤 손님의 의심이 풀린 상태로 두어 상처를 모두 보이게 한다. T로 바꿀 수 있다")]
        [SerializeField] private bool _showScars = true;

        [Header("탈출 안내")]
        [Tooltip("공용 문구 파일에 msg.maze_test_escape가 없을 때 쓰는 글. {seconds} = 걸린 시간")]
        [SerializeField, TextArea(2, 4)] private string _escapeMessage = "미로를 빠져나왔다. (걸린 시간 {seconds}초)\nR = 다시 하기";

        [Header("의심 풀기")]
        [Tooltip("공용 문구 파일에 msg.maze_test_doubt_on이 없을 때 쓰는 글")]
        [SerializeField, TextArea(1, 2)] private string _doubtOnMessage = "(테스트) 의심이 풀렸다.";
        [Tooltip("공용 문구 파일에 msg.maze_test_doubt_off가 없을 때 쓰는 글")]
        [SerializeField, TextArea(1, 2)] private string _doubtOffMessage = "(테스트) 다시 의심한다.";

        [Header("상처 단계")]
        [Tooltip("공용 문구 파일에 msg.maze_test_stage가 없을 때 쓰는 글. {stage} = 지금 단계, {stages} = 전체 단계")]
        [SerializeField, TextArea(1, 2)] private string _stageMessage = "(테스트) 상처 {stage} / {stages}단계";
        [Tooltip("공용 문구 파일에 msg.maze_test_help가 없을 때 쓰는 글. 들여다보기 시작할 때 보인다")]
        [SerializeField, TextArea(1, 3)] private string _helpMessage = "(테스트) [ ] 상처 단계 앞뒤 · T 상처 보이기/숨기기 · R 처음부터";
        [Tooltip("공용 문구 파일에 msg.maze_test_no_pattern이 없을 때 쓰는 글. 지금 손님에게 상처 무늬 장이 없을 때 보인다")]
        [SerializeField, TextArea(1, 3)] private string _noPatternMessage = "(테스트) 이 손님에게 상처 무늬가 없다. 상담의 '홍채 무늬' 목록을 확인하거나 씬을 다시 연다.";

        private float _gazeStartTime;
        private bool _escaped;

        void Start()
        {
            ApplyConditions();
            _director.GazeLocked = true;
            _director.CoreTouchOverride = Escape;
            _flow.EnterEncounterNow();
        }

        void LateUpdate()
        {
            // Play 중 Inspector에서 테스트 조건을 바꿔도 바로 반영한다.
            ApplyConditions();
            Keyboard kb = Keyboard.current;
            // 탈출 뒤에는 디렉터가 입력을 받지 않으므로 R을 여기서 받는다.
            if (_escaped && kb != null && kb.rKey.wasPressedThisFrame) Restart();
            if (!_escaped && kb != null && kb.tKey.wasPressedThisFrame) ToggleDoubt();
            if (!_escaped && kb != null && kb.rightBracketKey.wasPressedThisFrame) StepStage(1);
            if (!_escaped && kb != null && kb.leftBracketKey.wasPressedThisFrame) StepStage(-1);
            // 시작할 때와 R로 리셋한 뒤에는 대화 상태가 되므로 바로 다시 들여다본다.
            if (!_escaped && _director.CurrentMode == EyeMazeDirector.Mode.Talk) BeginGaze();
        }

        /// <summary>
        /// Inspector의 테스트 조건을 미로와 디렉터에 적용한다.
        /// _openAllGates와 _pauseBurden을 사용하며, 미로의 OpenAllGates와 디렉터의 BurdenPaused를 바꾼다.
        /// </summary>
        private void ApplyConditions()
        {
            _director.Maze.OpenAllGates = _openAllGates;
            _director.BurdenPaused = _pauseBurden;
        }

        /// <summary>
        /// 눈을 들여다보기 시작하고 걸린 시간을 새로 잰다. 리셋이 끈 상처 보이기를 Inspector 값으로 되돌리고 조작 안내를 띄운다.
        /// _showScars와 _helpMessage를 사용하며, 디렉터의 모드, ForceDoubtCleared, 문구와 _gazeStartTime을 바꾼다.
        /// </summary>
        private void BeginGaze()
        {
            _director.ToggleGaze();
            _director.ForceDoubtCleared = _showScars;
            _director.Message = Strings.Get("msg.maze_test_help", _helpMessage);
            _gazeStartTime = Time.time;
            // 상처 무늬 장이 비어 있으면 단계를 옮겨도 아무것도 새겨지지 않으므로 알려 준다.
            if (_director.ActiveCase != null && _director.ActiveCase.IrisPattern.Count > 0) return;
            _director.Message = Strings.Get("msg.maze_test_no_pattern", _noPatternMessage);
            Debug.LogWarning("[미로 테스트] 지금 손님에게 상처 무늬 장이 없다. 상담(ClientCase)의 '홍채 무늬' 목록을 채우거나, 씬 파일을 밖에서 고쳤다면 씬을 다시 연다.", _director.ActiveCase);
        }

        /// <summary>
        /// 상처 단계를 한 단계 앞이나 뒤로 옮기고 지금 단계를 문구로 알린다. 0단계와 전체 단계 사이에서 멈춘다.
        /// step(+1 앞, -1 뒤)과 미로의 지금 단계, 전체 단계를 사용하며, 디렉터의 테스트 단계와 문구를 바꾼다.
        /// </summary>
        private void StepStage(int step)
        {
            IrisMaze maze = _director.Maze;
            int stage = Mathf.Clamp(maze.ComplexityStage + step, 0, maze.ComplexityStages);
            _director.SetStageOverride(stage);
            _director.Message = Strings.Get("msg.maze_test_stage", _stageMessage, ("stage", stage), ("stages", maze.ComplexityStages));
        }

        /// <summary>
        /// 동공 한가운데에 닿았을 때 탈출 안내를 띄우고 미로 입력을 멈춘다.
        /// _gazeStartTime과 _escapeMessage를 사용하며, _escaped와 디렉터의 입력 상태, 문구를 바꾼다.
        /// </summary>
        private void Escape()
        {
            if (_escaped) return;
            _escaped = true;
            _director.AcceptInput = false;
            string seconds = (Time.time - _gazeStartTime).ToString("0.0");
            _director.Message = Strings.Get("msg.maze_test_escape", _escapeMessage, ("seconds", seconds));
        }

        /// <summary>
        /// 손님의 의심이 풀린 상태를 켜고 끄고, 바뀐 상태를 문구로 알린다.
        /// 디렉터의 ForceDoubtCleared를 사용하며, 그 값과 디렉터의 문구를 바꾼다.
        /// </summary>
        private void ToggleDoubt()
        {
            _director.ForceDoubtCleared = !_director.ForceDoubtCleared;
            _director.Message = _director.ForceDoubtCleared
                ? Strings.Get("msg.maze_test_doubt_on", _doubtOnMessage)
                : Strings.Get("msg.maze_test_doubt_off", _doubtOffMessage);
        }

        /// <summary>
        /// 탈출 뒤 미로를 처음 상태로 되돌리고 입력을 다시 받는다.
        /// 입력값은 없으며, 디렉터의 상태와 입력 상태, _escaped를 바꾼다. 다시 들여다보기는 LateUpdate가 한다.
        /// </summary>
        private void Restart()
        {
            _director.ResetAll();
            _director.AcceptInput = true;
            _escaped = false;
        }
    }
}

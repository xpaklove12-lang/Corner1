using UnityEngine;
using UnityEngine.InputSystem;

namespace EyeMaze
{
    /// <summary>
    /// 눈 미로 테스트 씬의 시작과 진행을 맡는다.
    /// Play하면 필드와 대화 없이 바로 만남 화면에 들어가 눈을 들여다보고, 리셋 뒤에도 다시 들여다본다.
    /// 동공 한가운데에 닿으면 엔딩 대신 탈출 안내와 걸린 시간을 보여 주고, R로 처음부터 다시 한다.
    /// </summary>
    public class MazeTestStarter : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private GameFlow _flow;
        [SerializeField] private EyeMazeDirector _director;
        [Tooltip("미로 없이 눈 하나로 하는 판. 켜져 있으면 탈출 안내에 맞은 횟수를 더한다")]
        [SerializeField] private HeartArena _arena;

        [Header("테스트 조건")]
        [Tooltip("신뢰나 문 플래그 없이 모든 층 문을 열어 둔다")]
        [SerializeField] private bool _openAllGates = true;
        [Tooltip("들여다보는 동안 부담감이 차지 않게 한다")]
        [SerializeField] private bool _pauseBurden = true;

        [Header("탈출 안내")]
        [Tooltip("공용 문구 파일에 msg.maze_test_escape가 없을 때 쓰는 글. {seconds} = 걸린 시간")]
        [SerializeField, TextArea(2, 4)] private string _escapeMessage = "미로를 빠져나왔다. (걸린 시간 {seconds}초)\nR = 다시 하기";
        [Tooltip("공용 문구 파일에 msg.arena_hits가 없을 때 쓰는 글. {hits} = 맞은 횟수, {combo} = 최고 콤보")]
        [SerializeField] private string _hitsMessage = "맞은 횟수 {hits} · 최고 콤보 {combo}";

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
        /// 눈을 들여다보기 시작하고 걸린 시간을 새로 잰다.
        /// 입력값은 없으며, 디렉터의 모드와 _gazeStartTime을 바꾼다.
        /// </summary>
        private void BeginGaze()
        {
            _director.ToggleGaze();
            _gazeStartTime = Time.time;
        }

        /// <summary>
        /// 동공 한가운데에 닿았을 때 탈출 안내를 띄우고 미로 입력을 멈춘다.
        /// _gazeStartTime, _escapeMessage, 판의 맞은 횟수와 최고 콤보를 사용하며, _escaped와 디렉터의 입력 상태, 문구를 바꾼다.
        /// </summary>
        private void Escape()
        {
            if (_escaped) return;
            _escaped = true;
            _director.AcceptInput = false;
            string seconds = (Time.time - _gazeStartTime).ToString("0.0");
            _director.Message = Strings.Get("msg.maze_test_escape", _escapeMessage, ("seconds", seconds));
            // 눈 하나로 하는 판이면 맞은 횟수를 함께 보여 준다.
            if (_arena != null && _arena.ArenaOn) _director.Message += "\n" + Strings.Get("msg.arena_hits", _hitsMessage, ("hits", _arena.Hits), ("combo", _arena.BestCombo));
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

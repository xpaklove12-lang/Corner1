using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046: 열쇠 조각 맞추기 화면. 기억 글과 순서가 섞인 낱말 조각을 보여 주고, 조각을 정답 순서대로 놓으면 완성을 알린다.
    /// 조각은 버튼을 누르거나 숫자키로 놓고, Backspace로 마지막 조각을 되돌리고, Esc로 그만둔다.
    /// 화면 오브젝트는 씬에 미리 만들어 두고 여기서는 참조만 한다.
    /// </summary>
    public class KeyForgeUI : MonoBehaviour
    {
        private const int MAX_SHUFFLES = 20;

        [Header("연결")]
        [Tooltip("기억 글과 들은 말을 보여 줄 글")]
        [SerializeField] private Text _memoryText;
        [Tooltip("낱말 조각 버튼. 조각 수보다 많은 버튼은 숨긴다")]
        [SerializeField] private Button[] _pieceButtons = new Button[KeyPieces.MAX_PIECES];
        [Tooltip("조각 버튼의 글. 버튼과 같은 순서")]
        [SerializeField] private Text[] _pieceLabels = new Text[KeyPieces.MAX_PIECES];
        [Tooltip("놓은 조각을 보여 줄 칸. 왼쪽부터 순서대로")]
        [SerializeField] private Text[] _slotLabels = new Text[KeyPieces.MAX_PIECES];

        [Header("문구")]
        [Tooltip("순서가 틀렸을 때 문구 줄에 나오는 글")]
        [SerializeField, TextArea(1, 2)] private string _wrongOrderMessage = "말이 이어지지 않는다.";
        [Tooltip("아직 조각을 놓지 않은 칸에 보이는 글")]
        [SerializeField] private string _emptySlotText = "＿＿";

        private readonly List<int> _placedButtons = new List<int>();
        private string[] _answer = Array.Empty<string>();
        private int[] _buttonPieces = Array.Empty<int>();
        private bool _open;
        private int _closedFrame = -1;

        /// <summary>조각을 정답 순서대로 모두 놓았을 때 완성한 문장과 함께 불린다.</summary>
        public event Action<string> Completed;
        /// <summary>Esc로 그만두었을 때 불린다.</summary>
        public event Action Cancelled;
        /// <summary>조각을 모두 놓았지만 순서가 틀렸을 때 불린다.</summary>
        public event Action WrongOrder;

        public bool IsOpen => _open;
        /// <summary>화면이 열려 있거나 방금 닫힌 프레임이면 다른 키 입력을 받지 않게 한다.</summary>
        public bool BlocksInput => _open || _closedFrame == Time.frameCount;
        public string WrongOrderMessage => _wrongOrderMessage;

        void Awake()
        {
            for (int i = 0; i < _pieceButtons.Length; i++)
            {
                int index = i;
                _pieceButtons[i].onClick.AddListener(() => Press(index));
            }
        }

        void Update()
        {
            // 대화 기록 창이 열려 있으면 조각 입력과 그만두기를 받지 않는다.
            if (!_open || DialogueLog.BlocksInput) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            if (keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            if (keyboard.backspaceKey.wasPressedThisFrame) { TakeBack(); return; }
            for (int i = 0; i < _answer.Length; i++)
            {
                if (!keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame) continue;
                Press(i);
                return;
            }
        }

        /// <summary>
        /// 화면을 열고 조각을 섞어 버튼에 나눠 준다. 섞은 순서가 정답과 같으면 다시 섞는다.
        /// memory(기억 글), heard(들은 말, 없으면 빈 글), pieces(정답 순서의 낱말 조각)를 사용하며, 화면 표시와 조각 상태를 바꾼다.
        /// </summary>
        public void Open(string memory, string heard, string[] pieces)
        {
            _answer = pieces;
            _buttonPieces = new int[pieces.Length];
            for (int i = 0; i < pieces.Length; i++) _buttonPieces[i] = i;
            // 같은 글의 조각뿐이라 어떻게 섞어도 정답과 같을 수 있으므로 다시 섞는 횟수를 정해 둔다.
            for (int attempt = 0; attempt < MAX_SHUFFLES; attempt++)
            {
                Shuffle(_buttonPieces);
                if (!IsInOrder(_buttonPieces, pieces)) break;
            }

            gameObject.SetActive(true);
            _open = true;
            _memoryText.text = string.IsNullOrEmpty(heard) ? memory : memory + "\n\n" + heard;
            for (int i = 0; i < _pieceButtons.Length; i++)
            {
                bool used = i < pieces.Length;
                _pieceButtons[i].gameObject.SetActive(used);
                _slotLabels[i].gameObject.SetActive(used);
                if (used) _pieceLabels[i].text = $"{i + 1}. {pieces[_buttonPieces[i]]}";
            }
            ClearPlaced();
        }

        /// <summary>
        /// 화면을 닫는다. 알림은 보내지 않는다.
        /// 입력값은 없으며, 열림 상태와 화면 표시, 닫힌 프레임 기록을 바꾼다.
        /// </summary>
        public void Close()
        {
            if (!_open) return;
            _open = false;
            _closedFrame = Time.frameCount;
            gameObject.SetActive(false);
        }

        /// <summary>
        /// 버튼의 조각을 다음 빈칸에 놓는다. 마지막 칸까지 차면 순서를 확인해 완성 또는 틀림을 알린다.
        /// 버튼 번호 button을 사용하며, 놓은 조각 목록과 버튼, 칸의 표시를 바꾼다.
        /// </summary>
        public void Press(int button)
        {
            if (!_open || button < 0 || button >= _answer.Length || _placedButtons.Contains(button)) return;
            _slotLabels[_placedButtons.Count].text = _answer[_buttonPieces[button]];
            _placedButtons.Add(button);
            _pieceButtons[button].interactable = false;
            if (_placedButtons.Count < _answer.Length) return;

            for (int i = 0; i < _placedButtons.Count; i++)
            {
                // 같은 글의 조각이 둘 있어도 보이는 문장이 같으면 맞은 것으로 본다.
                if (_answer[_buttonPieces[_placedButtons[i]]] == _answer[i]) continue;
                ClearPlaced();
                WrongOrder?.Invoke();
                return;
            }
            Close();
            Completed?.Invoke(string.Join(" ", _answer));
        }

        /// <summary>
        /// 마지막으로 놓은 조각을 버튼으로 되돌린다.
        /// 입력값은 없으며, 놓은 조각 목록과 버튼, 칸의 표시를 바꾼다.
        /// </summary>
        private void TakeBack()
        {
            if (_placedButtons.Count == 0) return;
            int button = _placedButtons[_placedButtons.Count - 1];
            _placedButtons.RemoveAt(_placedButtons.Count - 1);
            _pieceButtons[button].interactable = true;
            _slotLabels[_placedButtons.Count].text = _emptySlotText;
        }

        /// <summary>
        /// 화면을 닫고 그만두었음을 알린다.
        /// 입력값은 없으며, 열림 상태를 바꾸고 Cancelled를 부른다.
        /// </summary>
        private void Cancel()
        {
            Close();
            Cancelled?.Invoke();
        }

        /// <summary>
        /// 놓은 조각을 모두 비우고 버튼을 다시 누를 수 있게 한다.
        /// 입력값은 없으며, 놓은 조각 목록과 버튼, 칸의 표시를 바꾼다.
        /// </summary>
        private void ClearPlaced()
        {
            _placedButtons.Clear();
            for (int i = 0; i < _answer.Length; i++)
            {
                _pieceButtons[i].interactable = true;
                _slotLabels[i].text = _emptySlotText;
            }
        }

        /// <summary>
        /// 번호 배열을 무작위로 섞는다.
        /// values를 사용하며, 배열의 순서를 바꾼다.
        /// </summary>
        private static void Shuffle(int[] values)
        {
            for (int i = values.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (values[i], values[j]) = (values[j], values[i]);
            }
        }

        /// <summary>
        /// 섞은 조각을 순서대로 읽은 문장이 정답과 같은지 판단한다. 같은 글의 조각끼리 자리만 바뀐 경우도 같은 것으로 본다.
        /// order(버튼마다의 조각 번호)와 pieces(정답 순서의 조각)를 사용하며, 같으면 true를 반환한다.
        /// </summary>
        private static bool IsInOrder(int[] order, string[] pieces)
        {
            for (int i = 0; i < order.Length; i++)
                if (pieces[order[i]] != pieces[i]) return false;
            return true;
        }
    }
}

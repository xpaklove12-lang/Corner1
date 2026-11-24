using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046: 열쇠 조각 맞추기 화면. 기억 글과 순서가 섞인 낱말 조각을 보여 주고, 조각을 정답 순서대로 놓으면 완성을 알린다.
    /// 놓은 조각은 열쇠 몸통 아래의 빈 홈으로 움직여 끼워지고, 다 맞으면 열쇠가 완성된 색으로 잠깐 보인 뒤 닫힌다.
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
        [Tooltip("조각이 끼워질 빈 홈. 왼쪽부터 순서대로. 조각 버튼이 이 자리로 움직인다")]
        [SerializeField] private Text[] _slotLabels = new Text[KeyPieces.MAX_PIECES];
        [Tooltip("빈 홈의 그림. 놓은 칸과 같은 순서. 조각 수가 적으면 같은 가로 범위를 넓게 나눠 쓴다")]
        [SerializeField] private RectTransform[] _holes = new RectTransform[KeyPieces.MAX_PIECES];
        [Tooltip("완성할 때 색을 바꿀 열쇠 몸통 그림")]
        [SerializeField] private Graphic[] _keyGraphics = new Graphic[0];

        [Header("연출")]
        [Tooltip("조각이 홈으로 또는 원래 자리로 움직이는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _moveSeconds = 0.15f;
        [Tooltip("완성한 열쇠를 보여 주는 시간(초)")]
        [SerializeField, Min(0f)] private float _doneHoldSeconds = 0.6f;
        [Tooltip("완성한 열쇠와 조각의 색")]
        [SerializeField] private Color _doneColor = new Color(1f, 0.85f, 0.35f, 1f);
        // 조각 버튼의 원래 자리와 색, 지금 움직이는 출발점과 도착점, 진행도(1이면 멈춤)
        private Vector3[] _homePositions;
        private Color[] _pieceColors;
        private Color[] _keyColors;
        private Vector3[] _moveFrom;
        private Vector3[] _moveTo;
        private float[] _moveT;
        // 완성한 열쇠를 보여 주는 남은 시간(0 이하면 보여 주는 중이 아님)과 그때의 문장
        private float _doneT;
        private string _doneSentence;
        // 마지막 조각이 홈에 닿은 뒤 틀림을 알리려고 기다리는 중인지
        private bool _wrongPending;
        private Vector2[] _homeSizes;
        private Vector2[] _sizeFrom;
        private Vector2[] _sizeTo;
        // 씬에 놓인 홈 자리. 조각 수에 따라 어느 자리를 쓸지 고른다.
        private Vector2[] _holeMins;
        private Vector2[] _holeMaxes;

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
            _homePositions = new Vector3[_pieceButtons.Length];
            _pieceColors = new Color[_pieceButtons.Length];
            _moveFrom = new Vector3[_pieceButtons.Length];
            _moveTo = new Vector3[_pieceButtons.Length];
            _moveT = new float[_pieceButtons.Length];
            _homeSizes = new Vector2[_pieceButtons.Length];
            _sizeFrom = new Vector2[_pieceButtons.Length];
            _sizeTo = new Vector2[_pieceButtons.Length];
            for (int i = 0; i < _pieceButtons.Length; i++)
            {
                int index = i;
                _pieceButtons[i].onClick.AddListener(() => Press(index));
                // 씬에 놓인 자리와 색을 원래 값으로 기억한다.
                _homePositions[i] = _pieceButtons[i].transform.localPosition;
                _homeSizes[i] = ((RectTransform)_pieceButtons[i].transform).sizeDelta;
                _pieceColors[i] = _pieceButtons[i].targetGraphic.color;
                _moveT[i] = 1f;
            }
            _holeMins = new Vector2[_holes.Length];
            _holeMaxes = new Vector2[_holes.Length];
            for (int i = 0; i < _holes.Length; i++)
            {
                _holeMins[i] = _holes[i].anchorMin;
                _holeMaxes[i] = _holes[i].anchorMax;
            }
            _keyColors = new Color[_keyGraphics.Length];
            for (int i = 0; i < _keyGraphics.Length; i++) _keyColors[i] = _keyGraphics[i].color;
        }

        void Update()
        {
            // 대화 기록 창이 열려 있으면 조각 움직임, 완성, 입력을 모두 멈춘다.
            if (!_open || DialogueLog.BlocksInput) return;
            MovePieces(Time.unscaledDeltaTime);
            // 완성한 열쇠를 잠깐 보여 준 뒤 닫고 완성을 알린다.
            if (_doneT > 0f)
            {
                _doneT -= Time.unscaledDeltaTime;
                if (_doneT > 0f) return;
                string sentence = _doneSentence;
                Close();
                Completed?.Invoke(sentence);
                return;
            }
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) { Cancel(); return; }
            // 틀린 마지막 조각도 홈에 닿는 것을 보여 준 뒤 모두 돌려보낸다. 키보드가 없어도 진행한다.
            if (_wrongPending)
            {
                if (IsMoving()) return;
                _wrongPending = false;
                ClearPlaced();
                WrongOrder?.Invoke();
                return;
            }
            if (keyboard == null) return;
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
            _doneT = 0f;
            _wrongPending = false;
            for (int i = 0; i < _keyGraphics.Length; i++) _keyGraphics[i].color = _keyColors[i];
            for (int i = 0; i < _pieceButtons.Length; i++)
            {
                // 지난번에 홈에 끼워 둔 조각을 원래 자리와 색으로 바로 되돌린다.
                _pieceButtons[i].transform.localPosition = _homePositions[i];
                ((RectTransform)_pieceButtons[i].transform).sizeDelta = _homeSizes[i];
                _pieceButtons[i].targetGraphic.color = _pieceColors[i];
                _moveT[i] = 1f;
            }
            _memoryText.text = string.IsNullOrEmpty(heard) ? memory : memory + "\n\n" + heard;
            for (int i = 0; i < _pieceButtons.Length; i++)
            {
                bool used = i < pieces.Length;
                _pieceButtons[i].gameObject.SetActive(used);
                _slotLabels[i].gameObject.SetActive(used);
                _holes[i].gameObject.SetActive(used);
                if (!used) continue;
                // 씬에 놓인 홈들이 차지하는 가로 범위를 조각 수로 고르게 나눠, 조각이 적을수록 홈이 넓어져 글이 잘리지 않게 한다.
                // 홈의 깊이(세로)는 씬에 놓인 그 홈의 값을 그대로 쓴다.
                int last = _holes.Length - 1;
                float gap = _holes.Length > 1 ? _holeMins[1].x - _holeMaxes[0].x : 0f;
                float width = (_holeMaxes[last].x - _holeMins[0].x + gap) / pieces.Length - gap;
                float x = _holeMins[0].x + i * (width + gap);
                var min = new Vector2(x, _holeMins[i].y);
                var max = new Vector2(x + width, _holeMaxes[i].y);
                _holes[i].anchorMin = min;
                _holes[i].anchorMax = max;
                _slotLabels[i].rectTransform.anchorMin = min;
                _slotLabels[i].rectTransform.anchorMax = max;
                _pieceLabels[i].text = $"{i + 1}  {pieces[_buttonPieces[i]]}";
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
            if (!_open || _doneT > 0f || _wrongPending || button < 0 || button >= _answer.Length || _placedButtons.Contains(button)) return;
            // 조각 버튼이 다음 빈 홈으로 움직여 끼워진다. 홈의 빈칸 표시는 지운다.
            Text slot = _slotLabels[_placedButtons.Count];
            slot.text = "";
            RectTransform piece = (RectTransform)_pieceButtons[button].transform;
            // 홈마다 깊이가 달라, 조각이 그 홈의 크기에 맞게 늘거나 줄며 끼워진다.
            MoveTo(button, piece.parent.InverseTransformPoint(slot.transform.position),
                piece.sizeDelta + slot.rectTransform.rect.size - piece.rect.size);
            _placedButtons.Add(button);
            if (_placedButtons.Count < _answer.Length) return;

            for (int i = 0; i < _placedButtons.Count; i++)
            {
                // 같은 글의 조각이 둘 있어도 보이는 문장이 같으면 맞은 것으로 본다.
                if (_answer[_buttonPieces[_placedButtons[i]]] == _answer[i]) continue;
                _wrongPending = true;
                return;
            }
            // 완성한 열쇠와 조각을 완성 색으로 잠깐 보여 준다. 닫기와 알림은 Update가 한다.
            _doneSentence = string.Join(" ", _answer);
            _doneT = Mathf.Max(0.01f, _doneHoldSeconds);
            foreach (Graphic graphic in _keyGraphics) graphic.color = _doneColor;
            for (int i = 0; i < _answer.Length; i++) _pieceButtons[i].targetGraphic.color = _doneColor;
        }

        /// <summary>
        /// 조각 버튼 하나를 지금 자리와 크기에서 목표 자리와 크기로 움직이기 시작한다.
        /// 버튼 번호 button, target(버튼 부모 기준 자리), size(목표 sizeDelta)를 사용하며, _moveFrom, _moveTo, _sizeFrom, _sizeTo, _moveT를 바꾼다.
        /// </summary>
        private void MoveTo(int button, Vector3 target, Vector2 size)
        {
            _sizeFrom[button] = ((RectTransform)_pieceButtons[button].transform).sizeDelta;
            _sizeTo[button] = size;
            _moveFrom[button] = _pieceButtons[button].transform.localPosition;
            _moveTo[button] = target;
            _moveT[button] = 0f;
        }

        /// <summary>
        /// 움직이는 중인 조각 버튼을 목표 자리 쪽으로 옮긴다. 끝은 느려지게 한다.
        /// dt와 _moveSeconds를 사용하며, 조각 버튼의 자리와 _moveT를 바꾼다.
        /// </summary>
        private void MovePieces(float dt)
        {
            for (int i = 0; i < _pieceButtons.Length; i++)
            {
                if (_moveT[i] >= 1f) continue;
                _moveT[i] = Mathf.Min(1f, _moveT[i] + dt / _moveSeconds);
                float eased = 1f - (1f - _moveT[i]) * (1f - _moveT[i]);
                _pieceButtons[i].transform.localPosition = Vector3.Lerp(_moveFrom[i], _moveTo[i], eased);
                ((RectTransform)_pieceButtons[i].transform).sizeDelta = Vector2.Lerp(_sizeFrom[i], _sizeTo[i], eased);
            }
        }

        /// <summary>
        /// 아직 움직이는 조각이 있는지 판단한다.
        /// _moveT를 사용하며, 하나라도 움직이는 중이면 true를 반환한다.
        /// </summary>
        private bool IsMoving()
        {
            foreach (float t in _moveT)
                if (t < 1f) return true;
            return false;
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
            MoveTo(button, _homePositions[button], _homeSizes[button]);
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
            // 홈에 끼워져 있던 조각은 원래 자리로 움직여 돌아간다.
            foreach (int button in _placedButtons) MoveTo(button, _homePositions[button], _homeSizes[button]);
            _placedButtons.Clear();
            for (int i = 0; i < _answer.Length; i++) _slotLabels[i].text = _emptySlotText;
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

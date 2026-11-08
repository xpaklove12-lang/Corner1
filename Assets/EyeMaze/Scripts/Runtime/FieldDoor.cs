using UnityEngine;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 상담실 문. 손님이 초인종을 누르면 문 위에 호출 글씨가 보이고, 문이 울리는 동안만 Z로 문을 열어 줄 수 있다.
    /// 문짝 그림은 건드리지 않으며, 열어 준 사실만 Answered로 알린다 (FieldCutscene이 읽는다).
    /// 호출 글씨는 화면 전체를 덮는 필드 UI 아래의 uGUI Text이며, 보이는 동안 문 위치를 따라간다.
    /// </summary>
    public class FieldDoor : FieldInteractable
    {
        [Header("호출 글씨")]
        [Tooltip("문 위에 뜨는 호출 글씨. 화면 전체로 늘어난 필드 UI 아래에 둔다")]
        [SerializeField] private Text _bell;
        [Tooltip("필드를 그리는 월드 카메라")]
        [SerializeField] private Camera _camera;
        [Tooltip("문 위치에서 호출 글씨까지의 월드 거리")]
        [SerializeField] private Vector2 _bellWorldOffset = new Vector2(0f, 1.4f);

        private bool _ringing;

        public bool Answered { get; private set; }
        public override string Verb => "맞이하기";
        public override bool Available => _ringing;

        void Start()
        {
            if (Application.isPlaying) ShowBell(false);
        }

        void LateUpdate()
        {
            if (!Application.isPlaying || !_bell.enabled) return;
            // 카메라 뷰포트 좌표를 앵커로 쓰면 픽셀 렌더 여부와 관계없이 문 위에 맞는다.
            Vector3 vp = _camera.WorldToViewportPoint(transform.position + (Vector3)_bellWorldOffset);
            RectTransform rt = _bell.rectTransform;
            var anchor = new Vector2(vp.x, vp.y);
            if (rt.anchorMin != anchor || rt.anchorMax != anchor)
            {
                rt.anchorMin = anchor;
                rt.anchorMax = anchor;
            }
        }

        /// <summary>
        /// 호출 글씨만 보이거나 숨긴다. 상호작용 가능 여부는 바꾸지 않는다.
        /// on을 사용하며, _bell의 enabled를 바꾼다.
        /// </summary>
        public void ShowBell(bool on)
        {
            if (_bell.enabled != on) _bell.enabled = on;
        }

        /// <summary>
        /// 문을 울려 호출 글씨를 보이고 상호작용을 연다.
        /// 입력값은 없으며, _ringing을 켜고 Answered를 끈다.
        /// </summary>
        public void Ring()
        {
            Answered = false;
            _ringing = true;
            ShowBell(true);
        }

        /// <summary>
        /// 문을 열어 준다. 호출 글씨를 숨기고 다시 상호작용할 수 없게 한다.
        /// flow는 쓰지 않으며, _ringing을 끄고 Answered를 켠다.
        /// </summary>
        public override void Interact(GameFlow flow)
        {
            if (!_ringing) return;
            _ringing = false;
            ShowBell(false);
            Answered = true;
        }
    }
}

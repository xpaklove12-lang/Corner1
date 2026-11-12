using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 회상 방 안의 사물. 내담자가 범위 안에서 상호작용하면 회상 단계가 진행되거나 살펴보기 글이 나온다.
    /// 필드 상호작용 탐색에 섞이지 않도록 FieldInteractable을 상속하지 않는다.
    /// </summary>
    public class FlashbackProp : MonoBehaviour
    {
        [Header("사물")]
        [Tooltip("Flashbacks.csv의 prop 칸에 적는 id")]
        [SerializeField] private string _id;
        [Tooltip("가까이 갔을 때 안내 글에 보이는 이름")]
        [SerializeField] private string _displayName = "무언가";
        [Tooltip("지금 단계의 사물이 아닐 때 보이는 살펴보기 글. 비우면 반응하지 않는다")]
        [SerializeField, TextArea(1, 3)] private string _lookText;
        // 스펙 016 FR-009: 이 사물의 단계를 지난 뒤에 보일 살펴보기 글 추가
        [Tooltip("이 사물을 쓰는 단계를 한 번이라도 지난 뒤의 살펴보기 글. 비우면 위의 글을 그대로 쓴다")]
        [SerializeField, TextArea(1, 3)] private string _lookTextAfter;
        [Tooltip("내담자 가장자리에서 이 거리 안이면 상호작용할 수 있다")]
        [SerializeField] private float _interactRange = 0.9f;
        // 스펙 016: 사물마다 다른 행동 안내 글과 안내 글이 뜨는 위치 추가
        [Tooltip("가까이 갔을 때 안내 글에 보이는 행동. 비우면 이름을 쓴다")]
        [SerializeField] private string _actionText;
        [Tooltip("안내 글이 뜨는 위치. 사물 위치에서 더하는 거리")]
        [SerializeField] private Vector2 _hintOffset = new Vector2(0f, 0.6f);

        public string Id => _id;
        public string DisplayName => _displayName;
        public string LookText => _lookText;
        public string LookTextAfter => _lookTextAfter;
        public float InteractRange => _interactRange;
        /// <summary>안내 글에 보일 행동. _actionText가 비면 이름</summary>
        public string HintText => string.IsNullOrEmpty(_actionText) ? _displayName : _actionText;
        /// <summary>안내 글이 뜨는 월드 위치</summary>
        public Vector3 HintPoint => transform.position + (Vector3)_hintOffset;

        /// <summary>
        /// 내담자 가장자리에서 이 사물까지의 거리를 계산한다. 필드와 같이 사이드뷰는 가로 거리만 본다.
        /// actor의 위치, 반폭, 사이드뷰 여부를 사용하며, 거리를 반환한다.
        /// </summary>
        public float DistanceTo(FieldPlayer actor)
        {
            Vector2 p = actor.Position;
            float d = actor.SideView ? Mathf.Abs(p.x - transform.position.x) : Vector2.Distance(p, transform.position);
            return d - actor.Radius;
        }
    }
}

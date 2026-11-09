using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>필드에서 Space로 상호작용할 수 있는 것 (사람, 단서).</summary>
    public abstract class FieldInteractable : MonoBehaviour
    {
        [Header("상호작용")]
        [SerializeField, FormerlySerializedAs("displayName")] private string _displayName = "무언가";
        [Tooltip("플레이어 가장자리에서 이 거리 안이면 상호작용 가능")]
        [SerializeField, FormerlySerializedAs("interactRange")] private float _interactRange = 0.9f;
        [Tooltip("이 물체 자체의 반지름 (충돌/거리 계산용)")]
        [SerializeField, FormerlySerializedAs("radius")] private float _radius = 0.3f;
        [Tooltip("다른 물체처럼 플레이어를 막는지")]
        [SerializeField, FormerlySerializedAs("blocks")] private bool _blocks = true;

        public virtual string DisplayName => _displayName;
        public float InteractRange => _interactRange;
        public float Radius => _radius;
        public bool Blocks => _blocks;
        public abstract string Verb { get; }

        /// <summary>지금 상호작용할 수 있는지 (예: 아직 방에 들어오지 않은 사람은 false)</summary>
        public virtual bool Available => true;

        protected virtual void OnValidate() => EyeHooks.Repaint();

        /// <summary>
        /// 플레이어가 Space를 눌러 이 대상과 상호작용한다.
        /// flow를 사용하며, 대상마다 대화나 글 상자 같은 게임 흐름 상태를 바꾼다.
        /// </summary>
        public abstract void Interact(GameFlow flow);

        /// <summary>
        /// 플레이어 가장자리에서 이 물체 가장자리까지의 거리를 계산한다.
        /// p, playerRadius, horizontalOnly(사이드뷰는 가로 거리만)를 사용하며, 거리를 반환한다.
        /// </summary>
        public float DistanceTo(Vector2 p, float playerRadius, bool horizontalOnly = false)
        {
            float d = horizontalOnly ? Mathf.Abs(p.x - transform.position.x) : Vector2.Distance(p, transform.position);
            return d - _radius - playerRadius;
        }
    }
}

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046 FR-046: 슬픔과 침잠에서 시선 둘레만 남기고 미로를 어둡게 덮는다.
    /// 덮개와 구멍은 씬에 미리 만들어 두고 여기서는 켜고 끄고 옮기기만 한다.
    /// </summary>
    public class GazeFog : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("시선, 감정, 모드를 읽을 디렉터")]
        [SerializeField] private EyeMazeDirector _director;
        [Tooltip("미로를 덮는 어두운 그림. 구멍 바깥에서만 보이게 설정해 둔다")]
        [SerializeField] private SpriteRenderer _cover;
        [Tooltip("시선을 따라다니며 덮개에 구멍을 내는 마스크")]
        [SerializeField] private SpriteMask _hole;

        [Header("안개")]
        [Tooltip("슬픔에서 보이는 반지름")]
        [SerializeField, Min(0.05f)] private float _sadRadius = 0.55f;
        [Tooltip("침잠에서 보이는 반지름")]
        [SerializeField, Min(0.05f)] private float _deepRadius = 0.55f;
        [Tooltip("안개가 짙어지고 걷히는 속도(초당 투명도)")]
        [SerializeField, Min(0.1f)] private float _fadeSpeed = 2f;
        [Tooltip("안개가 가장 짙을 때의 투명도. 색 공간이 선형이라 1보다 조금만 낮아도 바깥이 꽤 보인다")]
        [SerializeField, Range(0f, 1f)] private float _maxAlpha = 1f;
        private float _alpha;

        void LateUpdate()
        {
            EmotionControl emotion = _director.EmotionControl;
            bool on = _director.CurrentMode == EyeMazeDirector.Mode.Gaze && !_director.InAbyss && emotion.Current == Emotion.Sad;
            _alpha = Mathf.MoveTowards(_alpha, on ? _maxAlpha : 0f, _fadeSpeed * Time.deltaTime);
            bool visible = _alpha > 0.001f;
            if (_cover.enabled != visible) _cover.enabled = visible;
            if (_hole.enabled != visible) _hole.enabled = visible;
            if (!visible) return;
            Color color = _cover.color;
            color.a = _alpha;
            _cover.color = color;
            // 구멍은 시선을 따라가고, 지름이 보이는 반지름의 두 배가 되게 크기를 맞춘다.
            float radius = emotion.Stage >= 2 ? _deepRadius : _sadRadius;
            Transform hole = _hole.transform;
            hole.position = _director.Cursor.transform.position;
            float size = radius * 2f / _hole.sprite.bounds.size.x;
            Vector3 parentScale = hole.parent.lossyScale;
            hole.localScale = new Vector3(size / parentScale.x, size / parentScale.y, 1f);
        }
    }
}

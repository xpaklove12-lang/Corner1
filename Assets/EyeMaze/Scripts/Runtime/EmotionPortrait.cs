using UnityEngine;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046 FR-041: 표정 그림이 있는 감정에서는 초상화의 애니메이터를 멈추고 그 그림을 보여 준다.
    /// 그림이 없는 감정(평온, 경계, 반감)에서는 애니메이터를 다시 켜 기존 애니메이션을 보여 준다.
    /// </summary>
    public class EmotionPortrait : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("초상화의 애니메이터. 표정 그림을 보여 주는 동안 멈춘다")]
        [SerializeField] private Animator _animator;
        [Tooltip("초상화 그림")]
        [SerializeField] private Image _image;

        [Header("표정")]
        [Tooltip("감정별 표정 그림. 순서: 동요, 격동, 슬픔, 침잠, 희망, 열망. 비워 둔 칸은 기존 애니메이션을 쓴다")]
        [SerializeField] private Sprite[] _sprites = new Sprite[6];

        /// <summary>
        /// 감정과 단계에 맞는 표정 그림을 보여 주거나, 그림이 없으면 애니메이터를 다시 켠다.
        /// emotion, stage, _sprites를 사용하며, 애니메이터의 켜짐과 초상화 그림을 바꾼다.
        /// </summary>
        public void Show(Emotion emotion, int stage)
        {
            // 동요부터 두 칸씩 1단계, 2단계 순서로 들어 있다.
            int index = ((int)emotion - (int)Emotion.Stir) * 2 + (stage >= 2 ? 1 : 0);
            Sprite sprite = emotion >= Emotion.Stir && index < _sprites.Length ? _sprites[index] : null;
            bool animate = sprite == null;
            if (_animator.enabled != animate) _animator.enabled = animate;
            if (!animate && _image.sprite != sprite) _image.sprite = sprite;
        }
    }
}

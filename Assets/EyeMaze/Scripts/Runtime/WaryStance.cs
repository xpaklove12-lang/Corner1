using UnityEngine;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 045: 경계태세를 켜고 끈다. 켜면 은재 초상화가 경계 전환 애니메이션을 재생하고 공격성 오브젝트가 켜진다.
    /// 만남을 떠나면 자동으로 평소 상태로 돌아간다.
    /// </summary>
    public class WaryStance : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("만남을 떠났는지(입력 받지 않음) 볼 디렉터")]
        [SerializeField] private EyeMazeDirector _director;
        [Tooltip("은재 초상화 Animator")]
        [SerializeField] private Animator _portraitAnimator;
        [Tooltip("경계태세에서만 켜는 공격성 오브젝트 (눈 가운데 포대 등)")]
        [SerializeField] private GameObject[] _aggressionObjects = new GameObject[0];

        [Header("애니메이션 상태")]
        [Tooltip("평소 상태 이름")]
        [SerializeField] private string _idleState = "Eunjae_Idle";
        [Tooltip("경계 전환 상태 이름. 반복하지 않아 마지막 경계 그림에서 멈춘다")]
        [SerializeField] private string _waryState = "Eunjae_ToWary";

        [Header("임시 버튼")]
        [Tooltip("상태에 따라 글을 바꿀 버튼 글")]
        [SerializeField] private Text _toggleLabel;
        [SerializeField] private string _enterLabel = "경계태세 전환 (임시)";
        [SerializeField] private string _exitLabel = "경계태세 해제 (임시)";

        private bool _isWary;

        public bool IsWary => _isWary;

        void Update()
        {
            // 만남을 떠나면 다음 만남이 평소 상태로 시작하도록 푼다.
            if (_isWary && !_director.AcceptInput) SetWary(false);
        }

        /// <summary>
        /// 경계태세를 반대로 바꾼다. 임시 버튼의 onClick에서 부른다.
        /// _isWary를 사용하며, SetWary가 바꾸는 상태를 바꾼다.
        /// </summary>
        public void Toggle()
        {
            SetWary(!_isWary);
        }

        /// <summary>
        /// 경계태세를 켜거나 끈다. 이미 같은 상태면 아무것도 하지 않는다.
        /// wary를 사용하며, _isWary와 초상화 애니메이션 상태, 공격성 오브젝트 활성, 버튼 글을 바꾼다.
        /// </summary>
        public void SetWary(bool wary)
        {
            if (_isWary == wary) return;
            _isWary = wary;
            if (_portraitAnimator != null) _portraitAnimator.Play(wary ? _waryState : _idleState, 0, 0f);
            foreach (GameObject target in _aggressionObjects)
                if (target != null) target.SetActive(wary);
            if (_toggleLabel != null) _toggleLabel.text = wary ? _exitLabel : _enterLabel;
        }
    }
}

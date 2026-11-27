using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 회상 방 하나. 파편 플래그, 장면 그림, 내담자, 시작 위치, 카메라 기준점을 가진다.
    /// 방 오브젝트는 꺼진 채로 저장하고 회상이 재생되는 동안만 FlashbackPlayer가 켠다.
    /// </summary>
    public class FlashbackRoom : MonoBehaviour
    {
        [Header("회상")]
        [Tooltip("이 방이 속한 파편의 플래그")]
        [SerializeField] private string _flag;
        [Tooltip("방에 들어가기 전에 먼저 나오는 장면 그림. 비우면 그림 없이 바로 방으로 들어간다")]
        [SerializeField] private Sprite _cover;
        // 스펙 016: 방으로 들어갈 때 장면 그림이 줄어들어 들어갈 방 테두리 추가
        [Tooltip("방으로 들어갈 때 장면 그림이 줄어들어 들어갈 방 테두리 (방 안의 FieldRoom)")]
        [SerializeField] private FieldRoom _frame;

        [Header("방")]
        [Tooltip("플레이어가 움직일 방 안의 내담자")]
        [SerializeField] private FieldPlayer _actor;
        [Tooltip("내담자의 시작 위치")]
        [SerializeField] private Transform _startPoint;

        [Header("카메라")]
        [Tooltip("회상 중 카메라가 놓일 위치")]
        [SerializeField] private Transform _cameraPoint;
        [Tooltip("회상 중 카메라 크기")]
        [SerializeField] private float _cameraSize = 4.5f;

        public string Flag => _flag;
        public Sprite Cover => _cover;
        /// <summary>방으로 들어갈 때 장면 그림이 줄어들어 들어갈 방 테두리</summary>
        public FieldRoom Frame => _frame;
        public FieldPlayer Actor => _actor;
        public Transform StartPoint => _startPoint;
        public Transform CameraPoint => _cameraPoint;
        public float CameraSize => _cameraSize;

        /// <summary>
        /// 지금 켜진 사물을 계층 순서대로 모은다.
        /// 입력값은 없으며, 켜진 FlashbackProp 목록을 반환한다.
        /// </summary>
        public List<FlashbackProp> ActiveProps()
        {
            return new List<FlashbackProp>(GetComponentsInChildren<FlashbackProp>(false));
        }
    }
}

using UnityEngine;

namespace EyeMaze
{
    public class FragmentPortal : MonoBehaviour
    {
        [Header("진입")]
        [SerializeField, Min(0.01f)] private float _entryRadius = 0.13f;
        private EyeMazeDirector _director;
        private Transform _destination;
        private FragmentPortal _pairedPortal;
        private bool _armed;

        /// <summary>
        /// director와 반대편 destination 포탈을 저장해 양방향 이동을 준비한다.
        /// 생성 직후 자동 이동하지 않도록 영역 이탈 대기 상태로 초기화한다.
        /// </summary>
        public void Initialize(EyeMazeDirector director, FragmentPortal destination)
        {
            _director = director;
            _pairedPortal = destination;
            _destination = destination.transform;
            _armed = false;
        }

        void LateUpdate()
        {
            if (_director == null || !_director.CanRunHazards || _destination == null) return;
            float distance = Vector2.Distance(_director.Cursor.transform.position, transform.position);
            if (!_armed)
            {
                if (distance > _entryRadius + 0.03f) _armed = true;
                return;
            }
            if (distance > _entryRadius) return;
            if (_director.TravelThroughPortal(_destination))
            {
                _armed = false;
                _pairedPortal._armed = false;
            }
        }
    }
}

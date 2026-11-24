using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 046 FR-049: 동요와 격동으로 머무는 동안 시선이 있는 층의 벽을 떨리게 하고 무너뜨린다. 다른 감정이 되면 다시 세운다.
    /// 층과 층 사이의 벽과 문은 건드리지 않는다. 미로 오브젝트에 붙인다.
    /// </summary>
    public class StirWalls : MonoBehaviour
    {
        [Header("연결")]
        [Tooltip("시선, 감정, 모드를 읽을 디렉터")]
        [SerializeField] private EyeMazeDirector _director;
        [Tooltip("벽을 가진 미로")]
        [SerializeField] private IrisMaze _maze;

        [Header("동요")]
        [Tooltip("동요가 된 뒤 벽이 무너지기 시작할 때까지 버텨야 하는 시간(초)")]
        [SerializeField, Min(0f)] private float _delaySeconds = 2f;
        [Tooltip("무너지기 시작할 때 시선에서 닿는 거리")]
        [SerializeField, Min(0f)] private float _reachStart = 0.65f;
        [Tooltip("머무는 동안 닿는 거리가 늘어나는 속도(초당)")]
        [SerializeField, Min(0f)] private float _reachSpeed = 0.35f;
        [Tooltip("닿는 거리의 한계")]
        [SerializeField, Min(0f)] private float _reachMax = 1.8f;

        [Header("격동")]
        [Tooltip("격동에서 버텨야 하는 시간(초)")]
        [SerializeField, Min(0f)] private float _delaySeconds2 = 1f;
        [Tooltip("격동에서 닿는 거리의 한계")]
        [SerializeField, Min(0f)] private float _reachMax2 = 3f;

        [Header("연출")]
        [Tooltip("벽이 무너지는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _fallSeconds = 0.4f;
        [Tooltip("벽이 다시 서는 시간(초)")]
        [SerializeField, Min(0.01f)] private float _riseSeconds = 1f;
        [Tooltip("곧 무너질 벽이 떨리는 범위. 닿는 거리 바깥으로 이만큼 더 떨린다")]
        [SerializeField, Min(0f)] private float _trembleBand = 0.35f;
        [Tooltip("떨림 세기(미로 거리)")]
        [SerializeField, Min(0f)] private float _trembleAmount = 0.012f;
        private readonly List<MazeWall> _walls = new List<MazeWall>();
        private float _stirSeconds;

        void Update()
        {
            EmotionControl emotion = _director.EmotionControl;
            bool stir = emotion.Current == Emotion.Stir;
            bool stage2 = emotion.Stage >= 2;
            float dt = Time.deltaTime;
            // 버틴 시간은 감정이 동요가 아니게 될 때만 0으로 돌아간다. 대화로 나가 있거나 깜빡이는 동안에는 세지 않는다.
            if (!stir) _stirSeconds = 0f;
            else if (_director.CanRunHazards) _stirSeconds += dt;
            // 동요인 채 대화로 나가 있는 동안에는 벽을 그대로 둔다.
            if (stir && (_director.CurrentMode != EyeMazeDirector.Mode.Gaze || _director.InAbyss))
            {
                // 무너진 벽은 그대로 두되 떨림은 멈춘다.
                _maze.GetComponentsInChildren(false, _walls);
                foreach (MazeWall wall in _walls) wall.Tremble = 0f;
                return;
            }

            float delay = stage2 ? _delaySeconds2 : _delaySeconds;
            float reach = 0f;
            if (stir && _stirSeconds >= delay)
                reach = Mathf.Min(stage2 ? _reachMax2 : _reachMax, _reachStart + _reachSpeed * (_stirSeconds - delay));
            // 무너지기 전에는 가까운 벽이, 무너진 뒤에는 다음에 무너질 벽이 떨린다.
            float trembleReach = stir ? Mathf.Max(_reachStart, reach + _trembleBand) : 0f;

            Vector2 gaze = _director.Cursor.LocalPos;
            int layer = _maze.LayerOfRing(_director.Cursor.Ring);
            _maze.GetComponentsInChildren(false, _walls);
            foreach (MazeWall wall in _walls)
            {
                bool shakeable = stir && CanShake(wall, layer, stage2);
                float distance = shakeable ? (LocalMiddle(wall) - gaze).magnitude : float.MaxValue;
                float target = distance <= reach ? 1f : 0f;
                float seconds = target > wall.Crumble ? _fallSeconds : _riseSeconds;
                wall.Crumble = Mathf.MoveTowards(wall.Crumble, target, dt / seconds);
                wall.Tremble = target < 1f && distance <= trembleReach ? _trembleAmount : 0f;
            }
        }

        /// <summary>
        /// 그 벽이 지금 흔들릴 수 있는 벽인지 판단한다. 시선이 있는 층 안의 벽만 흔들리고, 층과 층 사이의 벽은 흔들리지 않는다.
        /// 동요에서는 정해진 절반의 벽만, 격동에서는 그 층의 모든 벽이 흔들린다.
        /// wall, 시선의 층 layer, stage2를 사용하며, 흔들릴 수 있으면 true를 반환한다.
        /// </summary>
        private bool CanShake(MazeWall wall, int layer, bool stage2)
        {
            if (_maze.LayerOfRing(wall.Ring) != layer) return false;
            if (wall.WallKind == MazeWall.Kind.Arc)
            {
                // 원호 벽은 그 고리와 안쪽 고리 사이에 있다. 안쪽 고리가 다른 층이거나 동공이면 층 사이의 벽이다.
                if (wall.Ring + 1 >= _maze.Rings || _maze.LayerOfRing(wall.Ring + 1) != layer) return false;
            }
            // 매번 같은 벽이 무너지도록 벽의 자리로 정한다.
            return stage2 || ((wall.Ring * 7 + wall.Sector * 3 + (int)wall.WallKind) & 1) == 0;
        }

        /// <summary>
        /// 벽 선의 가운데 자리를 미로 기준으로 구한다. 층이 돌아가 있으면 그만큼 돌아간 자리다.
        /// wall의 종류, 고리, 섹터를 사용하며, 미로 기준 자리를 반환한다.
        /// </summary>
        private Vector2 LocalMiddle(MazeWall wall)
        {
            float degrees = _maze.SectorDeg;
            Vector3 middle = wall.WallKind == MazeWall.Kind.Arc
                ? Shapes.Polar(_maze.RingInner(wall.Ring), (wall.Sector + 0.5f) * degrees)
                : Shapes.Polar((_maze.RingOuter(wall.Ring) + _maze.RingInner(wall.Ring)) * 0.5f, (wall.Sector + 1) * degrees);
            return _maze.transform.InverseTransformPoint(wall.transform.parent.TransformPoint(middle));
        }
    }
}

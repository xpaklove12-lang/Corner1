using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>폭발 무늬 하나가 터뜨릴 칸을 고르는 모양</summary>
    public enum BoomShape
    {
        /// <summary>열·행 범위 안의 칸을 모두 터뜨린다.</summary>
        Area,
        /// <summary>범위 안에서 체크 무늬로 터뜨리고, 반복마다 무늬를 뒤집는다.</summary>
        Checker,
        /// <summary>범위 안에서 줄 간격마다 대각선 줄을 터뜨린다. 반복마다 줄 이동만큼 밀려난다.</summary>
        Diagonal,
        /// <summary>범위 안에서 밀도만큼 흩뿌려 터뜨린다. 반복마다 다른 칸이 뽑힌다.</summary>
        Scatter
    }

    /// <summary>
    /// 폭발 미로 순서표의 무늬 하나. 언제 몇 번 터질지, 어떤 칸이 터질지, 경고와 폭발이 얼마나 갈지 담는다.
    /// 칸은 사각 미로의 열(0 = 왼쪽)과 행(0 = 아래)으로 고른다.
    /// </summary>
    [Serializable]
    public class BoomWave
    {
        [Header("이름")]
        [SerializeField] private string _name;
        [SerializeField] private BoomShape _shape;

        [Header("시간")]
        [Tooltip("순서표가 시작되고 첫 경고가 뜨기까지의 시간(초)")]
        [SerializeField] private float _start;
        [Tooltip("이 무늬를 몇 번 반복하는지")]
        [SerializeField, Min(1)] private int _repeat = 1;
        [Tooltip("반복 사이의 간격(초)")]
        [SerializeField, Min(0.05f)] private float _interval = 1f;
        [Tooltip("터지기 전에 달아오르며 경고하는 시간(초)")]
        [SerializeField, Min(0.05f)] private float _warnSeconds = 1f;
        [Tooltip("터진 뒤 칸에 남아 있는 시간(초). 이 동안 칸에 들어오면 휩쓸린다")]
        [SerializeField, Min(0.05f)] private float _burstSeconds = 0.3f;

        [Header("범위")]
        [Tooltip("시작 열 (0 = 왼쪽)")]
        [SerializeField] private int _column;
        [Tooltip("열 몇 개를 덮는지. 0이면 모든 열")]
        [SerializeField, Min(0)] private int _columnSpan;
        [Tooltip("시작 행 (0 = 아래)")]
        [SerializeField] private int _row;
        [Tooltip("행 몇 개를 덮는지. 0이면 모든 행")]
        [SerializeField, Min(0)] private int _rowSpan;

        [Header("반복마다 옮기기")]
        [Tooltip("반복마다 범위를 몇 열 옮기는지 (+ 오른쪽, - 왼쪽)")]
        [SerializeField] private int _columnStep;
        [Tooltip("반복마다 범위를 몇 행 옮기는지 (+ 위, - 아래)")]
        [SerializeField] private int _rowStep;

        [Header("모양 설정")]
        [Tooltip("대각선 줄 사이 간격(칸). 2 이상으로 두어 줄 사이로 피할 칸이 남게 한다")]
        [SerializeField, Min(2)] private int _stripeGap = 3;
        [Tooltip("대각선 줄이 반복마다 밀려나는 칸 수")]
        [SerializeField] private int _stripeStep = 1;
        [Tooltip("흩뿌림에서 터지는 칸의 비율 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _density = 0.3f;
        [Tooltip("흩뿌림에서 칸을 뽑는 시드")]
        [SerializeField] private int _seed;

        public string Name => _name;
        public float Start => _start;
        public int Repeat => Mathf.Max(1, _repeat);
        public float Interval => Mathf.Max(0.05f, _interval);
        public float WarnSeconds => Mathf.Max(0.05f, _warnSeconds);
        public float BurstSeconds => Mathf.Max(0.05f, _burstSeconds);
        /// <summary>마지막 반복의 폭발이 끝나는 시각(초)</summary>
        public float End => _start + (Repeat - 1) * Interval + WarnSeconds + BurstSeconds;

        public BoomWave() { }

        /// <summary>
        /// 기본 순서표를 코드로 짤 때 쓰는 생성자. 넘기지 않은 범위와 모양 설정은 기본값을 쓴다.
        /// 이름, 모양, 시간, 범위, 반복 이동, 모양 설정 값을 사용하며, 새 무늬를 만든다.
        /// </summary>
        public BoomWave(string name, BoomShape shape, float start, int repeat, float interval, float warnSeconds, float burstSeconds,
            int column = 0, int columnSpan = 0, int row = 0, int rowSpan = 0, int columnStep = 0, int rowStep = 0,
            int stripeGap = 3, int stripeStep = 1, float density = 0.3f, int seed = 0)
        {
            _name = name;
            _shape = shape;
            _start = start;
            _repeat = repeat;
            _interval = interval;
            _warnSeconds = warnSeconds;
            _burstSeconds = burstSeconds;
            _column = column;
            _columnSpan = columnSpan;
            _row = row;
            _rowSpan = rowSpan;
            _columnStep = columnStep;
            _rowStep = rowStep;
            _stripeGap = stripeGap;
            _stripeStep = stripeStep;
            _density = density;
            _seed = seed;
        }

        /// <summary>
        /// repeat번째 반복에서 이 무늬가 칸 하나를 터뜨리는지 판단한다.
        /// column, row, repeat(0부터)를 사용하며, 터뜨리면 true를 반환한다.
        /// </summary>
        public bool Covers(int column, int row, int repeat)
        {
            if (!InRange(column, _column + repeat * _columnStep, _columnSpan)) return false;
            if (!InRange(row, _row + repeat * _rowStep, _rowSpan)) return false;

            switch (_shape)
            {
                case BoomShape.Checker:
                    return ((column + row + repeat) & 1) == 0;
                case BoomShape.Diagonal:
                    return Wrap(column + row - repeat * _stripeStep, Mathf.Max(2, _stripeGap)) == 0;
                case BoomShape.Scatter:
                    return Hash01(column, row, repeat) < _density;
                default:
                    return true;
            }
        }

        /// <summary>
        /// 값이 start에서 span칸 안에 있는지 판단한다. span이 0이면 범위를 두지 않는다.
        /// value, start, span을 사용하며, 범위 안이면 true를 반환한다.
        /// </summary>
        private static bool InRange(int value, int start, int span) => span <= 0 || (value >= start && value < start + span);

        /// <summary>
        /// 흩뿌림에서 칸 하나의 뽑기 값을 계산한다. 같은 칸, 반복, 시드면 언제나 같은 값이다.
        /// column, row, repeat, _seed를 사용하며, 0 이상 1 미만의 값을 반환한다.
        /// </summary>
        private float Hash01(int column, int row, int repeat)
        {
            unchecked
            {
                uint h = (uint)column * 374761393u + (uint)row * 668265263u + (uint)repeat * 2246822519u + (uint)_seed * 3266489917u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        /// <summary>
        /// 값을 0 ~ period-1 범위로 감는다.
        /// a, period를 사용하며, 감긴 값을 반환한다.
        /// </summary>
        private static int Wrap(int a, int period) => ((a % period) + period) % period;
    }
}

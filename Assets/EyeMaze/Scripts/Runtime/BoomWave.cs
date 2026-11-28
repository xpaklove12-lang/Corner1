using System;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>폭발 무늬 하나가 터뜨릴 칸을 고르는 모양</summary>
    public enum BoomShape
    {
        /// <summary>고리·칸 범위 안의 칸을 모두 터뜨린다.</summary>
        Area,
        /// <summary>범위 안에서 체크 무늬로 터뜨리고, 반복마다 무늬를 뒤집는다.</summary>
        Checker,
        /// <summary>고리를 따라 비스듬히 이어지는 나선 팔을 터뜨린다. 반복마다 칸 이동만큼 돈다.</summary>
        Spiral,
        /// <summary>범위 안에서 밀도만큼 흩뿌려 터뜨린다. 반복마다 다른 칸이 뽑힌다.</summary>
        Scatter
    }

    /// <summary>
    /// 폭발 미로 순서표의 무늬 하나. 언제 몇 번 터질지, 어떤 칸이 터질지, 경고와 폭발이 얼마나 갈지 담는다.
    /// 칸은 시선과 같은 미로 기준 고리(0 = 가장 바깥)와 칸 번호로 고른다.
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
        [Tooltip("터지기 전에 깜빡이며 경고하는 시간(초)")]
        [SerializeField, Min(0.05f)] private float _warnSeconds = 1f;
        [Tooltip("터진 뒤 칸에 남아 있는 시간(초). 이 동안 칸에 들어오면 휩쓸린다")]
        [SerializeField, Min(0.05f)] private float _burstSeconds = 0.3f;

        [Header("범위")]
        [Tooltip("시작 고리 (0 = 가장 바깥)")]
        [SerializeField] private int _ring;
        [Tooltip("고리 몇 개를 덮는지. 0이면 모든 고리")]
        [SerializeField, Min(0)] private int _ringSpan;
        [Tooltip("시작 칸 번호 (반시계 방향). 나선에서는 팔의 출발 칸이다")]
        [SerializeField] private int _sector;
        [Tooltip("칸 몇 개를 덮는지. 0이면 한 바퀴 전체. 나선에서는 쓰지 않는다")]
        [SerializeField, Min(0)] private int _sectorSpan;

        [Header("반복마다 옮기기")]
        [Tooltip("반복마다 시작 고리를 몇 개 옮기는지 (+ 안쪽, - 바깥쪽)")]
        [SerializeField] private int _ringStep;
        [Tooltip("반복마다 시작 칸을 몇 칸 옮기는지 (+ 반시계, - 시계)")]
        [SerializeField] private int _sectorStep;

        [Header("모양 설정")]
        [Tooltip("나선 팔의 수")]
        [SerializeField, Min(1)] private int _arms = 2;
        [Tooltip("나선이 고리 하나 안쪽으로 갈 때 비껴가는 칸 수")]
        [SerializeField] private int _twist = 1;
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
            int ring = 0, int ringSpan = 0, int sector = 0, int sectorSpan = 0, int ringStep = 0, int sectorStep = 0,
            int arms = 2, int twist = 1, float density = 0.3f, int seed = 0)
        {
            _name = name;
            _shape = shape;
            _start = start;
            _repeat = repeat;
            _interval = interval;
            _warnSeconds = warnSeconds;
            _burstSeconds = burstSeconds;
            _ring = ring;
            _ringSpan = ringSpan;
            _sector = sector;
            _sectorSpan = sectorSpan;
            _ringStep = ringStep;
            _sectorStep = sectorStep;
            _arms = arms;
            _twist = twist;
            _density = density;
            _seed = seed;
        }

        /// <summary>
        /// repeat번째 반복에서 이 무늬가 칸 하나를 터뜨리는지 판단한다.
        /// ring, sector(미로 기준), repeat(0부터), 미로의 고리 수 rings와 칸 수 sectors를 사용하며, 터뜨리면 true를 반환한다.
        /// </summary>
        public bool Covers(int ring, int sector, int repeat, int rings, int sectors)
        {
            int ring0 = _ring + repeat * _ringStep;
            int sector0 = _sector + repeat * _sectorStep;
            // 고리는 감지 않고, 범위를 벗어난 고리는 터지지 않는다.
            if (_ringSpan > 0 && (ring < ring0 || ring >= ring0 + _ringSpan)) return false;

            switch (_shape)
            {
                case BoomShape.Spiral:
                    int period = Mathf.Max(1, sectors / Mathf.Max(1, _arms));
                    return Wrap(sector - sector0 - ring * _twist, sectors) % period == 0;
                case BoomShape.Checker:
                    if (!InSectorRange(sector, sector0, sectors)) return false;
                    return ((ring + sector + repeat) & 1) == 0;
                case BoomShape.Scatter:
                    if (!InSectorRange(sector, sector0, sectors)) return false;
                    return Hash01(ring, sector, repeat) < _density;
                default:
                    return InSectorRange(sector, sector0, sectors);
            }
        }

        /// <summary>
        /// 칸이 sector0에서 반시계로 _sectorSpan칸 안에 있는지 판단한다. _sectorSpan이 0이면 한 바퀴 전체로 본다.
        /// sector, sector0, sectors를 사용하며, 범위 안이면 true를 반환한다.
        /// </summary>
        private bool InSectorRange(int sector, int sector0, int sectors)
        {
            if (_sectorSpan <= 0 || _sectorSpan >= sectors) return true;
            return Wrap(sector - sector0, sectors) < _sectorSpan;
        }

        /// <summary>
        /// 흩뿌림에서 칸 하나의 뽑기 값을 계산한다. 같은 칸, 반복, 시드면 언제나 같은 값이다.
        /// ring, sector, repeat, _seed를 사용하며, 0 이상 1 미만의 값을 반환한다.
        /// </summary>
        private float Hash01(int ring, int sector, int repeat)
        {
            unchecked
            {
                uint h = (uint)ring * 374761393u + (uint)sector * 668265263u + (uint)repeat * 2246822519u + (uint)_seed * 3266489917u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFFu) / 16777216f;
            }
        }

        /// <summary>
        /// 칸 번호를 0 ~ sectors-1 범위로 감는다.
        /// a, sectors를 사용하며, 감긴 번호를 반환한다.
        /// </summary>
        private static int Wrap(int a, int sectors) => ((a % sectors) + sectors) % sectors;
    }
}

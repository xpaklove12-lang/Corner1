using System;

using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>벽 하나. 오브젝트를 끄면(비활성) 그 자리는 지나갈 수 있다.</summary>
    [ExecuteAlways]
    public class MazeWall : MonoBehaviour
    {
        public enum Kind { Arc, Radial }

        [Header("위치")]
        [Tooltip("Arc: ring과 ring+1 사이 / Radial: ring 안에서 sector와 sector+1 사이")]
        [SerializeField, FormerlySerializedAs("kind")] private Kind _kind;
        [SerializeField, FormerlySerializedAs("ring")] private int _ring;
        [SerializeField, FormerlySerializedAs("sector")] private int _sector;
        [Tooltip("홍채 무늬의 숨은 벽이면 막히는 복잡도 단계(1부터). -1이면 처음부터 있는 보통 벽")]
        [SerializeField] private int _closeAtStage = -1;

        [Header("그림")]
        [SerializeField, FormerlySerializedAs("line")] private LineRenderer _line;

        [NonSerialized] private int _drawnKey = int.MinValue;
        // 연출 중 벽 색에 곱할 투명도 추가
        [NonSerialized] private float _visualAlpha = 1f;
        // 숨은 벽이 번쩍이기 시작한 시각. 번쩍이지 않으면 -1이다.
        [NonSerialized] private float _flashStart = -1f;
        // 상처가 새겨지기 시작하는 시각(늦춰서 미래일 수 있다)과 끝까지 그려지는 시간. 새기지 않으면 -1이다.
        [NonSerialized] private float _carveStart = -1f;
        [NonSerialized] private float _carveSeconds = 0.5f;
        // 시선이 부딪혀 찾아낸 상처인지
        [NonSerialized] private bool _discovered;
        private IrisMaze _maze;

        public Kind WallKind => _kind;
        public int Ring => _ring;
        public int Sector => _sector;
        /// <summary>홍채 무늬의 숨은 벽이면 막히는 복잡도 단계(1부터), 보통 벽이면 -1</summary>
        public int CloseAtStage { get => _closeAtStage; set => _closeAtStage = value; }
        /// <summary>홍채 무늬의 숨은 벽인지. 숨은 벽은 미로의 HiddenWallAlpha만큼만 보인다.</summary>
        public bool IsHidden => _closeAtStage >= 0;
        /// <summary>시선이 부딪혀 찾아낸 상처인지. 찾아낸 상처는 의심과 상관없이 보인다.</summary>
        public bool Discovered { get => _discovered; set => _discovered = value; }
        /// <summary>연출 중 벽 색에 곱하는 투명도. 벽 판정에는 쓰지 않는다.</summary>
        public float VisualAlpha { get => _visualAlpha; set => _visualAlpha = value; }

        /// <summary>
        /// 새로 만든 벽의 위치와 그림을 한 번에 설정한다.
        /// kind, ring, sector, line을 사용하며, 이 벽의 위치 정보와 LineRenderer 참조를 바꾼다.
        /// </summary>
        public void Setup(Kind kind, int ring, int sector, LineRenderer line)
        {
            _kind = kind;
            _ring = ring;
            _sector = sector;
            _line = line;
            _drawnKey = int.MinValue;
        }

        /// <summary>
        /// 숨은 벽을 지금부터 번쩍이게 한다.
        /// 입력값은 없으며, _flashStart를 지금 시각으로 바꾼다.
        /// </summary>
        public void Flash() => _flashStart = Time.time;

        /// <summary>
        /// 번쩍임이 얼마나 남았는지 계산한다. 시작 직후 1에서 미로의 HiddenWallFlashSeconds 동안 0으로 줄어든다.
        /// _flashStart와 지금 시각을 사용하며, 0~1 값을 반환하고 끝나면 _flashStart를 지운다.
        /// </summary>
        private float FlashAmount()
        {
            if (_flashStart < 0f) return 0f;
            float amount = 1f - (Time.time - _flashStart) / Mathf.Max(0.01f, _maze.HiddenWallFlashSeconds);
            if (amount > 0f) return Mathf.Min(1f, amount);
            _flashStart = -1f;
            return 0f;
        }

        /// <summary>
        /// 상처를 금이 번지듯 새기기 시작한다. delay초 뒤부터 seconds초 동안 끝까지 그려지고, 미로의 ScarGlimpseSeconds만큼 보였다가 ScarFadeSeconds 동안 가라앉는다.
        /// delay, seconds를 사용하며, _carveStart와 _carveSeconds를 바꾼다.
        /// </summary>
        public void Carve(float delay, float seconds)
        {
            _carveStart = Time.time + delay;
            _carveSeconds = Mathf.Max(0.01f, seconds);
        }

        /// <summary>
        /// 새기는 중인 상처가 얼마나 그려졌고 얼마나 보여야 하는지 계산한다.
        /// _carveStart, _carveSeconds와 미로의 머묾, 가라앉음 시간을 사용하며, drawn(0~1 그려진 길이), glimpse(0~1 보임), carving(그리는 중인지)을 내보낸다. 다 끝나면 _carveStart를 지운다.
        /// </summary>
        private void CarveState(out float drawn, out float glimpse, out bool carving)
        {
            drawn = 1f;
            glimpse = 0f;
            carving = false;
            if (_carveStart < 0f) return;
            float t = Time.time - _carveStart;
            // 아직 금이 닿지 않은 상처는 막혀 있지만 그려지지 않는다.
            if (t < 0f) { drawn = 0f; return; }
            drawn = Mathf.Clamp01(t / _carveSeconds);
            carving = drawn < 1f;
            float after = t - _carveSeconds - _maze.ScarGlimpseSeconds;
            glimpse = after <= 0f ? 1f : 1f - after / Mathf.Max(0.01f, _maze.ScarFadeSeconds);
            if (glimpse > 0f) return;
            glimpse = 0f;
            _carveStart = -1f;
        }

        void OnEnable() { _drawnKey = int.MinValue; }
        void OnValidate() { _drawnKey = int.MinValue; EyeHooks.Repaint(); }

        void Update()
        {
            if (_maze == null) _maze = GetComponentInParent<IrisMaze>();
            if (_maze == null || _line == null) return;
            // 상처는 의심이 풀린 만큼, 찾아냈거나 막 새겨진 동안, 번쩍이는 동안 보인다.
            float flash = IsHidden ? FlashAmount() : 0f;
            float drawn = 1f;
            float glimpse = 0f;
            bool carving = false;
            if (IsHidden) CarveState(out drawn, out glimpse, out carving);
            float heat = Mathf.Max(flash, carving ? 1f : 0f);
            // 보통 벽은 상처가 드러나는 만큼 어두워져 상처를 돋보이게 한다.
            float hiddenAlpha = IsHidden
                ? (drawn > 0f ? Mathf.Max(Mathf.Max(_maze.HiddenWallAlpha, _discovered ? 1f : 0f), Mathf.Max(glimpse, flash)) : 0f)
                : _maze.NormalWallAlpha;
            Color baseColor = IsHidden ? _maze.HiddenWallColor : _maze.WallColor;
            // 표시 투명도가 바뀌어도 다시 칠하도록 키에 포함
            int key = HashCode.Combine(HashCode.Combine(_kind, _ring, _sector, _maze.GeometryKey), baseColor, _maze.WallWidthNow, _visualAlpha, hiddenAlpha, heat, drawn);
            if (key == _drawnKey) return;
            _drawnKey = key;

            // 새기는 동안 원호는 가운데에서 양옆으로, 방사 벽은 동공 쪽에서 바깥으로 금이 번진다.
            float d = _maze.SectorDeg;
            if (_kind == Kind.Arc)
            {
                float mid = (_sector + 0.5f) * d;
                float half = d * 0.5f * drawn;
                Shapes.SetArc(_line, _maze.RingInner(_ring), mid - half, mid + half, 6);
            }
            else
            {
                float a = (_sector + 1) * d;
                float inner = _maze.RingInner(_ring);
                float outer = Mathf.Lerp(inner, _maze.RingOuter(_ring), drawn);
                Shapes.SetPoints(_line, new[] { Shapes.Polar(outer, a), Shapes.Polar(inner, a) }, false);
            }
            // 드러난 상처는 굵게 그리고, 새기거나 번쩍이는 동안 더 굵고 흰빛에 가깝게 그린다.
            _line.widthMultiplier = _maze.WallWidthNow * (IsHidden ? _maze.HiddenWallWidthScale * (1f + heat * 0.6f) : 1f);
            Color color = baseColor;
            if (heat > 0f) color = Color.Lerp(color, new Color(1f, 1f, 1f, color.a), heat * 0.7f);
            // 벽 색에 표시 투명도를 곱하고, 상처는 보여야 하는 만큼만 보인다.
            color.a *= _visualAlpha * hiddenAlpha;
            Shapes.SetColor(_line, color);
            _line.sortingOrder = 5;
        }
    }
}

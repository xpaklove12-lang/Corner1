using UnityEngine;

namespace IntroCuttoon
{
    /// <summary>
    /// 컷툰의 한 쪽(한 화면). 칸을 순서대로 들고 있고, 쪽 전체의 투명도를 조절한다.
    /// </summary>
    public class CuttoonPage : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField] private CanvasGroup _group;
        [Tooltip("이 쪽에서 열리는 순서대로 놓은 칸")]
        [SerializeField] private CuttoonPanel[] _panels;

        private int _openedCount;

        public int PanelCount => _panels.Length;

        public int OpenedCount => _openedCount;

        public float Alpha
        {
            get => _group.alpha;
            set => _group.alpha = value;
        }

        public bool IsBusy
        {
            get
            {
                for (int i = 0; i < _openedCount; i++)
                {
                    if (_panels[i].IsBusy)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        /// <summary>
        /// 아직 열지 않은 칸 가운데 첫 칸을 연다.
        /// _openedCount를 하나 늘린다.
        /// </summary>
        public void OpenNext()
        {
            _panels[_openedCount].Open();
            _openedCount++;
        }

        /// <summary>
        /// 이미 연 칸들의 나타나기와 연출을 모두 끝 상태로 맞춘다.
        /// </summary>
        public void FinishOpened()
        {
            for (int i = 0; i < _openedCount; i++)
            {
                _panels[i].FinishAll();
            }
        }

        /// <summary>
        /// 모든 칸을 열리기 전 상태로 숨기고 _openedCount를 0으로 되돌린다.
        /// </summary>
        public void ResetPage()
        {
            foreach (CuttoonPanel panel in _panels)
            {
                panel.Hide();
            }

            _openedCount = 0;
        }
    }
}

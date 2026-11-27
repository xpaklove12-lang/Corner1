using UnityEngine;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 코드가 글을 넣지 않는 고정 라벨에 붙여 Loc/UI.csv key의 현재 언어 글을 넣는다 (스펙 LOC-08).
    /// 켜질 때와 현재 언어가 바뀔 때 글을 다시 넣는다. 씬에 적힌 글은 편집용 미리보기로만 남는다.
    /// </summary>
    public class LocalizedLabel : MonoBehaviour
    {
        [Header("글")]
        [Tooltip("글을 넣을 uGUI Text")]
        [SerializeField] private Text _label;
        [Tooltip("Loc/UI.csv의 key")]
        [SerializeField] private string _key = "";

        void OnEnable()
        {
            Loc.LanguageChanged += Refresh;
            Refresh();
        }

        void OnDisable() => Loc.LanguageChanged -= Refresh;

        /// <summary>
        /// key의 현재 언어 글을 라벨에 넣는다.
        /// _key를 사용하며, _label의 글을 바꾼다.
        /// </summary>
        public void Refresh() => _label.text = Strings.Get(_key);
    }
}

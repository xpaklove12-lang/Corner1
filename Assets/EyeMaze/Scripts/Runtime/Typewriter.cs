using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 한 글자씩 찍히는 글 연출. 글이 바뀌면 빠르게 한 글자씩 보여준다.
    /// 마침표·쉼표 뒤에서 살짝 멈추고, 아직 안 나온 글자는 투명하게 깔아 둬서 줄바꿈이 도중에 튀지 않는다.
    /// 에디트 모드에서는 바로 전부 보여준다.
    /// </summary>
    [RequireComponent(typeof(Text))]
    public class Typewriter : MonoBehaviour
    {
        private static readonly List<Typewriter> _active = new List<Typewriter>();

        [Header("속도")]
        [Tooltip("초당 찍히는 글자 수")]
        [SerializeField, FormerlySerializedAs("charsPerSecond")] private float _charsPerSecond = 40f;
        [Tooltip("마침표·쉼표·물음표 뒤에서 멈추는 시간(초)")]
        [SerializeField, FormerlySerializedAs("punctuationPause")] private float _punctuationPause = 0.12f;

        [NonSerialized] private Text _text;
        [NonSerialized] private string _target = "";
        [NonSerialized] private int _shown;
        [NonSerialized] private float _timer;

        public bool IsTyping => Application.isPlaying && isActiveAndEnabled && _shown < _target.Length;

        public static bool AnyTyping
        {
            get
            {
                foreach (Typewriter t in _active) if (t != null && t.IsTyping) return true;
                return false;
            }
        }

        void OnEnable()
        {
            if (!_active.Contains(this)) _active.Add(this);
            _text = GetComponent<Text>();
        }

        void OnDisable()
        {
            _active.Remove(this);
            Complete();
        }

        void Update()
        {
            if (!Application.isPlaying || _shown >= _target.Length) return;
            _timer += Time.unscaledDeltaTime;
            float interval = 1f / Mathf.Max(1f, _charsPerSecond);
            while (_timer >= interval && _shown < _target.Length)
            {
                _timer -= interval;
                _shown++;
                char c = _target[_shown - 1];
                if (".,!?…".IndexOf(c) >= 0) _timer -= _punctuationPause;
            }
            Apply();
        }

        /// <summary>
        /// 찍히고 있는 모든 글을 한 번에 끝까지 보여준다.
        /// 입력값은 없으며, 활성화된 모든 Typewriter를 완료 상태로 바꾼다.
        /// </summary>
        public static void SkipAll()
        {
            foreach (Typewriter t in _active) if (t != null) t.Complete();
        }

        /// <summary>
        /// Text에 Typewriter가 있으면 한 글자씩, 없으면 바로 글을 쓴다.
        /// t, s를 사용하며, Text의 글 또는 Typewriter의 목표 글을 바꾼다.
        /// </summary>
        public static void SetText(Text t, string s)
        {
            if (t == null) return;
            s = s ?? "";
            Typewriter tw = t.GetComponent<Typewriter>();
            if (tw != null && tw.enabled) tw.SetTarget(s);
            else if (t.text != s) t.text = s;
        }

        /// <summary>
        /// 새로 찍을 글을 정하고 처음부터 찍기 시작한다. 같은 글이면 무시한다.
        /// s를 사용하며, _target과 진행 상태를 바꾼다.
        /// </summary>
        public void SetTarget(string s)
        {
            s = s ?? "";
            if (s == _target) return;
            _target = s;
            _timer = 0f;
            _shown = Application.isPlaying && isActiveAndEnabled ? 0 : s.Length;
            Apply();
        }

        /// <summary>
        /// 지금 글을 끝까지 바로 보여준다.
        /// 입력값은 없으며, _shown을 글 길이로 바꾼다.
        /// </summary>
        public void Complete()
        {
            _shown = _target.Length;
            Apply();
        }

        /// <summary>
        /// 지금까지 찍힌 글자를 Text에 반영한다. 남은 글자는 투명하게 붙인다.
        /// _target, _shown을 사용하며, Text의 글을 바꾼다.
        /// </summary>
        private void Apply()
        {
            if (_text == null) _text = GetComponent<Text>();
            if (_text == null) return;
            string s;
            if (_shown >= _target.Length) s = _target;
            else
            {
                // 남은 글자를 투명하게 깔아 줄바꿈 위치를 미리 고정한다.
                _text.supportRichText = true;
                s = _target.Substring(0, _shown) + "<color=#00000000>" + _target.Substring(_shown) + "</color>";
            }
            if (_text.text != s) _text.text = s;
        }
    }
}

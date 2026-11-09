using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    /// <summary>
    /// 스펙 007: 플레이 중 화면에 나온 대사를 모아 두는 기록.
    /// 필드 대화, 만남 대화, 엔딩 대사를 순서대로 담고, 대화 기록 창(DialogueLogUI)이 읽어서 보여준다.
    /// Play Mode가 끝날 때까지 손님이 바뀌거나 리셋해도 남는다.
    /// </summary>
    public static class DialogueLog
    {
        /// <summary>기록 한 줄. IsPlayer면 플레이어가 고른 말, 아니고 Speaker가 비어 있으면 서술이다. Portrait는 말한 사람 얼굴</summary>
        public readonly struct Entry
        {
            public readonly string Speaker;
            public readonly string Text;
            public readonly bool IsPlayer;
            public readonly Sprite Portrait;

            public Entry(string speaker, string text, bool isPlayer, Sprite portrait)
            {
                Speaker = speaker;
                Text = text;
                IsPlayer = isPlayer;
                Portrait = portrait;
            }
        }

        public const int DEFAULT_MAX_ENTRIES = 100;

        private static readonly List<Entry> _entries = new List<Entry>();
        private static int _maxEntries = DEFAULT_MAX_ENTRIES;

        public static IReadOnlyList<Entry> Entries => _entries;
        public static int Version { get; private set; }
        public static bool IsOpen { get; set; }

        /// <summary>창이 열려 있거나 이번 프레임에 닫혔으면 true. 닫는 키가 같은 프레임에 게임 입력으로 쓰이지 않게 한다</summary>
        public static bool BlocksInput => IsOpen || Time.frameCount == ClosedFrame;

        public static int ClosedFrame { get; set; } = -1;

        public static int MaxEntries
        {
            get => _maxEntries;
            set
            {
                _maxEntries = Mathf.Max(1, value);
                Trim();
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _entries.Clear();
            _maxEntries = DEFAULT_MAX_ENTRIES;
            Version = 0;
            IsOpen = false;
            ClosedFrame = -1;
        }

        /// <summary>
        /// 기록 끝에 대사 한 줄을 더한다. Play Mode가 아니거나 글이 비었거나 바로 앞 기록과 같으면 더하지 않는다.
        /// speaker(비면 서술), text, isPlayer(플레이어가 고른 말인지), portrait(말한 사람 얼굴)를 사용하며, _entries와 Version을 바꾼다.
        /// </summary>
        public static void Add(string speaker, string text, bool isPlayer = false, Sprite portrait = null)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(text)) return;
            speaker = speaker ?? "";
            if (_entries.Count > 0)
            {
                Entry last = _entries[_entries.Count - 1];
                if (last.Speaker == speaker && last.Text == text && last.IsPlayer == isPlayer) return;
            }
            _entries.Add(new Entry(speaker, text, isPlayer, portrait));
            Trim();
            Version++;
        }

        /// <summary>
        /// 최대 개수를 넘는 오래된 기록을 지운다.
        /// _maxEntries를 사용하며, _entries와 Version을 바꾼다.
        /// </summary>
        private static void Trim()
        {
            int over = _entries.Count - _maxEntries;
            if (over <= 0) return;
            _entries.RemoveRange(0, over);
            Version++;
        }
    }
}

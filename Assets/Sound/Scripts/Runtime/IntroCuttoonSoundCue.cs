using System;

using UnityEngine;

using FMODUnity;

namespace GameSound
{
    /// <summary>
    /// 칸 소리 하나가 하는 일의 종류.
    /// </summary>
    public enum CueKind
    {
        OneShot,
        AmbienceOn,
        AmbienceOff,
        Stop,
        Parameter,
    }

    /// <summary>
    /// 칸이 열린 뒤 정한 시점에 하는 소리 일 하나. 효과음 재생, 환경음 켜기와 끄기, 바로 끊기, 환경음 파라미터 바꾸기 중 하나다.
    /// </summary>
    [Serializable]
    public class SoundCue
    {
        [Tooltip("이 소리 일의 종류")]
        [SerializeField] private CueKind _kind;
        [Tooltip("재생하거나 켜고 끄거나 끊거나 파라미터를 바꿀 FMOD 이벤트")]
        [SerializeField] private EventReference _event;
        [Tooltip("칸이 열린 뒤 기다리는 시간(초)")]
        [SerializeField] private float _delay;
        [Tooltip("환경음이 켜지거나 꺼지는 시간, 또는 파라미터가 바뀌는 시간(초)")]
        [SerializeField] private float _duration;
        [Tooltip("바꿀 파라미터 이름. 파라미터 바꾸기에서만 쓴다")]
        [SerializeField] private string _parameter;
        [Tooltip("파라미터의 시작 값")]
        [SerializeField] private float _from;
        [Tooltip("파라미터의 끝 값")]
        [SerializeField] private float _to = 1f;
        [Tooltip("켜면 파라미터가 천천히 바뀌기 시작해 점점 빨라진다")]
        [SerializeField] private bool _easeIn;
        [Tooltip("켜면 연출을 건너뛰어도 이 효과음을 줄이지 않고 끝까지 울린다")]
        [SerializeField] private bool _keepOnSkip;

        public CueKind Kind => _kind;

        public EventReference Event => _event;

        public float Delay => _delay;

        public float Duration => _duration;

        public string Parameter => _parameter;

        public float From => _from;

        public float To => _to;

        public bool EaseIn => _easeIn;

        public bool KeepOnSkip => _keepOnSkip;
    }

    /// <summary>
    /// 한 칸에 붙인 소리 일의 목록. 쪽과 칸 번호는 화면에 보이는 대로 1부터 센다.
    /// </summary>
    [Serializable]
    public class PanelSounds
    {
        [Tooltip("쪽 번호(1부터)")]
        [SerializeField] private int _page = 1;
        [Tooltip("그 쪽의 칸 번호(1부터)")]
        [SerializeField] private int _panel = 1;
        [Tooltip("칸이 열린 뒤 할 소리 일")]
        [SerializeField] private SoundCue[] _cues;

        public int Page => _page;

        public int Panel => _panel;

        public SoundCue[] Cues => _cues;
    }

    /// <summary>
    /// 쪽 시작 상태에서 켜 둘 환경음 하나와 그 파라미터 값.
    /// </summary>
    [Serializable]
    public class AmbienceStart
    {
        [Tooltip("켜 둘 환경음")]
        [SerializeField] private EventReference _event;
        [Tooltip("미리 맞춰 둘 파라미터 이름. 비우면 맞추지 않는다")]
        [SerializeField] private string _parameter;
        [Tooltip("파라미터 값")]
        [SerializeField] private float _value;

        public EventReference Event => _event;

        public string Parameter => _parameter;

        public float Value => _value;
    }

    /// <summary>
    /// 처음으로나 쪽 바로 가기로 그 쪽에 들어왔을 때 첫 칸이 열리기 전에 만들어 둘 소리 상태.
    /// </summary>
    [Serializable]
    public class PageStartSounds
    {
        [Tooltip("쪽 번호(1부터)")]
        [SerializeField] private int _page = 1;
        [Tooltip("켜 둘 환경음")]
        [SerializeField] private AmbienceStart[] _ambiences;

        public int Page => _page;

        public AmbienceStart[] Ambiences => _ambiences;
    }
}

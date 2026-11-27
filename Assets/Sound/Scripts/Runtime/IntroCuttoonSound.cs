using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using FMOD.Studio;
using FMODUnity;

using IntroCuttoon;

namespace GameSound
{
    /// <summary>
    /// 인트로 컷툰의 칸 진행에 맞춰 효과음과 환경음을 낸다.
    /// 칸별 소리 표와 쪽 시작 상태는 Inspector 값이고, 씬에 컷툰 진행이 있으면 Resources의 프리팹이 스스로 놓인다.
    /// </summary>
    public class IntroCuttoonSound : MonoBehaviour
    {
        private const string PREFAB_PATH = "IntroCuttoonSound";
        private const float ONE_SHOT_CHECK_AGE = 0.1f;

        [Header("칸별 소리")]
        [Tooltip("칸마다 열린 뒤 할 소리 일")]
        [SerializeField] private PanelSounds[] _panels;

        [Header("쪽 시작 상태")]
        [Tooltip("처음으로나 쪽 바로 가기로 들어왔을 때 첫 칸 전에 켜 둘 환경음")]
        [SerializeField] private PageStartSounds[] _pageStarts;

        [Header("넘김")]
        [Tooltip("연출을 건너뛸 때 울리던 효과음을 줄여 끄는 시간(초)")]
        [SerializeField] private float _skipOneShotFade = 0.15f;
        [Tooltip("연출을 건너뛸 때 환경음과 파라미터를 칸의 끝 값으로 맞추는 시간(초)")]
        [SerializeField] private float _skipSettleTime = 0.2f;
        [Tooltip("처음으로나 쪽 바로 가기에서 울리던 소리를 줄여 끄는 시간(초)")]
        [SerializeField] private float _restartFade = 0.1f;

        private CuttoonPlayer _player;
        private readonly List<Voice> _voices = new List<Voice>();
        private readonly List<PendingCue> _pending = new List<PendingCue>();
        private int _currentPanelKey = -1;

        /// <summary>
        /// 울리고 있는 소리 하나. 음량을 목표 값으로 옮기고, 환경음이면 파라미터 변화도 들고 있다.
        /// </summary>
        private class Voice
        {
            public EventInstance Instance { get; set; }
            public FMOD.GUID Event { get; set; }
            public bool IsAmbience { get; set; }
            public bool KeepOnSkip { get; set; }
            public int PanelKey { get; set; }
            public float Age { get; set; }
            public float Volume { get; set; }
            public float Target { get; set; }
            public float Speed { get; set; }
            public bool StopAtTarget { get; set; }
            public Dictionary<string, ParameterRamp> Ramps { get; } = new Dictionary<string, ParameterRamp>();
            public Dictionary<string, float> Values { get; } = new Dictionary<string, float>();

            public bool IsStopping => StopAtTarget && Target <= 0f;
        }

        /// <summary>
        /// 환경음 파라미터 하나가 시작 값에서 끝 값으로 바뀌는 중인 상태.
        /// </summary>
        private class ParameterRamp
        {
            public float From { get; set; }
            public float To { get; set; }
            public float Duration { get; set; }
            public float Elapsed { get; set; }
            public bool EaseIn { get; set; }
            public int PanelKey { get; set; }
        }

        /// <summary>
        /// 시점을 기다리는 소리 일 하나.
        /// </summary>
        private class PendingCue
        {
            public SoundCue Cue { get; set; }
            public int PanelKey { get; set; }
            public float Elapsed { get; set; }
        }

        void Update()
        {
            float deltaTime = Time.deltaTime;
            TickPending(deltaTime);
            TickVoices(deltaTime);
        }

        void OnDestroy()
        {
            _player.PanelOpened -= HandlePanelOpened;
            _player.FinishingOpened -= HandleFinishingOpened;
            _player.Restarting -= HandleRestarting;
            _player.BlackoutStarted -= HandleBlackoutStarted;

            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                StopNow(_voices[i]);
            }
        }

        void OnValidate()
        {
            _skipOneShotFade = Mathf.Max(0f, _skipOneShotFade);
            _skipSettleTime = Mathf.Max(0f, _skipSettleTime);
            _restartFade = Mathf.Max(0f, _restartFade);
        }

        /// <summary>
        /// 씬이 열릴 때마다 소리 담당을 놓을지 보도록 씬 열림 알림에 등록한다.
        /// 도메인 다시 읽기를 끈 Play에서도 한 번만 등록되게 먼저 뺀 뒤 더한다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneHook()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        /// <summary>
        /// 열린 씬에 컷툰 진행이 있고 소리 담당이 아직 없으면 Resources의 프리팹을 놓고 컷툰 진행에 잇는다.
        /// scene과 mode는 쓰지 않는다. 씬 열림 알림은 컷툰 진행의 Start보다 먼저 오므로 첫 칸부터 소리가 난다.
        /// </summary>
        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CuttoonPlayer player = FindFirstObjectByType<CuttoonPlayer>();
            if (player == null || FindFirstObjectByType<IntroCuttoonSound>() != null)
            {
                return;
            }

            IntroCuttoonSound sound = Instantiate(Resources.Load<IntroCuttoonSound>(PREFAB_PATH));
            sound.Bind(player);
        }

        /// <summary>
        /// player의 칸 열림, 건너뛰기, 처음으로, 암전 알림을 받도록 등록한다.
        /// _player를 player로 바꾼다.
        /// </summary>
        public void Bind(CuttoonPlayer player)
        {
            _player = player;
            _player.PanelOpened += HandlePanelOpened;
            _player.FinishingOpened += HandleFinishingOpened;
            _player.Restarting += HandleRestarting;
            _player.BlackoutStarted += HandleBlackoutStarted;
        }

        /// <summary>
        /// 열린 칸의 소리 일을 시점이 0이면 바로 하고, 아니면 기다리는 목록에 넣는다.
        /// pageIndex와 panelIndex는 0부터 센 값이며, _currentPanelKey를 이 칸으로 바꾼다.
        /// </summary>
        private void HandlePanelOpened(int pageIndex, int panelIndex)
        {
            int key = pageIndex * 100 + panelIndex;
            _currentPanelKey = key;

            foreach (PanelSounds panel in _panels)
            {
                if (panel.Page - 1 != pageIndex || panel.Panel - 1 != panelIndex)
                {
                    continue;
                }

                foreach (SoundCue cue in panel.Cues)
                {
                    if (cue.Delay <= 0f)
                    {
                        Execute(cue, key, cue.Duration, cue.From, cue.EaseIn);
                    }
                    else
                    {
                        _pending.Add(new PendingCue { Cue = cue, PanelKey = key });
                    }
                }
            }
        }

        /// <summary>
        /// 마지막으로 연 칸의 소리를 그 칸의 끝 상태로 맞춘다. 아직 나지 않은 효과음은 버리고, 울리던 효과음은 _skipOneShotFade초 동안 줄여 끈다.
        /// 환경음 켜고 끄기와 파라미터는 _skipSettleTime초 안에 끝 값으로 맞춘다. 끝까지 울리게 한 효과음은 그대로 둔다.
        /// </summary>
        private void HandleFinishingOpened()
        {
            int key = _currentPanelKey;

            for (int i = 0; i < _pending.Count; i++)
            {
                PendingCue pending = _pending[i];
                if (pending.PanelKey != key)
                {
                    continue;
                }

                _pending.RemoveAt(i);
                i--;
                if (pending.Cue.Kind == CueKind.OneShot)
                {
                    continue;
                }

                Execute(pending.Cue, key, _skipSettleTime, CurrentValue(pending.Cue), false);
            }

            foreach (Voice voice in _voices)
            {
                if (voice.PanelKey == key)
                {
                    if (!voice.IsAmbience)
                    {
                        if (!voice.KeepOnSkip)
                        {
                            FadeTo(voice, 0f, _skipOneShotFade, true);
                        }
                    }
                    else if (!Mathf.Approximately(voice.Volume, voice.Target))
                    {
                        FadeTo(voice, voice.Target, _skipSettleTime, voice.StopAtTarget);
                    }
                }

                // 다른 칸이 켠 환경음이라도 이 칸이 건 파라미터 변화는 끝 값으로 맞춘다.
                foreach (KeyValuePair<string, ParameterRamp> pair in voice.Ramps)
                {
                    ParameterRamp ramp = pair.Value;
                    if (ramp.PanelKey != key)
                    {
                        continue;
                    }

                    ramp.From = voice.Values.TryGetValue(pair.Key, out float value) ? value : ramp.From;
                    ramp.Duration = _skipSettleTime;
                    ramp.Elapsed = 0f;
                    ramp.EaseIn = false;
                }
            }
        }

        /// <summary>
        /// 처음으로나 쪽 바로 가기에서 기다리던 소리 일을 모두 버리고, 울리던 소리를 _restartFade초 동안 줄여 끈다.
        /// pageIndex 쪽의 시작 상태에 있는 환경음은 겹치지 않게 바로 끊은 뒤 새로 켜고 파라미터를 미리 맞춘다.
        /// </summary>
        private void HandleRestarting(int pageIndex)
        {
            _pending.Clear();
            _currentPanelKey = -1;

            AmbienceStart[] starts = Array.Empty<AmbienceStart>();
            foreach (PageStartSounds page in _pageStarts)
            {
                if (page.Page - 1 == pageIndex)
                {
                    starts = page.Ambiences;
                }
            }

            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                Voice voice = _voices[i];
                if (IsInStarts(voice.Event, starts))
                {
                    StopNow(voice);
                }
                else
                {
                    FadeTo(voice, 0f, _restartFade, true);
                }
            }

            foreach (AmbienceStart start in starts)
            {
                Voice voice = CreateVoice(start.Event, true, false, -1, 1f);
                if (!string.IsNullOrEmpty(start.Parameter))
                {
                    SetParameter(voice, start.Parameter, start.Value);
                }

                voice.Instance.start();
            }
        }

        /// <summary>
        /// 암전이 시작되면 기다리던 소리 일을 버리고, 울리던 소리를 모두 duration초 동안 고르게 줄인 뒤 끈다.
        /// </summary>
        private void HandleBlackoutStarted(float duration)
        {
            _pending.Clear();
            foreach (Voice voice in _voices)
            {
                FadeTo(voice, 0f, duration, true);
            }
        }

        /// <summary>
        /// 기다리는 소리 일의 시간을 deltaTime만큼 늘리고, 시점이 된 것을 한다.
        /// 한 일은 _pending에서 뺀다.
        /// </summary>
        private void TickPending(float deltaTime)
        {
            for (int i = 0; i < _pending.Count; i++)
            {
                PendingCue pending = _pending[i];
                pending.Elapsed += deltaTime;
                if (pending.Elapsed < pending.Cue.Delay)
                {
                    continue;
                }

                _pending.RemoveAt(i);
                i--;
                Execute(pending.Cue, pending.PanelKey, pending.Cue.Duration, pending.Cue.From, pending.Cue.EaseIn);
            }
        }

        /// <summary>
        /// 울리는 소리마다 음량을 목표 쪽으로 옮기고 파라미터 변화를 진행한다.
        /// 목표 0에서 끄기로 한 소리와 스스로 끝난 효과음은 놓아 주고 _voices에서 뺀다.
        /// </summary>
        private void TickVoices(float deltaTime)
        {
            for (int i = _voices.Count - 1; i >= 0; i--)
            {
                Voice voice = _voices[i];
                voice.Age += deltaTime;

                if (!Mathf.Approximately(voice.Volume, voice.Target))
                {
                    voice.Volume = Mathf.MoveTowards(voice.Volume, voice.Target, voice.Speed * deltaTime);
                    voice.Instance.setVolume(voice.Volume);
                }

                if (voice.IsStopping && voice.Volume <= 0f)
                {
                    StopNow(voice);
                    continue;
                }

                TickRamps(voice, deltaTime);

                if (!voice.IsAmbience && voice.Age > ONE_SHOT_CHECK_AGE)
                {
                    voice.Instance.getPlaybackState(out PLAYBACK_STATE state);
                    if (state == PLAYBACK_STATE.STOPPED)
                    {
                        voice.Instance.release();
                        _voices.RemoveAt(i);
                    }
                }
            }
        }

        /// <summary>
        /// voice의 파라미터 변화를 deltaTime만큼 진행해 FMOD 인스턴스에 값을 넣는다.
        /// 끝난 변화는 voice.Ramps에서 빼고, 마지막 값은 voice.Values에 남긴다.
        /// </summary>
        private void TickRamps(Voice voice, float deltaTime)
        {
            if (voice.Ramps.Count == 0)
            {
                return;
            }

            List<string> finished = null;
            foreach (KeyValuePair<string, ParameterRamp> pair in voice.Ramps)
            {
                ParameterRamp ramp = pair.Value;
                ramp.Elapsed += deltaTime;
                float t = ramp.Duration > 0f ? Mathf.Clamp01(ramp.Elapsed / ramp.Duration) : 1f;
                float eased = ramp.EaseIn ? 1f - Mathf.Cos(t * Mathf.PI * 0.5f) : t;
                SetParameter(voice, pair.Key, Mathf.Lerp(ramp.From, ramp.To, eased));
                if (t >= 1f)
                {
                    finished ??= new List<string>();
                    finished.Add(pair.Key);
                }
            }

            if (finished == null)
            {
                return;
            }

            foreach (string name in finished)
            {
                voice.Ramps.Remove(name);
            }
        }

        /// <summary>
        /// cue 하나를 지금 한다. time은 켜고 끄는 시간이나 파라미터가 바뀌는 시간이고, from과 easeIn은 파라미터의 시작 값과 천천히 시작할지다.
        /// key는 이 일을 붙인 칸이며, 소리를 새로 내거나 _voices의 목표 음량과 파라미터 변화를 바꾼다.
        /// </summary>
        private void Execute(SoundCue cue, int key, float time, float from, bool easeIn)
        {
            FMOD.GUID id = cue.Event.Guid;
            Voice ambience = FindAmbience(id);

            switch (cue.Kind)
            {
                case CueKind.OneShot:
                    CreateVoice(cue.Event, false, cue.KeepOnSkip, key, 1f).Instance.start();
                    break;

                case CueKind.AmbienceOn:
                    if (ambience != null)
                    {
                        ambience.PanelKey = key;
                        break;
                    }

                    Voice created = CreateVoice(cue.Event, true, false, key, time > 0f ? 0f : 1f);
                    FadeTo(created, 1f, time, false);
                    created.Instance.start();
                    break;

                case CueKind.AmbienceOff:
                    if (ambience != null)
                    {
                        ambience.PanelKey = key;
                        FadeTo(ambience, 0f, time, true);
                    }

                    break;

                case CueKind.Stop:
                    for (int i = _voices.Count - 1; i >= 0; i--)
                    {
                        if (_voices[i].Event.Equals(id))
                        {
                            StopNow(_voices[i]);
                        }
                    }

                    break;

                case CueKind.Parameter:
                    if (ambience != null)
                    {
                        ambience.Ramps[cue.Parameter] = new ParameterRamp
                        {
                            From = from,
                            To = cue.To,
                            Duration = time,
                            EaseIn = easeIn,
                            PanelKey = key,
                        };
                    }

                    break;
            }
        }

        /// <summary>
        /// eventReference의 인스턴스를 만들어 _voices에 넣고 돌려준다. 재생은 부른 쪽이 시작한다.
        /// isAmbience, keepOnSkip, key, volume으로 소리의 종류, 건너뛰기 처리, 붙인 칸, 처음 음량을 정한다.
        /// </summary>
        private Voice CreateVoice(EventReference eventReference, bool isAmbience, bool keepOnSkip, int key, float volume)
        {
            Voice voice = new Voice
            {
                Instance = RuntimeManager.CreateInstance(eventReference),
                Event = eventReference.Guid,
                IsAmbience = isAmbience,
                KeepOnSkip = keepOnSkip,
                PanelKey = key,
                Volume = volume,
                Target = volume,
            };

            voice.Instance.setVolume(volume);
            _voices.Add(voice);
            return voice;
        }

        /// <summary>
        /// voice의 목표 음량을 target으로 두고 time초 안에 닿을 빠르기를 정한다.
        /// stop이 참이고 target이 0이면 닿는 순간 끈다.
        /// </summary>
        private void FadeTo(Voice voice, float target, float time, bool stop)
        {
            voice.Target = target;
            voice.StopAtTarget = stop;
            voice.Speed = time > 0f ? Mathf.Abs(target - voice.Volume) / time : float.PositiveInfinity;
        }

        /// <summary>
        /// voice를 페이드 없이 바로 끄고 놓아 준 뒤 _voices에서 뺀다.
        /// </summary>
        private void StopNow(Voice voice)
        {
            voice.Instance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            voice.Instance.release();
            _voices.Remove(voice);
        }

        /// <summary>
        /// voice의 FMOD 인스턴스에 name 파라미터를 value로 넣고 voice.Values에 남긴다.
        /// </summary>
        private void SetParameter(Voice voice, string name, float value)
        {
            voice.Instance.setParameterByName(name, value);
            voice.Values[name] = value;
        }

        /// <summary>
        /// 꺼지는 중이 아닌 환경음 가운데 id 이벤트의 것을 찾아 돌려준다. 없으면 null이다.
        /// </summary>
        private Voice FindAmbience(FMOD.GUID id)
        {
            foreach (Voice voice in _voices)
            {
                if (voice.IsAmbience && !voice.IsStopping && voice.Event.Equals(id))
                {
                    return voice;
                }
            }

            return null;
        }

        /// <summary>
        /// cue가 바꾸는 환경음 파라미터의 지금 값을 돌려준다. 환경음이 없거나 값을 아직 넣지 않았으면 cue.From을 돌려준다.
        /// </summary>
        private float CurrentValue(SoundCue cue)
        {
            Voice ambience = FindAmbience(cue.Event.Guid);
            if (ambience != null && !string.IsNullOrEmpty(cue.Parameter) && ambience.Values.TryGetValue(cue.Parameter, out float value))
            {
                return value;
            }

            return cue.From;
        }

        /// <summary>
        /// id 이벤트가 starts에 있으면 참을 돌려준다.
        /// </summary>
        private static bool IsInStarts(FMOD.GUID id, AmbienceStart[] starts)
        {
            foreach (AmbienceStart start in starts)
            {
                if (start.Event.Guid.Equals(id))
                {
                    return true;
                }
            }

            return false;
        }
    }
}

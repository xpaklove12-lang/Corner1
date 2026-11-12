using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 파편 회상을 재생한다. 미로를 가라앉히고 장면 그림을 보여 준 뒤, 진행 키로 회상 방에 들어가 내담자를 움직이게 한다.
    /// 지금 단계의 사물에 상호작용하면 그 단계의 대본 줄을 보여 주고, 마지막 단계가 끝나면 방을 닫고 미로를 되돌린 뒤 완료를 알린다.
    /// 회상 화면의 UI 오브젝트는 에디터에서 만든 것을 참조만 한다.
    /// </summary>
    public class FlashbackPlayer : MonoBehaviour
    {
        private enum Phase { Idle, Cover, CoverWait, Enter, Room, Exit }
        private enum LineKind { Intro, Step, Look }

        /// <summary>회상이 재생되는 중인지. EyeMazeDirector와 GameFlow가 입력 차단에 쓴다</summary>
        public static bool IsPlaying { get; private set; }

        [Header("회상 방")]
        [Tooltip("씬에 있는 회상 방 목록")]
        [SerializeField] private FlashbackRoom[] _rooms = new FlashbackRoom[0];

        [Header("카메라")]
        [Tooltip("회상 방을 비출 카메라 (씬의 메인 카메라)")]
        [SerializeField] private Camera _camera;

        [Header("연출")]
        [Tooltip("회상 중 미로의 투명도")]
        [SerializeField, Range(0f, 1f)] private float _mazeDimAlpha = 0.15f;
        [Tooltip("미로가 가라앉고 되돌아오는 시간(초)")]
        [SerializeField] private float _dimSeconds = 0.8f;
        [Tooltip("장면 그림이 나타나는 시간(초)")]
        [SerializeField] private float _coverFadeSeconds = 0.5f;
        [Tooltip("장면 그림이 방 테두리 크기로 줄어드는 시간(초)")]
        [SerializeField] private float _enterSeconds = 2f;
        // 스펙 016: 줄어드는 그림이 흐려지기 시작하는 시점 추가
        [Tooltip("장면 그림이 줄어드는 시간 중 흐려지기 시작하는 지점 (0이면 줄어들기 시작할 때부터 함께 흐려진다)")]
        [SerializeField, Range(0f, 0.9f)] private float _enterFadeStart = 0.35f;
        // 스펙 016: 진입 연출의 가속 곡선, 방이 다가오는 카메라 배율 추가
        [Tooltip("장면 그림이 줄어드는 빠르기 곡선 (가로 0~1 시간, 세로 0~1 진행)")]
        [SerializeField] private AnimationCurve _enterCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
        [Tooltip("진입을 시작할 때 방 카메라 크기 배율. 1보다 크면 방이 멀리서 다가오듯 보인다")]
        [SerializeField] private float _enterCameraZoom = 1.15f;

        [Header("화면")]
        [Tooltip("장면 그림을 보여 줄 Image")]
        [SerializeField] private Image _coverImage;
        [Tooltip("대본 줄을 보여 줄 글 칸")]
        [SerializeField] private GameObject _textBox;
        [Tooltip("화자 이름 글")]
        [SerializeField] private Text _speakerText;
        [Tooltip("본문 글 (Typewriter를 붙인다)")]
        [SerializeField] private Text _bodyText;
        [Tooltip("가까운 사물 이름 안내 글")]
        [SerializeField] private Text _hintText;
        [Tooltip("카메라 화면을 띄우는 RawImage (PixelCamera의 대상). 안내 글을 사물 위에 놓을 때 쓴다")]
        [SerializeField] private RectTransform _cameraView;
        [Tooltip("사물 안내 글 형식. {0}에 사물 행동 글이 들어간다")]
        [SerializeField] private string _hintFormat = "[Space] {0}";
        [Tooltip("회상 중 끄고 끝나면 다시 켤 만남 UI 루트")]
        [SerializeField] private GameObject _encounterUi;

        private Phase _phase = Phase.Idle;
        private float _phaseT;
        private float _mazeAlpha = 1f;
        private float _mazeTarget = 1f;
        private string _flag;
        private string _roomFlag;
        private FlashbackRoom _room;
        private FlashbackScript.Flashback _script;
        private Action _onFinished;
        private Vector3 _cameraPosBefore;
        private float _cameraSizeBefore;
        private Vector3 _coverScale = Vector3.one;
        private float _coverFit = 1f;
        private List<FlashbackProp> _props = new List<FlashbackProp>();
        private int _stepIndex;
        // 글 칸에 나오는 줄 묶음. null이면 글 칸이 닫혀 있다.
        private List<FlashbackScript.Line> _lines;
        private LineKind _lineKind;
        private int _lineIndex;
        private bool _typingPrev;

        /// <summary>회상 연출이 미로 투명도에 곱하는 값. 회상 중이 아니면 1</summary>
        public float MazeAlpha => _mazeAlpha;
        // 스펙 016: 데이터 검사가 FindRoom과 같은 방을 고르도록 방 목록 공개
        /// <summary>회상 방 목록. 같은 플래그의 방이 여럿이면 앞의 방이 재생된다</summary>
        public FlashbackRoom[] Rooms => _rooms;

        private FlashbackScript.Step CurrentStep => _script != null && _stepIndex < _script.Steps.Count ? _script.Steps[_stepIndex] : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => IsPlaying = false;

        void OnDisable()
        {
            IsPlaying = false;
        }

        void Update()
        {
            if (!IsPlaying) return;
            float dt = Time.deltaTime;
            _phaseT += dt;
            float dimRate = Mathf.Max(0.01f, 1f - _mazeDimAlpha) / Mathf.Max(0.01f, _dimSeconds);
            _mazeAlpha = Mathf.MoveTowards(_mazeAlpha, _mazeTarget, dimRate * dt);

            // 대화 기록 창이 열려 있는 동안은 입력을 처리하지 않는다.
            Keyboard kb = DialogueLog.BlocksInput ? null : Keyboard.current;
            Gamepad pad = DialogueLog.BlocksInput ? null : Gamepad.current;
            bool press = (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame))
                         || (pad != null && pad.buttonSouth.wasPressedThisFrame);
            float moveX = 0f;
            if (kb != null)
            {
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) moveX += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) moveX -= 1f;
            }
            if (pad != null && Mathf.Abs(pad.leftStick.ReadValue().x) > 0.2f) moveX += pad.leftStick.ReadValue().x;

            switch (_phase)
            {
                case Phase.Cover: TickCover(); break;
                case Phase.CoverWait: if (press) BeginEnter(); break;
                case Phase.Enter: TickEnter(); break;
                case Phase.Room: TickRoom(press, moveX, dt); break;
                case Phase.Exit: if (Mathf.Approximately(_mazeAlpha, 1f)) Finish(); break;
            }
            _typingPrev = Typewriter.AnyTyping;
        }

        /// <summary>
        /// 플래그의 회상을 재생할 수 있는지 확인한다.
        /// flag(대본 플래그)와 roomFlag(방 플래그)를 사용하며, 재생기가 켜져 있고 회상 방과 회상 대본이 모두 있으면 true를 반환한다.
        /// </summary>
        public bool CanPlay(string flag, string roomFlag)
        {
            // 수정(016): 재생기가 꺼져 있어도 회상을 시작해 입력이 잠기던 것을 글 표시로 넘어가게 바꿨다.
            return isActiveAndEnabled && FindRoom(roomFlag) != null && FlashbackScript.Find(flag) != null;
        }

        /// <summary>
        /// 회상을 시작한다. 미로를 가라앉히고 만남 UI를 숨기며 roomFlag 방의 장면 그림을 보여 주기 시작한다.
        /// flag(대본 플래그), roomFlag(방 플래그), onFinished(끝났을 때 부를 콜백)를 사용하며, IsPlaying과 재생 상태, 카메라 복귀 값을 바꾼다.
        /// </summary>
        public void Play(string flag, string roomFlag, Action onFinished)
        {
            if (!Application.isPlaying || IsPlaying) return;
            _room = FindRoom(roomFlag);
            _script = FlashbackScript.Find(flag);
            if (_room == null || _script == null) return;

            IsPlaying = true;
            _flag = flag;
            _roomFlag = roomFlag;
            _onFinished = onFinished;
            _cameraPosBefore = _camera.transform.position;
            _cameraSizeBefore = _camera.orthographicSize;
            _encounterUi.SetActive(false);
            _mazeTarget = _mazeDimAlpha;
            _stepIndex = 0;
            _lines = null;
            _textBox.SetActive(false);
            _hintText.text = "";
            _phase = Phase.Cover;
            _phaseT = 0f;
            // 그림이 없는 방은 켜진 채 저장된 그림 칸도 숨긴다.
            if (_room.Cover == null) { _coverImage.gameObject.SetActive(false); return; }
            _coverImage.sprite = _room.Cover;
            _coverScale = _coverImage.rectTransform.localScale;
            SetCoverAlpha(0f);
            _coverImage.gameObject.SetActive(true);
        }

        /// <summary>
        /// 장면 그림을 _coverFadeSeconds초에 걸쳐 보여 준다. 그림이 없는 방은 미로가 가라앉은 뒤 바로 방으로 들어간다.
        /// _phaseT, _mazeAlpha를 사용하며, 장면 그림 투명도와 재생 단계를 바꾼다.
        /// </summary>
        private void TickCover()
        {
            if (_room.Cover == null)
            {
                if (Mathf.Approximately(_mazeAlpha, _mazeTarget)) { EnterRoom(); StartRoom(); }
                return;
            }
            float a = Mathf.Clamp01(_phaseT / Mathf.Max(0.01f, _coverFadeSeconds));
            SetCoverAlpha(a);
            if (a >= 1f) _phase = Phase.CoverWait;
        }

        /// <summary>
        /// 장면 그림에서 방으로 들어가기 시작한다. 그림 뒤에서 방을 켜고 카메라와 내담자를 옮긴다.
        /// 입력값은 없으며, 재생 단계와 경과 시간, 방과 카메라 상태를 바꾼다.
        /// </summary>
        private void BeginEnter()
        {
            EnterRoom();
            AimCoverAtFrame();
            // 스펙 016: 방을 조금 멀리서 시작해 그림이 줄어드는 동안 다가오게 한다.
            _camera.orthographicSize = _room.CameraSize * _enterCameraZoom;
            _phase = Phase.Enter;
            _phaseT = 0f;
        }

        /// <summary>
        /// 스펙 016: 장면 그림이 방 테두리를 가로와 세로 모두 덮도록 줄어들 배율과 기준점을 정한다. 그림 비율을 지키며 테두리를 덮는 가장 작은 크기를 고른다.
        /// _room.Frame, _camera, _cameraView와 장면 그림의 화면 영역을 사용하며, _coverFit과 장면 그림의 기준점을 바꾼다.
        /// </summary>
        private void AimCoverAtFrame()
        {
            RectTransform cover = _coverImage.rectTransform;
            Rect area = cover.rect;
            Rect frame = _room.Frame.Bounds;
            Vector2 a = Rect.PointToNormalized(area, cover.InverseTransformPoint(WorldToCanvas(frame.min)));
            Vector2 b = Rect.PointToNormalized(area, cover.InverseTransformPoint(WorldToCanvas(frame.max)));
            // 비율을 지키는 그림은 영역 안에서 한쪽 축만 꽉 찬다. 그 크기를 기준으로 테두리에 맞춘다.
            float areaAspect = area.width / Mathf.Max(1f, area.height);
            float imageAspect = _room.Cover.rect.width / Mathf.Max(1f, _room.Cover.rect.height);
            Vector2 image = areaAspect > imageAspect ? new Vector2(imageAspect / areaAspect, 1f) : new Vector2(1f, areaAspect / imageAspect);
            // 수정(016): 테두리 안에 맞추던 크기(작은 쪽)를 테두리를 덮는 크기(큰 쪽)로 바꾼다. 다 줄어들 때쯤엔 거의 투명해 넘치는 부분이 드러나지 않는다.
            _coverFit = Mathf.Clamp(Mathf.Max((b.x - a.x) / image.x, (b.y - a.y) / image.y), 0.05f, 0.99f);
            Vector2 center = (a + b) * 0.5f;
            // 기준점을 중심으로 줄어들 때 그림 가운데가 테두리 가운데로 가도록 기준점을 구한다.
            cover.pivot = (center - Vector2.one * (0.5f * _coverFit)) / (1f - _coverFit);
        }

        /// <summary>
        /// 스펙 016: 월드 위치를 카메라 화면을 띄운 _cameraView 위의 화면 위치로 바꾼다.
        /// world, _camera, _cameraView를 사용하며, 화면 공간의 위치를 반환한다.
        /// </summary>
        private Vector3 WorldToCanvas(Vector3 world)
        {
            Vector3 viewport = _camera.WorldToViewportPoint(world);
            Rect r = _cameraView.rect;
            return _cameraView.TransformPoint(new Vector3(r.x + viewport.x * r.width, r.y + viewport.y * r.height, 0f));
        }

        /// <summary>
        /// _enterSeconds초에 걸쳐 장면 그림을 _enterCurve로 방 테두리 크기까지 줄이고, _enterFadeStart 지점부터 부드럽게 흐려지게 한다.
        /// 그동안 카메라는 _enterCameraZoom 배율에서 방 크기로 다가오다 천천히 멈춘다. 끝나면 방 안 조작을 시작한다.
        /// _phaseT, _enterSeconds, _enterFadeStart, _enterCurve, _enterCameraZoom, _coverFit을 사용하며, 장면 그림의 크기, 투명도, 카메라 크기, 재생 단계를 바꾼다.
        /// </summary>
        private void TickEnter()
        {
            float t = Mathf.Clamp01(_phaseT / Mathf.Max(0.01f, _enterSeconds));
            // 수정(016): 줄어듦은 곡선으로, 흐려짐은 부드러운 단계로, 카메라는 끝에서 천천히 멈추게 해 세 움직임이 서로 다른 박자로 겹친다.
            float shrink = _enterCurve.Evaluate(t);
            float fade = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(_enterFadeStart, 1f, t));
            float approach = 1f - (1f - t) * (1f - t) * (1f - t);
            RectTransform cover = _coverImage.rectTransform;
            cover.localScale = _coverScale * Mathf.Lerp(1f, _coverFit, shrink);
            SetCoverAlpha(1f - fade);
            _camera.orthographicSize = _room.CameraSize * Mathf.Lerp(_enterCameraZoom, 1f, approach);
            if (t < 1f) return;
            _coverImage.gameObject.SetActive(false);
            cover.localScale = _coverScale;
            _camera.orthographicSize = _room.CameraSize;
            StartRoom();
        }

        /// <summary>
        /// 회상 방을 켜고 추가 사물 묶음을 맞춘 뒤, 카메라를 방의 기준점과 크기로 옮기고 내담자를 시작 위치에 둔다.
        /// _room, _flag, _roomFlag를 사용하며, 방 활성 상태, _props, 카메라 위치와 크기, 내담자 위치를 바꾼다.
        /// </summary>
        private void EnterRoom()
        {
            _room.gameObject.SetActive(true);
            _room.ApplyExtraSets(_flag, _roomFlag);
            _props = _room.ActiveProps();
            Vector3 cameraPoint = _room.CameraPoint.position;
            _camera.transform.position = new Vector3(cameraPoint.x, cameraPoint.y, _camera.transform.position.z);
            _camera.orthographicSize = _room.CameraSize;
            FieldPlayer actor = _room.Actor;
            Vector3 start = _room.StartPoint.position;
            actor.transform.position = new Vector3(start.x, start.y, actor.transform.position.z);
            actor.Stop();
        }

        /// <summary>
        /// 방 안 조작을 시작한다. 도입 줄이 있으면 먼저 보여 주고, 단계가 하나도 없으면 회상을 끝낸다.
        /// _script를 사용하며, 재생 단계와 글 칸 상태를 바꾼다.
        /// </summary>
        private void StartRoom()
        {
            _phase = Phase.Room;
            _phaseT = 0f;
            if (_script.Intro.Count > 0) OpenLines(_script.Intro, LineKind.Intro);
            else if (_script.Steps.Count == 0) BeginExit();
        }

        /// <summary>
        /// 방 안의 한 프레임을 처리한다. 글 칸이 열려 있으면 다 나온 줄에서 진행 키로 넘기고,
        /// 닫혀 있으면 내담자를 좌우로 움직이고 가까운 사물 이름을 보여 주며 상호작용을 받는다.
        /// press(진행/상호작용 키), moveX(좌우 입력), dt를 사용하며, 글 칸, 내담자 위치, 안내 글, 단계를 바꾼다.
        /// </summary>
        private void TickRoom(bool press, float moveX, float dt)
        {
            if (_lines != null)
            {
                // 글자가 찍히는 중의 진행 키는 GameFlow가 줄 전체를 보여 주는 데 쓴다.
                if (press && !Typewriter.AnyTyping && !_typingPrev) NextLine();
                return;
            }
            FieldPlayer actor = _room.Actor;
            actor.Steer(new Vector2(moveX, 0f), dt);
            FlashbackProp near = FindNearest();
            // 수정(016): 사물 이름만 보이던 안내에 누를 키를 함께 보인다.
            string hint = near != null ? string.Format(_hintFormat, near.HintText) : "";
            if (_hintText.text != hint) _hintText.text = hint;
            // 스펙 016: 안내 글을 화면 아래 고정 위치 대신 가까운 사물 위에 띄운다.
            if (near != null) PlaceHint(near.HintPoint);
            if (press && near != null) Interact(near);
        }

        /// <summary>
        /// 내담자의 상호작용 범위 안에서 가장 가까운 사물을 찾는다. 거리가 같으면 지금 단계의 사물을, 그것도 아니면 목록에서 앞선 사물을 고른다.
        /// _props, 내담자 위치, 지금 단계를 사용하며, 찾은 사물 또는 null을 반환한다.
        /// </summary>
        private FlashbackProp FindNearest()
        {
            FieldPlayer actor = _room.Actor;
            FlashbackScript.Step step = CurrentStep;
            string current = step != null ? step.Prop : null;
            FlashbackProp best = null;
            float bestD = float.MaxValue;
            foreach (FlashbackProp p in _props)
            {
                float d = p.DistanceTo(actor);
                if (d > p.InteractRange) continue;
                bool closer = best == null || (d < bestD && !Mathf.Approximately(d, bestD));
                bool tieToCurrent = best != null && Mathf.Approximately(d, bestD) && p.Id == current && best.Id != current;
                if (!closer && !tieToCurrent) continue;
                best = p;
                bestD = d;
            }
            return best;
        }

        /// <summary>
        /// 사물에 상호작용한다. 지금 단계의 사물이면 그 단계의 줄을, 아니면 살펴보기 글 한 줄을 보여 준다.
        /// prop과 지금 단계를 사용하며, 글 칸 상태를 바꾼다. 살펴보기 글이 비어 있으면 아무것도 하지 않는다.
        /// </summary>
        private void Interact(FlashbackProp prop)
        {
            FlashbackScript.Step step = CurrentStep;
            if (step != null && prop.Id == step.Prop)
            {
                OpenLines(step.Lines, LineKind.Step);
                return;
            }
            // 스펙 016 FR-009: 이미 쓴 사물은 쓴 뒤의 글을 보여 앞선 단계와 어긋나지 않게 한다
            string lookText = WasUsed(prop) && !string.IsNullOrEmpty(prop.LookTextAfter) ? prop.LookTextAfter : prop.LookText;
            if (string.IsNullOrEmpty(lookText)) return;
            var look = new List<FlashbackScript.Line> { new FlashbackScript.Line { Speaker = "", Text = lookText } };
            OpenLines(look, LineKind.Look);
        }

        /// <summary>
        /// 스펙 016 FR-009: 이 사물을 쓰는 단계를 이미 한 번이라도 지났는지 판단한다.
        /// prop의 id와 _script의 단계, _stepIndex를 사용하며, 지났으면 true를 반환한다.
        /// </summary>
        private bool WasUsed(FlashbackProp prop)
        {
            for (int i = 0; i < _stepIndex && i < _script.Steps.Count; i++)
                if (_script.Steps[i].Prop == prop.Id) return true;
            return false;
        }

        /// <summary>
        /// 글 칸을 열고 줄 묶음의 첫 줄을 보여 준다. 내담자는 멈추고 안내 글은 지운다.
        /// lines와 kind를 사용하며, _lines, _lineKind, _lineIndex와 글 칸, 안내 글, 내담자 속도를 바꾼다.
        /// </summary>
        private void OpenLines(List<FlashbackScript.Line> lines, LineKind kind)
        {
            _lines = lines;
            _lineKind = kind;
            _lineIndex = 0;
            _textBox.SetActive(true);
            _hintText.text = "";
            _room.Actor.Stop();
            ShowLine();
        }

        /// <summary>
        /// 지금 줄을 글 칸에 쓴다. 화자가 있으면 이름을 함께 보여 주고, 대본 줄이면 대화 기록에 남긴다.
        /// _lines, _lineIndex, _lineKind를 사용하며, 화자 글과 본문 글, DialogueLog를 바꾼다.
        /// </summary>
        private void ShowLine()
        {
            FlashbackScript.Line line = _lines[_lineIndex];
            _speakerText.text = line.Speaker;
            // 앞줄과 글이 같아도 처음부터 다시 찍히도록 비운 뒤 쓴다.
            Typewriter.SetText(_bodyText, "");
            Typewriter.SetText(_bodyText, line.Text);
            if (_lineKind == LineKind.Look) return;
            Sprite portrait = string.IsNullOrEmpty(line.Speaker) ? null : FieldNPC.PortraitOf(line.Speaker);
            DialogueLog.Add(line.Speaker, line.Text, false, portrait);
        }

        /// <summary>
        /// 다음 줄로 넘긴다. 묶음의 마지막 줄이었으면 글 칸을 닫고, 단계 줄이었으면 다음 단계로 넘어가며, 남은 단계가 없으면 회상을 끝낸다.
        /// _lines, _lineIndex, _lineKind, _stepIndex를 사용하며, 글 칸과 단계 번호, 재생 단계를 바꾼다.
        /// </summary>
        private void NextLine()
        {
            _lineIndex++;
            if (_lineIndex < _lines.Count) { ShowLine(); return; }
            LineKind kind = _lineKind;
            CloseLines();
            if (kind == LineKind.Look) return;
            if (kind == LineKind.Step) _stepIndex++;
            if (_stepIndex >= _script.Steps.Count) BeginExit();
        }

        /// <summary>
        /// 글 칸을 닫는다.
        /// 입력값은 없으며, _lines와 글 칸 활성 상태, 화자 글과 본문 글을 바꾼다.
        /// </summary>
        private void CloseLines()
        {
            _lines = null;
            _textBox.SetActive(false);
            _speakerText.text = "";
            Typewriter.SetText(_bodyText, "");
        }

        /// <summary>
        /// 회상 방을 닫는다. 방을 끄고 카메라를 회상 전 위치와 크기로 되돌리고 만남 UI를 다시 켜며, 미로를 되돌리기 시작한다.
        /// _cameraPosBefore, _cameraSizeBefore를 사용하며, 방과 만남 UI 활성 상태, 카메라, _mazeTarget, 재생 단계를 바꾼다.
        /// </summary>
        private void BeginExit()
        {
            if (_lines != null) CloseLines();
            _hintText.text = "";
            _room.Actor.Stop();
            _room.gameObject.SetActive(false);
            _camera.transform.position = _cameraPosBefore;
            _camera.orthographicSize = _cameraSizeBefore;
            _encounterUi.SetActive(true);
            _mazeTarget = 1f;
            _phase = Phase.Exit;
            _phaseT = 0f;
        }

        /// <summary>
        /// 미로가 다 되돌아온 뒤 재생을 끝내고 완료 콜백을 부른다.
        /// _onFinished를 사용하며, IsPlaying과 재생 상태를 바꾼다.
        /// </summary>
        private void Finish()
        {
            IsPlaying = false;
            _phase = Phase.Idle;
            _room = null;
            _script = null;
            Action done = _onFinished;
            _onFinished = null;
            done?.Invoke();
        }

        /// <summary>
        /// 스펙 016: 안내 글을 월드 위치가 화면에 보이는 자리로 옮긴다.
        /// worldPoint, _camera, _cameraView를 사용하며, _hintText의 위치를 바꾼다.
        /// </summary>
        private void PlaceHint(Vector3 worldPoint)
        {
            // 카메라는 픽셀 텍스처에 그리고 _cameraView가 그것을 화면에 띄우므로 뷰포트 비율로 옮긴다.
            _hintText.rectTransform.position = WorldToCanvas(worldPoint);
        }

        /// <summary>
        /// 장면 그림의 투명도를 바꾼다.
        /// alpha를 사용하며, _coverImage의 색 알파를 바꾼다.
        /// </summary>
        private void SetCoverAlpha(float alpha)
        {
            Color c = _coverImage.color;
            c.a = alpha;
            _coverImage.color = c;
        }

        /// <summary>
        /// 플래그가 같은 회상 방을 찾는다.
        /// roomFlag와 _rooms를 사용하며, 찾은 방 또는 null을 반환한다.
        /// </summary>
        private FlashbackRoom FindRoom(string roomFlag)
        {
            if (string.IsNullOrEmpty(roomFlag)) return null;
            foreach (FlashbackRoom r in _rooms)
                if (r != null && r.Flag == roomFlag) return r;
            return null;
        }
    }
}

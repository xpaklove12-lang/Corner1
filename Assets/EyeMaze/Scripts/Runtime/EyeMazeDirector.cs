using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>
    /// 만남 화면의 흐름: 대화 → 눈 들여다보기(미로 탐색, 기억 파편 모으기) → 알게 된 것으로 대화 → 신뢰와 동공이 커짐 → 더 깊은 층 → 심연.
    /// Play 모드에서는 WASD / 방향키 / 왼쪽 스틱으로 이동하고, Space(패드 A)로 시선을 거두며, 1~4로 대화를 고르고, R로 처음부터 한다.
    /// 대화는 지금 손님의 대화 CSV(ClientCase.DialogueCsv)에서 읽는다.
    /// 에디트 모드에서는 디렉터 인스펙터의 버튼으로 같은 조작을 한다.
    /// </summary>
    [ExecuteAlways]
    public class EyeMazeDirector : MonoBehaviour
    {
        public enum Mode { Talk, Gaze, Ending }

        // 스펙 010: 기본 후보를 F6 자물쇠 맞춤으로 지정
        private const int BLINK_DEFAULT_PRESET_INDEX = 1;

        // 스펙 015: 손님에게 그 감정의 눈 표현이 없을 때 쓰는 기본 표현
        private static readonly EmotionLook DEFAULT_EMOTION_LOOK = new EmotionLook();

        [Header("연결")]
        [SerializeField, FormerlySerializedAs("maze")] private IrisMaze _maze;
        [SerializeField, FormerlySerializedAs("cursor")] private GazeCursor _cursor;
        [SerializeField, FormerlySerializedAs("person")] private Person _person;
        [Tooltip("동공 한가운데의 가장 반짝이는 파편")]
        [SerializeField, FormerlySerializedAs("core")] private CoreFragment _core;
        [Tooltip("키 입력을 받을지. 필드 화면에 있을 땐 GameFlow가 끈다")]
        [SerializeField, FormerlySerializedAs("acceptInput")] private bool _acceptInput = true;
        [Tooltip("플래그 목록 (Data/Flags.csv). condition이 있는 계산 플래그를 여기서 읽는다")]
        [SerializeField] private TextAsset _flagsCsv;
        [Tooltip("공용 문구 표 (Data/Strings.csv). 안내/시스템 문구를 여기서 읽는다")]
        [SerializeField] private TextAsset _stringsCsv;
        // 스펙 016: 회상 대본과 회상 재생기 연결 추가
        [Tooltip("회상 대본 (Data/Flashbacks.csv). Flags.csv와 같은 때에 읽는다")]
        [SerializeField] private TextAsset _flashbacksCsv;
        [Tooltip("파편 회상을 재생하는 회상 화면. 비우면 모든 파편이 글로 나온다")]
        [SerializeField] private FlashbackPlayer _flashback;
        // 스펙 017: 이어지는 쌍 데이터와 실 연결 추가
        [Tooltip("이어지는 기억의 쌍 (Data/Links.csv). Flags.csv와 같은 때에 읽는다. 비우면 쌍이 없는 것으로 본다")]
        [SerializeField] private TextAsset _linksCsv;
        [Tooltip("기억의 자리를 잇는 실 (IrisMaze 아래의 MemoryThread). 비우면 기억 잇기를 쓰지 않는다")]
        [SerializeField] private MemoryThread _thread;

        [Header("상태")]
        [SerializeField, FormerlySerializedAs("mode")] private Mode _mode = Mode.Talk;
        [Tooltip("지금 대화 장면 id (대화 CSV의 node)")]
        [SerializeField] private string _currentNodeId;
        [Tooltip("지나온 장면 id들. '다른 질문을 해볼까요?'를 고르면 하나씩 되돌아간다")]
        [SerializeField] private List<string> _nodeHistory = new List<string>();
        [SerializeField, FormerlySerializedAs("message"), TextArea(2, 5)] private string _message;
        [SerializeField, FormerlySerializedAs("inAbyss")] private bool _inAbyss;

        [Header("이야기")]
        [Tooltip("첫 노드가 아닐 때 맨 마지막에 보이는 공통 선택지. 고르면 첫 노드로 돌아간다")]
        [SerializeField, TextArea(1, 2)] private string _returnText = "다른 이야기를 해볼까요?";
        [Tooltip("공통 선택지를 고른 뒤 보이는 문구")]
        [SerializeField, TextArea(1, 2)] private string _returnMessage = "화제를 돌렸다.";
        [SerializeField, FormerlySerializedAs("introMessage"), TextArea(2, 4)] private string _introMessage = "은재는 석 달째 말을 하지 않았다. 눈을 들여다보면 마음속으로 들어갈 수 있다.";
        [SerializeField, TextArea(2, 4)] private string _firstGuide = "Space를 누르면 은재의 눈을 들여다본다.";
        [SerializeField, TextArea(2, 4)] private string _againGuide = "다시 마주 앉았다. Space를 누르면 눈을 들여다본다.";
        [SerializeField, FormerlySerializedAs("idleSituation"), TextArea(2, 4)] private string _idleSituation = "은재가 조용히 나를 바라보고 있다.";
        [SerializeField, FormerlySerializedAs("endingText"), TextArea(3, 6)] private string _endingText;
        [Tooltip("미로를 지나 동공(심연)에 들어섰을 때")]
        [SerializeField, FormerlySerializedAs("abyssMessage"), TextArea(1, 3)] private string _abyssMessage = "그녀의 마음 속 심연에 들어왔다.\n가장 깊은 곳에서 무언가가 반짝인다.";

        [Header("결말")]
        [Tooltip("이 손님의 결말들. 파편에 닿거나, 신뢰가 바닥나거나, 특정 선택지를 고르면 끝난다")]
        [SerializeField, FormerlySerializedAs("endings")] private List<EndingDef> _endings = new List<EndingDef>();
        [Tooltip("파편에 닿았을 때 이 신뢰 이상 + 열쇠 파편을 모았으면 truth, 아니면 half 엔딩")]
        [SerializeField, FormerlySerializedAs("truthTrust")] private int _truthTrust = 3;
        [Tooltip("진짜 결말에 꼭 필요한 기억 파편 (없으면 신뢰만 봄). 케이스를 불러오면 회차 CSV의 key_flag로 찾은 파편으로 바뀐다")]
        [SerializeField, FormerlySerializedAs("keyFragment")] private MemoryFragment _keyFragment;
        [Tooltip("신뢰가 이 값 이하로 떨어지면 leave 엔딩 (그 사람이 떠남)")]
        [SerializeField, FormerlySerializedAs("leaveTrust")] private int _leaveTrust = -2;
        [Tooltip("부담감이 이 값(0~1)에 닿으면 leave 엔딩. 케이스를 불러오면 회차 CSV의 burden_limit으로 바뀐다")]
        [SerializeField, Range(0.01f, 1f)] private float _burdenLimit = 1f;
        [Tooltip("지금 진행 중인 엔딩 id")]
        [SerializeField, FormerlySerializedAs("currentEnding")] private string _currentEnding;
        // 스펙 017: 진엔딩에 그 인물의 모든 쌍 잇기를 요구하는 옵션 추가
        [Tooltip("켜면 신뢰와 열쇠 파편 조건을 채워도 이 인물의 Links.csv 쌍을 모두 잇지 않았으면 half 엔딩이 된다")]
        [SerializeField] private bool _truthNeedsAllLinks = true;
        // 케이스의 회차 CSV에서 읽은 열쇠 파편 Flag ID (비면 파편 조건 없음)
        private string _keyFlag = "";

        [Header("상담 케이스")]
        [Tooltip("지금 눈 속에 불러온 손님")]
        [SerializeField, FormerlySerializedAs("activeCase")] private ClientCase _activeCase;

        [Header("이동")]
        [Tooltip("초당 이동 거리")]
        [SerializeField, FormerlySerializedAs("moveSpeed")] private float _moveSpeed = 1.6f;
        [Tooltip("클수록 즉각적으로 출발/정지")]
        [SerializeField, FormerlySerializedAs("acceleration")] private float _acceleration = 10f;
        [Tooltip("벽과의 최소 간격")]
        [SerializeField, FormerlySerializedAs("wallMargin")] private float _wallMargin = 0.07f;
        [Tooltip("에디트 모드에서 버튼 한 번에 움직이는 거리")]
        [SerializeField, FormerlySerializedAs("editNudge")] private float _editNudge = 0.2f;

        [Header("눈 깜빡임")]
        [Tooltip("들여다보는 동안 몇 초마다 눈을 깜빡이는지 (0이면 깜빡이지 않음)")]
        // 스펙 001: 깜빡임 주기 기본값을 8초로 조정
        [SerializeField] private float _blinkInterval = 8f;
        // 스펙 009: 깜빡임 시간과 회전 방식을 비교할 수 있는 연출 후보 목록으로 교체
        [Tooltip("깜빡임 연출 후보 목록. 쓸 후보는 아래 번호로 고른다")]
        [SerializeField] private BlinkTurnPreset[] _blinkPresets = CreateDefaultBlinkPresets();
        // 스펙 010: 자동 깜빡임 기본값을 F6 후보로 지정
        [Tooltip("시간 경과 깜빡임에 쓸 후보 번호 (0부터). 1은 F6 후보")]
        [SerializeField] private int _blinkPresetIndex = BLINK_DEFAULT_PRESET_INDEX;
        // 스펙 010: 벽 변화 연출 설정 추가
        [SerializeField] private MazeWallChangeEffect _wallChange = new MazeWallChangeEffect();
        [SerializeField, FormerlySerializedAs("blinkAmount"), Range(0f, 1f)] private float _blinkAmount;
        // 스펙 006: 회전량을 칸 단위로 바꾸고 누적 각도 제한과 뜬 직후 유예 추가
        [Tooltip("깜빡임 한 번에 미로가 도는 최소 칸 수")]
        [SerializeField] private int _blinkTurnMinSectors = 1;
        [Tooltip("깜빡임 한 번에 미로가 도는 최대 칸 수")]
        [SerializeField] private int _blinkTurnMaxSectors = 2;
        [Tooltip("처음 방향에서 벗어날 수 있는 최대 각도(도). 넘게 되면 반대로 돈다")]
        [SerializeField] private float _blinkTurnLimitDeg = 45f;
        [Tooltip("눈을 뜬 직후 이동 입력을 받지 않는 시간(초)")]
        [SerializeField] private float _blinkRecoverSeconds = 0.2f;

        [Header("파편 읽은 뒤")]
        [Tooltip("파편을 다 읽고 나면 지금 층을 뺀 나머지 층의 벽을 무작위로 다시 만든다 (멈춰 있는 동안 길을 외우지 못하게)")]
        [SerializeField, FormerlySerializedAs("respawnMazeAfterReading")] private bool _respawnMazeAfterReading = true;
        // 스펙 016: 회상을 본 파편 자리의 표시 색 추가
        [Tooltip("회상을 본 파편 자리(기억의 자리)에 남는 표시 색")]
        [SerializeField] private Color _seenMarkColor = new Color(0.6f, 0.95f, 1f, 0.45f);
        // 스펙 017: 이은 뒤의 기억의 자리 색 추가
        [Tooltip("다른 기억과 이은 뒤의 기억의 자리 표시 색")]
        [SerializeField] private Color _linkedMarkColor = Color.white;

        [Header("부담감")]
        [Tooltip("지금 부담감 (0~1). 들여다보는 동안 차오르고, 가득 차면 leave 엔딩이 된다")]
        [SerializeField, Range(0f, 1f)] private float _burden;
        [Tooltip("신뢰 0일 때 0에서 가득까지 걸리는 들여다보기 시간(초)")]
        [SerializeField] private float _burdenFillSeconds = 85.8f;
        [Tooltip("신뢰 1당 늘어나는 시간(초)")]
        [SerializeField] private float _burdenSecondsPerTrust = 17.2f;
        [Tooltip("좋은 말을 처음 했을 때 줄어드는 양")]
        [SerializeField, Range(0f, 1f)] private float _burdenRelief = 0.25f;
        // 스펙 005: 안 좋은 말을 했을 때 늘어나는 양 추가
        [Tooltip("안 좋은 말을 했을 때 늘어나는 양")]
        [SerializeField, Range(0f, 1f)] private float _burdenWrongChoice = 0.1f;
        [Tooltip("가득 찼을 때 엔딩 앞에 보이는 한 줄. '은재'는 지금 손님 이름으로 바뀐다")]
        [SerializeField, TextArea(1, 3)] private string _burdenEndLine = "은재가 시선을 피했다. 더는 마주 보기 힘든 것 같다.";
        // 스펙 011: 휴식 신호를 보고 쉬어 주었을 때의 감소량, 문구, 눈 연출 추가
        [Tooltip("휴식 신호를 보고 쉬어 준 뒤 돌아왔을 때 줄어드는 양")]
        [SerializeField, Range(0f, 1f)] private float _burdenRestRelief = 0.4f;
        [Tooltip("쉬어 주고 돌아왔을 때 보이는 문구. '은재'는 지금 손님 이름으로 바뀐다")]
        [SerializeField, TextArea(1, 3)] private string _restReturnMessage = "은재의 얼굴이 조금 편해졌다.\n(부담이 줄었다.)";
        [SerializeField] private ClientTearCue _tearCue = new ClientTearCue();

        // 스펙 007: 벽 부딪힘 밀려남 설정
        [Header("벽 부딪힘")]
        // 스펙 007: 부딪힘 부담감 증가를 툴팁에서 뺌
        [Tooltip("켜면 벽이나 잠긴 문에 세게 부딪혔을 때 밀려남, 지지직 연출, 번쩍임이 일어난다. 끄면 기존처럼 멈추거나 미끄러지기만 한다")]
        [SerializeField] private bool _bumpEnabled;
        [Tooltip("반응하는 최소 벽 쪽 속도 (초당 거리). 이보다 느리게 닿거나 벽을 따라 미끄러지면 반응하지 않는다")]
        [SerializeField] private float _bumpMinSpeed = 0.8f;
        [Tooltip("벽에서 밀려나는 거리. 통로 폭(약 0.21)의 절반 이하를 권장한다")]
        [SerializeField] private float _knockbackDistance = 0.09f;
        [Tooltip("밀려나는 데 걸리는 시간(초). 이 동안 이동 입력을 받지 않는다")]
        [SerializeField] private float _knockbackSeconds = 0.18f;
        [Tooltip("한 번 부딪힌 뒤 다시 부딪힘으로 치기까지 기다리는 시간(초)")]
        [SerializeField] private float _bumpCooldown = 0.45f;
        // 스펙 007: 부딪힘마다 오르던 부담감을 잠긴 문 반복 접촉 설정으로 바꿈
        [Tooltip("부담감이 오르는 잠긴 문 접촉 횟수")]
        [SerializeField] private int _gatePushCount = 3;
        [Tooltip("접촉 횟수를 세는 제한 시간(초). 첫 접촉 뒤 이 시간이 지나면 다시 센다")]
        [SerializeField] private float _gatePushWindow = 10f;
        [Tooltip("새 접촉으로 세기 위해 문에서 떨어져 있어야 하는 시간(초)")]
        [SerializeField] private float _gatePushGap = 0.4f;
        [Tooltip("횟수에 닿았을 때 오르는 부담감 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _gatePushBurden = 0.1f;
        [Tooltip("부딪혔을 때 미로가 지지직 흐려지는 시간(초)")]
        [SerializeField] private float _glitchSeconds = 0.3f;
        [Tooltip("미로 초점이 가장 크게 떨어지는 양. 0이면 지지직 연출이 없다")]
        [SerializeField, Range(0f, 1f)] private float _glitchStrength = 0.6f;
        [Tooltip("초당 초점이 튀는 횟수")]
        [SerializeField] private float _glitchRate = 20f;

        // 스펙 015: 감정 상태와 감정 전환 연출 설정 추가
        [Header("감정")]
        [Tooltip("지금 손님의 감정. 상담을 시작하면 손님의 시작 감정이 되고, 선택지의 emotion 값으로 바뀐다")]
        [SerializeField] private ClientEmotion _emotion = ClientEmotion.Calm;
        [Tooltip("눈 표현이 바뀌는 시간(초)")]
        [SerializeField] private float _emotionEyeSeconds = 1f;
        [Tooltip("눈이 변하기 시작한 뒤 미로 사물이 바뀌기까지의 시간(초)")]
        [SerializeField] private float _emotionMazeDelay = 1f;
        [Tooltip("사물이 나타나고 사라지는 시간(초)")]
        [SerializeField] private float _propFadeSeconds = 0.6f;
        [Tooltip("사물이 바뀐 뒤 미로의 흐려짐과 무관하게 선명하게 보이는 시간(초)")]
        [SerializeField] private float _propRevealSeconds = 1.5f;
        [Tooltip("시선 피함으로 동공이 비켜날 수 있는 최대 거리")]
        [SerializeField] private float _gazeShiftMax = 0.05f;

        [Header("연출")]
        [SerializeField, FormerlySerializedAs("talkWallAlpha"), Range(0f, 1f)] private float _talkWallAlpha = 0.22f;
        [SerializeField, FormerlySerializedAs("startRing")] private int _startRing;
        [SerializeField, FormerlySerializedAs("startSector")] private int _startSector = 4;
        [SerializeField, FormerlySerializedAs("bumpFlash")] private float _bumpFlash;
        [Tooltip("시선(플레이어) 색. 흑백 화면에서 눈에 띄도록 붉은색")]
        [SerializeField, FormerlySerializedAs("cursorColor")] private Color _cursorColor = new Color(1f, 0.25f, 0.25f, 1f);
        [Tooltip("벽에 부딪혔을 때 번쩍이는 색")]
        [SerializeField, FormerlySerializedAs("bumpColor")] private Color _bumpColor = Color.white;

        [NonSerialized] private Vector2 _velocity;
        [NonSerialized] private float _blinkPhase = -1f;
        [NonSerialized] private float _blinkElapsed;
        // 스펙 006: 미로 누적 회전각과 이번 깜빡임의 회전량 추가
        [NonSerialized] private float _mazeTurnDeg;
        [NonSerialized] private float _blinkTurnFrom;
        [NonSerialized] private float _blinkTurnDelta;
        // 스펙 009: 화면 회전 진행 상태와 재생 중인 후보, 층 목록 추가
        [NonSerialized] private bool _blinkTurning;
        [NonSerialized] private MazeLayer[] _blinkLayers = Array.Empty<MazeLayer>();
        [NonSerialized] private BlinkTurnPreset _activePreset;
        [NonSerialized] private float _blinkOpenFrom;
        // 스펙 006: 눈을 뜬 직후 입력 유예 시간 추가
        [NonSerialized] private float _blinkRecoverT;
        // 스펙 006: 눈꺼풀 값을 전달할 눈 그림 참조 추가
        [NonSerialized] private EyeVisual _eye;
        // 스펙 007: 이번 이동에서 막힌 축과 방향(+1 바깥쪽/반시계, -1 안쪽/시계, 0 막히지 않음)
        [NonSerialized] private int _hitRadial;
        [NonSerialized] private int _hitTangent;
        // 스펙 007: 밀려나는 방향(x 바깥쪽, y 반시계), 경과 시간(음수면 밀려나지 않는 중), 남은 대기 시간
        [NonSerialized] private Vector2 _knockbackDir;
        [NonSerialized] private float _knockbackT = -1f;
        [NonSerialized] private float _bumpCooldownT;
        // 스펙 007: 잠긴 문 접촉 횟수, 첫 접촉 뒤 지난 시간, 마지막으로 막힌 뒤 지난 시간
        [NonSerialized] private int _gatePushes;
        [NonSerialized] private float _gatePushWindowT;
        [NonSerialized] private float _gateIdleT;
        // 스펙 007: 초점 요동 중인지, 남은 시간, 요동 전 초점, 다음 무작위 값까지 남은 시간, 지금 무작위 값
        [NonSerialized] private bool _glitching;
        [NonSerialized] private float _glitchT;
        [NonSerialized] private float _glitchBaseFocus = 1f;
        [NonSerialized] private float _glitchSampleT;
        [NonSerialized] private float _glitchNoise;
        [NonSerialized] private int _lastLayer = -1;
        [NonSerialized] private int _heartIndex;
        [NonSerialized] private List<string> _endingLines;
        [NonSerialized] private readonly HashSet<string> _usedOptions = new HashSet<string>();
        [NonSerialized] private bool _reading;
        // 스펙 010 FR-017: 파편을 읽은 뒤의 벽 변화 연출 경과 시간(음수면 재생 중 아님)
        [NonSerialized] private float _respawnEffectT = -1f;
        // 스펙 019: 파편을 줍기 전에 잠겨 있던 선택지와 해금 안내 상태
        [NonSerialized] private readonly List<(DialogueScript.Node node, int index)> _lockedChoices = new List<(DialogueScript.Node node, int index)>();
        [NonSerialized] private bool _newTalkReady;
        [NonSerialized] private float _readT;
        [NonSerialized] private bool _typingPrev;
        [NonSerialized] private bool _acceptedLastFrame;
        [NonSerialized] private readonly List<ChoiceView> _choices = new List<ChoiceView>();
        // 스펙 014 FR-033: 여러 줄 결과를 한 줄씩 넘기는 상태
        [NonSerialized] private readonly List<string> _responseLines = new List<string>();
        [NonSerialized] private int _responseIndex;
        [NonSerialized] private string _pendingGoto;
        [NonSerialized] private int _responseDoneFrame = -1;
        // 스펙 004: 화면에 보이는 선택지의 CSV 순번 목록 추가
        [NonSerialized] private readonly List<int> _visibleChoiceIndices = new List<int>();
        // 스펙 011: 휴식 신호, 대사 표시, 승낙 상태 추가
        [NonSerialized] private bool _restRequested;
        [NonSerialized] private bool _restAccepted;
        [NonSerialized] private string _restLine;
        [NonSerialized] private Pupil _pupil;
        // 스펙 007: 마지막으로 상황 문구를 대화 기록에 남긴 장면 추가
        [NonSerialized] private DialogueScript.Node _loggedNode;
        // 스펙 015: 눈 표현 전환의 시작, 목표, 지금 값과 경과 시간(음수면 전환 중 아님)
        [NonSerialized] private readonly EmotionLook _lookFrom = new EmotionLook();
        [NonSerialized] private readonly EmotionLook _lookTo = new EmotionLook();
        [NonSerialized] private readonly EmotionLook _lookNow = new EmotionLook();
        [NonSerialized] private float _lookT = -1f;
        // 스펙 015: 사물 표시를 바꾸기까지 남은 시간(음수면 기다리지 않음), 시선 피함 적용 비율, 지금 손님의 사물 목록
        [NonSerialized] private float _propDelayT = -1f;
        [NonSerialized] private float _gazeShiftWeight = 1f;
        [NonSerialized] private readonly List<MazeProp> _props = new List<MazeProp>();
        // 스펙 015: 어지러움 연출로 흔들고 있는 벽과 이번 프레임에 흔들 벽
        [NonSerialized] private readonly List<MazeWall> _wobbling = new List<MazeWall>();
        [NonSerialized] private readonly Dictionary<MazeWall, (float amp, float speed)> _wobbleNow = new Dictionary<MazeWall, (float amp, float speed)>();
        // 스펙 016: 회상 중인 파편, 시선이 칸을 벗어날 때까지 줍지 않는 새로 나타난 파편
        [NonSerialized] private MemoryFragment _flashbackFragment;
        [NonSerialized] private readonly List<MemoryFragment> _pickupWait = new List<MemoryFragment>();
        // 스펙 017: 시선이 서 있는 기억의 자리, 잇는 중인 두 기억과 쌍, 이은 쌍의 결과 플래그
        [NonSerialized] private MemoryFragment _spotUnderCursor;
        [NonSerialized] private MemoryFragment _linkFrom;
        [NonSerialized] private MemoryFragment _linkTo;
        [NonSerialized] private MemoryLinkScript.Link _linkPair;
        [NonSerialized] private readonly HashSet<string> _linkedResults = new HashSet<string>();

        /// <summary>화면에 보여줄 선택지 하나 (뒤로가기 포함)</summary>
        public struct ChoiceView
        {
            public string Text;
            public bool Available;
            public bool IsBack;
            /// <summary>아직 켜지지 않은 need 플래그들</summary>
            public string Missing;
        }

        public IrisMaze Maze => _maze;
        public GazeCursor Cursor => _cursor;
        public Person Person => _person;
        public Mode CurrentMode => _mode;
        public bool AcceptInput { get => _acceptInput; set => _acceptInput = value; }
        public string Message { get => _message; set => _message = value; }
        public string IdleSituation => _activeCase != null && !string.IsNullOrEmpty(_activeCase.IdleSituation) ? _activeCase.IdleSituation : _idleSituation;
        /// <summary>지금 손님의 결말 목록 (엔딩 CSV가 바뀌면 바로 반영된다)</summary>
        public List<EndingDef> Endings => _activeCase != null ? _activeCase.Endings : _endings;
        private string IntroText => _activeCase != null && !string.IsNullOrEmpty(_activeCase.IntroMessage) ? _activeCase.IntroMessage : _introMessage;
        private string AbyssText => _activeCase != null && !string.IsNullOrEmpty(_activeCase.AbyssMessage) ? _activeCase.AbyssMessage : _abyssMessage;
        public ClientCase ActiveCase => _activeCase;
        // 스펙 006: 부담감 막대를 가리는 데 쓰는 눈꺼풀 닫힘 정도 공개
        public float BlinkAmount => _blinkAmount;
        public string ReturnText => _returnText;
        public float Burden => _burden;
        /// <summary>내담자의 휴식 신호가 나타난 상태인지</summary>
        public bool RestRequested => _restRequested;
        /// <summary>신호 동안 장면 설명 자리에 보여 줄 쉬자는 대사</summary>
        public string RestLine => _restLine;
        /// <summary>쉬어 주기로 해서 상담실로 돌아가는 중이거나 돌아온 상태인지</summary>
        public bool RestAccepted => _restAccepted;
        /// <summary>진실 엔딩에 필요한 신뢰 (지금 손님 기준)</summary>
        public int TruthTrust => _truthTrust;
        /// <summary>이 신뢰 이하가 되면 손님이 떠난다 (지금 손님 기준, 음수)</summary>
        public int LeaveTrust => _leaveTrust;

        /// <summary>대화 중이고 시작 장면이 아니며 4번 칸이 비어 있으면 처음 이야기로 돌아가는 공통 선택지를 고를 수 있다.</summary>
        public bool CanReturnToStart
        {
            get
            {
                // 스펙 004: CSV 장면 기준으로 판단하고 뒤로가기가 4번 칸을 차지하면 숨긴다.
                DialogueScript.Node node = CurrentNode;
                // 스펙 004: 뒤로가기가 있는 장면에는 공통 선택지를 만들지 않음
                return _mode == Mode.Talk && node != null && node != Script.Root && !CanGoBack;
            }
        }

        /// <summary>미로를 지나 동공 안(심연)에 들어와 있는지</summary>
        public bool InAbyss => _mode == Mode.Gaze && _inAbyss;
        /// <summary>스펙 019: 단서를 봐서 선택지가 새로 열렸고 아직 대화로 돌아가지 않았는지</summary>
        public bool NewTalkReady => _newTalkReady;
        /// <summary>스펙 019: 파편 요약 패널을 열 수 있는 상태인지</summary>
        public bool CanOpenRecap => Application.isPlaying && _acceptInput && _mode != Mode.Ending && !_inAbyss && !_reading
            && !IsReadingResponse && !FlashbackPlayer.IsPlaying && _blinkPhase < 0f && !_wallChange.IsPlaying && !DialogueLog.BlocksInput;

        /// <summary>파편을 읽는 중이면 시선이 멈춘다.</summary>
        public bool IsReading => _mode == Mode.Gaze && _reading;

        /// <summary>선택지 결과를 한 줄씩 넘기는 중인지. 이때는 선택지와 들여다보기 전환을 받지 않는다.</summary>
        public bool IsReadingResponse => _mode == Mode.Talk && _responseLines.Count > 0;
        // 마지막 결과 줄을 넘긴 그 프레임의 Space가 들여다보기를 켜지 않게 한다.
        private bool BlockTalkKeys => IsReadingResponse || _responseDoneFrame == Time.frameCount;

        /// <summary>엔딩 대사를 끝까지 봤는지 (그 다음 입력부터 필드로 돌아갈 수 있음)</summary>
        public bool HeartFinished => _mode == Mode.Ending && _heartIndex >= EndingLines.Count - 1;

        public EndingDef CurrentEnding => FindEnding(_currentEnding);
        /// <summary>지금 손님의 대화 (CSV를 읽은 것)</summary>
        public DialogueScript Script => _activeCase != null ? _activeCase.Script : null;

        /// <summary>지금 대화 장면. id가 비었거나 CSV에 없으면 시작 장면</summary>
        public DialogueScript.Node CurrentNode
        {
            get
            {
                DialogueScript s = Script;
                if (s == null) return null;
                return s.Find(_currentNodeId) ?? s.Root;
            }
        }

        /// <summary>지금 장면에서 뒤로가기('다른 질문을 해볼까요?')를 보여줄지</summary>
        public bool CanGoBack
        {
            get
            {
                DialogueScript.Node n = CurrentNode;
                return n != null && n != Script.Root && n.BackLabel != DialogueScript.NoBack;
            }
        }

        /// <summary>지금 케이스의 파편만 (다른 손님의 파편 묶음은 꺼져 있음)</summary>
        public MemoryFragment[] Fragments => _maze != null ? _maze.GetComponentsInChildren<MemoryFragment>(false) : new MemoryFragment[0];

        public bool CanGaze => _mode == Mode.Talk;
        public MazeLayer CursorLayer => _maze != null && _cursor != null ? _maze.GetLayer(_maze.LayerOfRing(_cursor.Ring)) : null;

        private string Name => _person != null ? _person.DisplayName : "그 사람";

        private List<string> EndingLines
        {
            get
            {
                if (_endingLines == null) _endingLines = BuildEndingLines(CurrentEnding, null);
                return _endingLines;
            }
        }

        void OnValidate()
        {
            Strings.Load(_stringsCsv);
            Flags.LoadDefinitions(_flagsCsv);
            // 스펙 016: 회상 대본을 Flags.csv와 같은 때에 읽는다
            FlashbackScript.Load(_flashbacksCsv);
            // 스펙 017: 이어지는 쌍을 Flags.csv와 같은 때에 읽는다
            MemoryLinkScript.Load(_linksCsv);
            EyeHooks.Repaint();
        }

        void Awake() => Strings.Load(_stringsCsv);

        void Start()
        {
            // 씬에 저장된 시선 위치와 무관하게 Play는 시작 칸에서 시작한다.
            if (Application.isPlaying) _cursor.SnapTo(_startRing, _startSector);
            // 스펙 015: 씬에 저장된 상담은 LoadCase를 거치지 않으므로 Play 시작 때 그 상담의 시작 감정으로 맞춘다.
            if (Application.isPlaying) ResetEmotion();
            // 스펙 015: 씬에 저장된 상담으로 시작해도 사물 칸을 피한 미로로 새로 만든다.
            if (Application.isPlaying && _activeCase != null) RebuildMaze(_activeCase, true);
        }

        /// <summary>
        /// 스펙 015: 손님의 막음, 밀어냄 사물 칸(감정과 무관)을 미로에 알리고, generate가 켜져 있으면 그 손님의 시드로 미로를 새로 만든다.
        /// c(손님)와 generate를 사용하며, 미로의 사물 칸 목록과 벽을 바꾼다.
        /// </summary>
        private void RebuildMaze(ClientCase c, bool generate)
        {
            // 스펙 018: 이 손님의 층 문을 여는 플래그를 미로에 알린다.
            _maze.SetGateFlags(c.GateFlags);
            _maze.SetLayoutProps(c.Props());
            if (generate) _maze.Generate(c.MazeSeed);
        }

        void OnEnable()
        {
            Strings.Load(_stringsCsv);
            Flags.Values = FlagValue;
            Flags.LoadDefinitions(_flagsCsv);
            // 스펙 016: 회상 대본을 Flags.csv와 같은 때에 읽는다
            FlashbackScript.Load(_flashbacksCsv);
            // 스펙 017: 이어지는 쌍을 Flags.csv와 같은 때에 읽는다
            MemoryLinkScript.Load(_linksCsv);
        }

        /// <summary>
        /// 계산 플래그의 condition에서 쓰는 수치를 알려준다.
        /// name(trust / dilation / spring)을 사용하며, 지금 상대의 값 또는 null을 반환한다.
        /// </summary>
        private float? FlagValue(string name)
        {
            switch (name)
            {
                case "trust": return _person != null ? _person.Trust : (float?)null;
                case "dilation": return _person != null ? _person.Dilation : (float?)null;
                default: return null;
            }
        }

        void Update()
        {
            if (_maze != null)
            {
                // 스펙 016: 회상 중에는 회상 재생기가 가라앉힌 투명도를 곱한다
                float a = (_mode == Mode.Gaze ? 1f : _talkWallAlpha) * (_flashback != null ? _flashback.MazeAlpha : 1f);
                if (!Mathf.Approximately(_maze.WallAlpha, a)) _maze.WallAlpha = a;
            }
            if (_cursor != null)
            {
                Color c = Color.Lerp(_cursorColor, _bumpColor, _bumpFlash);
                if (_cursor.Color != c) _cursor.Color = c;
            }

            if (!Application.isPlaying)
            {
                if (_blinkAmount != 0f) _blinkAmount = 0f;
                if (_bumpFlash != 0f) _bumpFlash = 0f;
                return;
            }

            // 스펙 007: 만남 중 장면이 바뀌면 그 장면의 상황 문구를 대화 기록에 남긴다.
            if (_acceptInput && _mode != Mode.Ending) LogSituation();

            float dt = Time.deltaTime;
            _bumpFlash = Mathf.MoveTowards(_bumpFlash, 0f, dt * 3f);

            // 스펙 006: 감기, 완전히 감김, 뜨기 순서로 진행하고 눈꺼풀 값을 눈 그림에 전달한다.
            if (_blinkRecoverT > 0f) _blinkRecoverT -= dt;
            if (_blinkPhase >= 0f) AdvanceBlink(dt);
            if (_eye == null && _maze != null) _eye = _maze.GetComponentInParent<EyeVisual>();
            if (_eye != null) _eye.Closed = _blinkAmount;
            // 스펙 011: 휴식 상태인 동안 동공 떨림과 눈물 신호를 보여 준다.
            if (_eye != null && _pupil == null) _pupil = _eye.GetComponentInChildren<Pupil>(true);
            _tearCue.Tick(dt, _mode != Mode.Ending && (_restRequested || _restAccepted), _maze, _eye, _pupil);
            // 스펙 015: 휴식 신호 뒤에 감정 전환을 진행하고 눈 표현을 전달한다.
            TickEmotion(dt);
            // 스펙 015: 시선이 다가오면 휴대폰이 뒤집히고, 피아노 둘레 벽이 흔들린다.
            TickPropReactions();

            // 스펙 007: 벽에 부딪힌 뒤 밀려남, 대기 시간, 초점 요동을 진행한다.
            TickKnockback(dt);
            TickGlitch(dt);
            // 스펙 007: 잠긴 문 반복 접촉의 시간을 잰다.
            TickGatePush(dt);

            // 들여다보는 동안 정해진 시간마다 눈을 깜빡이고, 부담감이 차오른다.
            // 스펙 010 FR-017: 파편을 읽은 뒤의 벽 변화 연출을 진행한다.
            if (_respawnEffectT >= 0f) TickRespawnEffect(dt);
            if (_mode == Mode.Gaze && !_inAbyss && !_reading)
            {
                // 스펙 010 FR-018: 벽 변화 연출 중에는 깜빡임 시간을 세지 않는다.
                if (_respawnEffectT < 0f) TickBlink(dt);
                // 스펙 010: 깜빡임 회전과 벽 변화 연출 중에는 부담감을 채우지 않음
                if (Application.isPlaying && !_blinkTurning && !_wallChange.IsPlaying) TickBurden(dt);
            }
            if (_reading) _readT += dt;

            // 스펙 007: 대화 기록 창이 열려 있으면 입력을 받지 않는다.
            // 스펙 019: 파편 요약 패널이 열려 있어도 입력을 받지 않는다.
            if (!_acceptInput || DialogueLog.BlocksInput || FragmentRecapUI.BlocksInput)
            {
                // 필드 화면에서는 입력을 받지 않는다.
                _velocity = Vector2.zero;
                _typingPrev = Typewriter.AnyTyping;
                _acceptedLastFrame = false;
                return;
            }
            HandleInput(dt);
            _typingPrev = Typewriter.AnyTyping;
            _acceptedLastFrame = true;
        }

        // 케이스

        /// <summary>
        /// 손님 한 명의 데이터(대화/파편/결말/미로)를 불러오고 처음 상태로 되돌린다.
        /// c를 사용하며, 디렉터의 이야기 설정과 파편 묶음, 대화 루트의 활성 상태, 미로를 바꾼다.
        /// </summary>
        public void LoadCase(ClientCase c)
        {
            if (c == null) return;
            _activeCase = c;
            if (_person != null) _person.DisplayName = c.PersonName;
            _endings = c.Endings;
            // 엔딩 판정 기준은 회차 CSV에서 읽는다. 값이 없거나 열쇠 파편을 못 찾으면 알리고, 파편 조건은 충족되지 않은 것으로 본다.
            _truthTrust = c.TruthTrust ?? _truthTrust;
            _leaveTrust = c.LeaveTrust ?? _leaveTrust;
            _burdenLimit = c.BurdenLimit ?? _burdenLimit;
            _keyFlag = c.KeyFlag;
            _keyFragment = c.FindKeyFragment(out string keyError);
            if (keyError.Length > 0) Debug.LogError($"[엔딩 판정] {keyError} truth 엔딩은 나오지 않는다.", c);
            if (c.TruthTrust == null || c.LeaveTrust == null || c.BurdenLimit == null)
                Debug.LogError($"[엔딩 판정] {c.PersonName}의 회차 CSV에 truth_trust, leave_trust, burden_limit 중 빠진 값이 있다. 디렉터의 값을 쓴다.", c);
            if (!string.IsNullOrEmpty(c.IntroMessage)) _introMessage = c.IntroMessage;
            if (!string.IsNullOrEmpty(c.IdleSituation)) _idleSituation = c.IdleSituation;
            if (!string.IsNullOrEmpty(c.AbyssMessage)) _abyssMessage = c.AbyssMessage;

            ClientCase[] all = FindObjectsByType<ClientCase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (ClientCase other in all)
                foreach (GameObject g in other.FragmentGroups)
                    if (g != null && g.activeSelf != (other == c)) g.SetActive(other == c);
            // 스펙 015: 불러온 손님의 사물 묶음만 켜고 다른 손님의 사물 묶음은 끈다
            foreach (ClientCase other in all)
                foreach (GameObject g in other.PropGroups)
                    if (g != null && g.activeSelf != (other == c)) g.SetActive(other == c);

            if (_maze != null) RebuildMaze(c, Application.isPlaying || _maze.Seed != c.MazeSeed);
            ResetAll();
        }

        /// <summary>
        /// 씬에 저장된 공용 문구 속 '은재'/'그녀'를 지금 손님의 이름/대명사로 바꾼다.
        /// s와 지금 케이스를 사용하며, 바뀐 문구를 반환한다.
        /// </summary>
        public string Personalize(string s)
        {
            if (string.IsNullOrEmpty(s) || _activeCase == null) return s;
            if (_activeCase.PersonName != "은재") s = s.Replace("은재", _activeCase.PersonName);
            if (_activeCase.Pronoun != "그녀") s = s.Replace("그녀", _activeCase.Pronoun);
            return s;
        }

        /// <summary>
        /// 결말 목록에서 id로 결말을 찾는다.
        /// id를 사용하며, 찾은 EndingDef 또는 null을 반환한다.
        /// </summary>
        public EndingDef FindEnding(string id)
        {
            List<EndingDef> list = Endings;
            if (list == null || string.IsNullOrEmpty(id)) return null;
            foreach (EndingDef e in list) if (e != null && e.Id == id) return e;
            return null;
        }

        // 시선

        /// <summary>
        /// 눈을 들여다보기 시작하거나 시선을 거둔다. 심연과 엔딩 중에는 거둘 수 없다.
        /// 지금 모드를 사용하며, 모드와 시선 표시, 문구를 바꾼다.
        /// </summary>
        public void ToggleGaze()
        {
            if (_mode == Mode.Ending) return;
            if (_mode == Mode.Gaze && _inAbyss) { _message = AbyssText; return; }
            // 스펙 003: 관찰을 멈출 때의 문구를 "관찰을 잠시 멈추고 생각에 잠긴다."로 변경
            if (_mode == Mode.Gaze) { ExitGaze(Strings.Get("msg.gaze_off", "관찰을 잠시 멈추고 생각에 잠긴다.")); return; }
            if (!CanGaze) return;
            _mode = Mode.Gaze;
            _velocity = Vector2.zero;
            if (_cursor != null) _cursor.Visible = true;
            _lastLayer = _maze != null && _cursor != null ? _maze.LayerOfRing(_cursor.Ring) : -1;
            // 스펙 015: 지금 손님의 사물을 모으고 지금 감정에 맞춰 표시를 정한다.
            SyncProps();
            _message = Strings.Get("msg.gaze_on", "{name}의 눈을 들여다본다.", ("name", Name));
        }

        /// <summary>
        /// 시선을 거두고 대화로 돌아온다.
        /// why(보여줄 문구)를 사용하며, 모드와 시선 표시, 읽기 상태를 바꾼다.
        /// </summary>
        public void ExitGaze(string why)
        {
            _reading = false;
            _mode = Mode.Talk;
            // 스펙 019: 대화로 돌아오면 해금 안내를 끈다.
            _newTalkReady = false;
            _velocity = Vector2.zero;
            if (_cursor != null) _cursor.Visible = false;
            _message = why;
            // 스펙 007: 밀려남과 초점 요동을 멈춘다.
            StopBump();
            // 스펙 007: 시선을 거두면 잠긴 문 접촉 횟수를 0으로 되돌린다.
            ResetGatePush();
        }

        /// <summary>
        /// 입력 방향(화면 기준, -1~1)으로 가속/감속하며 시선을 움직인다.
        /// input, dt를 사용하며, _velocity와 시선 위치를 바꾼다.
        /// </summary>
        public void Steer(Vector2 input, float dt)
        {
            // 스펙 006: 깜빡임 중과 뜬 직후에는 이동 입력을 받지 않음
            // 스펙 010 FR-018: 파편을 읽은 뒤의 벽 변화 연출 중에도 이동 입력을 받지 않는다.
            if (_mode != Mode.Gaze || _reading || _blinkPhase >= 0f || _blinkRecoverT > 0f || _respawnEffectT >= 0f) { _velocity = Vector2.zero; return; }
            // 스펙 007: 밀려나는 동안에는 이동 입력을 받지 않는다.
            if (_knockbackT >= 0f) { _velocity = Vector2.zero; return; }
            Vector2 target = Vector2.ClampMagnitude(input, 1f) * _moveSpeed;
            _velocity = Vector2.MoveTowards(_velocity, target, _acceleration * _moveSpeed * dt);
            if (_velocity.sqrMagnitude <= 1e-8f) return;

            // 스펙 006: 화면 기준 이동량을 미로 기준으로 변환
            Vector2 mazeVelocity = ScreenToMaze(_velocity);
            _hitRadial = 0;
            _hitTangent = 0;
            MoveBy(mazeVelocity * dt);
            TryStartBump(mazeVelocity);
        }

        /// <summary>
        /// 이번 이동에서 막힌 축의 벽 쪽 속도가 충분하면 부딪힘을 시작한다. 막힌 축의 반대 방향을 밀려나는 방향으로 정한다.
        /// mazeVelocity(미로 기준 속도), _hitRadial, _hitTangent를 사용하며, _knockbackDir, _knockbackT, _bumpCooldownT, _velocity를 바꾼다.
        /// 부딪히면 _bumpFlash도 올린다. 같은 프레임에 밀어냄 사물로 밀려나기 시작했으면 다른 축의 벽 방향만 더해 정규화한다. _bumpEnabled가 꺼져 있으면 아무것도 하지 않는다.
        /// </summary>
        private void TryStartBump(Vector2 mazeVelocity)
        {
            if (!_bumpEnabled) return;
            // 스펙 007: 파편을 읽기 시작한 프레임에는 밀려남을 시작하지 않는다.
            if (_mode != Mode.Gaze || _inAbyss || _reading) return;
            // 수정(015 FR-007): 같은 프레임에 밀어냄 사물로 밀려나기 시작했으면 벽 부딪힘을 건너뛰던 것을 다른 축의 벽 방향을 더해 정규화하게 바꿨다.
            bool propPushNow = _knockbackT == 0f;
            if (!propPushNow && (_bumpCooldownT > 0f || _knockbackT >= 0f)) return;
            if (_hitRadial == 0 && _hitTangent == 0) return;

            float rad = _cursor.Angle * Mathf.Deg2Rad;
            var radialU = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            var tangentU = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
            Vector2 dir = Vector2.zero;

            // 벽을 따라 미끄러지거나 살짝 닿은 축은 거르고, 세게 막힌 축만 반대로 밀어낸다.
            if (_hitRadial != 0 && Mathf.Abs(Vector2.Dot(mazeVelocity, radialU)) >= _bumpMinSpeed) dir.x = -_hitRadial;
            if (_hitTangent != 0 && Mathf.Abs(Vector2.Dot(mazeVelocity, tangentU)) >= _bumpMinSpeed) dir.y = -_hitTangent;
            // 사물이 이미 민 축은 빼고, 남은 벽 축이 있을 때만 사물 방향과 합친다.
            if (propPushNow)
            {
                if (_knockbackDir.x != 0f) dir.x = 0f;
                if (_knockbackDir.y != 0f) dir.y = 0f;
                if (dir != Vector2.zero) dir += new Vector2(Mathf.Sign(_knockbackDir.x) * (_knockbackDir.x != 0f ? 1f : 0f), Mathf.Sign(_knockbackDir.y) * (_knockbackDir.y != 0f ? 1f : 0f));
            }
            if (dir == Vector2.zero) return;

            _knockbackDir = dir.normalized;
            _knockbackT = 0f;
            _bumpCooldownT = _bumpCooldown;
            _velocity = Vector2.zero;

            // 일반 벽도 잠긴 문과 같이 시선을 번쩍이게 한다. 잠긴 문 문구는 GateBlocked가 이미 띄웠다.
            _bumpFlash = Mathf.Max(_bumpFlash, 0.6f);
            StartGlitch();
            // 스펙 007: 일반 벽 부딪힘으로는 부담감을 바꾸지 않는다. 잠긴 문 반복 접촉은 GateBlocked에서 센다.
        }

        /// <summary>
        /// 미로 초점 요동을 시작한다. 이미 요동 중이면 남은 시간만 다시 채우고 처음 기준값을 유지한다.
        /// _glitchSeconds와 지금 미로 초점을 사용하며, _glitching, _glitchT, _glitchBaseFocus, _glitchSampleT를 바꾼다.
        /// </summary>
        private void StartGlitch()
        {
            if (_maze == null || _glitchSeconds <= 0f) return;
            if (!_glitching) _glitchBaseFocus = _maze.Focus;
            _glitching = true;
            _glitchT = _glitchSeconds;
            _glitchSampleT = 0f;
        }

        /// <summary>
        /// 요동 중이면 남은 비율과 무작위 값으로 미로 초점을 낮추고, 시간이 끝나면 기준값으로 되돌린다.
        /// dt, _glitchStrength, _glitchRate, _glitchBaseFocus를 사용하며, 미로 초점과 요동 상태를 바꾼다.
        /// </summary>
        private void TickGlitch(float dt)
        {
            if (!_glitching) return;
            _glitchT -= dt;
            if (_glitchT <= 0f) { StopGlitch(); return; }

            // 정해진 간격마다 새 무작위 값을 뽑아 초점이 불규칙하게 튀게 한다.
            _glitchSampleT -= dt;
            if (_glitchSampleT <= 0f)
            {
                _glitchNoise = UnityEngine.Random.value;
                _glitchSampleT = 1f / Mathf.Max(1f, _glitchRate);
            }
            float remain = _glitchT / Mathf.Max(0.01f, _glitchSeconds);
            _maze.Focus = Mathf.Clamp01(_glitchBaseFocus - _glitchStrength * remain * _glitchNoise);
        }

        /// <summary>
        /// 초점 요동을 끝내고 미로 초점을 요동 전 기준값으로 되돌린다.
        /// _glitchBaseFocus를 사용하며, 미로 초점과 _glitching, _glitchT를 바꾼다.
        /// </summary>
        private void StopGlitch()
        {
            if (!_glitching) return;
            _glitching = false;
            _glitchT = 0f;
            if (_maze != null) _maze.Focus = _glitchBaseFocus;
        }

        /// <summary>
        /// 밀려남을 즉시 멈추고 초점 요동을 끝낸다. 시선 거두기, 엔딩, 리셋, 깜빡임, 파편 읽기가 시작될 때 부른다.
        /// 입력값은 없으며, _knockbackT와 미로 초점을 바꾼다.
        /// </summary>
        private void StopBump()
        {
            _knockbackT = -1f;
            StopGlitch();
        }

        /// <summary>
        /// 대기 시간을 줄이고, 밀려나는 중이면 감속 곡선으로 이번 프레임 몫만큼 시선을 옮긴다.
        /// dt, _knockbackDistance, _knockbackSeconds를 사용하며, _bumpCooldownT, _knockbackT와 시선 위치를 바꾼다.
        /// </summary>
        private void TickKnockback(float dt)
        {
            if (_bumpCooldownT > 0f) _bumpCooldownT -= dt;
            if (_knockbackT < 0f) return;
            if (_mode != Mode.Gaze || _inAbyss || _maze == null || _cursor == null) { _knockbackT = -1f; return; }

            // 처음엔 빠르고 끝은 느리게 밀리도록 ease-out 곡선의 증가분만큼 옮긴다.
            float duration = Mathf.Max(0.01f, _knockbackSeconds);
            float prev = 1f - (1f - _knockbackT / duration) * (1f - _knockbackT / duration);
            _knockbackT = Mathf.Min(_knockbackT + dt, duration);
            float next = 1f - (1f - _knockbackT / duration) * (1f - _knockbackT / duration);
            MoveKnockback(_knockbackDir * (_knockbackDistance * (next - prev)));
            if (_knockbackT >= duration) _knockbackT = -1f;
        }

        /// <summary>
        /// 밀려나는 이동량을 적용한다. 안쪽/바깥쪽은 지금 고리 띠 안으로 제한하고, 둘레 방향은 칸막이 벽 판정을 거친다.
        /// delta(x 바깥쪽, y 반시계 거리)를 사용하며, 시선의 반지름과 각도를 바꾼다.
        /// </summary>
        private void MoveKnockback(Vector2 delta)
        {
            float r = _cursor.Radius;
            float a = _cursor.Angle;

            // 고리 띠를 벗어나지 않으므로 원호를 넘거나 심연에 들어가지 않는다.
            int ring = _maze.RingAt(r);
            r = Mathf.Clamp(r + delta.x, _maze.RingInner(ring) + _wallMargin, _maze.RingOuter(ring) - _wallMargin);
            if (Mathf.Abs(delta.y) > 1e-6f) ResolveAngular(ref r, ref a, delta.y);

            _cursor.Radius = r;
            _cursor.Angle = Mathf.Repeat(a, 360f);
            OnMoved();
        }

        /// <summary>
        /// 에디트 모드 버튼용으로 화면 방향으로 editNudge만큼 움직인다.
        /// dir을 사용하며, 시선 위치를 바꾼다.
        /// </summary>
        public void Nudge(Vector2 dir)
        {
            if (_mode != Mode.Gaze) return;
            // 스펙 006: 화면 기준 이동량을 미로 기준으로 변환
            MoveBy(ScreenToMaze(dir.normalized * _editNudge));
        }

        /// <summary>
        /// 미로 기준 이동량만큼 시선을 옮긴다. 벽에 막히면 그 축만 막고 나머지 축으로 미끄러진다.
        /// delta를 사용하며, 시선의 반지름과 각도를 바꾼다.
        /// </summary>
        public void MoveBy(Vector2 delta)
        {
            if (_maze == null || _cursor == null) return;
            int steps = Mathf.Max(1, Mathf.CeilToInt(delta.magnitude / 0.04f));
            Vector2 step = delta / steps;
            for (int i = 0; i < steps && _mode == Mode.Gaze; i++)
            {
                float r = _cursor.Radius;
                float a = _cursor.Angle;
                if (_inAbyss) { MoveInAbyss(step); continue; }
                float rad = a * Mathf.Deg2Rad;
                var radialU = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                var tangentU = new Vector2(-Mathf.Sin(rad), Mathf.Cos(rad));
                float dr = Vector2.Dot(step, radialU);
                float dTan = Vector2.Dot(step, tangentU);

                if (Mathf.Abs(dr) > 1e-6f) ResolveRadial(ref r, ref a, dr);
                if (_mode != Mode.Gaze) return;
                if (!_inAbyss && Mathf.Abs(dTan) > 1e-6f) ResolveAngular(ref r, ref a, dTan);

                _cursor.Radius = r;
                _cursor.Angle = Mathf.Repeat(a, 360f);
            }
            OnMoved();
        }

        /// <summary>
        /// 파편을 읽은 뒤 지금 시선이 있는 층을 뺀 나머지 층의 벽을 무작위로 새로 만든다. Play 중에는 바뀌는 모습을 파문 교차 전환으로 보여 준다.
        /// 지금 시선의 층을 사용하며, 미로의 벽과 문구, _wallChange의 연출 상태, _respawnEffectT를 바꾼다.
        /// </summary>
        public void RespawnMaze()
        {
            if (!_respawnMazeAfterReading || _maze == null) return;
            int keep = _cursor != null ? _maze.LayerOfRing(_cursor.Ring) : -1;
            // 스펙 010 FR-017: 깜빡임이나 다른 벽 변화 연출과 겹치지 않을 때만 옛 벽을 잔상으로 남긴다.
            bool animate = Application.isPlaying && _blinkPhase < 0f && !_wallChange.IsPlaying;
            if (animate) _wallChange.Capture(_maze, keep);
            _maze.Generate(UnityEngine.Random.Range(1, 999999), keep);
            if (animate)
            {
                _wallChange.Begin();
                _wallChange.Tick(0f);
                _respawnEffectT = 0f;
            }
            _message = Strings.Get("msg.respawn", "다른 층의 길이 바뀌었다.");
        }

        /// <summary>
        /// 스펙 010 FR-017: 파편을 읽은 뒤의 벽 변화 연출을 한 프레임 진행하고, 연출 시간이 지나면 정리한다.
        /// dt와 _wallChange.Duration을 사용하며, _respawnEffectT와 파문, 잔상, 새 벽의 표시값을 바꾼다.
        /// </summary>
        private void TickRespawnEffect(float dt)
        {
            _respawnEffectT += dt;
            if (_respawnEffectT < _wallChange.Duration) { _wallChange.Tick(_respawnEffectT); return; }
            StopRespawnEffect();
        }

        /// <summary>
        /// 스펙 010 FR-019: 파편을 읽은 뒤의 벽 변화 연출을 바로 정리한다. 재생 중이 아니면 아무것도 하지 않는다.
        /// _respawnEffectT를 사용하며, _respawnEffectT와 _wallChange의 연출 상태를 바꾼다.
        /// </summary>
        private void StopRespawnEffect()
        {
            if (_respawnEffectT < 0f) return;
            _respawnEffectT = -1f;
            _wallChange.Finish();
        }

        /// <summary>
        /// 눈을 깜빡인다. 이미 깜빡이는 중이면 무시하고, 이번 연출 후보와 미로가 돌 칸 수, 방향을 정한다.
        /// 후보 목록, 칸 수 범위, _blinkTurnLimitDeg를 사용하며, 깜빡임 단계와 재생 후보, 이번 회전량을 바꾼다.
        /// </summary>
        public void Blink()
        {
            // 스펙 006: 칸 단위로 돌고 처음 방향에서 제한 각도를 넘으면 반대로 돈다.
            if (_blinkPhase >= 0f || _blinkPresets.Length == 0) return;
            // 스펙 010 FR-019: 깜빡임이 시작되면 파편을 읽은 뒤의 벽 변화 연출을 정리한다.
            StopRespawnEffect();
            // 스펙 009: 깜빡임이 끝날 때까지 시작할 때 고른 후보를 유지한다.
            _activePreset = _blinkPresets[Mathf.Clamp(_blinkPresetIndex, 0, _blinkPresets.Length - 1)];
            _blinkOpenFrom = Mathf.Max(0.01f, _activePreset.CloseSeconds) + Mathf.Max(0.01f, _activePreset.HoldSeconds);
            _blinkPhase = 0f;
            // 스펙 007: 밀려남과 초점 요동을 멈춘다.
            StopBump();
            _blinkTurnFrom = _mazeTurnDeg;
            float sectorDeg = _maze != null ? _maze.SectorDeg : 0f;
            int sectors = UnityEngine.Random.Range(_blinkTurnMinSectors, _blinkTurnMaxSectors + 1);
            float direction = UnityEngine.Random.value < 0.5f ? -1f : 1f;
            _blinkTurnDelta = direction * sectors * sectorDeg;
            if (Mathf.Abs(_mazeTurnDeg + _blinkTurnDelta) > _blinkTurnLimitDeg + 0.01f) _blinkTurnDelta = -_blinkTurnDelta;
        }

        /// <summary>
        /// 들여다보는 동안 부담감을 채운다. 신뢰가 높을수록 느리게 차고, 가득 차면 leave 엔딩을 시작한다.
        /// dt와 상대의 신뢰를 사용하며, _burden과 모드를 바꾼다.
        /// </summary>
        private void TickBurden(float dt)
        {
            int trust = _person != null ? Mathf.Max(0, _person.Trust) : 0;
            float seconds = Mathf.Max(0.1f, _burdenFillSeconds + _burdenSecondsPerTrust * trust);
            _burden = Mathf.Min(1f, _burden + dt / seconds);
            if (_burden < _burdenLimit) return;
            EndByBurden();
        }

        /// <summary>
        /// 부담감이 가득 차서 상담을 끝낸다. leave 엔딩이 없는 손님이면 시선만 거둔다.
        /// _burdenEndLine과 지금 손님의 엔딩 목록을 사용하며, 모드를 엔딩 또는 대화로 바꾼다.
        /// </summary>
        private void EndByBurden()
        {
            if (FindEnding("leave") != null) StartEnding("leave", _burdenEndLine);
            else ExitGaze(_burdenEndLine);
        }

        /// <summary>
        /// 들여다본 시간을 쌓아 _blinkInterval초마다 눈을 깜빡인다.
        /// dt를 사용하며, _blinkElapsed와 깜빡임 단계를 바꾼다.
        /// </summary>
        private void TickBlink(float dt)
        {
            // 스펙 009: 깜빡임이 끝난 뒤부터 다음 깜빡임 시간을 셈
            if (_blinkInterval <= 0f || _blinkPhase >= 0f) return;
            _blinkElapsed += dt;
            if (_blinkElapsed < _blinkInterval) return;
            _blinkElapsed -= _blinkInterval;
            Blink();
        }

        // 심연과 결말

        /// <summary>
        /// 가장 반짝이는 파편에 닿아 마음이 들린다. 신뢰와 열쇠 파편에 따라 truth / half 엔딩이 된다.
        /// 상대의 신뢰와 열쇠 파편을 사용하며, 엔딩을 시작한다.
        /// </summary>
        public void TouchCore()
        {
            if (_core != null) _core.Touch();
            int trust = _person != null ? _person.Trust : 0;
            // key_flag가 정해져 있으면 그 파편을 주웠을 때만 충족된다. 파편을 못 찾았으면 충족되지 않는다.
            bool keyOk = string.IsNullOrEmpty(_keyFlag) ? _keyFragment == null || _keyFragment.Collected : _keyFragment != null && _keyFragment.Collected;
            // 스펙 017: 옵션이 켜져 있으면 이 인물의 쌍을 모두 이어야 truth가 된다
            bool linksOk = !_truthNeedsAllLinks || AllCaseLinksDone();
            StartEnding(trust >= _truthTrust && keyOk && linksOk ? "truth" : "half", null);
        }

        /// <summary>
        /// 스펙 017: 지금 손님의 파편으로 이루어진 쌍을 모두 이었는지 확인한다. 쌍이 없으면 항상 true다.
        /// _activeCase와 _linkedResults를 사용하며, 잇지 않은 쌍이 없으면 true를 반환한다.
        /// </summary>
        private bool AllCaseLinksDone()
        {
            foreach (MemoryLinkScript.Link l in CaseLinks())
                if (!_linkedResults.Contains(l.Result)) return false;
            return true;
        }

        /// <summary>
        /// 스펙 017: Links.csv에서 a와 b가 모두 지금 손님의 파편 플래그인 쌍을 모은다.
        /// _activeCase와 읽은 쌍 목록을 사용하며, 그 쌍 목록을 반환한다. 손님이 없으면 빈 목록이다.
        /// </summary>
        private List<MemoryLinkScript.Link> CaseLinks()
        {
            var list = new List<MemoryLinkScript.Link>();
            if (_activeCase == null) return list;
            var flags = new HashSet<string>(_activeCase.Fragments().Select(f => f.Flag));
            foreach (MemoryLinkScript.Link l in MemoryLinkScript.Links)
                if (flags.Contains(l.A) && flags.Contains(l.B)) list.Add(l);
            return list;
        }

        /// <summary>
        /// 엔딩을 시작한다.
        /// id와 prefix(엔딩 대사 앞에 먼저 보여줄 한 줄)를 사용하며, 모드를 Ending으로 바꾸고 첫 줄을 띄운다.
        /// </summary>
        public void StartEnding(string id, string prefix)
        {
            _mode = Mode.Ending;
            _inAbyss = false;
            _velocity = Vector2.zero;
            // 스펙 007: 밀려남과 초점 요동을 멈춘다.
            StopBump();
            if (_cursor != null) _cursor.Visible = false;
            _currentEnding = id;
            _responseLines.Clear();
            _endingLines = BuildEndingLines(FindEnding(id), prefix);
            _heartIndex = 0;
            ShowHeartLine();
        }

        /// <summary>
        /// 엔딩 대사를 다음 줄로 넘긴다. 입력은 GameFlow가 받아서 이 메서드를 부른다.
        /// 입력값은 없으며, 엔딩 줄 번호와 문구를 바꾼다.
        /// </summary>
        public void NextHeartLine()
        {
            if (_mode != Mode.Ending || _heartIndex >= EndingLines.Count - 1) return;
            _heartIndex++;
            ShowHeartLine();
        }

        // 대화

        /// <summary>
        /// 선택지를 고를 수 있는지 판단한다.
        /// o의 need 플래그들을 사용하며, 모두 켜져 있으면 true를 반환한다.
        /// </summary>
        public bool IsAvailable(DialogueScript.Choice o) => o != null && Flags.AllSet(o.Need);

        /// <summary>
        /// 지금 장면에서 보여줄 선택지 목록을 만든다. 시작 장면이 아니면 맨 끝에 뒤로가기가 붙는다.
        /// 지금 장면과 플래그를 사용하며, 선택지 목록을 반환한다 (매번 같은 리스트를 다시 채운다).
        /// </summary>
        public IReadOnlyList<ChoiceView> CurrentChoices()
        {
            _choices.Clear();
            DialogueScript.Node node = CurrentNode;
            if (node == null) return _choices;
            // 스펙 004: 이미 고른 안 좋은 말을 목록에서 제외
            foreach (int i in VisibleChoiceIndices(node))
            {
                DialogueScript.Choice c = node.Choices[i];
                bool ok = IsAvailable(c);
                _choices.Add(new ChoiceView
                {
                    Text = c.Text,
                    Available = ok,
                    Missing = ok ? "" : string.Join(", ", c.Need.Where(f => !Flags.IsOn(f))),
                });
            }
            if (CanGoBack)
                _choices.Add(new ChoiceView
                {
                    Text = string.IsNullOrEmpty(node.BackLabel) ? Strings.Get("dialogue.back", DialogueScript.DefaultBackLabel) : node.BackLabel,
                    Available = true,
                    IsBack = true,
                    Missing = "",
                });
            return _choices;
        }

        /// <summary>
        /// 지금 손님의 미로에서 지나갈 수 있는 문의 수를 센다.
        /// 활성화된 문과 _person을 사용하며, 열린 문의 수를 반환한다.
        /// </summary>
        private int OpenGateCount() => _maze.GetComponentsInChildren<MazeGate>(false).Count(g => g.CanPass(_person));

        /// <summary>
        /// 숫자키로 고른 번호를 처리한다. 공통 선택지의 번호면 처음 이야기로 돌아가고, 아니면 그 선택지를 고른다.
        /// 화면 번호 index와 공통 선택지 표시 여부를 사용하며, ReturnToStart 또는 ChooseOption이 바꾸는 상태를 바꾼다.
        /// </summary>
        private void PressChoice(int index)
        {
            if (CanReturnToStart && index == CurrentChoices().Count) ReturnToStart();
            else ChooseOption(index);
        }

        /// <summary>
        /// 장면에서 화면에 보일 선택지의 CSV 순번을 모은다.
        /// node와 _usedOptions를 사용하며, 다시 채운 _visibleChoiceIndices를 반환한다.
        /// </summary>
        private List<int> VisibleChoiceIndices(DialogueScript.Node node)
        {
            _visibleChoiceIndices.Clear();
            for (int i = 0; i < node.Choices.Count; i++)
                if (!IsHiddenChoice(node, i)) _visibleChoiceIndices.Add(i);
            return _visibleChoiceIndices;
        }

        /// <summary>
        /// 이미 고른 안 좋은 말이라 숨겨야 하는지 판단한다.
        /// node와 CSV 순번 index, _usedOptions를 사용하며, 숨겨야 하면 true를 반환한다.
        /// </summary>
        private bool IsHiddenChoice(DialogueScript.Node node, int index)
        {
            // 스펙 005: 안 좋은 말 판정을 IsWrongChoice로 모음
            // 스펙 004 FR-010: 신뢰와 동공만 바꾸는 좋은 말도 고른 뒤에는 숨긴다
            DialogueScript.Choice c = node.Choices[index];
            return (IsWrongChoice(c) || IsPlainGoodChoice(c)) && _usedOptions.Contains(node.Id + ":" + index);
        }

        /// <summary>
        /// 스펙 004 FR-010: 신뢰와 동공만 바꾸는 좋은 말인지 판단한다. 장면 이동, 엔딩, 플래그, 휴식 신호, 감정 변화가 하나도 없어야 한다.
        /// c의 Trust, Dilation, Goto, Ending, Set, Rest, Emotion을 사용하며, 해당하면 true를 반환한다.
        /// </summary>
        private static bool IsPlainGoodChoice(DialogueScript.Choice c)
        {
            bool good = c.Trust > 0 || c.Dilation > 0f;
            bool story = !string.IsNullOrEmpty(c.Goto) || !string.IsNullOrEmpty(c.Ending) || c.Set.Length > 0
                || !string.IsNullOrEmpty(c.Rest) || c.Emotion.HasValue;
            return good && !story;
        }

        /// <summary>
        /// 안 좋은 말(신뢰 또는 동공이 내려가고 오르는 값이 없는 선택지)인지 판단한다.
        /// c의 Trust와 Dilation을 사용하며, 안 좋은 말이면 true를 반환한다.
        /// </summary>
        private static bool IsWrongChoice(DialogueScript.Choice c)
        {
            return c.Trust <= 0 && c.Dilation <= 0f && (c.Trust < 0 || c.Dilation < 0f);
        }

        /// <summary>
        /// 대화 선택지를 고른다. 좋은 말은 한 번만 통하며, 신뢰가 바닥나면 leave 엔딩이 된다.
        /// 마지막 번호가 뒤로가기면 이전 장면으로 돌아간다.
        /// index를 사용하며, 상대의 신뢰와 동공, 부담감, 플래그, 문구, 대화 장면 또는 엔딩을 바꾼다.
        /// </summary>
        public void ChooseOption(int index)
        {
            if (_mode == Mode.Ending || IsReadingResponse) return;
            if (_mode == Mode.Gaze) { _message = Strings.Get("msg.no_talk_in_gaze", "들여다보는 중에는 말할 수 없다. (Space로 관찰 중지)"); return; }
            DialogueScript.Node node = CurrentNode;
            if (node == null || index < 0) return;
            // 스펙 004: 화면 번호를 CSV 순번으로 변환
            List<int> visible = VisibleChoiceIndices(node);
            if (index == visible.Count && CanGoBack) { GoBack(); return; }
            if (index >= visible.Count) return;
            int choiceIndex = visible[index];
            DialogueScript.Choice o = node.Choices[choiceIndex];
            if (!IsAvailable(o)) { _message = Strings.Get("msg.locked_choice", "아직 모르는 이야기다. 눈 속에서 단서를 찾아야 한다."); return; }

            // 같은 말을 반복해서 신뢰를 올릴 수 없게 좋은 말은 한 번만 통한다.
            // 다른 장면으로 넘어가는 말이면 수치는 다시 주지 않고 그 장면으로만 간다 (뒤로 갔다가 다시 들어올 때).
            // 스펙 004: 기록 키를 CSV 순번 기준으로 저장
            string key = node.Id + ":" + choiceIndex;
            bool helpful = o.Trust > 0 || o.Dilation > 0f;
            if (helpful && _usedOptions.Contains(key))
            {
                if (!string.IsNullOrEmpty(o.Goto) && string.IsNullOrEmpty(o.Ending))
                {
                    // 스펙 007: 다른 장면으로 넘어가는 말은 다시 골라도 대화 기록에 남긴다.
                    DialogueLog.Add("", Personalize(o.Text), true);
                    DialogueLog.Add(Name, Personalize(o.Response), false, FieldNPC.PortraitOf(Name));
                    ShowResponse(o.Response, "", o.Goto, null);
                }
                else _message = Strings.Get("msg.already", "이미 한 이야기다. {name}는 다음 말을 기다리고 있다.", ("name", Name));
                return;
            }
            _usedOptions.Add(key);
            // 스펙 007: 고른 말을 대화 기록에 남긴다.
            DialogueLog.Add("", Personalize(o.Text), true);

            // 스펙 004: 선택 전에 열려 있던 문 수를 기억
            int openBefore = OpenGateCount();
            if (_person != null)
            {
                _person.Trust += o.Trust;
                // 스펙 004: 옳은 대답의 신뢰를 문 판정용으로 따로 누적
                if (o.Trust > 0) _person.EarnedTrust += o.Trust;
                _person.Dilation = Mathf.Clamp01(_person.Dilation + o.Dilation);
            }
            float burdenBefore = _burden;
            if (helpful) _burden = Mathf.Max(0f, _burden - _burdenRelief);
            // 스펙 005: 안 좋은 말을 고르면 부담감을 올린다.
            if (IsWrongChoice(o)) _burden = Mathf.Min(1f, _burden + _burdenWrongChoice);
            foreach (string f in o.Set) Flags.Set(f);
            // 스펙 015: 선택지의 emotion 값으로 감정 변경
            if (o.Emotion.HasValue) ChangeEmotion(o.Emotion.Value);
            // 스펙 014 FR-038: 결과 분기 조건이 모두 맞을 때만 엔딩으로 가고, 아니면 일반 선택지처럼 진행한다.
            if (!string.IsNullOrEmpty(o.Ending) && Flags.AllSet(o.EndingNeed)) { StartEnding(o.Ending, o.Response); return; }
            var sb = new StringBuilder();
            // 스펙 018: 신뢰 안내와 문 안내를 따로 붙인다. 문은 선택지가 켠 플래그로 열린다.
            if (o.Trust > 0) sb.Append("\n" + Strings.Get("msg.trust_up_no_gate", "(신뢰가 깊어졌다.)"));
            if (OpenGateCount() > openBefore) sb.Append("\n" + Strings.Get("msg.gate_open", "(눈 속 어딘가의 문이 열렸다.)"));
            if (o.Trust < 0) sb.Append(Strings.Get("msg.trust_down", "") is string td && td.Length > 0 ? "\n" + td : "");
            if (o.Dilation > 0.05f) sb.Append("\n" + Strings.Get("msg.dilation_up", "(동공이 조금 커졌다.)"));
            if (o.Dilation < -0.05f) sb.Append("\n" + Strings.Get("msg.dilation_down", "(동공이 좁아졌다.)"));
            if (_burden < burdenBefore) sb.Append("\n(부담이 조금 줄었다.)");
            // 스펙 005: 부담감이 오른 선택지에 증가 문구를 붙인다.
            if (_burden > burdenBefore) sb.Append("\n(부담이 조금 늘었다.)");
            // 스펙 011: rest 대사가 있는 선택지를 처음 고르면 휴식 신호를 켠다.
            if (!string.IsNullOrEmpty(o.Rest)) { _restRequested = true; _restLine = o.Rest; }
            bool leave = _person != null && _person.Trust <= _leaveTrust && FindEnding("leave") != null;
            // 스펙 005: 신뢰 leave가 아니고 부담감이 가득 찼으면 _burdenEndLine으로 끝낸다.
            if (!leave && _burden > burdenBefore && _burden >= _burdenLimit) { EndByBurden(); return; }
            // 스펙 007: 엔딩으로 이어지지 않을 때만 반응을 남긴다. 엔딩이면 엔딩 줄로 남는다.
            if (!leave) DialogueLog.Add(Name, Personalize(o.Response), false, FieldNPC.PortraitOf(Name));
            ShowResponse(o.Response, sb.ToString(), o.Goto, leave ? o.Response : null);
        }

        /// <summary>
        /// 선택지 결과를 메시지 줄에 띄운다. 결과가 여러 줄이면 첫 줄만 띄우고 장면 이동을 마지막 줄 뒤로 미룬다. leave 판정이면 결과 줄을 앞줄로 leave 엔딩을 시작한다.
        /// response(줄바꿈으로 나눈 결과), notes(마지막 줄 뒤에 붙일 안내 문구), gotoId, leaveLine(leave 엔딩 앞줄, 판정이 없으면 null)을 사용하며,
        /// _message와 결과 넘기기 상태를 바꾸고, 한 줄이면 바로 엔딩이나 장면 이동을 한다.
        /// </summary>
        private void ShowResponse(string response, string notes, string gotoId, string leaveLine)
        {
            _responseLines.Clear();
            // leave 엔딩이면 결과 줄을 엔딩 앞줄로 넘겨 엔딩 안에서 한 줄씩 보인다 (같은 줄을 두 번 보이지 않게).
            if (leaveLine != null) { StartEnding("leave", leaveLine); return; }
            string[] lines = (response ?? "").Split('\n');
            if (lines.Length <= 1)
            {
                _message = (response ?? "") + notes;
                if (!string.IsNullOrEmpty(gotoId)) GoTo(gotoId);
                return;
            }
            _responseLines.AddRange(lines);
            _responseLines[_responseLines.Count - 1] += notes;
            _responseIndex = 0;
            _pendingGoto = gotoId;
            _message = _responseLines[0];
        }

        /// <summary>
        /// 선택지 결과를 다음 줄로 넘긴다. 마지막 줄 다음이면 미뤄 둔 장면 이동을 한다. 입력은 GameFlow가 받아서 이 메서드를 부른다.
        /// 입력값은 없으며, 결과 줄 번호와 _message, 대화 장면 또는 엔딩을 바꾼다.
        /// </summary>
        public void NextResponseLine()
        {
            if (!IsReadingResponse) return;
            if (_responseIndex < _responseLines.Count - 1)
            {
                _responseIndex++;
                _message = _responseLines[_responseIndex];
                return;
            }
            // 마지막 줄은 메시지 줄에 남긴 채 다음 장면으로 간다.
            string gotoId = _pendingGoto;
            _responseLines.Clear();
            _pendingGoto = null;
            _responseDoneFrame = Time.frameCount;
            if (!string.IsNullOrEmpty(gotoId)) GoTo(gotoId);
        }

        /// <summary>
        /// 다른 장면으로 간다. 이미 지나온 장면이면 그곳까지 되감는다 (뒤로가기가 꼬이지 않게).
        /// id를 사용하며, 지금 장면과 지나온 장면 목록을 바꾼다.
        /// </summary>
        public void GoTo(string id)
        {
            DialogueScript s = Script;
            if (s == null || s.Find(id) == null) return;
            DialogueScript.Node cur = CurrentNode;
            if (cur != null && cur.Id == id) return;
            int seen = _nodeHistory.LastIndexOf(id);
            if (seen >= 0) _nodeHistory.RemoveRange(seen, _nodeHistory.Count - seen);
            else if (cur != null) _nodeHistory.Add(cur.Id);
            _currentNodeId = id;
        }

        /// <summary>
        /// 바로 전 장면으로 돌아간다 ('다른 질문을 해볼까요?').
        /// 지나온 장면 목록을 사용하며, 지금 장면과 문구를 바꾼다.
        /// </summary>
        public void GoBack()
        {
            DialogueScript s = Script;
            DialogueScript.Node node = CurrentNode;
            if (s == null || node == null) return;
            _message = string.IsNullOrEmpty(node.BackLabel) ? Strings.Get("dialogue.back", DialogueScript.DefaultBackLabel) : node.BackLabel;
            if (_nodeHistory.Count > 0)
            {
                _currentNodeId = _nodeHistory[_nodeHistory.Count - 1];
                _nodeHistory.RemoveAt(_nodeHistory.Count - 1);
            }
            else _currentNodeId = s.Root != null ? s.Root.Id : "";
        }

        /// <summary>
        /// 첫 번째 선택지를 고른다. UI 버튼에서 쓴다.
        /// 입력값은 없으며, ChooseOption(0)과 같은 상태를 바꾼다.
        /// </summary>
        public void ChooseOption0() => ChooseOption(0);

        /// <summary>
        /// 두 번째 선택지를 고른다. UI 버튼에서 쓴다.
        /// 입력값은 없으며, ChooseOption(1)과 같은 상태를 바꾼다.
        /// </summary>
        public void ChooseOption1() => ChooseOption(1);

        /// <summary>
        /// 세 번째 선택지를 고른다. UI 버튼에서 쓴다.
        /// 입력값은 없으며, ChooseOption(2)와 같은 상태를 바꾼다.
        /// </summary>
        public void ChooseOption2() => ChooseOption(2);

        /// <summary>
        /// 네 번째 선택지(보통 뒤로가기)를 고른다. UI 버튼에서 쓴다.
        /// 입력값은 없으며, ChooseOption(3)과 같은 상태를 바꾼다.
        /// </summary>
        public void ChooseOption3() => ChooseOption(3);

        /// <summary>
        /// 공통 선택지를 고르고 대화를 시작 장면으로 되돌린다. 신뢰, 동공, 파편, 이야기 기록은 그대로 둔다.
        /// 입력값은 없으며, 지금 장면과 지나온 장면 목록, 문구를 바꾼다.
        /// </summary>
        public void ReturnToStart()
        {
            if (!CanReturnToStart) return;
            // 스펙 004: CSV 대화의 시작 장면으로 돌아가고 지나온 장면 목록을 비운다.
            _currentNodeId = Script.Root.Id;
            _nodeHistory.Clear();
            _message = _returnMessage;
        }

        /// <summary>
        /// 지금 케이스에서 모은 파편 목록을 글로 만든다.
        /// 활성화된 파편들을 사용하며, 화면에 보일 요약 글을 반환한다.
        /// </summary>
        public string CollectedSummary()
        {
            MemoryFragment[] all = Fragments;
            string[] got = all.Where(f => f.Collected).Select(f => "· " + f.Title).ToArray();
            return Strings.Get("ui.fragments", "파편들 ({got}/{all})", ("got", got.Length), ("all", all.Length)) + "\n"
                + (got.Length > 0 ? string.Join("\n", got) : Strings.Get("ui.fragments_none", "· 아직 없음"));
        }

        /// <summary>
        /// 만남을 시작할 때 안내 문구를 보여준다. 휴식 신호를 보고 쉬어 준 뒤라면 부담감을 줄이고 돌아온 문구를 보여준다.
        /// first(처음 만나는지)와 _restAccepted를 사용하며, 문구와 _burden, _restAccepted를 바꾼다.
        /// </summary>
        public void ShowEncounterGuide(bool first)
        {
            _message = first ? _firstGuide : _againGuide;
            if (!_restAccepted) return;
            // 스펙 011: 막대가 내려가는 모습이 보이도록 돌아왔을 때 줄인다.
            _restAccepted = false;
            _burden = Mathf.Max(0f, _burden - _burdenRestRelief);
            _message = _restReturnMessage;
        }

        /// <summary>
        /// 휴식 신호 중 Q를 눌러 쉬어 주기로 한 것을 기록한다.
        /// 입력값은 없으며, _restRequested와 _restAccepted를 바꾼다.
        /// </summary>
        public void AcceptRest()
        {
            _restRequested = false;
            _restAccepted = true;
        }

        /// <summary>
        /// 만남을 처음 상태로 되돌린다.
        /// 입력값은 없으며, 모드와 대화 진행, 상대 상태, 안내 문구, 파편, 층 회전, 시선 위치를 초기화한다.
        /// </summary>
        public void ResetAll()
        {
            _mode = Mode.Talk;
            _inAbyss = false;
            _reading = false;
            _heartIndex = 0;
            _currentEnding = "";
            _endingLines = null;
            _responseLines.Clear();
            _pendingGoto = null;
            _usedOptions.Clear();
            if (_core != null) _core.ResetState();
            DialogueScript script = Script;
            _currentNodeId = script != null && script.Root != null ? script.Root.Id : "";
            _nodeHistory.Clear();
            // 이 대화의 선택지가 켜는 플래그는 만남을 다시 시작하면 끈다 (단서처럼 필드에서 켠 플래그는 유지).
            if (script != null) foreach (string f in script.FlagsSet()) Flags.Set(f, false);
            _message = IntroText;
            _blinkAmount = 0f;
            _blinkPhase = -1f;
            _blinkElapsed = 0f;
            // 스펙 009: 진행 중인 화면 회전 각도를 바로 지움
            ClearBlinkVisual();
            // 스펙 010: 진행 중인 벽 변화 연출을 바로 정리
            _wallChange.Finish();
            _respawnEffectT = -1f;
            // 스펙 019: 해금 안내를 지운다.
            _newTalkReady = false;
            // 스펙 007: 밀려남과 초점 요동을 눈물 초점 복원보다 먼저 멈춘다.
            StopBump();
            // 스펙 007: 잠긴 문 접촉 횟수를 0으로 되돌린다.
            ResetGatePush();
            // 스펙 011: 휴식 상태와 눈물 신호를 바로 해제
            _restRequested = false;
            _restAccepted = false;
            _tearCue.Clear(_maze, _eye, _pupil);
            // 스펙 015: 감정을 시작 감정으로 되돌리고 눈 표현과 사물 표시를 맞춤
            ResetEmotion();
            // 스펙 006: 깜빡임으로 돌아간 미로를 원래 각도로 복원
            SetMazeTurn(0f);
            _blinkRecoverT = 0f;
            _burden = 0f;
            _velocity = Vector2.zero;
            // 스펙 004: 옳은 대답으로 얻은 신뢰도 함께 초기화
            if (_person != null) { _person.Trust = 0; _person.EarnedTrust = 0; _person.Dilation = 0.2f; }
            ShowEncounterGuide(true);
            foreach (MemoryFragment f in Fragments) f.Collected = false;
            // 스펙 016: 기억의 자리를 지우고, 회상 뒤에 나타나는 파편을 다시 숨긴다
            ResetFlashbackFragments();
            // 스펙 017: 든 실을 없애고, 이 손님 쌍의 결과 플래그와 이은 뒤 남은 실을 되돌린다
            ResetLinks();
            if (_maze != null) foreach (MazeLayer l in _maze.Layers) l.Offset = 0;
            if (_cursor != null) { _cursor.SnapTo(_startRing, _startSector); _cursor.Visible = false; }
        }

        /// <summary>
        /// 스펙 016: 지금 손님 파편의 회상 본 상태를 지우고, _appearAfter가 있는 파편은 줍지 않은 상태로 숨긴다. 숨기기는 Play 중에만 한다.
        /// _activeCase를 사용하며, 파편의 회상 본 상태, 수집 상태, 활성 상태와 _pickupWait, _flashbackFragment를 바꾼다.
        /// </summary>
        private void ResetFlashbackFragments()
        {
            _pickupWait.Clear();
            _flashbackFragment = null;
            if (_activeCase == null) return;
            foreach (MemoryFragment f in _activeCase.Fragments())
            {
                f.ClearSeen();
                if (string.IsNullOrEmpty(f.AppearAfter)) continue;
                f.Collected = false;
                if (Application.isPlaying) f.Hide();
            }
        }

        /// <summary>
        /// 스펙 017: 든 실을 없애고, 지금 손님 쌍 중 이은 쌍의 결과 플래그를 끄며, 그 파편들의 이은 뒤 남은 실을 지운다.
        /// _thread, _activeCase, _linkedResults를 사용하며, 결과 플래그, _linkedResults, _spotUnderCursor, 실 상태를 바꾼다.
        /// </summary>
        private void ResetLinks()
        {
            _spotUnderCursor = null;
            if (_thread != null) _thread.Release();
            if (_activeCase == null) return;
            foreach (MemoryLinkScript.Link l in CaseLinks())
            {
                if (!_linkedResults.Remove(l.Result)) continue;
                Flags.Set(l.Result, false);
            }
            if (_thread != null) _thread.ClearLinked(_activeCase.Fragments());
        }

        // 감정

        /// <summary>
        /// 스펙 015: 감정을 바꾸고 눈 표현 전환을 시작한다. 사물 표시는 _emotionMazeDelay초 뒤에 바꾼다.
        /// 전환 중에 다시 바뀌면 진행 중인 전환을 끝 상태로 건너뛴다. 같은 감정이거나 에디트 모드면 아무것도 하지 않는다.
        /// emotion을 사용하며, _emotion과 눈 표현 전환 상태, 사물 표시를 바꾼다.
        /// </summary>
        private void ChangeEmotion(ClientEmotion emotion)
        {
            if (!Application.isPlaying || emotion == _emotion) return;
            if (_lookT >= 0f)
            {
                _lookNow.CopyFrom(_lookTo);
                _lookT = -1f;
            }
            if (_propDelayT >= 0f)
            {
                _propDelayT = -1f;
                FadeProps();
            }
            foreach (MazeProp p in _props) p.Finish();

            _emotion = emotion;
            _lookFrom.CopyFrom(_lookNow);
            _lookTo.CopyFrom(LookOf(emotion));
            _lookT = 0f;
            _propDelayT = Mathf.Max(0f, _emotionMazeDelay);
        }

        /// <summary>
        /// 스펙 015: 감정 전환을 한 프레임 진행하고 눈 표현을 눈 그림과 동공에 전달한다. 기다린 시간이 지나면 사물 표시를 바꾼다.
        /// 들여다보는 동안은 시선 피함을 0으로 두고, 대화로 돌아오면 _emotionEyeSeconds초에 걸쳐 되돌린다.
        /// dt와 전환 상태를 사용하며, _lookNow, _lookT, _propDelayT, _gazeShiftWeight와 눈 그림, 동공의 감정 값을 바꾼다.
        /// </summary>
        private void TickEmotion(float dt)
        {
            float eyeSeconds = Mathf.Max(0.01f, _emotionEyeSeconds);
            if (_lookT >= 0f)
            {
                _lookT += dt;
                float t = Mathf.Clamp01(_lookT / eyeSeconds);
                _lookNow.Lerp(_lookFrom, _lookTo, Mathf.SmoothStep(0f, 1f, t));
                if (t >= 1f) _lookT = -1f;
            }
            if (_propDelayT >= 0f)
            {
                _propDelayT -= dt;
                if (_propDelayT <= 0f)
                {
                    _propDelayT = -1f;
                    FadeProps();
                }
            }
            _gazeShiftWeight = _mode == Mode.Gaze ? 0f : Mathf.MoveTowards(_gazeShiftWeight, 1f, dt / eyeSeconds);
            ApplyEmotionLook();
        }

        /// <summary>
        /// 스펙 015: 지금 눈 표현 값을 눈 그림과 동공의 감정 입력에 쓴다. 시선 피함은 _gazeShiftMax 길이로 줄이고 적용 비율을 곱한다.
        /// _lookNow, _gazeShiftMax, _gazeShiftWeight를 사용하며, 눈 그림과 동공의 감정 값을 바꾼다.
        /// </summary>
        private void ApplyEmotionLook()
        {
            if (_eye == null || _pupil == null) return;
            _eye.EmotionClosed = _lookNow.LidClosed;
            _eye.EmotionTint = _lookNow.IrisTint;
            _eye.EmotionTearLevel = _lookNow.TearLevel;
            _pupil.EmotionTrembleDistance = _lookNow.TrembleDistance;
            _pupil.EmotionTrembleSpeed = _lookNow.TrembleSpeed;
            _pupil.EmotionShift = Vector2.ClampMagnitude(_lookNow.GazeShift, _gazeShiftMax) * _gazeShiftWeight;
        }

        /// <summary>
        /// 스펙 015: 감정을 지금 손님의 시작 감정으로 되돌리고, 눈 표현과 사물 표시를 연출 없이 그 감정에 맞춘다.
        /// _activeCase를 사용하며, _emotion, 전환 상태, _props와 사물 표시, 눈 그림과 동공의 감정 값을 바꾼다.
        /// </summary>
        private void ResetEmotion()
        {
            if (_activeCase != null) _emotion = _activeCase.StartEmotion;
            _lookT = -1f;
            _propDelayT = -1f;
            _gazeShiftWeight = 1f;
            _lookNow.CopyFrom(LookOf(_emotion));
            CollectProps();
            foreach (MazeProp p in _props) p.SetShown(p.ShowsIn(_emotion));
            ApplyEmotionLook();
        }

        /// <summary>
        /// 스펙 015: 지금 손님의 사물을 다시 모으고, 사물 표시를 기다리는 중이 아니면 지금 감정과 다른 사물만 연출 없이 맞춘다.
        /// _activeCase, _emotion, _propDelayT를 사용하며, _props와 사물 표시를 바꾼다.
        /// </summary>
        private void SyncProps()
        {
            CollectProps();
            if (_propDelayT >= 0f) return;
            foreach (MazeProp p in _props)
                if (p.Visible != p.ShowsIn(_emotion)) p.SetShown(p.ShowsIn(_emotion));
        }

        /// <summary>
        /// 스펙 015: 각 사물을 지금 감정에서 보일지에 따라 _propFadeSeconds초에 걸쳐 나타내거나 사라지게 한다.
        /// _emotion, _propFadeSeconds, _propRevealSeconds를 사용하며, 사물의 표시 상태를 바꾼다.
        /// </summary>
        private void FadeProps()
        {
            foreach (MazeProp p in _props) p.FadeTo(p.ShowsIn(_emotion), _propFadeSeconds, _propRevealSeconds);
        }

        /// <summary>
        /// 스펙 015: 지금 손님의 사물 묶음에서 사물을 모은다.
        /// _activeCase를 사용하며, _props를 다시 채운다.
        /// </summary>
        private void CollectProps()
        {
            _props.Clear();
            if (_activeCase != null) _props.AddRange(_activeCase.Props());
        }

        /// <summary>
        /// 스펙 015: 지금 손님의 감정 눈 표현을 찾는다. 없으면 기본 표현을 쓴다.
        /// emotion과 _activeCase를 사용하며, 눈 표현을 반환한다.
        /// </summary>
        private EmotionLook LookOf(ClientEmotion emotion)
        {
            EmotionLook look = _activeCase != null ? _activeCase.LookOf(emotion) : null;
            return look ?? DEFAULT_EMOTION_LOOK;
        }

        // 내부 처리

        /// <summary>
        /// 만남 화면의 키 입력을 처리한다. 파편을 읽는 중에는 넘기기 입력만 받는다.
        /// dt를 사용하며, 시선 이동과 시선 거두기, 대화 선택, 처음부터를 실행한다.
        /// </summary>
        private void HandleInput(float dt)
        {
            Vector2 move = Vector2.zero;
            Keyboard kb = Keyboard.current;
            Gamepad pad = Gamepad.current;
            if (_reading)
            {
                // 찍히는 중의 Space는 GameFlow가 글자를 다 보여주는 데 쓰고, 다 찍힌 뒤의 입력으로 계속한다.
                bool next = (kb != null && (kb.enterKey.wasPressedThisFrame || kb.spaceKey.wasPressedThisFrame))
                            || (pad != null && pad.buttonSouth.wasPressedThisFrame);
                // 스펙 016: 회상 중의 진행 키는 회상 재생기가 받으므로 읽기를 끝내지 않는다
                if (next && !FlashbackPlayer.IsPlaying && _readT > 0.3f && !Typewriter.AnyTyping && !_typingPrev) { _reading = false; RespawnMaze(); }
                Steer(Vector2.zero, dt);
                return;
            }
            if (kb != null)
            {
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move.y += 1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move.y -= 1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) move.x += 1f;
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) move.x -= 1f;

                // Space를 누르면 들여다보거나 시선을 거둔다. 만남 화면에 막 들어온 프레임의 누름은 무시한다.
                // 결과 줄을 넘기는 동안 Space와 숫자는 받지 않는다. 줄 넘기기는 GameFlow가 한다.
                if (kb.spaceKey.wasPressedThisFrame && _acceptedLastFrame && !BlockTalkKeys) ToggleGaze();
                if (kb.rKey.wasPressedThisFrame) ResetAll();
                if (_mode == Mode.Talk && !BlockTalkKeys)
                {
                    // 스펙 004: 공통 선택지의 번호가 바뀌므로 모든 숫자키를 같은 처리로 보냄
                    if (kb.digit1Key.wasPressedThisFrame) PressChoice(0);
                    else if (kb.digit2Key.wasPressedThisFrame) PressChoice(1);
                    else if (kb.digit3Key.wasPressedThisFrame) PressChoice(2);
                    else if (kb.digit4Key.wasPressedThisFrame) PressChoice(3);
                }
            }
            if (pad != null)
            {
                Vector2 stick = pad.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.04f) move += stick;
                if (pad.buttonSouth.wasPressedThisFrame && _acceptedLastFrame && !BlockTalkKeys) ToggleGaze();
            }
            Steer(move, dt);
        }

        /// <summary>
        /// 안쪽/바깥쪽 이동을 벽과 문에 맞춰 보정한다.
        /// r, a(참조), dr을 사용하며, 보정된 반지름과 각도를 r, a에 쓴다. 가장 안쪽을 지나면 심연에 들어간다.
        /// 벽이나 잠긴 문에 막히면 막힌 방향을 _hitRadial에 기록한다.
        /// </summary>
        private void ResolveRadial(ref float r, ref float a, float dr)
        {
            int ring = _maze.RingAt(r);
            int s = _maze.SectorAt(a);
            float m = _wallMargin;
            float outerB = _maze.RingOuter(ring);
            float innerB = _maze.RingInner(ring);
            float target = r + dr;

            if (dr > 0f)
            {
                // 바깥쪽으로 움직인다.
                if (target <= outerB - m) { r = target; return; }
                if (ring > 0 && CanCrossArc(ring - 1, s, r, ref a)) { r = target; return; }
                r = Mathf.Min(r, outerB - m);
                // 스펙 007: 바깥쪽으로 가다 막혔음을 기록한다.
                _hitRadial = 1;
            }
            else
            {
                // 안쪽으로 움직인다.
                if (target >= innerB + m) { r = target; return; }
                if (CanCrossArc(ring, s, r, ref a))
                {
                    r = target;
                    if (ring == _maze.Rings - 1 && r < innerB) EnterAbyss();
                    return;
                }
                r = Mathf.Max(r, innerB + m);
                // 스펙 007: 안쪽으로 가다 막혔음을 기록한다.
                _hitRadial = -1;
            }
        }

        /// <summary>
        /// 둘레 방향 이동을 칸막이 벽에 맞춰 보정한다.
        /// r, a(참조), dTan을 사용하며, 보정된 반지름과 각도를 r, a에 쓴다.
        /// 칸막이 벽에 막히면 막힌 방향을 _hitTangent에 기록한다.
        /// </summary>
        private void ResolveAngular(ref float r, ref float a, float dTan)
        {
            int ring = _maze.RingAt(r);
            int s = _maze.SectorAt(a);
            float d = _maze.SectorDeg;
            float m = _wallMargin;
            float rr = Mathf.Max(0.2f, r);
            float dDeg = dTan / rr * Mathf.Rad2Deg;
            float mDeg = m / rr * Mathf.Rad2Deg;
            float local = Mathf.Repeat(a, 360f) - s * d;
            float target = local + dDeg;

            if (dDeg > 0f)
            {
                // 반시계 방향으로 움직인다.
                if (target <= d - mDeg) { a += dDeg; return; }
                // 스펙 015: 사물이 막은 칸막이 길목은 지나지 않음
                if (CanCrossRadial(ring, s, 1)) { KeepInBand(ref r, ring); a += dDeg; return; }
                a = s * d + Mathf.Max(local, d - mDeg);
                // 스펙 007: 반시계 방향으로 가다 막혔음을 기록한다.
                _hitTangent = 1;
            }
            else
            {
                // 시계 방향으로 움직인다.
                if (target >= mDeg) { a += dDeg; return; }
                // 스펙 015: 사물이 막은 칸막이 길목은 지나지 않음
                if (CanCrossRadial(ring, s, -1)) { KeepInBand(ref r, ring); a += dDeg; return; }
                a = s * d + Mathf.Min(local, mDeg);
                // 스펙 007: 시계 방향으로 가다 막혔음을 기록한다.
                _hitTangent = -1;
            }
        }

        /// <summary>
        /// 원호 통로를 지날 수 있는지 판단한다. 통로 가장자리에 걸쳐 있으면 살짝 가운데로 끌어당긴다.
        /// arcRing, sector, r, a(참조)를 사용하며, 지날 수 있으면 true를 반환하고 a를 보정한다.
        /// 스펙 015: 이 원호 길목을 사물이 막고 있으면 지나지 않는다.
        /// </summary>
        private bool CanCrossArc(int arcRing, int sector, float r, ref float a)
        {
            if (!_maze.IsArcOpen(arcRing, sector, out MazeGate gate)) return false;
            if (gate != null && !gate.CanPass(_person)) { GateBlocked(gate); return false; }
            // 스펙 015: 이 원호 길목을 사물이 막고 있으면 지나지 않음. 사물 판정은 GateBlocked를 부르지 않는다.
            int from = _maze.RingAt(r);
            int to = from == arcRing ? arcRing + 1 : arcRing;
            if (PropBlocksFace(MazeWall.Kind.Arc, arcRing, sector, new Vector2(to < from ? -1f : 1f, 0f))) return false;

            float d = _maze.SectorDeg;
            float mDeg = _wallMargin / Mathf.Max(0.2f, r) * Mathf.Rad2Deg;
            float local = Mathf.Repeat(a, 360f) - sector * d;
            if (local < mDeg) a += mDeg - local;
            else if (local > d - mDeg) a -= local - (d - mDeg);
            return true;
        }

        /// <summary>
        /// 반지름을 고리 안쪽 띠 범위로 맞춘다.
        /// r(참조), ring을 사용하며, r을 보정한다.
        /// </summary>
        private void KeepInBand(ref float r, int ring)
        {
            float m = _wallMargin;
            r = Mathf.Clamp(r, _maze.RingInner(ring) + m, _maze.RingOuter(ring) - m);
        }

        /// <summary>
        /// 스펙 015: 칸막이 벽을 넘어 옆 칸으로 갈 수 있는지 판단한다. 칸막이 길목을 사물이 막고 있으면 지나지 않는다.
        /// ring, sector(지금 칸), dir(+1 반시계, -1 시계)을 사용하며, 지날 수 있으면 true를 반환한다. 밀어냄 사물이면 밀려남을 시작한다.
        /// </summary>
        private bool CanCrossRadial(int ring, int sector, int dir)
        {
            int boundary = _maze.Mod(dir > 0 ? sector : sector - 1);
            if (!_maze.IsRadialOpen(ring, boundary)) return false;
            // 스펙 015: 이 칸막이 길목을 사물이 막고 있으면 지나지 않음
            return !PropBlocksFace(MazeWall.Kind.Radial, ring, boundary, new Vector2(0f, -dir));
        }

        /// <summary>
        /// 스펙 015: 지나려는 길목을 막고 있는 사물이 있으면 막는다. 밀어냄 사물이면 밀려남을 시작한다.
        /// kind, ring, worldSector(화면 기준 면), pushDir(밀려나는 방향, x 바깥쪽, y 반시계)을 사용하며, 막았으면 true를 반환한다.
        /// </summary>
        private bool PropBlocksFace(MazeWall.Kind kind, int ring, int worldSector, Vector2 pushDir)
        {
            foreach (MazeProp p in _props)
            {
                if (!p.Blocks || p.PassageKind != kind || p.PassageRing != ring || p.WorldPassageSector != worldSector) continue;
                if (p.Kind == MazePropKind.Push) StartPropPush(pushDir);
                // 스펙 015 FR-020: 막은 사물이 무엇이고 왜 막는지 적은 글을 보인다
                ShowPropBlockedText(p);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 스펙 015: 들여다보는 동안 사물의 반응을 처리한다. 시선이 다가오면 다가가면 막음 사물을 뒤집고, 어지러움 사물 주변 벽을 흔든다.
        /// _mode, _reading, _cursor, _props, _maze를 사용하며, 사물의 뒤집힘 상태와 벽 흔들림을 바꾼다. 들여다보지 않으면 흔들림을 모두 멈춘다.
        /// </summary>
        private void TickPropReactions()
        {
            bool gazing = _mode == Mode.Gaze && !_reading && _cursor != null && _maze != null;
            _wobbleNow.Clear();
            if (gazing)
            {
                Vector3 gaze = _cursor.transform.position;
                foreach (MazeProp p in _props)
                {
                    if (p.CanFlip && Vector2.Distance(gaze, p.PassageCenterWorld) <= p.FlipDistance) p.Flip();
                    if (p.DizzyRadius > 0f && p.Visible) CollectWobble(p, gaze);
                }
            }
            foreach (MazeWall w in _wobbling)
                if (w != null && !_wobbleNow.ContainsKey(w)) w.SetWobble(0f, 0f);
            _wobbling.Clear();
            foreach (KeyValuePair<MazeWall, (float amp, float speed)> pair in _wobbleNow)
            {
                pair.Key.SetWobble(pair.Value.amp, pair.Value.speed);
                _wobbling.Add(pair.Key);
            }
        }

        /// <summary>
        /// 스펙 015: 시선이 어지러움 사물에 가까우면 그 사물 둘레의 벽마다 흔들림 폭을 정한다. 시선이 사물에 가까울수록, 벽이 사물에 가까울수록 크다.
        /// p(사물), gaze(시선 월드 위치), _maze의 벽을 사용하며, _wobbleNow에 벽별 가장 큰 폭과 빠르기를 기록한다.
        /// </summary>
        private void CollectWobble(MazeProp p, Vector3 gaze)
        {
            Vector3 center = p.PassageCenterWorld;
            float near = 1f - Vector2.Distance(gaze, center) / p.DizzyRadius;
            if (near <= 0f) return;
            foreach (MazeWall w in _maze.GetComponentsInChildren<MazeWall>())
            {
                float wd = Vector2.Distance(w.transform.TransformPoint(w.MidLocal), center);
                if (wd > p.DizzyRadius) continue;
                float amp = p.DizzyAmplitude * near * (1f - wd / p.DizzyRadius);
                if (!_wobbleNow.TryGetValue(w, out (float amp, float speed) old) || old.amp < amp) _wobbleNow[w] = (amp, p.DizzySpeed);
            }
        }

        /// <summary>
        /// 스펙 015: 밀어냄 사물에 닿아 밀려남과 번쩍임을 시작한다. _bumpEnabled와 무관하며 부담감과 초점 요동은 바꾸지 않는다.
        /// dir(밀려나는 방향, x 바깥쪽, y 반시계)과 _bumpCooldown을 사용하며, _knockbackDir, _knockbackT, _bumpCooldownT, _velocity, _bumpFlash를 바꾼다.
        /// </summary>
        private void StartPropPush(Vector2 dir)
        {
            // 밀려나는 중에는 새로 밀어내지 않는다. 대기 시간을 채워 같은 프레임의 벽 부딪힘이 겹치지 않게 한다.
            if (!Application.isPlaying || _mode != Mode.Gaze || _inAbyss || _reading) return;
            // 수정(015 FR-007): 먼저 막힌 축 하나로만 밀던 것을 같은 프레임에 두 축이 막히면 두 방향을 더해 정규화하게 바꿨다.
            if (_knockbackT == 0f)
            {
                float x = dir.x != 0f ? dir.x : Mathf.Sign(_knockbackDir.x) * (_knockbackDir.x != 0f ? 1f : 0f);
                float y = dir.y != 0f ? dir.y : Mathf.Sign(_knockbackDir.y) * (_knockbackDir.y != 0f ? 1f : 0f);
                _knockbackDir = new Vector2(x, y).normalized;
                return;
            }
            if (_knockbackT >= 0f) return;
            _knockbackDir = dir;
            _knockbackT = 0f;
            _bumpCooldownT = _bumpCooldown;
            _velocity = Vector2.zero;
            _bumpFlash = Mathf.Max(_bumpFlash, 0.6f);
        }

        /// <summary>
        /// 스펙 015 FR-020: 사물에 막혔을 때 그 사물의 글을 문구로 보인다. key가 비었거나 글이 없으면 아무것도 하지 않는다.
        /// prop의 BlockedKey와 Strings.csv를 사용하며, 문구를 바꾼다.
        /// </summary>
        private void ShowPropBlockedText(MazeProp prop)
        {
            if (!Application.isPlaying || string.IsNullOrEmpty(prop.BlockedKey)) return;
            string text = Personalize(Strings.Get(prop.BlockedKey, ""));
            // 같은 글이 이미 보이면 다시 쓰지 않아 글자가 처음부터 다시 찍히지 않게 한다.
            if (text.Length > 0 && _message != text) _message = text;
        }

        /// <summary>
        /// 잠긴 문에 막혔을 때 문구를 띄우고 시선을 번쩍이게 한다.
        /// gate를 사용하며, 문구와 _bumpFlash를 바꾼다.
        /// </summary>
        private void GateBlocked(MazeGate gate)
        {
            if (_message != gate.LockedMessage) _message = gate.LockedMessage;
            _bumpFlash = Mathf.Max(_bumpFlash, 0.6f);
            // 스펙 007: 문에서 떨어져 있다 다시 막힐 때만 접촉 1회로 센다.
            if (!Application.isPlaying) return;
            bool newPush = _gateIdleT >= _gatePushGap;
            _gateIdleT = 0f;
            if (newPush) CountGatePush();
        }

        /// <summary>
        /// 잠긴 문 접촉을 1회 세고, 횟수가 _gatePushCount에 닿으면 부담감을 올린 뒤 횟수를 0으로 되돌린다.
        /// _gatePushCount, _gatePushBurden, _burdenLimit을 사용하며, _gatePushes, _gatePushWindowT, _burden을 바꾸고 부담감이 가득 차면 상담을 끝낸다.
        /// </summary>
        private void CountGatePush()
        {
            if (_gatePushes == 0) _gatePushWindowT = 0f;
            _gatePushes++;
            if (_gatePushes < _gatePushCount) return;
            _gatePushes = 0;
            _burden = Mathf.Min(1f, _burden + _gatePushBurden);
            if (_burden >= _burdenLimit) EndByBurden();
        }

        /// <summary>
        /// 잠긴 문에서 떨어져 있던 시간과 첫 접촉 뒤 시간을 잰다. 제한 시간이 지나면 접촉 횟수를 0으로 되돌린다.
        /// dt와 _gatePushWindow를 사용하며, _gateIdleT, _gatePushWindowT, _gatePushes를 바꾼다.
        /// </summary>
        private void TickGatePush(float dt)
        {
            _gateIdleT += dt;
            if (_gatePushes == 0) return;
            _gatePushWindowT += dt;
            if (_gatePushWindowT >= _gatePushWindow) ResetGatePush();
        }

        /// <summary>
        /// 잠긴 문 접촉 횟수와 제한 시간을 처음 상태로 되돌린다.
        /// 입력값은 없으며, _gatePushes와 _gatePushWindowT를 바꾼다.
        /// </summary>
        private void ResetGatePush()
        {
            _gatePushes = 0;
            _gatePushWindowT = 0f;
        }

        /// <summary>
        /// 시선이 움직인 뒤 층이 바뀌었는지와 파편에 닿았는지 확인한다.
        /// 시선 위치를 사용하며, 문구와 마지막 층 기록을 바꾼다.
        /// </summary>
        private void OnMoved()
        {
            if (_mode != Mode.Gaze || _cursor == null) return;
            int layer = _maze.LayerOfRing(_cursor.Ring);
            if (layer != _lastLayer)
            {
                MazeLayer l = _maze.GetLayer(layer);
                if (layer > _lastLayer && l != null) _message = Strings.Get("msg.deeper", "더 깊은 곳으로 들어왔다 — {layer}", ("layer", l.DisplayName));
                _lastLayer = layer;
            }
            CheckFragment();
        }

        /// <summary>
        /// 시선이 있는 칸의 파편을 줍는다. Play 모드에서는 읽는 동안 멈춘다.
        /// 시선 위치와 파편들을 사용하며, 파편의 수집 상태와 문구, 읽기 상태를 바꾼다.
        /// </summary>
        private void CheckFragment()
        {
            Vector3 pos = _cursor.LocalPos;
            // 스펙 016: 시선이 벗어난 칸의 새 파편은 다시 주울 수 있게 한다
            if (_pickupWait.Count > 0) ReleasePickupWait();
            foreach (MemoryFragment f in Fragments)
            {
                if (f.Collected || f.Ring != _cursor.Ring || f.WorldSector != _cursor.Sector) continue;
                if (Vector3.Distance(pos, _maze.CellLocal(f.Ring, f.WorldSector)) > _maze.RingWidth * 0.55f) continue;
                // 스펙 016: 새로 나타난 파편은 시선이 그 칸을 벗어났다 다시 들어올 때 줍는다
                if (_pickupWait.Contains(f)) continue;
                // 스펙 019 FR-001: 줍기 전에 잠겨 있던 선택지를 기억해 두고, 주운 뒤 열린 것이 있으면 해금 안내를 켠다
                if (Application.isPlaying) CollectLockedChoices();
                f.Collected = true;
                if (Application.isPlaying) CheckUnlockedChoices();
                // 스펙 016: 회상 방과 대본이 있으면 글 대신 회상을 재생한다
                bool flashback = Application.isPlaying && _flashback != null && _flashback.CanPlay(f.Flag, f.Flag);
                if (!flashback) _message = Strings.Get("msg.fragment", "파편 — {title}\n{text}", ("title", f.Title), ("text", f.Text));
                if (Application.isPlaying) { _reading = true; _readT = 0f; _velocity = Vector2.zero; StopBump(); }
                if (!flashback) continue;
                _flashbackFragment = f;
                _flashback.Play(f.Flag, f.Flag, FinishFlashback);
                return;
            }
            // 스펙 017: 파편을 줍지 않았으면 기억의 자리 진입으로 실을 들거나 잇는다
            CheckMemorySpot();
        }

        /// <summary>
        /// 스펙 019 FR-001: 지금 손님 대화의 모든 장면에서 아직 고를 수 없는 선택지를 모은다.
        /// Script의 장면들과 플래그를 사용하며, _lockedChoices를 다시 채운다.
        /// </summary>
        private void CollectLockedChoices()
        {
            _lockedChoices.Clear();
            DialogueScript script = Script;
            if (script == null) return;
            foreach (DialogueScript.Node node in script.Nodes)
                for (int i = 0; i < node.Choices.Count; i++)
                    if (!IsAvailable(node.Choices[i])) _lockedChoices.Add((node, i));
        }

        /// <summary>
        /// 스펙 019 FR-001: 방금까지 잠겨 있다가 고를 수 있게 된 선택지가 하나라도 있으면 해금 안내를 켠다.
        /// _lockedChoices와 플래그를 사용하며, _newTalkReady를 바꾸고 _lockedChoices를 비운다.
        /// </summary>
        private void CheckUnlockedChoices()
        {
            foreach ((DialogueScript.Node node, int index) in _lockedChoices)
                if (IsAvailable(node.Choices[index])) _newTalkReady = true;
            _lockedChoices.Clear();
        }

        /// <summary>
        /// 스펙 019: 파편에 다시 재생할 회상(회상 방과 대본)이 있는지 판단한다.
        /// fragment의 플래그와 _flashback을 사용하며, 재생할 수 있으면 true를 반환한다.
        /// </summary>
        public bool CanReplay(MemoryFragment fragment) => _flashback != null && _flashback.CanPlay(fragment.Flag, fragment.Flag);

        /// <summary>
        /// 스펙 019 FR-012: 이미 본 파편의 회상을 처음부터 다시 재생한다. 재생하는 동안 읽기 상태로 두어 이동과 입력을 멈춘다.
        /// fragment의 플래그를 사용하며, 읽기 상태와 속도, 밀려남, 회상 재생 상태를 바꾼다.
        /// </summary>
        public void ReplayFlashback(MemoryFragment fragment)
        {
            if (!CanOpenRecap || !CanReplay(fragment)) return;
            _reading = true;
            _readT = 0f;
            _velocity = Vector2.zero;
            StopBump();
            _flashback.Play(fragment.Flag, fragment.Flag, FinishReplay);
        }

        /// <summary>
        /// 스펙 019 FR-013: 다시 재생한 회상이 끝났을 때 읽기 상태만 끝낸다. 감정, 추가 파편, 미로 벽은 바꾸지 않는다.
        /// 입력값은 없으며, _reading을 바꾼다.
        /// </summary>
        private void FinishReplay() => _reading = false;

        /// <summary>
        /// 스펙 017: 시선이 새로 들어간 기억의 자리를 처리한다. 실이 없으면 들고, 다른 자리면 쌍을 찾아 잇거나 실을 새로 든다.
        /// 시선 위치, _spotUnderCursor, 든 실의 시작 기억, 이은 쌍 목록을 사용하며, _spotUnderCursor와 실 상태를 바꾸거나 잇기를 시작한다.
        /// </summary>
        private void CheckMemorySpot()
        {
            MemoryFragment spot = FindSpotUnderCursor();
            bool entered = spot != null && spot != _spotUnderCursor;
            _spotUnderCursor = spot;
            if (!entered || !Application.isPlaying || _thread == null) return;
            MemoryFragment from = _thread.From;
            if (from == spot) return;
            MemoryLinkScript.Link link = from != null ? MemoryLinkScript.Find(from.Flag, spot.Flag) : null;
            // 이어지지 않는 쌍이거나 이미 이은 쌍이면 다른 반응 없이 이 자리에서 실을 새로 든다.
            if (link == null || _linkedResults.Contains(link.Result))
            {
                _thread.Hold(spot);
                return;
            }
            StartLink(from, spot, link);
        }

        /// <summary>
        /// 스펙 017: 시선이 서 있는 칸의 기억의 자리(회상을 본 파편)를 찾는다. 파편을 줍는 거리와 같은 기준을 쓴다.
        /// 시선 위치와 지금 손님의 파편들을 사용하며, 찾은 파편 또는 null을 반환한다.
        /// </summary>
        private MemoryFragment FindSpotUnderCursor()
        {
            Vector3 pos = _cursor.LocalPos;
            foreach (MemoryFragment f in Fragments)
            {
                if (!f.Seen || !f.Collected || f.Ring != _cursor.Ring || f.WorldSector != _cursor.Sector) continue;
                if (Vector3.Distance(pos, _maze.CellLocal(f.Ring, f.WorldSector)) > _maze.RingWidth * 0.55f) continue;
                return f;
            }
            return null;
        }

        /// <summary>
        /// 스펙 017: 두 기억을 잇기 시작한다. 회상 방과 연결 회상 대본이 있고 room이 a나 b이면 연결 회상을 재생하고, 아니면 회상 없이 바로 잇는다.
        /// from, to(이을 두 기억)와 link(쌍)를 사용하며, 잇는 중인 상태와 읽기 상태를 바꾸고 회상을 시작한다.
        /// </summary>
        private void StartLink(MemoryFragment from, MemoryFragment to, MemoryLinkScript.Link link)
        {
            _linkFrom = from;
            _linkTo = to;
            _linkPair = link;
            bool roomOk = link.Room == link.A || link.Room == link.B;
            if (!roomOk || _flashback == null || !_flashback.CanPlay(link.Result, link.Room))
            {
                CompleteLink();
                return;
            }
            _reading = true;
            _readT = 0f;
            _velocity = Vector2.zero;
            StopBump();
            _flashback.Play(link.Result, link.Room, FinishLinkFlashback);
        }

        /// <summary>
        /// 스펙 017: 연결 회상이 끝났을 때 읽기 상태를 끝내고 미로를 다시 만든 뒤, 회상 뒤 감정을 바꾸고 잇기를 마친다.
        /// _linkPair와 연결 회상 대본의 emotion_after를 사용하며, _reading, 미로, _emotion과 잇기 결과를 바꾼다.
        /// </summary>
        private void FinishLinkFlashback()
        {
            _reading = false;
            RespawnMaze();
            FlashbackScript.Flashback script = FlashbackScript.Find(_linkPair.Result);
            if (script != null && script.EmotionAfter.HasValue) ChangeEmotion(script.EmotionAfter.Value);
            CompleteLink();
        }

        /// <summary>
        /// 스펙 017: 잇기를 마친다. 결과 플래그를 켜고 실을 없애며, 두 기억의 이은 상태를 켜고 두 자리 사이에 실을 남긴다.
        /// _linkFrom, _linkTo, _linkPair, _linkedMarkColor를 사용하며, 플래그, _linkedResults, 파편의 이은 상태, 실 상태를 바꾸고 잇는 중인 상태를 비운다.
        /// </summary>
        private void CompleteLink()
        {
            Flags.Set(_linkPair.Result, true);
            _linkedResults.Add(_linkPair.Result);
            _linkFrom.MarkLinked(_linkedMarkColor);
            _linkTo.MarkLinked(_linkedMarkColor);
            _thread.Release();
            _thread.AddLinked(_linkFrom, _linkTo);
            _linkFrom = null;
            _linkTo = null;
            _linkPair = null;
        }

        /// <summary>
        /// 스펙 016: 시선이 칸을 벗어난 새로 나타난 파편을 다시 주울 수 있게 한다.
        /// 시선 칸과 _pickupWait를 사용하며, _pickupWait에서 시선이 없는 칸의 파편을 뺀다.
        /// </summary>
        private void ReleasePickupWait()
        {
            _pickupWait.RemoveAll(f => f == null || f.Ring != _cursor.Ring || f.WorldSector != _cursor.Sector);
        }

        /// <summary>
        /// 스펙 016: 회상이 끝났을 때 읽기 상태를 끝내고 미로를 다시 만든 뒤, 회상 뒤 감정, 추가 파편, 기억의 자리를 처리한다.
        /// _flashbackFragment와 회상 대본의 emotion_after를 사용하며, _reading, 미로, _emotion, 추가 파편 표시, 파편의 회상 본 상태를 바꾼다.
        /// </summary>
        private void FinishFlashback()
        {
            MemoryFragment f = _flashbackFragment;
            _flashbackFragment = null;
            _reading = false;
            RespawnMaze();
            FlashbackScript.Flashback script = FlashbackScript.Find(f.Flag);
            if (script != null && script.EmotionAfter.HasValue) ChangeEmotion(script.EmotionAfter.Value);
            ShowFragmentsAfter(f.Flag);
            f.MarkSeen(_seenMarkColor);
            // 스펙 017: 회상이 끝난 자리는 벗어났다 다시 들어올 때 실을 든다
            _spotUnderCursor = f;
        }

        /// <summary>
        /// 스펙 016: _appearAfter가 flag인 지금 손님의 파편을 미로에 나타낸다. 시선이 서 있는 칸에 나타난 파편은 시선이 그 칸을 벗어날 때까지 줍지 않는다.
        /// flag와 _activeCase를 사용하며, 파편의 활성 상태와 _pickupWait를 바꾼다.
        /// </summary>
        private void ShowFragmentsAfter(string flag)
        {
            if (_activeCase == null || string.IsNullOrEmpty(flag)) return;
            foreach (MemoryFragment f in _activeCase.Fragments())
            {
                if (f.AppearAfter != flag || f.gameObject.activeSelf) continue;
                f.Appear();
                // 스펙 016: 시선이 서 있는 칸에 나타난 파편만 줍기를 미룬다
                if (f.Ring == _cursor.Ring && f.WorldSector == _cursor.Sector) _pickupWait.Add(f);
            }
        }

        /// <summary>
        /// 깜빡임을 한 프레임 진행한다. 구간 경계에서 시간을 멈춰 눈을 뜨기 전에 화면 회전이나 벽 변화 연출이 시작되지 않게 한다.
        /// dt와 깜빡임 시간 설정을 사용하며, _blinkPhase(경과 초), _blinkAmount, 문구, 미로 회전각, 화면 회전 각도, 벽과 벽 변화 연출을 바꾼다.
        /// </summary>
        private void AdvanceBlink(float dt)
        {
            // 스펙 009: 감긴 채 머문 뒤 눈을 뜨면서 재생 중인 후보대로 화면 회전을 진행한다.
            BlinkTurnPreset preset = _activePreset;
            float close = Mathf.Max(0.01f, preset.CloseSeconds);
            float open = Mathf.Max(0.01f, preset.OpenSeconds);
            float turn = Mathf.Max(0.01f, preset.TurnSeconds);
            float delay = preset.LayerOrder == BlinkLayerOrder.Together ? 0f : Mathf.Max(0f, preset.LayerDelaySeconds);
            float openFrom = _blinkOpenFrom;
            // 스펙 010: 회전이 아닌 후보는 벽 변화 연출을 재생한다.
            bool changesWalls = preset.ChangeKind != BlinkChangeKind.Rotate;

            float prev = _blinkPhase;
            float next = prev + dt;
            if (prev < close) next = Mathf.Min(next, close);
            else if (prev < openFrom) next = Mathf.Min(next, openFrom);
            _blinkPhase = next;

            // 스펙 010: 후보와 상관없이 깜빡임 문구를 하나로 표시
            if (prev < close && next >= close && _mode == Mode.Gaze) _message = Strings.Get("msg.blink_rotate", "눈이 깜빡였다.");
            if (prev < close && next >= close)
            {
                // 스펙 010: 후보 종류에 따라 벽 교체 또는 회전을 확정
                if (changesWalls) CommitBlinkWallChange();
                else CommitBlinkRotation();
            }
            // 스펙 009: 눈을 뜨기 시작한 뒤 후보의 출발 지연만큼 늦게 회전을 시작
            float startDelay = Mathf.Max(0f, preset.TurnStartDelaySeconds);
            if (_blinkTurning) ApplyBlinkVisual(next - openFrom - startDelay, turn, delay);
            if (_wallChange.IsPlaying) _wallChange.Tick(next - openFrom);

            if (next < close) _blinkAmount = Mathf.SmoothStep(0f, 1f, next / close);
            else if (next <= openFrom) _blinkAmount = 1f;
            else _blinkAmount = 1f - Mathf.SmoothStep(0f, 1f, (next - openFrom) / open);

            // 스펙 010: 벽 변화 후보는 연출 길이가 끝날 때까지 깜빡임을 이어 간다.
            // 스펙 009: 회전 후보의 전체 길이에 출발 지연을 포함
            float effectTotal = changesWalls ? _wallChange.Duration : startDelay + turn + delay * Mathf.Max(0, _blinkLayers.Length - 1);
            if (next < openFrom + Mathf.Max(open, effectTotal)) return;
            _blinkRecoverT = _blinkRecoverSeconds;
            _blinkPhase = -1f;
            _blinkAmount = 0f;
            ClearBlinkVisual();
            _wallChange.Finish();
        }

        /// <summary>
        /// 눈이 완전히 감긴 순간 시선이 있는 층을 뺀 층의 벽과 문을 새로 만들고 벽 변화 연출을 준비한다.
        /// 시선의 층을 사용하며, 미로의 벽과 _wallChange의 연출 상태를 바꾼다.
        /// </summary>
        private void CommitBlinkWallChange()
        {
            int keepLayer = _maze.LayerOfRing(_cursor.Ring);
            _wallChange.Capture(_maze, keepLayer);
            _maze.Generate(UnityEngine.Random.Range(1, 999999), keepLayer);
            _wallChange.Begin();
        }

        /// <summary>
        /// 눈이 완전히 감긴 순간 미로를 시선과 함께 최종 각도로 확정하고 화면 회전을 준비한다.
        /// 이번 회전량과 미로의 층 목록을 사용하며, 미로의 회전각, _blinkLayers, _blinkTurning을 바꾼다.
        /// </summary>
        private void CommitBlinkRotation()
        {
            // 스펙 009: 판정 각도는 눈이 감긴 순간 한 번에 확정한다.
            SetMazeTurn(_blinkTurnFrom + _blinkTurnDelta);
            _blinkLayers = _maze != null ? _maze.Layers : Array.Empty<MazeLayer>();
            _blinkTurning = true;
        }

        /// <summary>
        /// 층과 시선의 화면 표시 각도를 후보의 곡선에 맞춰 줄이고, 층이 도는 동안 벽과 문을 옅게 한다. 후보의 층 순서대로 시간차를 둔다.
        /// elapsed(회전이 출발한 뒤 지난 초), turn(층 하나의 회전 시간), delay(층 사이 시간차)를 사용하며, 층과 시선의 BlinkVisualDeg, 벽과 문의 표시 투명도를 바꾼다.
        /// </summary>
        private void ApplyBlinkVisual(float elapsed, float turn, float delay)
        {
            int cursorLayer = _maze != null && _cursor != null ? _maze.LayerOfRing(_cursor.Ring) : -1;
            for (int i = 0; i < _blinkLayers.Length; i++)
            {
                MazeLayer layer = _blinkLayers[i];
                float timeRatio = Mathf.Clamp01((elapsed - delay * i) / turn);
                float progress = _activePreset.Curve.Evaluate(timeRatio);
                float side = _activePreset.AlternateLayers && layer.Index % 2 == 1 ? -1f : 1f;
                float degrees = -_blinkTurnDelta * side * (1f - progress);
                layer.BlinkVisualDeg = degrees;
                // 스펙 009: 층이 도는 동안 벽과 문이 옅어졌다가 멈추면서 선명해짐
                layer.SetWallVisualAlpha(1f - _activePreset.WallFadeAmount * Mathf.Sin(timeRatio * Mathf.PI));
                if (layer.Index == cursorLayer) _cursor.BlinkVisualDeg = degrees;
            }
        }

        /// <summary>
        /// 기본 연출 후보 2종을 만든다.
        /// 입력값은 없으며, F5와 F6 순서의 후보 배열을 반환한다.
        /// </summary>
        private static BlinkTurnPreset[] CreateDefaultBlinkPresets()
        {
            // 스펙 010: 남긴 후보를 F5 동공 파문, F6 자물쇠 맞춤으로 구성
            return new[]
            {
                new BlinkTurnPreset("F5 동공 파문", 0.4f, 0.08f, 0.55f, 1f, 0f, BlinkLayerOrder.Together, false, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), BlinkChangeKind.PupilRipple),
                // 스펙 009: F6 후보에 회전 출발 지연과 벽 옅어짐 값 추가
                new BlinkTurnPreset("F6 자물쇠 맞춤", 0.4f, 0.08f, 0.55f, 1f, 0.12f, BlinkLayerOrder.OuterFirst, true, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f), turnStartDelaySeconds: 0.2f, wallFadeAmount: 0.9f),            };
        }

        /// <summary>
        /// 층과 시선의 화면 표시 각도와 벽과 문의 표시 투명도를 지우고 화면 회전을 끝낸다.
        /// _blinkLayers를 사용하며, 층과 시선의 BlinkVisualDeg, 벽과 문의 표시 투명도, _blinkTurning을 바꾼다.
        /// </summary>
        private void ClearBlinkVisual()
        {
            _blinkTurning = false;
            // 스펙 009: 깜빡임이 끝나거나 초기화될 때 벽과 문의 표시 투명도를 복원
            foreach (MazeLayer layer in _blinkLayers)
            {
                layer.BlinkVisualDeg = 0f;
                layer.SetWallVisualAlpha(1f);
            }
            if (_cursor != null) _cursor.BlinkVisualDeg = 0f;
        }

        /// <summary>
        /// 미로의 깜빡임 회전각을 지정한 값으로 맞춘다. 씬에 저장된 기본 각도는 그대로 두고 차이만큼만 돌린다.
        /// degrees를 사용하며, 미로 Transform의 회전과 _mazeTurnDeg를 바꾼다.
        /// </summary>
        private void SetMazeTurn(float degrees)
        {
            if (_maze == null || Mathf.Approximately(degrees, _mazeTurnDeg)) return;
            _maze.transform.Rotate(0f, 0f, degrees - _mazeTurnDeg, Space.Self);
            _mazeTurnDeg = degrees;
        }

        /// <summary>
        /// 화면 기준 벡터를 지금 미로 회전을 뺀 미로 기준 벡터로 바꾼다.
        /// screen과 미로 Transform의 회전을 사용하며, 변환된 벡터를 반환한다.
        /// </summary>
        private Vector2 ScreenToMaze(Vector2 screen)
        {
            // 스펙 006: 씬 기본 회전까지 반영하도록 Transform 기준으로 변환
            return _maze != null ? _maze.transform.InverseTransformDirection(screen) : screen;
        }

        /// <summary>
        /// 미로의 가장 안쪽을 지나 동공(심연)으로 들어선다. 여기서부턴 벽이 없다.
        /// 입력값은 없으며, _inAbyss와 문구를 바꾼다.
        /// </summary>
        private void EnterAbyss()
        {
            _inAbyss = true;
            _message = AbyssText;
        }

        /// <summary>
        /// 심연 안에서 벽 없이 움직인다. 동공 밖으로는 못 나가고, 한가운데 파편에 닿으면 마음이 들린다.
        /// step을 사용하며, 시선 위치를 바꾸고 닿으면 TouchCore를 부른다.
        /// </summary>
        private void MoveInAbyss(Vector2 step)
        {
            Vector2 p = (Vector2)Shapes.Polar(_cursor.Radius, _cursor.Angle) + step;
            float limit = Mathf.Max(0.1f, _maze.InnerRadius - _wallMargin);
            if (p.magnitude > limit) p = p.normalized * limit;
            _cursor.Radius = p.magnitude;
            if (p.sqrMagnitude > 1e-8f) _cursor.Angle = Mathf.Repeat(Mathf.Atan2(p.y, p.x) * Mathf.Rad2Deg, 360f);
            float touch = _core != null ? _core.TouchRadius : 0.22f;
            if (_cursor.Radius <= touch) TouchCore();
        }

        /// <summary>
        /// 엔딩에서 보여줄 줄 목록을 만든다.
        /// e와 prefix를 사용하며, 앞줄 + 엔딩 대사 + 제목 줄 목록을 반환한다.
        /// </summary>
        private List<string> BuildEndingLines(EndingDef e, string prefix)
        {
            var list = new List<string>();
            // 직접 엔딩 선택지의 결과가 여러 줄이면 한 줄씩 넘긴다.
            if (!string.IsNullOrEmpty(prefix)) foreach (string l in prefix.Split('\n')) if (l.Length > 0) list.Add(l);
            if (e != null)
            {
                if (e.Lines != null) foreach (string l in e.Lines) if (!string.IsNullOrEmpty(l)) list.Add(l);
                list.Add($"— {e.Title} —");
            }
            else if (!string.IsNullOrEmpty(_endingText)) list.Add(_endingText);
            if (list.Count == 0) list.Add("…");
            return list;
        }

        /// <summary>
        /// 지금 엔딩 줄을 문구로 띄운다.
        /// _heartIndex를 사용하며, _message를 바꾼다.
        /// </summary>
        private void ShowHeartLine()
        {
            List<string> l = EndingLines;
            _message = l[Mathf.Clamp(_heartIndex, 0, l.Count - 1)];
            // 스펙 007: 엔딩 줄을 서술로 대화 기록에 남긴다.
            DialogueLog.Add("", Personalize(_message));
        }

        /// <summary>
        /// 스펙 007: 지금 장면이 마지막으로 기록한 장면과 다르면 그 장면의 상황 문구를 대화 기록에 남긴다.
        /// CurrentNode와 _loggedNode를 사용하며, DialogueLog와 _loggedNode를 바꾼다.
        /// </summary>
        private void LogSituation()
        {
            DialogueScript.Node node = CurrentNode;
            if (node == null || node == _loggedNode) return;
            _loggedNode = node;
            DialogueLog.Add("", Personalize(node.Situation));
        }
    }
}

using System.Collections.Generic;
using System.IO;

using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

using UnityEditor;
using UnityEditor.SceneManagement;

namespace IntroCuttoon.EditorTools
{
    /// <summary>
    /// 인트로 컷툰 그레이 박스 씬을 까는 메뉴.
    /// UI 오브젝트는 에디터의 GameObject 생성 메뉴를 실행해 만들고, 칸 배치와 연출 값을 넣어 씬에 저장한다.
    /// 씬을 만든 뒤에는 씬에 저장된 값을 쓰므로, 다시 실행하면 손으로 고친 값이 사라진다.
    /// </summary>
    public static class IntroCuttoonGrayboxBuilder
    {
        // CUT-01, CUT-14: 씬을 옮긴 자리 Assets/Scenes/Intro.unity에 저장한다.
        private const string SCENE_PATH = "Assets/Scenes/Intro.unity";
        private const string SCENE_FOLDER = "Assets/Scenes";
        private const string SPRITE_FOLDER = "Assets/IntroCuttoon/Sprites";
        private const string CIRCLE_PATH = "Assets/IntroCuttoon/Sprites/GB_Circle.png";
        private const string TRIANGLE_PATH = "Assets/IntroCuttoon/Sprites/GB_Triangle.png";
        // CUT-30: 2-2 운석 둘레의 짓눌려 달아오른 공기 그림이다.
        private const string GLOW_PATH = "Assets/IntroCuttoon/Sprites/GB_Glow.png";
        // CUT-34: 2-2 띠 가운데 구름을 향한 뜨거운 쪽 그림이다.
        private const string GLOW_HOT_PATH = "Assets/IntroCuttoon/Sprites/GB_GlowHot.png";
        private const string FONT_PATH = "Assets/EyeMaze/Fonts/Galmuri11.ttf";
        private const string ROUNDED_SPRITE = "UI/Skin/UISprite.psd";
        private const string MENU_CANVAS = "GameObject/UI (Canvas)/Canvas";
        private const string MENU_IMAGE = "GameObject/UI (Canvas)/Image";
        private const string MENU_TEXT = "GameObject/UI (Canvas)/Legacy/Text";
        private const string MENU_EMPTY = "GameObject/Create Empty";
        private const string MENU_EMPTY_CHILD = "GameObject/Create Empty Child";
        private const string MENU_RAW_IMAGE = "GameObject/UI (Canvas)/Raw Image";
        private const string MENU_CUBE = "GameObject/3D Object/Cube";
        private const string MENU_SPHERE = "GameObject/3D Object/Sphere";
        private const string MENU_CAPSULE = "GameObject/3D Object/Capsule";
        private const string MENU_CYLINDER = "GameObject/3D Object/Cylinder";
        private const string MENU_PLANE = "GameObject/3D Object/Plane";
        private const string MENU_QUAD = "GameObject/3D Object/Quad";
        private const string MATERIAL_FOLDER = "Assets/IntroCuttoon/Materials";
        private const string RENDER_TEXTURE_FOLDER = "Assets/IntroCuttoon/RenderTextures";
        private const string LIT_SHADER = "Universal Render Pipeline/Lit";
        private const string UNLIT_SHADER = "Universal Render Pipeline/Unlit";
        private const string UI_LAYER = "UI";
        private const int SHAPE_TEXTURE_SIZE = 256;
        private const int LABEL_FONT_SIZE = 22;
        private const float PAGE_WIDTH = 1800f;
        private const float PAGE_HEIGHT = 940f;
        private const float GUTTER = 24f;
        private const int QUESTION_FONT_SIZE = 60;
        private const float QUESTION_GAP = 26f;
        private const float QUESTION_HEIGHT_RATIO = 0.75f;
        // CUT-23: 1-3 카메라 시작 자리는 최종 카메라보다 머리에 이만큼 가깝다(미터).
        private const float BUST_START_CLOSER = 0.5f;
        // CUT-26: 1-2 새 떼는 칸이 열리고 이만큼 뒤에 날아오르기 시작한다(초). 칸이 다 나타난 직후다.
        private const float BIRD_START_DELAY = 0.5f;
        // CUT-26: 1-2 운석 그림자와 칸 어두워짐은 칸이 열리고 이만큼 뒤에 시작한다(초). 새 떼가 모두 칸을 벗어난 뒤 그림자가 칸에 들어오는 때다.
        private const float SHADOW_DELAY = 1.4f;
        // CUT-26: 새가 칸을 지나가는 시간은 카메라에서 떨어진 거리(미터)에 이 값을 곱한다. 가까운 새일수록 빨리 지나간다.
        private const float BIRD_SECONDS_PER_METER = 0.082f;
        // CUT-26: 새가 출발하고 도착하는 칸 화면 가로 위치다. 칸 오른쪽 밖에서 왼쪽 밖으로 간다.
        private const float BIRD_START_X = 1.25f;
        private const float BIRD_END_X = -0.25f;
        // CUT-26: 새가 칸을 지나며 오르는 높이의 절반(칸 화면 세로 비율)이다. 오른쪽 아래에서 왼쪽 위로 비스듬히 난다.
        private const float BIRD_RISE = 0.55f;
        private const float BIRD_FLAP_AMPLITUDE = 50f;
        // CUT-28: 2-1 얼굴 카메라의 시야각(도), 표정 가운데 거리에서 칸에 담을 가로 폭(미터), 표정 가운데가 놓일 칸 화면 세로 위치다.
        // 가로 폭은 두 눈 바깥 끝이 칸 양옆에 닿는 정도로, 익스트림 클로즈업보다 조금 떨어진 거리다. 세로 위치는 턱 끝이 칸 아래 끝에 닿아 목이 보이지 않는 높이다.
        private const float FACE_FOV = 30f;
        private const float FACE_FRAME_WIDTH = 0.8f;
        private const float FACE_VIEW_Y = 0.24f;
        // CUT-32: 2-1 표정이 바뀌는 부위별 시작 시각과 시간(초)이다. 눈썹, 눈과 시선, 동공, 입 순서로 조금씩 늦게 시작해 겹치며 바뀐다.
        private const float BROW_CHANGE_DELAY = 0.6f;
        private const float BROW_CHANGE_DURATION = 1.2f;
        private const float EYE_OPEN_DELAY = 0.7f;
        private const float EYE_OPEN_DURATION = 1.2f;
        private const float PUPIL_DILATE_DELAY = 0.8f;
        private const float PUPIL_DILATE_DURATION = 1.4f;
        private const float MOUTH_OPEN_DELAY = 1f;
        private const float MOUTH_OPEN_DURATION = 1.6f;
        // CUT-37: 동공에 비친 운석의 끝 몸체 지름(미터), 동공 가운데에서 앞으로 나온 거리(미터), 몸체 밝기다.
        // 칸이 열릴 때는 끝 크기의 REFLECTION_START배(동공에 견준 크기)이고, REFLECTION_DELAY초부터 REFLECTION_DURATION초 동안 커진다.
        // 동공 확장(2.2초)과 입(2.6초)이 끝난 뒤에도 잠시 혼자 커져서, 반사가 커지는 것이 동공 확장에 묻히지 않는다.
        private const float REFLECTION_BODY_DIAMETER = 0.047f;
        private const float REFLECTION_DEPTH = 0.018f;
        private const float REFLECTION_BODY_GRAY = 0.3f;
        private const float REFLECTION_START = 0.46f;
        private const float REFLECTION_DELAY = 0f;
        private const float REFLECTION_DURATION = 3.2f;
        // CUT-28, CUT-29: 2-1과 2-2 카메라 배경(하늘) 밝기다.
        private const float SKY_GRAY = 0.5f;
        // CUT-29: 2-2 시민 눈높이(미터), 올려다보는 각도(도), 좌우 각도(도), 시야각(도)이다.
        private const float POV_EYE_HEIGHT = 1.7f;
        private const float POV_PITCH = 60f;
        private const float POV_YAW = 0f;
        private const float POV_FOV = 66f;
        // CUT-30: 2-2 운석 반지름(미터), 처음 보이는 반지름 각도(도), 다 다가온 거리(미터, 시민 눈에서 운석 중심까지), 다가오기 시작과 시간(초)이다.
        // 처음 거리는 반지름이 처음 각도로 보이는 거리(약 1,067m)다. 다 다가오면 운석이 칸 위아래를 넘치게 차고, 바닥은 건물 꼭대기보다 약 5m 높다.
        private const float POV_METEOR_RADIUS = 150f;
        private const float POV_METEOR_START_ANGLE = 8f;
        private const float POV_METEOR_DISTANCE = 230f;
        private const float POV_APPROACH_DELAY = 0.2f;
        private const float POV_APPROACH_DURATION = 2.6f;
        // CUT-30: 다가옴 곡선의 끝 기울기다. 처음 기울기가 0이라 거의 멈춘 듯 떠나고, 1.3이면 마지막 0.6초에 거리의 31%를 간다.
        private const float POV_APPROACH_END_SLOPE = 1.3f;
        // CUT-30: 처음 거리의 운석 뒤쪽까지 그리도록 넓힌 2-2 카메라 원평면 거리(미터)다.
        private const float POV_FAR = 1400f;
        // CUT-30: 달아오른 공기 그림에서 투명한 안쪽 원의 반지름과 가장 진한 구간이 끝나는 반지름(그림 반지름 비율)이다.
        // 안쪽 원은 띠 판 위에서 운석 반지름에 놓인다. 판 위의 운석 윤곽은 운석 반지름의 1.01~1.10배라 1.125배까지 가장 진하게 둔다.
        // 바깥 끝은 운석 반지름의 1.47배로, 다 다가왔을 때 띠가 운석 화면 반지름의 1.33배까지 번진다.
        private const float GLOW_INNER = 0.68f;
        private const float GLOW_PLATEAU = 0.765f;
        // CUT-34: 하늘색 막은 구덩이까지 덮도록 운석보다 이 배율만큼 크고, 처음 투명도에서 0으로 걷힌다.
        private const float HAZE_SCALE = 1.05f;
        private const float HAZE_START = 0.425f;
        // CUT-35: 바람에 구름이 칸 왼쪽으로 흐르는 거리(미터)다.
        private const float CLOUD_DRIFT = 5f;
        // CUT-34: 띠의 뜨거운 쪽이 향할 구름 번호, 밝아지는 시작과 시간(초), 끝 투명도, 흐르며 도는 각도(도)와 시간(초)이다.
        private const int HOT_CLOUD = 2;
        private const float GLOW_HOT_DELAY = 1.5f;
        private const float GLOW_HOT_DURATION = 1f;
        private const float GLOW_HOT_ALPHA = 0.75f;
        private const float GLOW_HOT_SWEEP = 12f;
        private const float GLOW_HOT_SWEEP_DURATION = 1.3f;
        // CUT-35: 띠가 HOT_CLOUD 구름 윗면에 닿아 가장자리가 뜯기는 때와 시간(초)이다.
        // 왼쪽 덩이가 떨어져 나가는 방향(도, 0이 칸 오른쪽, 90이 위)과 거리(미터), 윗면 조각이 날아가는 방향과 거리다.
        private const float TEAR_DELAY = 2.32f;
        private const float TEAR_DURATION = 0.3f;
        private const float TEAR_PUSH_ANGLE = 195f;
        private const float TEAR_PUSH = 14f;
        private const float TEAR_FLING_ANGLE = 185f;
        private const float TEAR_FLING = 26f;

        private static readonly Color _fireColor = new Color(0.58f, 0.24f, 0.2f, 1f);
        private static readonly Color _burningEarthColor = new Color(0.52f, 0.26f, 0.22f, 1f);

        // 세트 안 좌표는 세트 루트 기준 로컬 값이다. 세트는 UI 카메라와 서로의 카메라에 보이지 않게 멀리 둔다.
        private static readonly Vector3 _citySetPosition = new Vector3(1000f, 0f, 0f);
        private static readonly Vector3 _bustSetPosition = new Vector3(2000f, 0f, 0f);
        private static readonly Vector3 _zoomSetPosition = new Vector3(3000f, 0f, 0f);
        private static readonly Vector3 _cityCameraPosition = new Vector3(-14f, 22f, -14f);
        private static readonly Vector3 _cityLookTarget = new Vector3(-1f, 0f, 1f);
        private static readonly Vector3 _citizenStart = new Vector3(-8f, 0.9f, 0f);
        private static readonly Vector3 _citizenEnd = new Vector3(-2.5f, 0.9f, 0f);
        // 운석 위치는 그림자 중심에서 빛 방향만큼 뺀 값이다. 그림자 중심은 시민에서 화면 오른쪽 위로 24.4m에서 9.4m까지 온다.
        // 시작점에서는 그림자 경계가 1-2 칸 오른쪽 위 모서리 바로 밖에 있어 칸 안에 그림자가 보이지 않는다.
        private static readonly Vector3 _meteorStart = new Vector3(11.55f, 30f, -14.67f);
        private static readonly Vector3 _meteorEnd = new Vector3(-3.1f, 30f, -11.4f);
        private static readonly Vector3 _bodyTurn = new Vector3(0f, -30f, 0f);
        private static readonly Vector3 _bustCameraPosition = new Vector3(1.4f, 3.23f, -3.84f);
        private static readonly Vector3 _bustLookTarget = new Vector3(1.75f, 2f, 0f);
        // CUT-23: 1-3 카메라 시작 자리에서 머리 중심이 놓일 칸 안 정규화 위치다. 라벨 상자에 걸리지 않게 가운데보다 조금 아래에 둔다.
        private static readonly Vector2 _bustStartHeadViewport = new Vector2(0.5f, 0.39f);
        private static readonly Vector2 _questionSize = new Vector2(80f, 90f);
        private static readonly Vector3 _headDown = new Vector3(-8f, 0f, 0f);
        private static readonly Vector3 _headUp = new Vector3(32f, 0f, 0f);
        // CUT-19: 정면 구간에서 고개를 드는 최대 각도다. 카메라가 빠지는 동안 _headUp으로 내려온다.
        private static readonly Vector3 _headPeak = new Vector3(40f, 0f, 0f);

        // CUT-26: 새마다 다른 값이다. 시민이 보이는 높이에서 벗어난 정도, 카메라에서 떨어진 거리(미터), 출발 늦춤(초), 날갯짓 시작 자리, 1초당 날갯짓 횟수.
        private static readonly float[] _birdLanes = { 0.05f, -0.12f, 0.18f, -0.02f, 0.24f, -0.20f, 0.10f, -0.08f, 0.15f };
        private static readonly float[] _birdDepths = { 10.5f, 12.5f, 9.0f, 13.5f, 11.0f, 8.5f, 12.0f, 14.0f, 9.5f };
        private static readonly float[] _birdDelays = { 0.00f, 0.05f, 0.10f, 0.14f, 0.19f, 0.24f, 0.29f, 0.34f, 0.40f };
        private static readonly float[] _birdPhases = { 0.00f, 0.37f, 0.71f, 0.18f, 0.55f, 0.90f, 0.26f, 0.63f, 0.44f };
        private static readonly float[] _birdFrequencies = { 5.0f, 4.6f, 5.4f, 4.8f, 5.2f, 5.6f, 4.7f, 5.1f, 4.9f };

        // CUT-28, CUT-29: 2쪽 얼굴 세트와 올려다보기 세트 자리, 얼굴이 위를 보는 각도다.
        private static readonly Vector3 _faceSetPosition = new Vector3(4000f, 0f, 0f);
        private static readonly Vector3 _povSetPosition = new Vector3(5000f, 0f, 0f);
        private static readonly Vector3 _faceTilt = new Vector3(25f, 0f, 0f);
        // CUT-35: 구름마다 다른 값이다. 시민 눈에서의 거리(미터), 칸 화면에서 운석 중심 둘레로 놓인 방향(도, 0이 오른쪽, 90이 위), 시선에서 떨어진 각도(도), 크기 배율.
        // 첫 구름은 운석 처음 자리의 왼쪽 위 가장자리를 가리고 운석이 지나는 거리에 있어, 운석이 다가오며 그 구름 앞으로 나온다.
        private static readonly float[] _cloudDistances = { 220f, 320f, 160f };
        private static readonly float[] _cloudAngles = { 135f, 50f, 270f };
        private static readonly float[] _cloudOffsets = { 9.5f, 27f, 26f };
        private static readonly float[] _cloudSizes = { 1.6f, 1.3f, 1.4f };
        // CUT-34: 달아오른 공기 띠의 처음 색(흰빛, 투명도 0.3)과 끝 색(옅은 살구빛, 투명도 1)이다.
        private static readonly Color _glowStartColor = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color _glowEndColor = new Color(0.93f, 0.81f, 0.7f, 1f);
        // CUT-34: 띠의 뜨거운 쪽 색(흰빛에 가까움)이다.
        private static readonly Color _glowHotColor = new Color(1f, 0.96f, 0.9f, 1f);
        // CUT-35: 떨어져 나가며 작아진 왼쪽 덩이의 크기, 윗면 조각이 숨어 있는 자리와 다 생긴 크기(구름 묶음 기준 로컬 값)다.
        private static readonly Vector3 _tornPuffScale = new Vector3(8f, 5.5f, 5.5f);
        private static readonly Vector3 _shredStart = new Vector3(-4f, 4f, 0f);
        private static readonly Vector3 _shredScale = new Vector3(5f, 3.5f, 3.5f);
        // CUT-32: 2-1 입 구멍의 다문 자리와 크기, 벌린 크기(입 축 기준 로컬 값)다. 다문 선을 위쪽에 두어 아래로 벌어지게 한다.
        private static readonly Vector3 _mouthClosedPosition = new Vector3(0f, 0.1f, 0f);
        private static readonly Vector3 _mouthClosedScale = new Vector3(0.28f, 0.035f, 0.1f);
        private static readonly Vector3 _mouthOpenScale = new Vector3(0.34f, 0.32f, 0.1f);
        // CUT-32: 눈썹의 호기심 어린 자리·각도와 놀란 자리·각도다. 값은 왼쪽, 오른쪽 순서다.
        // 처음에는 칸 오른쪽 눈썹만 조금 높고 바깥 끝이 올라가 갸웃하는 표정이고, 끝에는 두 눈썹이 높이 올라가 안쪽 끝이 더 높다. 자리의 Z는 이마 곡면을 따른다.
        private static readonly Vector3[] _browCuriousPositions = { new Vector3(-0.27f, 1.06f, -0.596f), new Vector3(0.27f, 1.11f, -0.58f) };
        private static readonly float[] _browCuriousAngles = { -3f, 6f };
        private static readonly Vector3[] _browShockedPositions = { new Vector3(-0.27f, 1.15f, -0.565f), new Vector3(0.27f, 1.15f, -0.565f) };
        private static readonly float[] _browShockedAngles = { 14f, -14f };
        // CUT-32: 눈 크기(흰자와 테의 세로 배율), 시선(홍채 묶음 자리), 동공 크기의 처음과 끝 값이다.
        // 처음에는 홍채가 위아래 테에 닿는 보통 눈이고, 끝에는 홍채 둘레로 흰자가 다 보인다.
        private static readonly Vector3 _eyeCuriousOpening = new Vector3(1f, 0.56f, 1f);
        private static readonly Vector3 _gazeCurious = new Vector3(0.02f, 0f, 0f);
        private static readonly Vector3 _gazeShocked = new Vector3(0.02f, 0.04f, 0f);
        private static readonly Vector3 _pupilNormal = new Vector3(0.055f, 0.055f, 0.03f);
        private static readonly Vector3 _pupilDilated = new Vector3(0.11f, 0.11f, 0.03f);
        // CUT-37: 동공에 비친 운석 띠의 색이다. 2-2 처음 화면의 흰 띠처럼 보이게 한다.
        private static readonly Color _reflectionBandColor = new Color(1f, 1f, 1f, 0.65f);

        private static Sprite _circle;
        private static Sprite _triangle;
        private static Sprite _glow;
        private static Sprite _glowHot;
        private static Sprite _rounded;
        private static Font _font;

        /// <summary>
        /// 메뉴에서 그레이 박스 씬을 만든다. 씬 파일이 이미 있으면 덮어쓸지 묻고,
        /// 열린 씬에 저장하지 않은 변경이 있으면 먼저 저장할지 묻는다. 취소하면 아무것도 바꾸지 않는다.
        /// </summary>
        [MenuItem("IntroCuttoon/그레이박스 씬 만들기")]
        private static void BuildFromMenu()
        {
            if (File.Exists(SCENE_PATH)
                && !EditorUtility.DisplayDialog(
                    "그레이박스 씬 만들기",
                    "이미 있는 Intro 씬을 덮어씁니다. 씬에서 고친 칸 배치와 연출 값이 사라집니다.",
                    "덮어쓰기",
                    "취소"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            Build();
        }

        /// <summary>
        /// 도형 그림(원, 삼각형, 달아오른 공기, 띠의 뜨거운 쪽)이 없으면 만들고, 칸을 만들 때 쓰는 그림과 글꼴을 불러 둔다.
        /// _circle, _triangle, _glow, _glowHot, _rounded, _font를 채운다.
        /// </summary>
        private static void PrepareAssets()
        {
            EnsureShapeSprites();
            _circle = AssetDatabase.LoadAssetAtPath<Sprite>(CIRCLE_PATH);
            _triangle = AssetDatabase.LoadAssetAtPath<Sprite>(TRIANGLE_PATH);
            _glow = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_PATH);
            _glowHot = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_HOT_PATH);
            _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>(ROUNDED_SPRITE);
            _font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);
        }

        /// <summary>
        /// 도형 그림을 준비하고 새 씬에 카메라, 캔버스, 5쪽 14칸, 번쩍임과 암전 화면, 하단 안내, 진행 오브젝트를 깔아 SCENE_PATH로 저장한다.
        /// 저장한 씬이 열린 채로 끝난다.
        /// </summary>
        public static void Build()
        {
            PrepareAssets();
            EnsureFolder(SCENE_FOLDER);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Gray(0.45f);
            CreateLight();
            Transform citySet = BuildCitySet("Set_City", _citySetPosition, _citizenStart, out Transform citizen);
            Transform zoomSet = BuildCitySet("Set_City_Zoom", _zoomSetPosition, _citizenEnd, out _);
            Transform meteor = CreateMeteor(zoomSet);
            Transform bustSet = BuildBustSet(out Transform headPivot);
            Transform faceSet = BuildFaceSet(out Transform faceHead);
            Transform povSet = BuildPovSet(out Transform povMeteor, out Transform[] clouds);
            Camera camera = CreateCamera();
            Canvas canvas = CreateCanvas(camera);
            RectTransform root = (RectTransform)canvas.transform;

            Image background = CreateImage(root, "Background", null, Color.black);
            SetAnchors(background.rectTransform, 0f, 0f, 1f, 1f);

            // 쪽 묶음을 먼저 만들어 번쩍임, 암전, 안내가 그 위에 그려지게 한다.
            RectTransform pagesRoot = CreateRect(root, "Pages");
            SetAnchors(pagesRoot, 0f, 0f, 1f, 1f);
            pagesRoot.offsetMin = new Vector2(60f, 90f);
            pagesRoot.offsetMax = new Vector2(-60f, -50f);

            // 암전 위에는 안내가 보이고, 번쩍임은 안내까지 덮도록 암전, 안내, 번쩍임 순서로 만든다.
            CanvasGroup blackout = CreateOverlay(root, "Blackout", Color.black);
            GameObject guide = CreateGuide(root, out Text pageText);
            CanvasGroup flash = CreateOverlay(root, "FlashOverlay", Color.white);

            CuttoonPage[] pages =
            {
                BuildPage1(pagesRoot, citySet, citizen, zoomSet, meteor, bustSet, headPivot),
                BuildPage2(pagesRoot, faceSet, faceHead, povSet, povMeteor, clouds),
                BuildPage3(pagesRoot, flash),
                BuildPage4(pagesRoot),
                BuildPage5(pagesRoot),
            };

            GameObject playerObject = CreateFromMenu(MENU_EMPTY, null, "CuttoonPlayer");
            CuttoonPlayer player = playerObject.AddComponent<CuttoonPlayer>();
            SetRefArray(player, "_pages", pages);
            SetRef(player, "_blackout", blackout);
            SetRef(player, "_guideRoot", guide);
            SetRef(player, "_pageText", pageText);

            Selection.activeGameObject = null;
            EditorSceneManager.SaveScene(scene, SCENE_PATH);
            AssetDatabase.Refresh();
        }

        /// <summary>
        /// 1쪽(1-1 도시 거리, 1-2 그림자, 1-3 올려다보기)을 pagesRoot 아래에 깔고 쪽 컴포넌트를 돌려준다.
        /// 1-1은 citySet, 1-2는 같은 모양의 zoomSet, 1-3은 bustSet을 찍는 칸 카메라 화면을 보여 주고,
        /// citizen, meteor, headPivot, 1-3 카메라와 ?를 움직이는 연출을 단다.
        /// </summary>
        private static CuttoonPage BuildPage1(RectTransform pagesRoot, Transform citySet, Transform citizen, Transform zoomSet, Transform meteor, Transform bustSet, Transform headPivot)
        {
            RectTransform page = CreatePage(pagesRoot, "Page1");
            List<CuttoonPanel> panels = new List<CuttoonPanel>();

            // 1-1 도시 거리: 왼쪽 앞 높은 곳에서 거리를 비스듬히 내려다보는 부감이다.
            RectTransform street = CreatePanel(page, "1-1", 0f, 0.48f, 1f, 1f, Gray(0.2f), out Vector2 streetSize);
            Camera streetCamera = CreateSetCamera("Camera_1-1", citySet, _cityCameraPosition, _cityLookTarget, 32f, Gray(0.5f), EnsureRenderTexture("1-1", streetSize));
            CreateView(street, streetCamera);
            CuttoonTransformTween walk = AddTransformTween(street, citizen, 0f, 3.2f);
            TweenLocalPosition(walk, _citizenStart, _citizenEnd);
            SetCurve(walk, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            CreateLabel(street, streetSize, "1-1", "도시 거리", "비스듬히 내려다본 거리, 시민이 왼쪽에서 오른쪽으로 걷는다");
            panels.Add(FinishPanel(street));

            // 1-2 그림자: 같은 모양의 줌인용 세트를 1-1과 같은 위치와 각도에서 시야각만 좁혀 찍는다.
            RectTransform shadowPanel = CreatePanel(page, "1-2", 0f, 0f, 0.5f, 0.48f, Gray(0.22f), out Vector2 shadowSize);
            Camera shadowCamera = CreateSetCamera("Camera_1-2", zoomSet, _cityCameraPosition, _cityLookTarget, 11f, Gray(0.5f), EnsureRenderTexture("1-2", shadowSize));
            CreateView(shadowPanel, shadowCamera);
            CuttoonTransformTween approach = AddTransformTween(shadowPanel, meteor, SHADOW_DELAY, 2.2f);
            TweenLocalPosition(approach, _meteorStart, _meteorEnd);
            SetCurve(approach, EaseInCurve());

            // 그림자가 오는 동안 칸 전체를 어둡게 하는 막은 라벨보다 먼저 만들어 라벨 아래에 그려지게 한다.
            Image dim = CreateImage(shadowPanel, "Dim", null, Color.black);
            SetAnchors(dim.rectTransform, 0f, 0f, 1f, 1f);
            CanvasGroup dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            dimGroup.alpha = 0f;
            dimGroup.blocksRaycasts = false;
            dimGroup.interactable = false;
            CuttoonTween darken = AddTween(shadowPanel, dim.rectTransform, SHADOW_DELAY, 2.2f);
            TweenAlpha(darken, dimGroup, 0f, 0.35f);
            SetCurve(darken, EaseInCurve());
            // CUT-22 폐기: 운석이 부딪히기 전이라 1-2 칸 흔들림을 넣지 않는다.
            // CUT-26: 어두워지는 하늘을 먼저 느낀 흰 새 떼가 카메라 가까이를 지나 달아나고, 그 뒤 그림자가 들어온다.
            CreateBirdFlock(zoomSet, shadowCamera, zoomSet.TransformPoint(_citizenEnd), shadowPanel);
            CreateLabel(shadowPanel, shadowSize, "1-2", "그림자", "같은 각도로 줌인, 놀란 흰 새 떼가 앞을 지나간 뒤 그림자가 점점 빨리 덮쳐 온다");
            panels.Add(FinishPanel(shadowPanel));

            // 1-3 올려다보기: 비스듬히 선 인물을 위에서 내려다보는 상반신 구도다. 카메라는 몸 정면 쪽에서 출발해 이 구도로 온다.
            RectTransform lookUp = CreatePanel(page, "1-3", 0.5f, 0f, 1f, 0.48f, Gray(0.24f), out Vector2 lookUpSize);
            Camera bustCamera = CreateSetCamera("Camera_1-3", bustSet, _bustCameraPosition, _bustLookTarget, 50f, Gray(0.4f), EnsureRenderTexture("1-3", lookUpSize));
            RawImage lookUpView = CreateView(lookUp, bustCamera);

            // CUT-23: 카메라는 몸 정면 쪽 위의 가까운 자리에서 기다리다가, 고개를 다 든 뒤 머리 둘레로 돌며 뒤로 빠져 최종 구도에 멈춘다.
            float raiseDelay = 0.4f;
            float raiseDuration = 1f;
            float dollyDelay = 1.5f;
            float dollyDuration = 1.6f;
            Transform bustCameraTransform = bustCamera.transform;
            Vector3 endRotation = bustCameraTransform.localEulerAngles;
            MeshFilter[] headParts = headPivot.GetComponentsInChildren<MeshFilter>();
            bustCamera.aspect = (float)bustCamera.targetTexture.width / bustCamera.targetTexture.height;
            BustStartPose(bustCamera, bustSet, headPivot, out Vector3 dollyStart, out Vector3 startRotation, out Vector3 orbitPivot, out float startDistance);

            // 끝 거리와 화면 위치는 최종 카메라에서 본 중심점으로 구해, 연출이 끝나면 카메라가 최종 구도 자리에 선다.
            Vector3 pivotWorld = bustSet.TransformPoint(orbitPivot);
            float endDistance = Vector3.Dot(pivotWorld - bustCameraTransform.position, bustCameraTransform.forward);
            Vector2 endView = bustCamera.WorldToViewportPoint(pivotWorld);
            CuttoonOrbitTween pullBack = lookUp.gameObject.AddComponent<CuttoonOrbitTween>();
            SetRef(pullBack, "_camera", bustCamera);
            SetVector3(pullBack, "_pivot", orbitPivot);
            SetFloat(pullBack, "_delay", dollyDelay);
            SetFloat(pullBack, "_duration", dollyDuration);
            SetFloat(pullBack, "_pitch", endRotation.x);
            SetFloat(pullBack, "_fromYaw", startRotation.y);
            SetFloat(pullBack, "_toYaw", endRotation.y);
            SetFloat(pullBack, "_fromDistance", startDistance);
            SetFloat(pullBack, "_toDistance", endDistance);
            SetVector2(pullBack, "_fromView", _bustStartHeadViewport);
            SetVector2(pullBack, "_toView", endView);

            // ?는 씬에서는 고개를 든 머리의 오른쪽 옆에 둔다. 카메라가 빠지기 전과 뒤의 머리 크기로 ?가 작아지는 비율을 구한다.
            Rect headAtEnd = ViewportBounds(bustCamera, headParts);
            bustCameraTransform.localPosition = dollyStart;
            bustCameraTransform.localRotation = Quaternion.Euler(startRotation);
            headPivot.localRotation = Quaternion.Euler(_headPeak);
            Rect headAtStart = ViewportBounds(bustCamera, headParts);
            headPivot.localRotation = Quaternion.Euler(_headUp);
            bustCameraTransform.localPosition = _bustCameraPosition;
            bustCameraTransform.localRotation = Quaternion.Euler(endRotation);
            bustCamera.ResetAspect();
            Vector2 questionAtEnd = QuestionPoint(headAtEnd, lookUpSize);

            Text question = CreateText(lookUp, "Question", "?", QUESTION_FONT_SIZE, Gray(0.95f), TextAnchor.MiddleLeft);
            SetPoint(question.rectTransform, questionAtEnd, _questionSize, new Vector2(0f, 0.5f));
            CanvasGroup questionGroup = question.gameObject.AddComponent<CanvasGroup>();
            questionGroup.blocksRaycasts = false;
            questionGroup.interactable = false;
            // CUT-19: 정면 구간에서는 _headPeak까지 들고, 카메라가 빠지는 동안 _headUp으로 내려온다. 끝 값은 _headUp이다.
            float raiseTotal = dollyDelay + dollyDuration - raiseDelay;
            CuttoonTransformTween raise = AddTransformTween(lookUp, headPivot, raiseDelay, raiseTotal);
            TweenLocalRotation(raise, _headDown, _headUp);
            SetCurve(raise, HeadRaiseCurve(raiseDuration / raiseTotal, (dollyDelay - raiseDelay) / raiseTotal, (_headPeak.x - _headDown.x) / (_headUp.x - _headDown.x)));
            // CUT-19: ?는 고개를 다 든 뒤 나타난다.
            CuttoonTween fadeIn = AddTween(lookUp, question.rectTransform, raiseDelay + raiseDuration, 0.9f);
            TweenAlpha(fadeIn, questionGroup, 0f, 1f);
            SetCurve(fadeIn, EaseInCurve());

            // ?는 카메라가 빠지는 동안 머리가 작아지는 만큼 같이 작아진다. Play 동안 자리는 CuttoonScreenFollow가 매 프레임 머리 옆으로 맞춘다.
            CuttoonTween shrink = AddTween(lookUp, question.rectTransform, dollyDelay, dollyDuration);
            TweenScale(shrink, Vector3.one * (headAtStart.height / headAtEnd.height), Vector3.one);
            CuttoonScreenFollow pin = question.gameObject.AddComponent<CuttoonScreenFollow>();
            SetRef(pin, "_camera", bustCamera);
            SetRef(pin, "_view", lookUpView.rectTransform);
            SetRefArray(pin, "_parts", headParts);
            SetRef(pin, "_mark", question.rectTransform);
            SetBool(pin, "_follow", true);
            SetFloat(pin, "_gap", QUESTION_GAP);
            SetFloat(pin, "_heightRatio", QUESTION_HEIGHT_RATIO);
            CreateLabel(lookUp, lookUpSize, "1-3", "올려다보기", "시민이 고개를 들어 오른쪽 위를 보고, 머리 옆에 ?가 뜬 뒤 카메라가 빠진다");
            panels.Add(FinishPanel(lookUp));

            return FinishPage(page, panels, 1f);
        }

        /// <summary>
        /// position에 name이라는 거리 세트(바닥, 길, 높이가 다른 건물 상자, 시민 캡슐)를 만든다.
        /// 시민은 로컬 위치 citizenPosition에 둔다. 세트 루트를 돌려주고, 시민 Transform을 citizen으로 내보낸다.
        /// </summary>
        private static Transform BuildCitySet(string name, Vector3 position, Vector3 citizenPosition, out Transform citizen)
        {
            Transform root = CreateFromMenu(MENU_EMPTY, null, name).transform;
            root.position = position;
            Material ground = EnsureMaterial("GB_Ground", 0.34f);
            Material road = EnsureMaterial("GB_Road", 0.5f);
            Material building = EnsureMaterial("GB_Building", 0.66f);
            Material person = EnsureMaterial("GB_Person", 0.92f);

            CreatePrimitive(MENU_PLANE, root, "Ground", Vector3.zero, new Vector3(8f, 1f, 8f), ground);
            CreatePrimitive(MENU_CUBE, root, "Road", new Vector3(0f, 0.02f, 0f), new Vector3(60f, 0.04f, 5f), road);

            // 건물은 길 건너편에 두 줄, 카메라 쪽에는 시야를 가리지 않게 양 끝에만 낮게 둔다. 값은 x, 높이 쌍이다.
            CreateBuildingRow(root, "BuildingFar", new[] { -20f, 7f, -15f, 10f, -10f, 6f, -5f, 12f, 0f, 8f, 5f, 11f, 10f, 6f, 15f, 9f, 20f, 7f }, 7f, 4f, building);
            CreateBuildingRow(root, "BuildingBack", new[] { -17f, 14f, -8f, 16f, 3f, 13f, 12f, 15f, 21f, 12f }, 13f, 5f, building);
            CreateBuildingRow(root, "BuildingNear", new[] { -18f, 3f, -12f, 4f, 12f, 3f, 18f, 4f }, -7f, 4f, building);

            citizen = CreatePrimitive(MENU_CAPSULE, root, "Citizen", citizenPosition, new Vector3(0.7f, 0.9f, 0.7f), person);
            return root;
        }

        /// <summary>
        /// setRoot 하늘의 _meteorStart에 그림자만 드리우고 보이지 않는 운석 구체를 만들어 Transform을 돌려준다.
        /// </summary>
        private static Transform CreateMeteor(Transform setRoot)
        {
            Transform meteor = CreatePrimitive(MENU_SPHERE, setRoot, "MeteorShadowCaster", _meteorStart, Vector3.one * 30f, EnsureMaterial("GB_Dark", 0.15f));
            meteor.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            return meteor;
        }

        /// <summary>
        /// parent 아래 z 줄에 건물 상자를 줄지어 만든다. xAndHeights는 x 위치와 높이 쌍이고, 상자의 가로·세로는 width다.
        /// </summary>
        private static void CreateBuildingRow(Transform parent, string name, float[] xAndHeights, float z, float width, Material material)
        {
            for (int i = 0; i < xAndHeights.Length / 2; i++)
            {
                float height = xAndHeights[i * 2 + 1];
                Vector3 position = new Vector3(xAndHeights[i * 2], height * 0.5f, z);
                CreatePrimitive(MENU_CUBE, parent, name + i, position, new Vector3(width, height, width), material);
            }
        }

        /// <summary>
        /// _bustSetPosition에 상반신 세트(어깨, 목, 머리, 머리카락, 눈, 코, 입)를 만든다. 얼굴은 -Z 쪽을 보고,
        /// 몸 전체를 _bodyTurn만큼 돌려 화면 오른쪽 방향으로 비스듬히 세운다.
        /// 세트 루트를 돌려주고, 고개를 돌릴 머리 축 Transform을 headPivot으로 내보낸다.
        /// </summary>
        private static Transform BuildBustSet(out Transform headPivot)
        {
            Transform root = CreateFromMenu(MENU_EMPTY, null, "Set_Bust").transform;
            root.position = _bustSetPosition;
            Material skin = EnsureMaterial("GB_Skin", 0.8f);
            Material clothes = EnsureMaterial("GB_Clothes", 0.55f);
            Material dark = EnsureMaterial("GB_Dark", 0.15f);

            // 카메라는 세트 루트 아래에 두므로, 몸만 따로 돌리도록 몸 축을 둔다.
            Transform body = CreateFromMenu(MENU_EMPTY_CHILD, root, "Body").transform;
            body.localPosition = Vector3.zero;
            body.localRotation = Quaternion.Euler(_bodyTurn);
            Transform shoulders = CreatePrimitive(MENU_CAPSULE, body, "Shoulders", Vector3.zero, new Vector3(1.4f, 1.9f, 1.1f), clothes);
            shoulders.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreatePrimitive(MENU_CYLINDER, body, "Neck", new Vector3(0f, 0.85f, 0f), new Vector3(0.45f, 0.3f, 0.45f), skin);

            // 머리 축을 목 위에 두고, 얼굴 부품은 모두 머리 축의 자식으로 둔다. 축의 X 회전이 커질수록 얼굴이 위를 본다.
            headPivot = CreateFromMenu(MENU_EMPTY_CHILD, body, "HeadPivot").transform;
            headPivot.localPosition = new Vector3(0f, 1.05f, 0f);
            headPivot.localRotation = Quaternion.Euler(_headUp);
            CreatePrimitive(MENU_SPHERE, headPivot, "Head", new Vector3(0f, 0.85f, 0f), new Vector3(1.3f, 1.55f, 1.35f), skin);
            CreatePrimitive(MENU_SPHERE, headPivot, "Hair", new Vector3(0f, 1.25f, 0.12f), new Vector3(1.4f, 1.1f, 1.4f), dark);
            CreatePrimitive(MENU_SPHERE, headPivot, "EyeLeft", new Vector3(-0.25f, 0.95f, -0.62f), Vector3.one * 0.16f, dark);
            CreatePrimitive(MENU_SPHERE, headPivot, "EyeRight", new Vector3(0.25f, 0.95f, -0.62f), Vector3.one * 0.16f, dark);
            CreatePrimitive(MENU_CUBE, headPivot, "Nose", new Vector3(0f, 0.75f, -0.7f), new Vector3(0.14f, 0.25f, 0.18f), skin);
            CreatePrimitive(MENU_CUBE, headPivot, "Mouth", new Vector3(0f, 0.45f, -0.62f), new Vector3(0.32f, 0.06f, 0.06f), dark);
            return root;
        }

        /// <summary>
        /// _faceSetPosition에 2-1 얼굴 세트(어깨, 목, 머리, 머리카락, 귀, 눈, 눈썹, 코, 입)를 만든다.
        /// 사람은 1-3 상반신과 같은 크기와 재질이고, 얼굴은 세트 기준 -Z 쪽을 보며 머리 축을 _faceTilt만큼 젖혀 위를 올려다본다.
        /// 눈썹, 눈, 입은 호기심 어린 표정 값(_browCuriousPositions, _eyeCuriousOpening, _mouthClosedScale 등)으로 둔다. 세트는 얼굴이 해가 있는 쪽을 보게 돌린다.
        /// 세트 루트를 돌려주고, 머리 축 Transform을 faceHead로 내보낸다.
        /// </summary>
        private static Transform BuildFaceSet(out Transform faceHead)
        {
            Transform root = CreateFromMenu(MENU_EMPTY, null, "Set_Face").transform;
            root.position = _faceSetPosition;
            root.rotation = Quaternion.Euler(0f, SunYaw() - 180f, 0f);
            Material skin = EnsureMaterial("GB_Skin", 0.8f);
            Material clothes = EnsureMaterial("GB_Clothes", 0.55f);
            Material dark = EnsureMaterial("GB_Dark", 0.15f);

            Transform shoulders = CreatePrimitive(MENU_CAPSULE, root, "Shoulders", Vector3.zero, new Vector3(1.4f, 1.9f, 1.1f), clothes);
            shoulders.localRotation = Quaternion.Euler(0f, 0f, 90f);
            CreatePrimitive(MENU_CYLINDER, root, "Neck", new Vector3(0f, 0.85f, 0f), new Vector3(0.45f, 0.3f, 0.45f), skin);

            // 얼굴 부품은 모두 머리 축의 자식이다. 축의 X 회전이 커질수록 얼굴이 위를 본다.
            faceHead = CreateFromMenu(MENU_EMPTY_CHILD, root, "HeadPivot").transform;
            faceHead.localPosition = new Vector3(0f, 1.05f, 0f);
            faceHead.localRotation = Quaternion.Euler(_faceTilt);
            CreatePrimitive(MENU_SPHERE, faceHead, "Head", new Vector3(0f, 0.85f, 0f), new Vector3(1.3f, 1.55f, 1.35f), skin);
            CreatePrimitive(MENU_SPHERE, faceHead, "Hair", new Vector3(0f, 1.32f, 0.14f), new Vector3(1.4f, 1.1f, 1.4f), dark);
            CreatePrimitive(MENU_SPHERE, faceHead, "EarLeft", new Vector3(-0.64f, 0.82f, 0.05f), new Vector3(0.14f, 0.32f, 0.22f), skin);
            CreatePrimitive(MENU_SPHERE, faceHead, "EarRight", new Vector3(0.64f, 0.82f, 0.05f), new Vector3(0.14f, 0.32f, 0.22f), skin);
            CreateFaceEye(faceHead, "EyeLeft", new Vector3(-0.26f, 0.85f, -0.6f), dark);
            CreateFaceEye(faceHead, "EyeRight", new Vector3(0.26f, 0.85f, -0.6f), dark);

            // 눈썹은 호기심 어린 자리에서 시작하고, 2-1이 열리면 놀란 자리로 올라간다(CUT-32).
            Transform browLeft = CreatePrimitive(MENU_CUBE, faceHead, "BrowLeft", _browCuriousPositions[0], new Vector3(0.3f, 0.055f, 0.07f), dark);
            browLeft.localRotation = Quaternion.Euler(0f, 0f, _browCuriousAngles[0]);
            Transform browRight = CreatePrimitive(MENU_CUBE, faceHead, "BrowRight", _browCuriousPositions[1], new Vector3(0.3f, 0.055f, 0.07f), dark);
            browRight.localRotation = Quaternion.Euler(0f, 0f, _browCuriousAngles[1]);
            CreatePrimitive(MENU_SPHERE, faceHead, "Nose", new Vector3(0f, 0.69f, -0.68f), new Vector3(0.17f, 0.22f, 0.17f), skin);

            // 입은 얼굴 아래쪽 곡면을 따라 아래로 기울인 어두운 타원이다. 처음에는 다문 선이고, 2-1이 열리면 벌어진다(CUT-32).
            Transform mouth = CreateFromMenu(MENU_EMPTY_CHILD, faceHead, "Mouth").transform;
            mouth.localPosition = new Vector3(0f, 0.4f, -0.53f);
            mouth.localRotation = Quaternion.Euler(-32f, 0f, 0f);
            CreatePrimitive(MENU_SPHERE, mouth, "Hole", _mouthClosedPosition, _mouthClosedScale, dark);
            return root;
        }

        /// <summary>
        /// 머리 축 headPivot 아래 localPosition에 눈 name을 만든다. 흰자와 그 둘레의 어두운 테(rim 재질)는 눈 크기 묶음(Opening) 아래에 두어 세로 배율로 눈을 크게 뜨게 하고,
        /// 홍채와 동공은 시선 묶음(Gaze) 아래에 두어 함께 움직이게 하고, 운석이 비친 반사(Reflection)는 동공 아래에 두어 동공과 함께 커지게 한다.
        /// 처음 값은 호기심 어린 표정(_eyeCuriousOpening, _gazeCurious, _pupilNormal)과 REFLECTION_START 크기의 반사다.
        /// 동공은 빛을 받아도 어둡게 남도록 아주 어두운 재질을, 비친 운석은 빛을 받지 않는 몸체와 띠 그림 재질을 쓴다.
        /// </summary>
        private static void CreateFaceEye(Transform headPivot, string name, Vector3 localPosition, Material rim)
        {
            Transform eye = CreateFromMenu(MENU_EMPTY_CHILD, headPivot, name).transform;
            eye.localPosition = localPosition;
            eye.localRotation = Quaternion.identity;
            Transform opening = CreateFromMenu(MENU_EMPTY_CHILD, eye, "Opening").transform;
            opening.localPosition = Vector3.zero;
            opening.localRotation = Quaternion.identity;
            opening.localScale = _eyeCuriousOpening;
            CreatePrimitive(MENU_SPHERE, opening, "Rim", new Vector3(0f, 0f, 0.012f), new Vector3(0.34f, 0.38f, 0.1f), rim);
            CreatePrimitive(MENU_SPHERE, opening, "White", Vector3.zero, new Vector3(0.3f, 0.34f, 0.12f), EnsureMaterial("GB_EyeWhite", 0.95f));
            Transform gaze = CreateFromMenu(MENU_EMPTY_CHILD, eye, "Gaze").transform;
            gaze.localPosition = _gazeCurious;
            gaze.localRotation = Quaternion.identity;
            CreatePrimitive(MENU_SPHERE, gaze, "Iris", new Vector3(0f, 0f, -0.05f), new Vector3(0.17f, 0.17f, 0.05f), EnsureMaterial("GB_Iris", 0.3f));
            Transform pupil = CreatePrimitive(MENU_SPHERE, gaze, "Pupil", new Vector3(0f, 0f, -0.066f), _pupilNormal, EnsureMaterial("GB_Pupil", 0.04f));

            // CUT-37: 동공 가운데 앞에 2-2 처음 운석과 같은 모습(흐린 회색 몸체와 흰 띠)의 비친 운석을 둔다.
            // 동공의 자식이라 크기 값은 동공 끝 크기(_pupilDilated) 기준 로컬 값이고, 띠 그림의 안쪽 원이 몸체 둘레에 놓인다.
            Transform reflection = CreateFromMenu(MENU_EMPTY_CHILD, pupil, "Reflection").transform;
            reflection.localPosition = new Vector3(0f, 0f, -REFLECTION_DEPTH / _pupilDilated.z);
            reflection.localRotation = Quaternion.identity;
            reflection.localScale = Vector3.one * REFLECTION_START;
            float bodySize = REFLECTION_BODY_DIAMETER / _pupilDilated.x;
            Transform body = CreatePrimitive(MENU_SPHERE, reflection, "Body", Vector3.zero, new Vector3(bodySize, bodySize, 0.004f / _pupilDilated.z), EnsureUnlitMaterial("GB_ReflectionBody", REFLECTION_BODY_GRAY));
            Transform band = CreatePrimitive(MENU_QUAD, reflection, "Band", new Vector3(0f, 0f, -0.003f / _pupilDilated.z), new Vector3(bodySize / GLOW_INNER, bodySize / GLOW_INNER, 1f), EnsureTransparentMaterial("GB_ReflectionBand", _reflectionBandColor, _glow.texture, 0));
            foreach (Transform part in new[] { body, band })
            {
                MeshRenderer partRenderer = part.GetComponent<MeshRenderer>();
                partRenderer.shadowCastingMode = ShadowCastingMode.Off;
                partRenderer.receiveShadows = false;
            }
        }

        /// <summary>
        /// 2-1 칸 panel에 표정이 호기심에서 경악으로 점진적으로 바뀌는 3D 연출을 단다. 머리 축 faceHead 아래 눈썹(BrowLeft, BrowRight),
        /// 눈 크기(Opening), 시선(Gaze), 동공(Gaze/Pupil), 입 구멍(Mouth/Hole)을 이름으로 찾아 호기심 어린 값에서 놀란 값으로 바꾼다.
        /// 눈썹, 눈과 시선, 동공, 입 순서로 시작 시각을 조금씩 늦추고, 모두 천천히 시작해 천천히 끝나는 곡선으로 바꾼다.
        /// 동공에 비친 운석(Gaze/Pupil/Reflection)은 REFLECTION_DELAY초부터 REFLECTION_DURATION초 동안 REFLECTION_START배에서 1배로 커져, 눈이 다 커진 뒤에도 잠시 더 커진다.
        /// </summary>
        private static void AddFaceChange(RectTransform panel, Transform faceHead)
        {
            string[] sides = { "Left", "Right" };
            for (int i = 0; i < sides.Length; i++)
            {
                CuttoonTransformTween brow = AddTransformTween(panel, faceHead.Find("Brow" + sides[i]), BROW_CHANGE_DELAY, BROW_CHANGE_DURATION);
                TweenLocalPosition(brow, _browCuriousPositions[i], _browShockedPositions[i]);
                TweenLocalRotation(brow, new Vector3(0f, 0f, _browCuriousAngles[i]), new Vector3(0f, 0f, _browShockedAngles[i]));

                // 눈이 커지는 동안 시선은 위로 더 올라가고, 동공은 조금 늦게 따라 커진다.
                Transform eye = faceHead.Find("Eye" + sides[i]);
                CuttoonTransformTween open = AddTransformTween(panel, eye.Find("Opening"), EYE_OPEN_DELAY, EYE_OPEN_DURATION);
                TweenLocalScale(open, _eyeCuriousOpening, Vector3.one);
                CuttoonTransformTween look = AddTransformTween(panel, eye.Find("Gaze"), EYE_OPEN_DELAY, EYE_OPEN_DURATION);
                TweenLocalPosition(look, _gazeCurious, _gazeShocked);
                CuttoonTransformTween dilate = AddTransformTween(panel, eye.Find("Gaze/Pupil"), PUPIL_DILATE_DELAY, PUPIL_DILATE_DURATION);
                TweenLocalScale(dilate, _pupilNormal, _pupilDilated);

                // CUT-37: 동공에 비친 운석이 고르게 커지고, 눈이 다 커진 뒤에도 잠시 더 커져 운석이 2-2 전부터 다가오고 있음을 보여 준다.
                CuttoonTransformTween reflect = AddTransformTween(panel, eye.Find("Gaze/Pupil/Reflection"), REFLECTION_DELAY, REFLECTION_DURATION);
                TweenLocalScale(reflect, Vector3.one * REFLECTION_START, Vector3.one);
                SetCurve(reflect, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }

            // 입은 가장 늦게, 위쪽 입술 자리는 거의 두고 아래로 벌어진다.
            CuttoonTransformTween mouthOpen = AddTransformTween(panel, faceHead.Find("Mouth/Hole"), MOUTH_OPEN_DELAY, MOUTH_OPEN_DURATION);
            TweenLocalPosition(mouthOpen, _mouthClosedPosition, Vector3.zero);
            TweenLocalScale(mouthOpen, _mouthClosedScale, _mouthOpenScale);
        }

        /// <summary>
        /// _povSetPosition에 2-2 시민 시점 세트(바닥, 넓은 길, 길 양옆의 높은 건물, 하늘의 운석과 구름)를 만든다.
        /// 시민 눈높이 POV_EYE_HEIGHT에서 POV_PITCH만큼 올려다보고 길 방향에서 POV_YAW만큼 돈 시선 위에, 반지름 POV_METEOR_RADIUS 운석을 반지름 각도 POV_METEOR_START_ANGLE로 보이는 거리에 둔다.
        /// 운석은 시민 쪽 면에 구덩이가 있다.
        /// 운석에는 구덩이까지 덮는 하늘색 막(Haze), 운석 중심을 지나 시민을 향한 달아오른 공기 띠 판(Glow), HOT_CLOUD 구름 쪽으로 돌린 띠의 뜨거운 쪽 판(GlowHot)을 붙인다.
        /// 구름은 _cloudDistances 거리에서 시선 둘레 _cloudAngles 방향으로 _cloudOffsets만큼 떨어진 곳에 _cloudSizes 크기로, 칸 화면에 똑바로 서게 둔다.
        /// HOT_CLOUD 구름에는 운석 쪽 가장자리에서 뜯겨 나갈 조각(Shred)을 크기 0으로 숨겨 둔다.
        /// 세트는 시민이 해가 있는 쪽을 올려다보게 돌려, 운석이 해를 가리고 길과 건물이 운석 그림자에 든다.
        /// 해를 등진 건물은 어두운 재질로 실루엣처럼 보이게 하고, 구름은 그늘진 하늘에서도 밝게 보이도록 빛을 받지 않는 밝은 재질로 만든다.
        /// 세트 루트를 돌려주고, 운석 Transform과 구름 Transform 배열을 내보낸다.
        /// </summary>
        private static Transform BuildPovSet(out Transform povMeteor, out Transform[] clouds)
        {
            Transform root = CreateFromMenu(MENU_EMPTY, null, "Set_Pov").transform;
            root.position = _povSetPosition;
            root.rotation = Quaternion.Euler(0f, SunYaw() - POV_YAW, 0f);
            Material building = EnsureMaterial("GB_BuildingShade", 0.32f);
            Material crater = EnsureMaterial("GB_Crater", 0.18f);
            Material cloud = EnsureUnlitMaterial("GB_Cloud", 0.86f);

            CreatePrimitive(MENU_PLANE, root, "Ground", new Vector3(0f, 0f, 30f), new Vector3(20f, 1f, 20f), EnsureMaterial("GB_Ground", 0.34f));
            CreatePrimitive(MENU_CUBE, root, "Road", new Vector3(0f, 0.02f, 30f), new Vector3(10f, 0.04f, 200f), EnsureMaterial("GB_Road", 0.5f));

            // 길은 Z 방향으로 뻗는다. 건물 줄 함수가 X 방향으로 줄을 세우므로 90도 돌린 묶음 아래에 두 줄을 세운다. 값은 길 방향 위치, 높이 쌍이다.
            Transform street = CreateFromMenu(MENU_EMPTY_CHILD, root, "Street").transform;
            street.localPosition = Vector3.zero;
            street.localRotation = Quaternion.Euler(0f, -90f, 0f);
            // 하늘이 칸 가운데를 넉넉히 차지하도록 길을 넓게 두고, 먼 건물일수록 낮춘다.
            CreateBuildingRow(street, "BuildingLeft", new[] { -18f, 36f, -9f, 44f, 0f, 30f, 9f, 40f, 18f, 26f, 27f, 32f, 36f, 22f, 45f, 28f, 54f, 20f, 63f, 24f }, 20f, 8f, building);
            CreateBuildingRow(street, "BuildingRight", new[] { -18f, 40f, -9f, 32f, 0f, 46f, 9f, 28f, 18f, 34f, 27f, 24f, 36f, 30f, 45f, 22f, 54f, 26f, 63f, 20f }, -20f, 8f, building);

            Vector3 eye = new Vector3(0f, POV_EYE_HEIGHT, 0f);
            Quaternion look = Quaternion.Euler(-POV_PITCH, POV_YAW, 0f);
            Vector3 forward = look * Vector3.forward;
            // CUT-30: 운석은 반지름이 처음 각도로 보이는 먼 거리에서 출발한다. 크기는 그대로라 다가오면 화면 크기가 거리에 반비례해 커진다.
            float radius = POV_METEOR_RADIUS;
            float startDistance = radius / Mathf.Tan(POV_METEOR_START_ANGLE * Mathf.Deg2Rad);
            povMeteor = CreatePrimitive(MENU_SPHERE, root, "Meteor", eye + forward * startDistance, Vector3.one * (radius * 2f), EnsureMaterial("GB_Meteor", 0.3f));
            // 운석의 -Z가 시민을 보게 돌리고, 시민에게 보이는 반구에 납작한 구덩이를 둔다. 값은 구 반지름 0.5 기준 x, y, 크기다.
            povMeteor.localRotation = look;
            float[] craters = { -0.18f, -0.15f, 0.16f, 0.12f, -0.23f, 0.11f, 0f, 0.06f, 0.09f, 0.22f, 0f, 0.06f, -0.1f, 0.24f, 0.07f };
            for (int i = 0; i < craters.Length / 3; i++)
            {
                float x = craters[i * 3];
                float y = craters[i * 3 + 1];
                float size = craters[i * 3 + 2];
                Vector3 surface = new Vector3(x, y, -Mathf.Sqrt(0.25f - x * x - y * y));
                Transform pit = CreatePrimitive(MENU_SPHERE, povMeteor, "Crater" + i, surface * 0.98f, new Vector3(size, size, size * 0.3f), crater);
                pit.localRotation = Quaternion.LookRotation(surface);
                pit.GetComponent<MeshRenderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            // CUT-34: 하늘색 막은 구덩이까지 덮도록 운석보다 조금 크다. 처음 투명도로 저장하고 연출이 걷어 낸다.
            Color hazeColor = new Color(SKY_GRAY, SKY_GRAY, SKY_GRAY, HAZE_START);
            Transform haze = CreatePrimitive(MENU_SPHERE, povMeteor, "Haze", Vector3.zero, Vector3.one * HAZE_SCALE, EnsureTransparentMaterial("GB_Haze", hazeColor, null, 1));
            // CUT-30: 띠 판은 운석과 함께 돌아 앞면(-Z)이 시민을 본다. 띠 그림의 안쪽 원이 운석 반지름에 놓이는 크기라, 안쪽은 운석 몸체에 가려진다.
            Transform glow = CreatePrimitive(MENU_QUAD, povMeteor, "Glow", Vector3.zero, Vector3.one / GLOW_INNER, EnsureTransparentMaterial("GB_Glow", _glowStartColor, _glow.texture, 0));
            // CUT-34: 뜨거운 쪽 판은 띠 판과 같은 크기이고, 그림 오른쪽이 HOT_CLOUD 구름 방향을 보게 돌린다. 처음엔 투명하고 막과 띠보다 나중에 그린다.
            Color hotClear = new Color(_glowHotColor.r, _glowHotColor.g, _glowHotColor.b, 0f);
            Transform glowHot = CreatePrimitive(MENU_QUAD, povMeteor, "GlowHot", Vector3.zero, Vector3.one / GLOW_INNER, EnsureTransparentMaterial("GB_GlowHot", hotClear, _glowHot.texture, 2));
            glowHot.localRotation = Quaternion.Euler(0f, 0f, _cloudAngles[HOT_CLOUD] - GLOW_HOT_SWEEP * 0.5f);
            foreach (Transform layer in new[] { haze, glow, glowHot })
            {
                MeshRenderer layerRenderer = layer.GetComponent<MeshRenderer>();
                layerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                layerRenderer.receiveShadows = false;
            }

            // CUT-35: 구름은 칸 화면에 똑바로 서도록 시선과 같은 방향으로 돌린다. 구름 묶음의 로컬 X가 칸 가로다.
            Vector3 right = look * Vector3.right;
            Vector3 up = look * Vector3.up;
            clouds = new Transform[_cloudDistances.Length];
            for (int i = 0; i < _cloudDistances.Length; i++)
            {
                float around = _cloudAngles[i] * Mathf.Deg2Rad;
                Vector3 outward = right * Mathf.Cos(around) + up * Mathf.Sin(around);
                Transform puff = CreateFromMenu(MENU_EMPTY_CHILD, root, "Cloud" + i).transform;
                puff.localPosition = eye + SkyDirection(forward, outward, _cloudOffsets[i]) * _cloudDistances[i];
                puff.localRotation = look;
                puff.localScale = Vector3.one * _cloudSizes[i];
                CreatePrimitive(MENU_SPHERE, puff, "Middle", Vector3.zero, new Vector3(18f, 11f, 11f), cloud);
                CreatePrimitive(MENU_SPHERE, puff, "Left", new Vector3(-9f, -1.5f, 0f), new Vector3(12f, 8f, 8f), cloud);
                CreatePrimitive(MENU_SPHERE, puff, "Right", new Vector3(9f, -1.5f, 0f), new Vector3(12f, 8f, 8f), cloud);
                // CUT-35: HOT_CLOUD 구름은 운석 바로 아래에 있어 윗면이 운석을 향한다. 윗면 왼쪽에 뜯겨 나갈 조각을 크기 0으로 숨겨 둔다.
                if (i == HOT_CLOUD)
                {
                    CreatePrimitive(MENU_SPHERE, puff, "Shred", _shredStart, Vector3.zero, cloud);
                }

                foreach (MeshRenderer part in puff.GetComponentsInChildren<MeshRenderer>())
                {
                    part.shadowCastingMode = ShadowCastingMode.Off;
                    part.receiveShadows = false;
                }

                clouds[i] = puff;
            }

            return root;
        }

        /// <summary>
        /// 시선 방향 forward에서 그와 수직인 방향 outward 쪽으로 angle도 기울인 단위 방향을 돌려준다.
        /// </summary>
        private static Vector3 SkyDirection(Vector3 forward, Vector3 outward, float angle)
        {
            float radians = angle * Mathf.Deg2Rad;
            return forward * Mathf.Cos(radians) + outward * Mathf.Sin(radians);
        }

        /// <summary>
        /// 씬 방향광이 오는 쪽, 곧 해가 있는 쪽의 좌우 각도(도)를 돌려준다. 빛이 비추는 방향의 반대다.
        /// </summary>
        private static float SunYaw()
        {
            return Object.FindFirstObjectByType<Light>().transform.eulerAngles.y + 180f;
        }

        /// <summary>
        /// 2-1 얼굴 카메라 자리를 구한다. 표정 가운데(FaceFeatureCenter)에서 얼굴이 향한 방향으로 카메라를 두어, 위를 올려다보는 얼굴을 정면에서 찍는다.
        /// 칸 화면 비율 aspect로 그 점의 거리에서 가로 FACE_FRAME_WIDTH가 담기는 거리를 구하고, 그 점이 칸 화면 세로 FACE_VIEW_Y에 오게 위아래로 옮긴다.
        /// setRoot 기준 로컬 위치 localPosition과 바라볼 로컬 지점 localLookTarget을 내보낸다.
        /// </summary>
        private static void FaceCameraPose(Transform setRoot, Transform faceHead, float aspect, out Vector3 localPosition, out Vector3 localLookTarget)
        {
            Vector3 target = FaceFeatureCenter(faceHead);
            // 얼굴은 머리 축의 -Z 쪽을 본다. 카메라를 그 방향에 두어야 젖힌 얼굴도 아래에서 올려다보지 않고 정면으로 보인다.
            Vector3 toCamera = -faceHead.forward;
            float halfHeight = FACE_FRAME_WIDTH * 0.5f / aspect;
            float distance = halfHeight / Mathf.Tan(FACE_FOV * 0.5f * Mathf.Deg2Rad);

            // 바라보는 방향은 그대로 두고 카메라를 위아래로만 옮겨 표정 가운데의 화면 높이를 맞춘다.
            Vector3 cameraUp = Quaternion.LookRotation(-toCamera, Vector3.up) * Vector3.up;
            Vector3 shift = -cameraUp * ((FACE_VIEW_Y - 0.5f) * 2f * halfHeight);
            localPosition = setRoot.InverseTransformPoint(target + toCamera * distance + shift);
            localLookTarget = setRoot.InverseTransformPoint(target + shift);
        }

        /// <summary>
        /// 머리 축 faceHead 아래 두 눈(EyeLeft, EyeRight)의 가운데와 입(Mouth)의 중간점, 곧 표정의 가운데를 월드 위치로 돌려준다.
        /// </summary>
        private static Vector3 FaceFeatureCenter(Transform faceHead)
        {
            Vector3 eyes = (faceHead.Find("EyeLeft").position + faceHead.Find("EyeRight").position) * 0.5f;
            return (eyes + faceHead.Find("Mouth").position) * 0.5f;
        }

        /// <summary>
        /// 1-2 줌인 세트 setRoot에 새 떼를 만들고, 칸 panel에 새마다 날아가기 연출과 날갯짓 연출을 단다.
        /// 새는 camera에서 _birdDepths만큼 떨어진 곳에서 칸 화면 오른쪽 아래 밖을 출발해, 시민 subject(월드 위치)가 보이는 높이를 지나 왼쪽 위 밖으로 날아간다.
        /// 새 등은 카메라를 향해 날개 편 모양이 보이고, 가까운 새일수록 크게 보이고 빨리 지나가며, 그림자를 드리우지 않는다. 씬에는 출발 자리에 둔다.
        /// </summary>
        private static void CreateBirdFlock(Transform setRoot, Camera camera, Vector3 subject, RectTransform panel)
        {
            camera.aspect = (float)camera.targetTexture.width / camera.targetTexture.height;
            Vector2 subjectView = camera.WorldToViewportPoint(subject);
            // CUT-26: 새는 흰색이다.
            Material material = EnsureMaterial("GB_Bird", 0.92f);
            for (int i = 0; i < _birdDepths.Length; i++)
            {
                float lane = subjectView.y + _birdLanes[i];
                Vector3 start = camera.ViewportToWorldPoint(new Vector3(BIRD_START_X, lane - BIRD_RISE, _birdDepths[i]));
                Vector3 end = camera.ViewportToWorldPoint(new Vector3(BIRD_END_X, lane + BIRD_RISE, _birdDepths[i]));
                Transform bird = CreateBird(setRoot, "Bird_" + (i + 1), material, out Transform leftWing, out Transform rightWing);
                bird.position = start;
                // 내려다보는 카메라에서 날개 편 모양이 보이도록 새 등을 카메라 쪽으로 향한다. 출발점과 도착점은 카메라에서 같은 거리라 나는 방향이 등과 수직이다.
                bird.rotation = Quaternion.LookRotation(end - start, -camera.transform.forward);

                float delay = BIRD_START_DELAY + _birdDelays[i];
                float duration = _birdDepths[i] * BIRD_SECONDS_PER_METER;
                CuttoonTransformTween fly = AddTransformTween(panel, bird, delay, duration);
                TweenLocalPosition(fly, setRoot.InverseTransformPoint(start), setRoot.InverseTransformPoint(end));
                SetCurve(fly, AnimationCurve.Linear(0f, 0f, 1f, 1f));

                CuttoonFlap flap = panel.gameObject.AddComponent<CuttoonFlap>();
                SetRef(flap, "_leftWing", leftWing);
                SetRef(flap, "_rightWing", rightWing);
                SetFloat(flap, "_delay", delay);
                SetFloat(flap, "_duration", duration);
                SetFloat(flap, "_frequency", _birdFrequencies[i]);
                SetFloat(flap, "_amplitude", BIRD_FLAP_AMPLITUDE);
                SetFloat(flap, "_phase", _birdPhases[i]);
            }

            camera.ResetAspect();
        }

        /// <summary>
        /// setRoot 아래에 새 name 하나(길쭉한 몸통과 머리, 꼬리, 조금 뒤로 젖힌 가늘고 긴 판 날개 두 장)를 material로 만든다.
        /// 날갯짓 축이 되는 왼쪽·오른쪽 날개 Transform을 leftWing, rightWing으로 내보내고, 그림자를 끈 새 Transform을 돌려준다.
        /// </summary>
        private static Transform CreateBird(Transform setRoot, string name, Material material, out Transform leftWing, out Transform rightWing)
        {
            Transform bird = CreateFromMenu(MENU_EMPTY_CHILD, setRoot, name).transform;
            CreatePrimitive(MENU_SPHERE, bird, "Body", Vector3.zero, new Vector3(0.1f, 0.08f, 0.3f), material);
            CreatePrimitive(MENU_SPHERE, bird, "Head", new Vector3(0f, 0f, 0.17f), Vector3.one * 0.08f, material);
            CreatePrimitive(MENU_CUBE, bird, "Tail", new Vector3(0f, 0f, -0.2f), new Vector3(0.11f, 0.015f, 0.12f), material);
            leftWing = CreateFromMenu(MENU_EMPTY_CHILD, bird, "WingLeft").transform;
            rightWing = CreateFromMenu(MENU_EMPTY_CHILD, bird, "WingRight").transform;
            Transform leftBlade = CreatePrimitive(MENU_CUBE, leftWing, "Blade", new Vector3(-0.27f, 0f, -0.02f), new Vector3(0.54f, 0.015f, 0.09f), material);
            leftBlade.localRotation = Quaternion.Euler(0f, -8f, 0f);
            Transform rightBlade = CreatePrimitive(MENU_CUBE, rightWing, "Blade", new Vector3(0.27f, 0f, -0.02f), new Vector3(0.54f, 0.015f, 0.09f), material);
            rightBlade.localRotation = Quaternion.Euler(0f, 8f, 0f);

            // 새는 카메라 앞을 지나는 실루엣이라 땅에 그림자를 남기지 않는다.
            foreach (MeshRenderer part in bird.GetComponentsInChildren<MeshRenderer>())
            {
                part.shadowCastingMode = ShadowCastingMode.Off;
            }

            return bird;
        }

        /// <summary>
        /// 3D 기본 도형 메뉴 menuPath로 parent 아래에 name을 만들고 로컬 위치, 크기, 재질을 넣는다. 만든 Transform을 돌려준다.
        /// </summary>
        private static Transform CreatePrimitive(string menuPath, Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject created = CreateFromMenu(menuPath, parent, name);
            Transform primitive = created.transform;
            primitive.localPosition = localPosition;
            primitive.localRotation = Quaternion.identity;
            primitive.localScale = localScale;
            created.GetComponent<MeshRenderer>().sharedMaterial = material;
            return primitive;
        }

        /// <summary>
        /// 씬 최상위에 그림자를 드리우는 방향광을 만든다. 카메라 쪽 뒤에서 비춰 건물의 보이는 면이 밝게 나온다.
        /// </summary>
        private static void CreateLight()
        {
            GameObject lightObject = CreateFromMenu(MENU_EMPTY, null, "Directional Light");
            lightObject.transform.rotation = Quaternion.Euler(65f, 45f, 0f);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.shadows = LightShadows.Hard;
            light.shadowStrength = 0.9f;
        }

        /// <summary>
        /// setRoot 아래 로컬 위치 localPosition에 칸 카메라 name을 만들고 localLookTarget을 바라보게 한다.
        /// 시야각 fieldOfView, 배경색 background로 UI 레이어를 뺀 장면을 target에 그린다. 만든 Camera를 돌려준다.
        /// </summary>
        private static Camera CreateSetCamera(string name, Transform setRoot, Vector3 localPosition, Vector3 localLookTarget, float fieldOfView, Color background, RenderTexture target)
        {
            GameObject cameraObject = CreateFromMenu(MENU_EMPTY_CHILD, setRoot, name);
            cameraObject.transform.localPosition = localPosition;
            cameraObject.transform.LookAt(setRoot.TransformPoint(localLookTarget));
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;
            camera.fieldOfView = fieldOfView;
            camera.nearClipPlane = 0.3f;
            camera.farClipPlane = 300f;
            camera.cullingMask = ~(1 << LayerMask.NameToLayer(UI_LAYER));
            camera.targetTexture = target;
            return camera;
        }

        /// <summary>
        /// UI Raw Image 메뉴로 panel을 꽉 채우는 화면을 만들고 camera가 그리는 RenderTexture를 보여 준다. 만든 RawImage를 돌려준다.
        /// </summary>
        private static RawImage CreateView(RectTransform panel, Camera camera)
        {
            RawImage view = CreateFromMenu(MENU_RAW_IMAGE, panel, "View").GetComponent<RawImage>();
            view.texture = camera.targetTexture;
            view.raycastTarget = false;
            SetAnchors(view.rectTransform, 0f, 0f, 1f, 1f);
            return view;
        }

        /// <summary>
        /// 1-3 카메라의 시작 자리를 구한다. 최종 구도 camera의 좌우 방향을 몸이 돌아선 _bodyTurn만큼 돌려 몸 정면 쪽에서 내려다보게 하고,
        /// 고개를 숙였을 때와 든 때의 머리 중심 평균이 _bustStartHeadViewport에 오도록 최종보다 BUST_START_CLOSER만큼 가까이 둔다.
        /// setRoot 기준 로컬 위치 localPosition과 로컬 회전(오일러 각) localRotation, 카메라가 둘레를 도는 머리 중심 pivot(로컬)과
        /// 시작 거리 distance를 내보내며, headPivot 회전은 원래대로 되돌린다. 고개를 든 쪽은 정면 구간의 최대 각도 _headPeak로 잰다.
        /// </summary>
        private static void BustStartPose(Camera camera, Transform setRoot, Transform headPivot, out Vector3 localPosition, out Vector3 localRotation, out Vector3 pivot, out float distance)
        {
            Quaternion keptRotation = headPivot.localRotation;
            headPivot.localRotation = Quaternion.Euler(_headDown);
            Vector3 downCenter = HeadCenter(headPivot);
            headPivot.localRotation = Quaternion.Euler(_headPeak);
            Vector3 upCenter = HeadCenter(headPivot);
            headPivot.localRotation = keptRotation;
            Vector3 head = (downCenter + upCenter) * 0.5f;

            pivot = setRoot.InverseTransformPoint(head);
            Transform cameraTransform = camera.transform;
            distance = Vector3.Dot(head - cameraTransform.position, cameraTransform.forward) - BUST_START_CLOSER;
            localRotation = cameraTransform.localEulerAngles + new Vector3(0f, _bodyTurn.y, 0f);
            Quaternion rotation = setRoot.rotation * Quaternion.Euler(localRotation);
            float halfHeight = distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float halfWidth = halfHeight * camera.aspect;

            // 머리를 칸 안 정규화 위치에 두려면 카메라를 그 반대 방향으로 옮긴다.
            Vector3 world = head - rotation * Vector3.forward * distance
                - rotation * Vector3.right * ((_bustStartHeadViewport.x - 0.5f) * 2f * halfWidth)
                - rotation * Vector3.up * ((_bustStartHeadViewport.y - 0.5f) * 2f * halfHeight);
            localPosition = setRoot.InverseTransformPoint(world);
        }

        /// <summary>
        /// 고개 들기 곡선을 만든다. 0에서 출발해 raiseEnd 지점에서 peak 값까지 오르고, holdEnd까지 머문 뒤 끝에서 1로 내려온다.
        /// 모든 열쇠의 기울기를 0으로 두어 구간마다 천천히 시작해 천천히 끝난다. 만든 곡선을 돌려준다.
        /// </summary>
        private static AnimationCurve HeadRaiseCurve(float raiseEnd, float holdEnd, float peak)
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f, 0f, 0f),
                new Keyframe(raiseEnd, peak, 0f, 0f),
                new Keyframe(holdEnd, peak, 0f, 0f),
                new Keyframe(1f, 1f, 0f, 0f));
        }

        /// <summary>
        /// headPivot 아래 메시 렌더러 범위를 모두 합친 월드 중심을 돌려준다.
        /// </summary>
        private static Vector3 HeadCenter(Transform headPivot)
        {
            MeshRenderer[] renderers = headPivot.GetComponentsInChildren<MeshRenderer>();
            Bounds bounds = renderers[0].bounds;
            foreach (MeshRenderer part in renderers)
            {
                bounds.Encapsulate(part.bounds);
            }

            return bounds.center;
        }

        /// <summary>
        /// camera로 본 parts 메시 꼭짓점의 화면 범위를 뷰포트 좌표(0~1)로 구해 돌려준다.
        /// 칸 화면은 칸을 꽉 채우므로, 돌려주는 Rect는 칸 안 정규화 범위와 같다.
        /// </summary>
        private static Rect ViewportBounds(Camera camera, MeshFilter[] parts)
        {
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (MeshFilter part in parts)
            {
                Matrix4x4 toWorld = part.transform.localToWorldMatrix;
                foreach (Vector3 vertex in part.sharedMesh.vertices)
                {
                    Vector2 viewport = camera.WorldToViewportPoint(toWorld.MultiplyPoint3x4(vertex));
                    min = Vector2.Min(min, viewport);
                    max = Vector2.Max(max, viewport);
                }
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        /// <summary>
        /// 머리 화면 범위 head(칸 안 정규화 값)에서 오른쪽으로 QUESTION_GAP 픽셀 떨어지고, 머리 높이의 QUESTION_HEIGHT_RATIO 지점인 위치를 돌려준다.
        /// 픽셀 간격은 칸 크기 panelSize로 정규화하며, ?의 왼쪽 가운데를 이 위치에 맞춘다.
        /// </summary>
        private static Vector2 QuestionPoint(Rect head, Vector2 panelSize)
        {
            return new Vector2(head.xMax + QUESTION_GAP / panelSize.x, head.yMin + head.height * QUESTION_HEIGHT_RATIO);
        }

        /// <summary>
        /// 칸 id용 RenderTexture 애셋을 RENDER_TEXTURE_FOLDER에서 찾아 돌려준다. 없으면 칸 크기 size로 만든다.
        /// </summary>
        private static RenderTexture EnsureRenderTexture(string id, Vector2 size)
        {
            EnsureFolder(RENDER_TEXTURE_FOLDER);
            string path = RENDER_TEXTURE_FOLDER + "/RT_" + id + ".renderTexture";
            RenderTexture texture = AssetDatabase.LoadAssetAtPath<RenderTexture>(path);
            if (texture != null)
            {
                return texture;
            }

            texture = new RenderTexture(Mathf.RoundToInt(size.x), Mathf.RoundToInt(size.y), 24);
            texture.antiAliasing = 4;
            texture.name = "RT_" + id;
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }

        /// <summary>
        /// 회색 재질 name을 MATERIAL_FOLDER에서 찾아 돌려준다. 없으면 밝기 gray인 URP Lit 재질로 만든다.
        /// </summary>
        private static Material EnsureMaterial(string name, float gray)
        {
            EnsureFolder(MATERIAL_FOLDER);
            string path = MATERIAL_FOLDER + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find(LIT_SHADER));
            material.SetColor("_BaseColor", Gray(gray));
            material.SetFloat("_Smoothness", 0.1f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 빛을 받지 않는 회색 재질 name을 MATERIAL_FOLDER에서 찾아 돌려준다. 없으면 밝기 gray인 URP Unlit 재질로 만든다.
        /// </summary>
        private static Material EnsureUnlitMaterial(string name, float gray)
        {
            EnsureFolder(MATERIAL_FOLDER);
            string path = MATERIAL_FOLDER + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find(UNLIT_SHADER));
            material.SetColor("_BaseColor", Gray(gray));
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 투명한 재질 name을 MATERIAL_FOLDER에서 찾아 돌려준다. 없으면 색 color, 그림 texture(없으면 null)로 알파 섞기 URP Unlit 재질을 만든다.
        /// 깊이를 쓰지 않아 앞의 불투명 도형에는 가려지고 뒤의 것 위에는 섞여 그려진다. 같은 거리의 투명 재질끼리는 queueOffset이 큰 쪽이 나중에 그려진다.
        /// </summary>
        private static Material EnsureTransparentMaterial(string name, Color color, Texture texture, int queueOffset)
        {
            EnsureFolder(MATERIAL_FOLDER);
            string path = MATERIAL_FOLDER + "/" + name + ".mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find(UNLIT_SHADER));
            material.SetColor("_BaseColor", color);
            material.SetTexture("_BaseMap", texture);
            // 재질 화면이 설정을 다시 맞춰도 같은 결과가 나오도록 표면 종류, 섞기 방식, 순서 값도 함께 넣는다.
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_QueueOffset", queueOffset);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            material.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.renderQueue = (int)RenderQueue.Transparent + queueOffset;
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 2쪽(2-1 시민의 얼굴, 2-2 운석)을 pagesRoot 아래에 깔고 쪽 컴포넌트를 돌려준다.
        /// 2-1은 faceSet의 얼굴(머리 축 faceHead)을 정면에서 표정이 칸을 채우게 찍고, 표정이 호기심에서 경악으로 바뀌는 연출을 단다.
        /// 2-2는 povSet에서 시민 눈높이로 올려다본 하늘을 보여 주고, povMeteor가 묵직하게 떠나 점점 빨라지며 시선 방향으로 다가오고, 흐림이 걷히고, 가장자리 띠가 진해지며 살구빛이 되는 연출을 단다.
        /// 띠의 HOT_CLOUD 구름 쪽이 흰빛에 가깝게 세지며 흐르고, 띠가 그 구름에 닿을 때 운석 쪽 가장자리가 뜯기며, clouds는 바람에 천천히 흐른다.
        /// </summary>
        private static CuttoonPage BuildPage2(RectTransform pagesRoot, Transform faceSet, Transform faceHead, Transform povSet, Transform povMeteor, Transform[] clouds)
        {
            RectTransform page = CreatePage(pagesRoot, "Page2");
            List<CuttoonPanel> panels = new List<CuttoonPanel>();

            // CUT-28: 2-1 시민의 얼굴. 위를 올려다보는 얼굴을 정면에서, 표정이 칸을 꽉 채우게 찍는다.
            RectTransform facePanel = CreatePanel(page, "2-1", 0f, 0f, 0.24f, 1f, Gray(0.14f), out Vector2 faceSize);
            FaceCameraPose(faceSet, faceHead, faceSize.x / faceSize.y, out Vector3 faceCameraPosition, out Vector3 faceLookTarget);
            Camera faceCamera = CreateSetCamera("Camera_2-1", faceSet, faceCameraPosition, faceLookTarget, FACE_FOV, Gray(SKY_GRAY), EnsureRenderTexture("2-1", faceSize));
            CreateView(facePanel, faceCamera);
            // CUT-32: 호기심 어린 표정이 눈썹, 눈, 동공, 입 순서로 조금씩 겹치며 경악한 표정으로 바뀐다.
            AddFaceChange(facePanel, faceHead);
            CreateLabel(facePanel, faceSize, "2-1", "시민의 얼굴", "위를 올려다본 얼굴 정면, 호기심 어린 표정이 점점 경악으로 바뀐다. 동공에 비친 운석이 점점 커진다");
            panels.Add(FinishPanel(facePanel));

            // CUT-29: 2-2 운석. 2-1의 시민이 길 양옆 건물 사이로 올려다본 하늘이다. 운석은 화면 가운데에 있다.
            RectTransform meteorPanel = CreatePanel(page, "2-2", 0.24f, 0f, 1f, 1f, Gray(0.16f), out Vector2 meteorSize);
            Vector3 eye = new Vector3(0f, POV_EYE_HEIGHT, 0f);
            Quaternion look = Quaternion.Euler(-POV_PITCH, POV_YAW, 0f);
            Vector3 forward = look * Vector3.forward;
            Camera povCamera = CreateSetCamera("Camera_2-2", povSet, eye, eye + forward * 10f, POV_FOV, Gray(SKY_GRAY), EnsureRenderTexture("2-2", meteorSize));
            // CUT-30: 처음 거리의 운석과 그 뒤의 구름까지 그린다.
            povCamera.farClipPlane = POV_FAR;
            CreateView(meteorPanel, povCamera);

            // CUT-30: 운석은 크기 그대로 시선 방향으로 다가온다. 거의 멈춘 듯 묵직하게 떠나 점점 빨라지고, 끝에는 칸 위아래를 넘치게 차서 하늘을 닫는다.
            CuttoonTransformTween approach = AddTransformTween(meteorPanel, povMeteor, POV_APPROACH_DELAY, POV_APPROACH_DURATION);
            TweenLocalPosition(approach, povMeteor.localPosition, eye + forward * POV_METEOR_DISTANCE);
            SetCurve(approach, new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, POV_APPROACH_END_SLOPE, 0f)));

            // CUT-34: 멀 때는 하늘색 막이 운석 몸체와 구덩이를 하늘빛에 묻고, 다가올수록 걷혀 운석이 짙고 또렷해진다.
            CuttoonColorTween clear = AddColorTween(meteorPanel, povMeteor.Find("Haze").GetComponent<Renderer>(), POV_APPROACH_DELAY, POV_APPROACH_DURATION);
            TweenColor(clear, new Color(SKY_GRAY, SKY_GRAY, SKY_GRAY, HAZE_START), new Color(SKY_GRAY, SKY_GRAY, SKY_GRAY, 0f));
            SetCurve(clear, EaseInCurve());

            // CUT-30, CUT-34: 운석 앞에서 짓눌려 달아오른 공기는 흰빛으로 옅게 시작해 다가올수록 진해지며 옅은 살구빛이 된다.
            CuttoonColorTween heat = AddColorTween(meteorPanel, povMeteor.Find("Glow").GetComponent<Renderer>(), POV_APPROACH_DELAY, POV_APPROACH_DURATION);
            TweenColor(heat, _glowStartColor, _glowEndColor);
            SetCurve(heat, EaseInCurve());

            // CUT-34: 운석이 가까워지면 띠 가운데 HOT_CLOUD 구름을 향한 쪽이 흰빛에 가깝게 세지고, 들쭉날쭉한 폭이 천천히 흐른다.
            // 달아오른 공기가 그 구름을 스치기 직전임을 먼저 보여 준다.
            Transform glowHot = povMeteor.Find("GlowHot");
            Color hotClear = new Color(_glowHotColor.r, _glowHotColor.g, _glowHotColor.b, 0f);
            CuttoonColorTween hot = AddColorTween(meteorPanel, glowHot.GetComponent<Renderer>(), GLOW_HOT_DELAY, GLOW_HOT_DURATION);
            TweenColor(hot, hotClear, new Color(_glowHotColor.r, _glowHotColor.g, _glowHotColor.b, GLOW_HOT_ALPHA));
            float hotAngle = _cloudAngles[HOT_CLOUD];
            CuttoonTransformTween flow = AddTransformTween(meteorPanel, glowHot, GLOW_HOT_DELAY, GLOW_HOT_SWEEP_DURATION);
            TweenLocalRotation(flow, new Vector3(0f, 0f, hotAngle - GLOW_HOT_SWEEP * 0.5f), new Vector3(0f, 0f, hotAngle + GLOW_HOT_SWEEP * 0.5f));
            SetCurve(flow, AnimationCurve.Linear(0f, 0f, 1f, 1f));

            // CUT-35: 구름은 운석에 밀리지 않고 바람에 아주 천천히 칸 왼쪽으로 흐른다. 같은 거리를 흘러 가까운 구름이 화면에서 더 빨리 움직인다.
            Vector3 wind = look * Vector3.left * CLOUD_DRIFT;
            foreach (Transform cloud in clouds)
            {
                CuttoonTransformTween drift = AddTransformTween(meteorPanel, cloud, 0f, POV_APPROACH_DELAY + POV_APPROACH_DURATION);
                TweenLocalPosition(drift, cloud.localPosition, cloud.localPosition + wind);
                SetCurve(drift, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }

            // CUT-35: 띠가 HOT_CLOUD 구름 윗면에 닿는 순간, 왼쪽 덩이가 운석 가장자리를 따라 옆으로 떨어져 나가며 작아지고 윗면 조각 하나가 더 멀리 날아간다.
            // 구름 묶음은 시선과 같은 방향으로 돌아 있어 로컬 X·Y가 칸 가로·세로다. 거리는 묶음 크기로 나눠 미터로 맞춘다.
            Transform hotCloud = clouds[HOT_CLOUD];
            Transform edge = hotCloud.Find("Left");
            Transform shred = hotCloud.Find("Shred");
            Vector3 pushWay = new Vector3(Mathf.Cos(TEAR_PUSH_ANGLE * Mathf.Deg2Rad), Mathf.Sin(TEAR_PUSH_ANGLE * Mathf.Deg2Rad), 0f) / hotCloud.localScale.x;
            Vector3 flingWay = new Vector3(Mathf.Cos(TEAR_FLING_ANGLE * Mathf.Deg2Rad), Mathf.Sin(TEAR_FLING_ANGLE * Mathf.Deg2Rad), 0f) / hotCloud.localScale.x;
            CuttoonTransformTween tear = AddTransformTween(meteorPanel, edge, TEAR_DELAY, TEAR_DURATION);
            TweenLocalPosition(tear, edge.localPosition, edge.localPosition + pushWay * TEAR_PUSH);
            TweenLocalScale(tear, edge.localScale, _tornPuffScale);
            SetCurve(tear, EaseOutCurve());
            CuttoonTransformTween fling = AddTransformTween(meteorPanel, shred, TEAR_DELAY, TEAR_DURATION);
            TweenLocalPosition(fling, _shredStart, _shredStart + flingWay * TEAR_FLING);
            TweenLocalScale(fling, Vector3.zero, _shredScale);
            SetCurve(fling, EaseOutCurve());

            // CUT-31 폐기: 효과음 글자는 싸 보여서 넣지 않는다.
            // CUT-09 폐기: 원인이 화면에 없는 2쪽 흔들림은 넣지 않는다.
            CreateLabel(meteorPanel, meteorSize, "2-2", "운석", "시민이 올려다본 하늘, 흐릿하던 운석이 점점 빨라지며 하늘을 덮고, 달아오른 공기에 아래 구름이 뜯긴다");
            panels.Add(FinishPanel(meteorPanel));

            return FinishPage(page, panels, 0f);
        }

        /// <summary>
        /// 3쪽(3-1 충돌, 3-2 멸망과 출발, 3-3 우주선)을 pagesRoot 아래에 깔고 쪽 컴포넌트를 돌려준다.
        /// 3-1에는 flash를 쓰는 번쩍임 연출을 단다.
        /// </summary>
        private static CuttoonPage BuildPage3(RectTransform pagesRoot, CanvasGroup flash)
        {
            RectTransform page = CreatePage(pagesRoot, "Page3");
            List<CuttoonPanel> panels = new List<CuttoonPanel>();

            // 3-1 충돌
            RectTransform impact = CreatePanel(page, "3-1", 0f, 0.8f, 1f, 1f, Gray(0.92f), out Vector2 impactSize);
            CuttoonFlash flashEffect = impact.gameObject.AddComponent<CuttoonFlash>();
            SetRef(flashEffect, "_overlay", flash);
            CreateLabel(impact, impactSize, "3-1", "충돌", "칸 전체가 하얗게 번쩍인다");
            panels.Add(FinishPanel(impact));

            // 3-2 멸망과 출발
            RectTransform departure = CreatePanel(page, "3-2", 0f, 0f, 0.7f, 0.8f, Gray(0.06f), out Vector2 departureSize);
            CreateStars(departure, new[]
            {
                0.05f, 0.9f, 0.15f, 0.75f, 0.28f, 0.95f, 0.72f, 0.88f, 0.85f, 0.7f, 0.93f, 0.92f, 0.66f, 0.35f,
                0.9f, 0.2f, 0.78f, 0.1f, 0.08f, 0.15f, 0.18f, 0.35f, 0.95f, 0.5f, 0.58f, 0.95f, 0.45f, 0.05f,
            });
            Image earth = CreateImage(departure, "Earth", _circle, Gray(0.4f));
            SetPoint(earth.rectTransform, new Vector2(0.36f, 0.5f), new Vector2(600f, 600f));
            earth.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            Image fire = CreateImage(earth.transform, "Fire", _circle, _fireColor);
            SetPoint(fire.rectTransform, new Vector2(0.74f, 0.76f), new Vector2(240f, 240f));
            Image ship = CreateImage(departure, "Ship", _triangle, Gray(0.88f));
            SetPoint(ship.rectTransform, new Vector2(0.6f, 0.6f), new Vector2(64f, 40f));
            ship.rectTransform.localEulerAngles = new Vector3(0f, 0f, 28f);
            CuttoonTween burn = AddTween(departure, fire.rectTransform, 0.2f, 2.6f);
            TweenScale(burn, Vector3.one * 0.2f, Vector3.one * 1.4f);
            CuttoonTween escape = AddTween(departure, ship.rectTransform, 0.4f, 3f);
            TweenPosition(escape, Vector2.zero, new Vector2(360f, 210f));
            CreateLabel(departure, departureSize, "3-2", "멸망과 출발", "불길이 번지는 지구 옆으로 작은 우주선이 빠져나간다");
            panels.Add(FinishPanel(departure));

            // 3-3 우주선
            RectTransform voyage = CreatePanel(page, "3-3", 0.7f, 0f, 1f, 0.8f, Gray(0.06f), out Vector2 voyageSize);
            CreateStars(voyage, new[] { 0.1f, 0.85f, 0.3f, 0.2f, 0.7f, 0.9f, 0.85f, 0.3f, 0.55f, 0.68f, 0.2f, 0.55f });
            RectTransform shipSide = CreateRect(voyage, "Ship");
            SetPoint(shipSide, new Vector2(0.45f, 0.5f), new Vector2(300f, 90f));
            Image flame = CreateImage(shipSide, "Flame", _triangle, _fireColor);
            SetAnchors(flame.rectTransform, -0.2f, 0.36f, 0.12f, 0.64f);
            flame.rectTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
            SetAnchors(CreateImage(shipSide, "FinTop", null, Gray(0.55f)).rectTransform, 0.14f, 0.7f, 0.36f, 0.9f);
            SetAnchors(CreateImage(shipSide, "FinBottom", null, Gray(0.55f)).rectTransform, 0.14f, 0.1f, 0.36f, 0.3f);
            SetAnchors(CreateImage(shipSide, "Body", _rounded, Gray(0.72f)).rectTransform, 0.1f, 0.3f, 0.76f, 0.7f);
            SetAnchors(CreateImage(shipSide, "Nose", _triangle, Gray(0.72f)).rectTransform, 0.75f, 0.3f, 1f, 0.7f);
            SetPoint(CreateImage(shipSide, "Window", _circle, Gray(0.25f)).rectTransform, new Vector2(0.62f, 0.5f), new Vector2(18f, 18f));
            CuttoonTween cruise = AddTween(voyage, shipSide, 0f, 2.8f);
            TweenPosition(cruise, new Vector2(-140f, 0f), new Vector2(60f, 0f));
            CreateLabel(voyage, voyageSize, "3-3", "우주선", "왼쪽에서 오른쪽으로 나아가는 우주선과 엔진 불꽃");
            panels.Add(FinishPanel(voyage));

            return FinishPage(page, panels, 0f);
        }

        /// <summary>
        /// 4쪽(4-1 창 너머 지구, 4-2 우는 사람들, 4-3 주인공의 눈)을 pagesRoot 아래에 깔고 쪽 컴포넌트를 돌려준다.
        /// </summary>
        private static CuttoonPage BuildPage4(RectTransform pagesRoot)
        {
            RectTransform page = CreatePage(pagesRoot, "Page4");
            List<CuttoonPanel> panels = new List<CuttoonPanel>();

            // 4-1 창 너머 지구
            RectTransform window = CreatePanel(page, "4-1", 0f, 0.45f, 1f, 1f, Gray(0.22f), out Vector2 windowSize);
            Image frame = CreateImage(window, "WindowFrame", _rounded, Gray(0.34f));
            SetAnchors(frame.rectTransform, 0.1f, 0.12f, 0.9f, 0.97f);
            Image glass = CreateImage(frame.transform, "Glass", null, Gray(0.05f));
            SetAnchors(glass.rectTransform, 0.02f, 0.05f, 0.98f, 0.95f);
            glass.gameObject.AddComponent<RectMask2D>();
            CreateStars(glass.rectTransform, new[] { 0.1f, 0.8f, 0.25f, 0.3f, 0.75f, 0.85f, 0.9f, 0.4f, 0.6f, 0.15f, 0.35f, 0.9f });
            Image burningEarth = CreateImage(glass.transform, "BurningEarth", _circle, _burningEarthColor);
            SetPoint(burningEarth.rectTransform, new Vector2(0.5f, 0.55f), new Vector2(280f, 280f));
            float[] backs = { 0.1f, 0.22f, 0.34f, 0.47f, 0.6f, 0.73f, 0.86f };
            for (int i = 0; i < backs.Length; i++)
            {
                RectTransform back = CreatePerson(window, "Back" + i, new Vector2(backs[i], 0f), 100f + (i % 3) * 12f, Gray(0.09f));
                back.anchoredPosition = new Vector2(0f, -40f);
            }

            CuttoonTween shrink = AddTween(window, burningEarth.rectTransform, 0.3f, 4.5f);
            TweenScale(shrink, Vector3.one, Vector3.one * 0.4f);
            CreateLabel(window, windowSize, "4-1", "창 너머 지구", "창가에 늘어선 뒷모습들, 창밖의 붉은 지구가 작아진다");
            panels.Add(FinishPanel(window));

            // 4-2 우는 사람들
            RectTransform crying = CreatePanel(page, "4-2", 0f, 0f, 0.62f, 0.45f, Gray(0.2f), out Vector2 cryingSize);
            float[] headX = { 0.14f, 0.37f, 0.6f, 0.84f };
            float[] headY = { 0.62f, 0.56f, 0.6f, 0.53f };
            const int PROTAGONIST_INDEX = 2;
            for (int i = 0; i < headX.Length; i++)
            {
                bool isProtagonist = i == PROTAGONIST_INDEX;
                Image body = CreateImage(crying, "Shoulders" + i, _rounded, isProtagonist ? Gray(0.62f) : Gray(0.42f));
                SetPoint(body.rectTransform, new Vector2(headX[i], 0f), new Vector2(210f, headY[i] * cryingSize.y - 60f), new Vector2(0.5f, 0f));
                if (isProtagonist)
                {
                    SetAnchors(CreateImage(body.transform, "Collar", null, Gray(0.2f)).rectTransform, 0.38f, 0.82f, 0.62f, 1f);
                    Text mark = CreateText(body.transform, "Mark", "주인공 · 제복", LABEL_FONT_SIZE, Gray(0.15f), TextAnchor.MiddleCenter);
                    SetAnchors(mark.rectTransform, 0f, 0.2f, 1f, 0.7f);
                }

                Image head = CreateImage(crying, "Head" + i, _circle, Gray(0.72f));
                SetPoint(head.rectTransform, new Vector2(headX[i], headY[i]), new Vector2(150f, 150f));
                SetPoint(CreateImage(head.transform, "Nose", null, Gray(0.72f)).rectTransform, new Vector2(1f, 0.45f), new Vector2(22f, 30f), new Vector2(0f, 0.5f));
                SetPoint(CreateImage(head.transform, "Eye", _circle, Gray(0.1f)).rectTransform, new Vector2(0.7f, 0.6f), new Vector2(16f, 16f));
                Image tear = CreateImage(head.transform, "Tear", _circle, Gray(0.95f));
                SetPoint(tear.rectTransform, new Vector2(0.68f, 0.44f), new Vector2(14f, 14f));
                if (i == 0 || isProtagonist)
                {
                    CuttoonTween fall = AddTween(crying, tear.rectTransform, i == 0 ? 0.4f : 1f, 2.4f);
                    TweenPosition(fall, Vector2.zero, new Vector2(0f, -40f));
                }
            }

            CreateLabel(crying, cryingSize, "4-2", "우는 사람들", "옆얼굴과 눈물, 그중 제복을 입은 사람이 주인공");
            panels.Add(FinishPanel(crying));

            // 4-3 주인공의 눈
            RectTransform heroEye = CreatePanel(page, "4-3", 0.62f, 0f, 1f, 0.45f, Gray(0.14f), out Vector2 heroEyeSize);
            CreateEye(heroEye, heroEyeSize, 0.1f, 0.25f, 0.9f, 0.75f, _burningEarthColor);
            Image heroTear = CreateImage(heroEye, "Tear", _circle, Gray(0.95f));
            SetPoint(heroTear.rectTransform, new Vector2(0.42f, 0.25f), new Vector2(24f, 24f));
            CuttoonTween heroFall = AddTween(heroEye, heroTear.rectTransform, 0.5f, 2.2f);
            TweenPosition(heroFall, Vector2.zero, new Vector2(0f, -70f));
            CreateLabel(heroEye, heroEyeSize, "4-3", "주인공의 눈", "눈물이 맺히고, 동공에 타는 지구가 비친다");
            panels.Add(FinishPanel(heroEye));

            return FinishPage(page, panels, 0f);
        }

        /// <summary>
        /// 5쪽(5-1 수면 캡슐, 5-2 뚜껑이 닫힘, 5-3 눈을 감다)을 pagesRoot 아래에 깔고 쪽 컴포넌트를 돌려준다.
        /// 5-3에는 뜬 눈, 반쯤 감은 눈, 감은 눈을 차례로 보이는 단계 연출을 단다.
        /// </summary>
        private static CuttoonPage BuildPage5(RectTransform pagesRoot)
        {
            RectTransform page = CreatePage(pagesRoot, "Page5");
            List<CuttoonPanel> panels = new List<CuttoonPanel>();

            // 5-1 수면 캡슐
            RectTransform hall = CreatePanel(page, "5-1", 0f, 0.45f, 1f, 1f, Gray(0.18f), out Vector2 hallSize);
            SetAnchors(CreateImage(hall, "Floor", null, Gray(0.24f)).rectTransform, 0f, 0f, 1f, 0.22f);

            // 캡슐은 오른쪽으로 갈수록 작아지고 조금씩 높아져 멀리 이어지는 줄처럼 보이게 한다.
            float capsuleX = 50f;
            float capsuleScale = 1f;
            for (int i = 0; i < 12; i++)
            {
                Vector2 capsuleSize = new Vector2(170f, 340f) * capsuleScale;
                Image capsule = CreateImage(hall, "Capsule" + i, _rounded, Gray(0.5f));
                SetPoint(capsule.rectTransform, Vector2.zero, capsuleSize, Vector2.zero);
                capsule.rectTransform.anchoredPosition = new Vector2(capsuleX, hallSize.y * 0.2f + i * 9f);
                SetAnchors(CreateImage(capsule.transform, "Glass", _rounded, Gray(0.28f)).rectTransform, 0.16f, 0.1f, 0.84f, 0.88f);
                capsuleX += capsuleSize.x + 30f * capsuleScale;
                capsuleScale *= 0.86f;
            }

            RectTransform sleeper = CreatePerson(hall, "Sleeper0", new Vector2(0.05f, 0.08f), 44f, Gray(0.78f));
            CreatePerson(hall, "Sleeper1", new Vector2(0.2f, 0.08f), 40f, Gray(0.78f));
            CreatePerson(hall, "Sleeper2", new Vector2(0.33f, 0.1f), 36f, Gray(0.78f));
            CuttoonTween enter = AddTween(hall, sleeper, 0.2f, 1.8f);
            TweenPosition(enter, Vector2.zero, new Vector2(70f, 0f));
            CreateLabel(hall, hallSize, "5-1", "수면 캡슐", "끝없이 늘어선 캡슐에 사람들이 하나씩 들어간다");
            panels.Add(FinishPanel(hall));

            // 5-2 뚜껑이 닫힘
            RectTransform closing = CreatePanel(page, "5-2", 0f, 0f, 0.62f, 0.45f, Gray(0.16f), out Vector2 closingSize);
            Image shell = CreateImage(closing, "Capsule", _rounded, Gray(0.5f));
            SetAnchors(shell.rectTransform, 0.05f, 0.14f, 0.95f, 0.84f);
            Image bed = CreateImage(shell.transform, "Bed", _rounded, Gray(0.3f));
            SetAnchors(bed.rectTransform, 0.04f, 0.14f, 0.96f, 0.86f);
            SetPoint(CreateImage(bed.transform, "Head", _circle, Gray(0.75f)).rectTransform, new Vector2(0.1f, 0.5f), new Vector2(96f, 96f));
            SetAnchors(CreateImage(bed.transform, "Body", _rounded, Gray(0.62f)).rectTransform, 0.17f, 0.3f, 0.86f, 0.7f);
            Image lid = CreateImage(closing, "GlassLid", _rounded, new Color(1f, 1f, 1f, 0.22f));
            SetAnchors(lid.rectTransform, 0.06f, 0.17f, 0.94f, 0.81f);
            RectTransform frost = CreateRect(lid.transform, "Frost");
            SetAnchors(frost, 0f, 0f, 1f, 1f);
            CanvasGroup frostGroup = frost.gameObject.AddComponent<CanvasGroup>();
            frostGroup.blocksRaycasts = false;
            frostGroup.interactable = false;
            SetAnchors(CreateImage(frost, "Fill", _rounded, new Color(1f, 1f, 1f, 0.3f)).rectTransform, 0f, 0f, 1f, 1f);
            CreateFrostDots(frost, new[] { 0.04f, 0.2f, 0.07f, 0.8f, 0.12f, 0.1f, 0.93f, 0.85f, 0.96f, 0.3f, 0.88f, 0.12f, 0.5f, 0.95f, 0.3f, 0.05f }, 0.8f);
            CuttoonTween shut = AddTween(closing, lid.rectTransform, 0.3f, 1.4f);
            TweenPosition(shut, new Vector2(1100f, 0f), Vector2.zero);
            CuttoonTween freeze = AddTween(closing, frost, 1.7f, 1.8f);
            TweenAlpha(freeze, frostGroup, 0f, 1f);
            CreateLabel(closing, closingSize, "5-2", "뚜껑이 닫힘", "캡슐에 누운 주인공, 유리 뚜껑이 닫히고 서리가 낀다");
            panels.Add(FinishPanel(closing));

            // 5-3 눈을 감다
            RectTransform sleep = CreatePanel(page, "5-3", 0.62f, 0f, 1f, 0.45f, Gray(0.12f), out Vector2 sleepSize);
            RectTransform eye = CreateEye(sleep, sleepSize, 0.1f, 0.25f, 0.9f, 0.75f, Gray(0.42f));
            RectTransform open = CreateRect(eye, "Step_Open");
            SetAnchors(open, 0f, 0f, 1f, 1f);
            RectTransform half = CreateRect(eye, "Step_Half");
            SetAnchors(half, 0f, 0f, 1f, 1f);
            SetAnchors(CreateImage(half, "Lid", null, Gray(0.62f)).rectTransform, -0.05f, 0.5f, 1.05f, 1.05f);
            SetAnchors(CreateImage(half, "Lash", null, Gray(0.08f)).rectTransform, -0.05f, 0.47f, 1.05f, 0.53f);
            RectTransform closed = CreateRect(eye, "Step_Closed");
            SetAnchors(closed, 0f, 0f, 1f, 1f);
            SetAnchors(CreateImage(closed, "Lid", null, Gray(0.62f)).rectTransform, -0.05f, -0.05f, 1.05f, 1.05f);
            SetAnchors(CreateImage(closed, "Lash", null, Gray(0.08f)).rectTransform, -0.05f, 0.47f, 1.05f, 0.53f);
            half.gameObject.SetActive(false);
            closed.gameObject.SetActive(false);
            SetAnchors(CreateImage(sleep, "Glass", null, new Color(1f, 1f, 1f, 0.07f)).rectTransform, 0f, 0f, 1f, 1f);
            CreateFrostDots(sleep, new[] { 0.03f, 0.08f, 0.08f, 0.15f, 0.95f, 0.1f, 0.9f, 0.92f, 0.04f, 0.6f }, 0.5f);
            CuttoonSteps steps = sleep.gameObject.AddComponent<CuttoonSteps>();
            SetRefArray(steps, "_steps", new Object[] { open.gameObject, half.gameObject, closed.gameObject });
            SetFloat(steps, "_delay", 0.6f);
            CreateLabel(sleep, sleepSize, "5-3", "눈을 감다", "유리 너머 주인공의 눈이 천천히 감기고, 암전");
            panels.Add(FinishPanel(sleep));

            return FinishPage(page, panels, 0f);
        }

        /// <summary>
        /// 에디터의 GameObject 생성 메뉴 menuPath를 parent를 고른 상태로 실행해 새 오브젝트를 만들고 이름을 name으로 바꾼다.
        /// parent가 null이면 씬 최상위에 만든다. 만든 GameObject를 돌려준다.
        /// </summary>
        private static GameObject CreateFromMenu(string menuPath, Transform parent, string name)
        {
            Selection.activeGameObject = parent != null ? parent.gameObject : null;
            if (!EditorApplication.ExecuteMenuItem(menuPath))
            {
                throw new System.InvalidOperationException("메뉴를 실행하지 못했다: " + menuPath);
            }

            GameObject created = Selection.activeGameObject;
            if (created == null || (parent != null && created == parent.gameObject))
            {
                throw new System.InvalidOperationException("메뉴가 새 오브젝트를 만들지 않았다: " + menuPath);
            }

            if (created.transform.parent != parent)
            {
                created.transform.SetParent(parent, false);
            }

            created.name = name;
            return created;
        }

        /// <summary>
        /// 빈 자식 오브젝트 만들기 메뉴로 parent 아래에 name이라는 RectTransform 오브젝트를 만들어 돌려준다.
        /// </summary>
        private static RectTransform CreateRect(Transform parent, string name)
        {
            GameObject created = CreateFromMenu(MENU_EMPTY_CHILD, parent, name);
            RectTransform rect = created.GetComponent<RectTransform>();
            if (rect == null)
            {
                rect = created.AddComponent<RectTransform>();
            }

            return rect;
        }

        /// <summary>
        /// UI Image 메뉴로 parent 아래에 name이라는 Image를 만들고 sprite와 color를 넣는다.
        /// 둥근 상자 그림이면 늘여도 모서리가 유지되게 Sliced로 둔다. 만든 Image를 돌려준다.
        /// </summary>
        private static Image CreateImage(Transform parent, string name, Sprite sprite, Color color)
        {
            Image image = CreateFromMenu(MENU_IMAGE, parent, name).GetComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = false;
            if (sprite == _rounded)
            {
                image.type = Image.Type.Sliced;
            }

            return image;
        }

        /// <summary>
        /// UI Legacy Text 메뉴로 parent 아래에 name이라는 Text를 만들고 글, 크기, 색, 정렬을 넣는다.
        /// 글꼴은 _font를 쓴다. 만든 Text를 돌려준다.
        /// </summary>
        private static Text CreateText(Transform parent, string name, string content, int fontSize, Color color, TextAnchor alignment)
        {
            Text text = CreateFromMenu(MENU_TEXT, parent, name).GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            text.text = content;
            return text;
        }

        /// <summary>
        /// 씬 최상위에 검은 배경의 정사영 카메라를 만들어 돌려준다. UI 레이어만 찍는다.
        /// </summary>
        private static Camera CreateCamera()
        {
            GameObject cameraObject = CreateFromMenu(MENU_EMPTY, null, "Main Camera");
            cameraObject.tag = "MainCamera";
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Color.black;
            camera.orthographic = true;
            camera.cullingMask = 1 << LayerMask.NameToLayer(UI_LAYER);
            return camera;
        }

        /// <summary>
        /// UI Canvas 메뉴로 캔버스를 만들고, camera로 그리는 화면 공간 캔버스와 1920x1080 기준 크기 맞춤으로 바꾼다.
        /// 만든 Canvas를 돌려준다.
        /// </summary>
        private static Canvas CreateCanvas(Camera camera)
        {
            GameObject canvasObject = CreateFromMenu(MENU_CANVAS, null, "CuttoonCanvas");
            Canvas canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 5f;
            CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>
        /// root 전체를 덮는 color 화면(번쩍임, 암전용)을 만들고 투명도 0인 CanvasGroup을 돌려준다.
        /// </summary>
        private static CanvasGroup CreateOverlay(RectTransform root, string name, Color color)
        {
            Image image = CreateImage(root, name, null, color);
            SetAnchors(image.rectTransform, 0f, 0f, 1f, 1f);
            CanvasGroup group = image.gameObject.AddComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;
            group.interactable = false;
            return group;
        }

        /// <summary>
        /// 화면 아래에 조작 안내(왼쪽)와 쪽 번호(오른쪽)를 만든다.
        /// 안내 묶음 GameObject를 돌려주고, 쪽 번호 Text를 pageText로 내보낸다.
        /// </summary>
        private static GameObject CreateGuide(RectTransform root, out Text pageText)
        {
            RectTransform guide = CreateRect(root, "Guide");
            SetAnchors(guide, 0f, 0f, 1f, 0f, new Vector2(0.5f, 0f));
            guide.offsetMin = new Vector2(60f, 20f);
            guide.offsetMax = new Vector2(-60f, 80f);
            Text controls = CreateText(guide, "Controls", "Space·Enter·클릭: 다음    R: 처음부터", LABEL_FONT_SIZE, Gray(0.6f), TextAnchor.MiddleLeft);
            SetAnchors(controls.rectTransform, 0f, 0f, 0.7f, 1f);
            pageText = CreateText(guide, "PageNumber", "쪽 1 / 5", LABEL_FONT_SIZE, Gray(0.6f), TextAnchor.MiddleRight);
            SetAnchors(pageText.rectTransform, 0.7f, 0f, 1f, 1f);
            return guide.gameObject;
        }

        /// <summary>
        /// pagesRoot 전체를 채우는 쪽 오브젝트 name을 만들어 돌려준다.
        /// </summary>
        private static RectTransform CreatePage(RectTransform pagesRoot, string name)
        {
            RectTransform page = CreateRect(pagesRoot, name);
            SetAnchors(page, 0f, 0f, 1f, 1f);
            return page;
        }

        /// <summary>
        /// 쪽 오브젝트에 CanvasGroup과 CuttoonPage를 달고 panels를 연결한다.
        /// 편집할 때 보이는 투명도를 alpha로 둔다. 만든 CuttoonPage를 돌려준다.
        /// </summary>
        private static CuttoonPage FinishPage(RectTransform page, List<CuttoonPanel> panels, float alpha)
        {
            CanvasGroup group = page.gameObject.AddComponent<CanvasGroup>();
            group.alpha = alpha;
            group.blocksRaycasts = false;
            group.interactable = false;
            CuttoonPage cuttoonPage = page.gameObject.AddComponent<CuttoonPage>();
            SetRef(cuttoonPage, "_group", group);
            SetRefArray(cuttoonPage, "_panels", panels.ToArray());
            return cuttoonPage;
        }

        /// <summary>
        /// 쪽 안의 정규화 영역(xMin~xMax, yMin~yMax)에 칸 id를 만든다. 회색 판, 흰 테두리, 칸 밖 잘라 내기를 단다.
        /// 칸 사이는 GUTTER만큼 띄운다. 칸 RectTransform을 돌려주고, 1920x1080 기준 칸 크기를 size로 내보낸다.
        /// </summary>
        private static RectTransform CreatePanel(RectTransform page, string id, float xMin, float yMin, float xMax, float yMax, Color background, out Vector2 size)
        {
            Image image = CreateImage(page, "Panel_" + id, null, background);
            RectTransform rect = image.rectTransform;
            SetAnchors(rect, xMin, yMin, xMax, yMax);
            rect.offsetMin = new Vector2(GUTTER * 0.5f, GUTTER * 0.5f);
            rect.offsetMax = new Vector2(-GUTTER * 0.5f, -GUTTER * 0.5f);
            Outline outline = image.gameObject.AddComponent<Outline>();
            outline.effectColor = Gray(0.92f);
            outline.effectDistance = new Vector2(3f, 3f);
            image.gameObject.AddComponent<RectMask2D>();
            size = new Vector2((xMax - xMin) * PAGE_WIDTH - GUTTER, (yMax - yMin) * PAGE_HEIGHT - GUTTER);
            return rect;
        }

        /// <summary>
        /// 칸 오브젝트에 CanvasGroup과 CuttoonPanel을 달고, 칸에 이미 단 연출을 모두 연결한다. 만든 CuttoonPanel을 돌려준다.
        /// </summary>
        private static CuttoonPanel FinishPanel(RectTransform panel)
        {
            CanvasGroup group = panel.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            CuttoonPanel cuttoonPanel = panel.gameObject.AddComponent<CuttoonPanel>();
            SetRef(cuttoonPanel, "_group", group);
            SetRefArray(cuttoonPanel, "_effects", panel.GetComponents<CuttoonEffect>());
            return cuttoonPanel;
        }

        /// <summary>
        /// 칸 왼쪽 위에 어두운 상자를 두고 첫 줄에 칸 번호와 이름, 둘째 줄부터 내용을 적는다.
        /// 상자 높이는 칸 너비 size.x와 내용 길이로 줄 수를 어림해 정한다.
        /// </summary>
        private static void CreateLabel(RectTransform panel, Vector2 size, string id, string title, string description)
        {
            float width = Mathf.Min(size.x - 20f, 620f);
            int descriptionLines = Mathf.Max(1, Mathf.CeilToInt(description.Length * LABEL_FONT_SIZE / (width - 24f)));
            float height = 20f + (1 + descriptionLines) * 30f;
            Image box = CreateImage(panel, "Label", null, new Color(0f, 0f, 0f, 0.62f));
            SetPoint(box.rectTransform, new Vector2(0f, 1f), new Vector2(width, height), new Vector2(0f, 1f));
            box.rectTransform.anchoredPosition = new Vector2(10f, -10f);
            Text text = CreateText(box.transform, "Text", "<b>" + id + "</b>  " + title + "\n" + description, LABEL_FONT_SIZE, Gray(0.92f), TextAnchor.UpperLeft);
            SetAnchors(text.rectTransform, 0f, 0f, 1f, 1f);
            text.rectTransform.offsetMin = new Vector2(12f, 8f);
            text.rectTransform.offsetMax = new Vector2(-12f, -8f);
            text.lineSpacing = 1.15f;
        }

        /// <summary>
        /// 원 머리와 둥근 몸통으로 된 사람 도형을 parent의 정규화 위치 anchor에 발끝 기준으로 세운다.
        /// 키는 너비 width의 2.1배다. 사람 묶음 RectTransform을 돌려준다.
        /// </summary>
        private static RectTransform CreatePerson(Transform parent, string name, Vector2 anchor, float width, Color color)
        {
            RectTransform person = CreateRect(parent, name);
            SetPoint(person, anchor, new Vector2(width, width * 2.1f), new Vector2(0.5f, 0f));
            SetAnchors(CreateImage(person, "Body", _rounded, color).rectTransform, 0f, 0f, 1f, 0.66f);
            SetAnchors(CreateImage(person, "Head", _circle, color).rectTransform, 0.18f, 0.7f, 0.82f, 1f);
            return person;
        }

        /// <summary>
        /// 칸 안의 정규화 영역에 타원 눈을 만든다. 흰자 안에 홍채, 동공, 동공에 비친 reflection 색 원을 두고 흰자 밖은 잘라 낸다.
        /// 홍채 크기는 칸 크기 panelSize로 구한 눈 높이에 맞춘다. 흰자 RectTransform을 돌려준다.
        /// </summary>
        private static RectTransform CreateEye(RectTransform panel, Vector2 panelSize, float xMin, float yMin, float xMax, float yMax, Color reflection)
        {
            Image white = CreateImage(panel, "Eye", _circle, Gray(0.86f));
            SetAnchors(white.rectTransform, xMin, yMin, xMax, yMax);
            white.gameObject.AddComponent<Mask>().showMaskGraphic = true;
            float eyeHeight = (yMax - yMin) * panelSize.y;
            Image iris = CreateImage(white.transform, "Iris", _circle, Gray(0.32f));
            SetPoint(iris.rectTransform, new Vector2(0.5f, 0.5f), Vector2.one * (eyeHeight * 0.82f));
            Image pupil = CreateImage(iris.transform, "Pupil", _circle, Gray(0.07f));
            SetPoint(pupil.rectTransform, new Vector2(0.5f, 0.5f), Vector2.one * (eyeHeight * 0.42f));
            Image glint = CreateImage(pupil.transform, "Reflection", _circle, reflection);
            SetPoint(glint.rectTransform, new Vector2(0.38f, 0.62f), Vector2.one * (eyeHeight * 0.2f));
            return white.rectTransform;
        }

        /// <summary>
        /// parent 안의 정규화 위치 points(x, y 쌍)마다 작은 별 점을 만든다.
        /// </summary>
        private static void CreateStars(RectTransform parent, float[] points)
        {
            for (int i = 0; i < points.Length / 2; i++)
            {
                Image star = CreateImage(parent, "Star" + i, _circle, Gray(0.6f));
                SetPoint(star.rectTransform, new Vector2(points[i * 2], points[i * 2 + 1]), new Vector2(5f, 5f));
            }
        }

        /// <summary>
        /// parent 안의 정규화 위치 points(x, y 쌍)마다 흰 서리 점을 alpha 투명도로 만든다. 점 크기는 차례로 조금씩 다르다.
        /// </summary>
        private static void CreateFrostDots(RectTransform parent, float[] points, float alpha)
        {
            for (int i = 0; i < points.Length / 2; i++)
            {
                Image dot = CreateImage(parent, "FrostDot" + i, _circle, new Color(1f, 1f, 1f, alpha));
                SetPoint(dot.rectTransform, new Vector2(points[i * 2], points[i * 2 + 1]), Vector2.one * (20f + (i % 3) * 10f));
            }
        }

        /// <summary>
        /// panel에 target을 바꾸는 CuttoonTween을 달고 기다림 delay와 시간 duration을 넣는다. 단 CuttoonTween을 돌려준다.
        /// </summary>
        private static CuttoonTween AddTween(RectTransform panel, RectTransform target, float delay, float duration)
        {
            CuttoonTween tween = panel.gameObject.AddComponent<CuttoonTween>();
            SetRef(tween, "_target", target);
            SetFloat(tween, "_delay", delay);
            SetFloat(tween, "_duration", duration);
            return tween;
        }

        /// <summary>
        /// tween이 대상 위치를 from에서 to로 바꾸도록 켠다.
        /// </summary>
        private static void TweenPosition(CuttoonTween tween, Vector2 from, Vector2 to)
        {
            SetBool(tween, "_animatePosition", true);
            SetVector2(tween, "_fromPosition", from);
            SetVector2(tween, "_toPosition", to);
        }

        /// <summary>
        /// tween이 대상 크기를 from에서 to로 바꾸도록 켠다.
        /// </summary>
        private static void TweenScale(CuttoonTween tween, Vector3 from, Vector3 to)
        {
            SetBool(tween, "_animateScale", true);
            SetVector3(tween, "_fromScale", from);
            SetVector3(tween, "_toScale", to);
        }

        /// <summary>
        /// tween이 group의 투명도를 from에서 to로 바꾸도록 켠다.
        /// </summary>
        private static void TweenAlpha(CuttoonTween tween, CanvasGroup group, float from, float to)
        {
            SetRef(tween, "_targetGroup", group);
            SetBool(tween, "_animateAlpha", true);
            SetFloat(tween, "_fromAlpha", from);
            SetFloat(tween, "_toAlpha", to);
        }

        /// <summary>
        /// panel에 3D Transform target을 움직이는 CuttoonTransformTween을 달고 기다림 delay와 시간 duration을 넣는다. 단 컴포넌트를 돌려준다.
        /// </summary>
        private static CuttoonTransformTween AddTransformTween(RectTransform panel, Transform target, float delay, float duration)
        {
            CuttoonTransformTween tween = panel.gameObject.AddComponent<CuttoonTransformTween>();
            SetRef(tween, "_target", target);
            SetFloat(tween, "_delay", delay);
            SetFloat(tween, "_duration", duration);
            return tween;
        }

        /// <summary>
        /// tween이 대상의 로컬 위치를 from에서 to로 바꾸도록 켠다.
        /// </summary>
        private static void TweenLocalPosition(CuttoonTransformTween tween, Vector3 from, Vector3 to)
        {
            SetBool(tween, "_animatePosition", true);
            SetVector3(tween, "_fromPosition", from);
            SetVector3(tween, "_toPosition", to);
        }

        /// <summary>
        /// tween이 대상의 로컬 회전(오일러 각)을 from에서 to로 바꾸도록 켠다.
        /// </summary>
        private static void TweenLocalRotation(CuttoonTransformTween tween, Vector3 from, Vector3 to)
        {
            SetBool(tween, "_animateRotation", true);
            SetVector3(tween, "_fromRotation", from);
            SetVector3(tween, "_toRotation", to);
        }

        /// <summary>
        /// tween이 대상의 로컬 크기를 from에서 to로 바꾸도록 켠다.
        /// </summary>
        private static void TweenLocalScale(CuttoonTransformTween tween, Vector3 from, Vector3 to)
        {
            SetBool(tween, "_animateScale", true);
            SetVector3(tween, "_fromScale", from);
            SetVector3(tween, "_toScale", to);
        }

        /// <summary>
        /// panel에 3D Renderer target의 색을 바꾸는 CuttoonColorTween을 달고 기다림 delay와 시간 duration을 넣는다. 단 컴포넌트를 돌려준다.
        /// </summary>
        private static CuttoonColorTween AddColorTween(RectTransform panel, Renderer target, float delay, float duration)
        {
            CuttoonColorTween tween = panel.gameObject.AddComponent<CuttoonColorTween>();
            SetRef(tween, "_target", target);
            SetFloat(tween, "_delay", delay);
            SetFloat(tween, "_duration", duration);
            return tween;
        }

        /// <summary>
        /// tween이 대상 색을 from에서 to로 바꾸도록 시작 색과 끝 색을 넣는다.
        /// </summary>
        private static void TweenColor(CuttoonColorTween tween, Color from, Color to)
        {
            SetColor(tween, "_fromColor", from);
            SetColor(tween, "_toColor", to);
        }

        /// <summary>
        /// 연출 컴포넌트 tween의 진행 곡선 _curve를 curve로 바꾼다.
        /// </summary>
        private static void SetCurve(Object tween, AnimationCurve curve)
        {
            SerializedObject serializedObject = new SerializedObject(tween);
            FindProperty(serializedObject, "_curve").animationCurveValue = curve;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// 천천히 시작해 끝으로 갈수록 빨라지는 이즈 인 곡선을 돌려준다.
        /// </summary>
        private static AnimationCurve EaseInCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, 2f, 0f));
        }

        /// <summary>
        /// 빨리 시작해 끝으로 갈수록 느려지는 이즈 아웃 곡선을 돌려준다.
        /// </summary>
        private static AnimationCurve EaseOutCurve()
        {
            return new AnimationCurve(new Keyframe(0f, 0f, 0f, 2f), new Keyframe(1f, 1f, 0f, 0f));
        }

        /// <summary>
        /// rect를 정규화 영역(xMin~xMax, yMin~yMax)에 꽉 채우고 가운데 기준점, 크기 1, 회전 없음으로 둔다.
        /// </summary>
        private static void SetAnchors(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            SetAnchors(rect, xMin, yMin, xMax, yMax, new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// rect를 정규화 영역(xMin~xMax, yMin~yMax)에 꽉 채우고 기준점 pivot, 크기 1, 회전 없음으로 둔다.
        /// </summary>
        private static void SetAnchors(RectTransform rect, float xMin, float yMin, float xMax, float yMax, Vector2 pivot)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.pivot = pivot;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// rect를 부모의 정규화 위치 anchor에 가운데 기준점으로 놓고 픽셀 크기 size로 둔다.
        /// </summary>
        private static void SetPoint(RectTransform rect, Vector2 anchor, Vector2 size)
        {
            SetPoint(rect, anchor, size, new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// rect를 부모의 정규화 위치 anchor에 기준점 pivot으로 놓고 픽셀 크기 size, 크기 1, 회전 없음으로 둔다.
        /// </summary>
        private static void SetPoint(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 pivot)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
        }

        /// <summary>
        /// 같은 값의 회색(value)을 불투명하게 돌려준다.
        /// </summary>
        private static Color Gray(float value)
        {
            return new Color(value, value, value, 1f);
        }

        /// <summary>
        /// 원, 삼각형, 달아오른 공기, 띠의 뜨거운 쪽 그림이 SPRITE_FOLDER에 없을 때만 만든다. 이미 있으면 그대로 둔다.
        /// </summary>
        private static void EnsureShapeSprites()
        {
            EnsureFolder(SPRITE_FOLDER);
            if (!File.Exists(CIRCLE_PATH))
            {
                WriteShapeTexture(CIRCLE_PATH, CircleCoverage);
            }

            if (!File.Exists(TRIANGLE_PATH))
            {
                WriteShapeTexture(TRIANGLE_PATH, TriangleCoverage);
            }

            // CUT-30: 2-2 운석 둘레의 달아오른 공기 그림을 더했다.
            if (!File.Exists(GLOW_PATH))
            {
                WriteShapeTexture(GLOW_PATH, GlowCoverage);
            }

            // CUT-34: 띠 가운데 구름을 향한 뜨거운 쪽 그림을 더했다.
            if (!File.Exists(GLOW_HOT_PATH))
            {
                WriteShapeTexture(GLOW_HOT_PATH, GlowHotCoverage);
            }
        }

        /// <summary>
        /// 흰색 도형 PNG를 path에 쓰고 스프라이트로 가져온다. 픽셀마다 coverage(x, y, 그림 크기)로 도형 안쪽 정도를 구한다.
        /// 가장자리는 1픽셀 폭으로 부드럽게 만든다.
        /// </summary>
        private static void WriteShapeTexture(string path, System.Func<float, float, float, float> coverageOf)
        {
            int size = SHAPE_TEXTURE_SIZE;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x + 0.5f;
                    float py = y + 0.5f;
                    float coverage = coverageOf(px, py, size);
                    byte alpha = (byte)Mathf.RoundToInt(Mathf.Clamp01(coverage) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// size 크기 그림의 한가운데 원에 점 (x, y)가 얼마나 들어가는지 돌려준다. 1 이상이면 안쪽, 0 이하면 바깥이다.
        /// </summary>
        private static float CircleCoverage(float x, float y, float size)
        {
            float radius = size * 0.5f;
            float dx = x - radius;
            float dy = y - radius;
            return radius - Mathf.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// size 크기 달아오른 공기 그림에서 점 (x, y)의 투명도(0~1)를 돌려준다.
        /// 그림 반지름의 GLOW_INNER 비율 안쪽은 투명하고, 그 경계부터 GLOW_PLATEAU 비율까지 가장 진했다가 바깥 끝으로 갈수록 옅어진다.
        /// </summary>
        private static float GlowCoverage(float x, float y, float size)
        {
            float radius = size * 0.5f;
            float dx = x - radius;
            float dy = y - radius;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float inner = radius * GLOW_INNER;
            float plateau = radius * GLOW_PLATEAU;
            // 안쪽 경계는 3픽셀 폭으로 부드럽게 올라오고, 가장 진한 구간 뒤로는 제곱 곡선으로 옅어진다.
            float rise = Mathf.Clamp01((distance - inner + 1.5f) / 3f);
            float fade = 1f - Mathf.Clamp01((distance - plateau) / (radius - plateau));
            return rise * fade * fade;
        }

        /// <summary>
        /// size 크기 띠의 뜨거운 쪽 그림에서 점 (x, y)의 투명도(0~1)를 돌려준다.
        /// 안쪽 투명 원은 띠 그림과 같은 GLOW_INNER 비율이고, 그림 오른쪽(0도)으로 갈수록 진하며 90도 밖은 비어 있다.
        /// 가장 진한 구간과 바깥 끝이 각도마다 들쭉날쭉해 띠 폭이 일그러져 보인다.
        /// </summary>
        private static float GlowHotCoverage(float x, float y, float size)
        {
            float radius = size * 0.5f;
            float dx = x - radius;
            float dy = y - radius;
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float angle = Mathf.Atan2(dy, dx);
            float facing = Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle)), 1.5f);
            // 띠 폭은 두 겹의 물결로 각도마다 다르다. 0~1 사이 값이다.
            float wobble = 0.5f + 0.3f * Mathf.Sin(angle * 7f + 1.3f) + 0.2f * Mathf.Sin(angle * 13f + 0.4f);
            float inner = radius * GLOW_INNER;
            float plateau = inner * (1.12f + 0.12f * wobble);
            float outer = inner * (1.3f + 0.15f * wobble);
            float rise = Mathf.Clamp01((distance - inner + 1.5f) / 3f);
            float fade = 1f - Mathf.Clamp01((distance - plateau) / (outer - plateau));
            return rise * fade * fade * facing;
        }

        /// <summary>
        /// 왼쪽 변을 밑변으로 하고 오른쪽 가운데를 꼭짓점으로 하는 삼각형에 점 (x, y)가 얼마나 들어가는지 돌려준다.
        /// </summary>
        private static float TriangleCoverage(float x, float y, float size)
        {
            const float EDGE_LENGTH_FACTOR = 1.118034f;
            float lowerEdge = (y - x * 0.5f) / EDGE_LENGTH_FACTOR;
            float upperEdge = (size - x * 0.5f - y) / EDGE_LENGTH_FACTOR;
            return Mathf.Min(lowerEdge, upperEdge) + 0.5f;
        }

        /// <summary>
        /// folder 폴더가 없으면 상위 폴더부터 차례로 만든다.
        /// </summary>
        private static void EnsureFolder(string folder)
        {
            if (AssetDatabase.IsValidFolder(folder))
            {
                return;
            }

            string parent = Path.GetDirectoryName(folder).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(folder));
        }

        /// <summary>
        /// serializedObject에서 field 이름의 직렬화 속성을 찾아 돌려준다. 없으면 예외를 던진다.
        /// </summary>
        private static SerializedProperty FindProperty(SerializedObject serializedObject, string field)
        {
            SerializedProperty property = serializedObject.FindProperty(field);
            if (property == null)
            {
                throw new System.ArgumentException(serializedObject.targetObject.GetType().Name + "에 " + field + " 필드가 없다.");
            }

            return property;
        }

        /// <summary>
        /// target의 직렬화 필드 field에 오브젝트 참조 value를 넣는다.
        /// </summary>
        private static void SetRef(Object target, string field, Object value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, field).objectReferenceValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// target의 직렬화 배열 필드 field를 values 참조들로 채운다.
        /// </summary>
        private static void SetRefArray(Object target, string field, Object[] values)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            SerializedProperty property = FindProperty(serializedObject, field);
            property.arraySize = values.Length;
            for (int i = 0; i < values.Length; i++)
            {
                property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            }

            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// target의 직렬화 필드 field에 실수 value를 넣는다.
        /// </summary>
        private static void SetFloat(Object target, string field, float value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, field).floatValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// target의 직렬화 필드 field에 참거짓 value를 넣는다.
        /// </summary>
        private static void SetBool(Object target, string field, bool value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, field).boolValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// target의 직렬화 필드 field에 Vector2 value를 넣는다.
        /// </summary>
        private static void SetVector2(Object target, string field, Vector2 value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, field).vector2Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// target의 직렬화 필드 field에 Vector3 value를 넣는다.
        /// </summary>
        private static void SetVector3(Object target, string field, Vector3 value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, field).vector3Value = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// target의 직렬화 필드 field에 색 value를 넣는다.
        /// </summary>
        private static void SetColor(Object target, string field, Color value)
        {
            SerializedObject serializedObject = new SerializedObject(target);
            FindProperty(serializedObject, field).colorValue = value;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}

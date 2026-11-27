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
        // 씬을 옮긴 자리 Assets/Scenes/Intro.unity에 저장한다.
        private const string SCENE_PATH = "Assets/Scenes/Intro.unity";
        private const string SCENE_FOLDER = "Assets/Scenes";
        private const string SPRITE_FOLDER = "Assets/IntroCuttoon/Sprites";
        private const string CIRCLE_PATH = "Assets/IntroCuttoon/Sprites/GB_Circle.png";
        private const string TRIANGLE_PATH = "Assets/IntroCuttoon/Sprites/GB_Triangle.png";
        // 2-2 운석 둘레의 짓눌려 달아오른 공기 그림이다.
        private const string GLOW_PATH = "Assets/IntroCuttoon/Sprites/GB_Glow.png";
        // 2-2 띠 가운데 구름을 향한 뜨거운 쪽 그림이다.
        private const string GLOW_HOT_PATH = "Assets/IntroCuttoon/Sprites/GB_GlowHot.png";
        // 4-1 유리에 비친 비스듬한 반사 줄 그림이다.
        private const string SHEEN_PATH = "Assets/IntroCuttoon/Sprites/GB_GlassSheen.png";
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
        // 1-3 카메라 시작 자리는 최종 카메라보다 머리에 이만큼 가깝다(미터).
        private const float BUST_START_CLOSER = 0.5f;
        // 1-2 새 떼는 칸이 열리고 이만큼 뒤에 날아오르기 시작한다(초). 칸이 다 나타난 직후다.
        private const float BIRD_START_DELAY = 0.5f;
        // 1-2 운석 그림자와 칸 어두워짐은 칸이 열리고 이만큼 뒤에 시작한다(초). 새 떼가 모두 칸을 벗어난 뒤 그림자가 칸에 들어오는 때다.
        private const float SHADOW_DELAY = 1.4f;
        // 새가 칸을 지나가는 시간은 카메라에서 떨어진 거리(미터)에 이 값을 곱한다. 가까운 새일수록 빨리 지나간다.
        private const float BIRD_SECONDS_PER_METER = 0.082f;
        // 새가 출발하고 도착하는 칸 화면 가로 위치다. 칸 오른쪽 밖에서 왼쪽 밖으로 간다.
        private const float BIRD_START_X = 1.25f;
        private const float BIRD_END_X = -0.25f;
        // 새가 칸을 지나며 오르는 높이의 절반(칸 화면 세로 비율)이다. 오른쪽 아래에서 왼쪽 위로 비스듬히 난다.
        private const float BIRD_RISE = 0.55f;
        private const float BIRD_FLAP_AMPLITUDE = 50f;
        // 2-1 얼굴 카메라의 시야각(도), 표정 가운데 거리에서 칸에 담을 가로 폭(미터), 표정 가운데가 놓일 칸 화면 세로 위치다.
        // 가로 폭은 두 눈 바깥 끝이 칸 양옆에 닿는 정도로, 익스트림 클로즈업보다 조금 떨어진 거리다. 세로 위치는 턱 끝이 칸 아래 끝에 닿아 목이 보이지 않는 높이다.
        private const float FACE_FOV = 30f;
        private const float FACE_FRAME_WIDTH = 0.8f;
        private const float FACE_VIEW_Y = 0.24f;
        // 2-1 표정이 바뀌는 부위별 시작 시각과 시간(초)이다. 눈썹, 눈과 시선, 동공, 입 순서로 조금씩 늦게 시작해 겹치며 바뀐다.
        private const float BROW_CHANGE_DELAY = 0.6f;
        private const float BROW_CHANGE_DURATION = 1.2f;
        private const float EYE_OPEN_DELAY = 0.7f;
        private const float EYE_OPEN_DURATION = 1.2f;
        private const float PUPIL_DILATE_DELAY = 0.8f;
        private const float PUPIL_DILATE_DURATION = 1.4f;
        private const float MOUTH_OPEN_DELAY = 1f;
        private const float MOUTH_OPEN_DURATION = 1.6f;
        // 동공에 비친 운석의 끝 몸체 지름(미터), 동공 가운데에서 앞으로 나온 거리(미터), 몸체 밝기다.
        // 칸이 열릴 때는 끝 크기의 REFLECTION_START배(동공에 견준 크기)이고, REFLECTION_DELAY초부터 REFLECTION_DURATION초 동안 커진다.
        // 동공 확장(2.2초)과 입(2.6초)이 끝난 뒤에도 잠시 혼자 커져서, 반사가 커지는 것이 동공 확장에 묻히지 않는다.
        private const float REFLECTION_BODY_DIAMETER = 0.047f;
        private const float REFLECTION_DEPTH = 0.018f;
        private const float REFLECTION_BODY_GRAY = 0.3f;
        private const float REFLECTION_START = 0.46f;
        private const float REFLECTION_DELAY = 0f;
        private const float REFLECTION_DURATION = 3.2f;
        // 3-2 지구와 로켓 세트다. 칸 카메라 시야각과 원평면, 지구 반지름과 카메라에서의 거리, 칸 화면에서 지구 가운데의 가로·세로 비율 자리다.
        private const float LAUNCH_FOV = 40f;
        private const float LAUNCH_FAR = 420f;
        private const float LAUNCH_EARTH_RADIUS = 40f;
        private const float LAUNCH_EARTH_DISTANCE = 150f;
        private const float LAUNCH_EARTH_VIEW_X = 0.28f;
        private const float LAUNCH_EARTH_VIEW_Y = 0.42f;
        private const int LAUNCH_STAR_COUNT = 110;
        // 불길 구는 지구의 BURN_SCALE배 크기다. 지구와 크기가 너무 비슷하면 두 면이 얕게 만나 경계가 톱니처럼 보인다. 착탄점 둘레 반경 BURN_START_ANGLE도에서 BURN_END_ANGLE도까지 BURN_DURATION초 동안 번진다.
        private const float BURN_SCALE = 0.8f;
        private const float BURN_START_ANGLE = 6f;
        private const float BURN_END_ANGLE = 35f;
        private const float BURN_DURATION = 2.6f;
        // 로켓 크기 배율, 출발 자리의 지구 표면 아래 깊이(미터), 멈추는 자리의 카메라 앞 거리(미터), 출발 기다림과 시간(초), 몸체가 도는 각도(도)다.
        private const float ROCKET_SCALE = 2.5f;
        private const float ROCKET_START_DEPTH = 6f;
        private const float ROCKET_END_DISTANCE = 28f;
        private const float ROCKET_DELAY = 0.3f;
        private const float ROCKET_DURATION = 2.6f;
        private const float ROCKET_ROLL = 80f;
        // 불꽃은 로켓이 나는 동안 노즐을 축으로 FLAME_FLICKER_CYCLES번 떨린다. 떨림 수를 정수로 두어 처음과 끝에 불꽃이 곧게 선다.
        private const float FLAME_FLICKER_CYCLES = 24f;
        private const float FLAME_FLICKER_AMPLITUDE = 6f;
        // 지구 불길 경계가 각지지 않도록 쓰는 촘촘한 구 메시의 위도 칸 수와 저장 자리다. 경도 칸 수는 두 배다.
        private const int SPHERE_MESH_LATITUDE = 80;
        private const string MESH_FOLDER = "Assets/IntroCuttoon/Meshes";
        private const string SPHERE_MESH_PATH = MESH_FOLDER + "/GB_SphereDense.asset";
        // 운석 충돌로 퍼지는 충격파 껍질의 셰이더 이름이다. 가장자리가 밝은 투명 셰이더로 3-2 테두리와 3-3 앞면에 함께 쓴다.
        private const string SHOCK_SHADER = "IntroCuttoon/Shockwave";
        // 3-2 충격파 테두리는 불길과 함께 생겨 SHOCK_RING_DURATION초 동안 반지름 SHOCK_RING_END_RADIUS(미터)까지 커진다. 시간마다 불길 가장자리까지 거리의 SHOCK_RING_LEAD배보다 작아지지 않는다.
        private const float SHOCK_RING_END_RADIUS = 44f;
        private const float SHOCK_RING_DURATION = 3f;
        private const float SHOCK_RING_LEAD = 1.12f;
        // 3-3 충격파 칸의 시야각(도), 원평면(미터), 연출 시간(초)이다. 움직임 식은 SHOCK_PROGRESS_SECONDS초를 진행도 1로 본다.
        private const float SHOCK_FOV = 40f;
        private const float SHOCK_FAR = 400f;
        private const float SHOCK_DURATION = 3.4f;
        private const float SHOCK_PROGRESS_SECONDS = 2.8f;
        // 3-3 로켓은 충돌 지점에서 SHOCK_ROCKET_START(미터)부터 진행도의 제곱에 SHOCK_ROCKET_RUN을 곱한 만큼 더 간다. 크기 배율과 진행도 1마다 몸체가 도는 각도(도)다.
        private const float SHOCK_ROCKET_START = 16.6f;
        private const float SHOCK_ROCKET_RUN = 27.5f;
        private const float SHOCK_ROCKET_SCALE = 1.2f;
        private const float SHOCK_ROCKET_ROLL = 60f;
        // 3-3 충격파 반지름(미터)은 진행도 u에 대해 SHOCK_WAVE_START + SHOCK_WAVE_SPEED·u − SHOCK_WAVE_SLOW·u²이다. 중력으로 점점 느려지고, 진행도 1마다 투명도가 SHOCK_WAVE_FADE씩 준다.
        private const float SHOCK_WAVE_START = 3.9f;
        private const float SHOCK_WAVE_SPEED = 26f;
        private const float SHOCK_WAVE_SLOW = 8f;
        private const float SHOCK_WAVE_FADE = 0.45f;
        // 3-3 카메라는 진로 SHOCK_CAMERA_MID(미터) 지점에서 SHOCK_CAMERA_DISTANCE만큼 떨어진 자리에 머문다. 바라보는 진로 높이는 진행도 0에 SHOCK_LOOK_START, 1에 SHOCK_LOOK_END이고, 화면을 SHOCK_CAMERA_ROLL도 기울인다.
        private const float SHOCK_CAMERA_MID = 24f;
        private const float SHOCK_CAMERA_DISTANCE = 36f;
        private const float SHOCK_LOOK_START = 14f;
        private const float SHOCK_LOOK_END = 33f;
        private const float SHOCK_CAMERA_ROLL = 33f;
        // 3-3 별 수와 카메라에서의 거리(미터), 거리 폭(미터)이다. 별은 카메라가 보는 쪽에만 둔다.
        private const int SHOCK_STAR_COUNT = 260;
        private const float SHOCK_STAR_DISTANCE = 280f;
        private const float SHOCK_STAR_DEPTH = 50f;
        // 2-1과 2-2 카메라 배경(하늘) 밝기다.
        private const float SKY_GRAY = 0.5f;
        // 2-2 시민 눈높이(미터), 올려다보는 각도(도), 좌우 각도(도), 시야각(도)이다.
        private const float POV_EYE_HEIGHT = 1.7f;
        private const float POV_PITCH = 60f;
        private const float POV_YAW = 0f;
        private const float POV_FOV = 66f;
        // 2-2 운석 반지름(미터), 처음 보이는 반지름 각도(도), 다 다가온 거리(미터, 시민 눈에서 운석 중심까지), 다가오기 시작과 시간(초)이다.
        // 처음 거리는 반지름이 처음 각도로 보이는 거리(약 1,067m)다. 다 다가오면 운석이 칸 위아래를 넘치게 차고, 바닥은 건물 꼭대기보다 약 5m 높다.
        private const float POV_METEOR_RADIUS = 150f;
        private const float POV_METEOR_START_ANGLE = 8f;
        private const float POV_METEOR_DISTANCE = 230f;
        private const float POV_APPROACH_DELAY = 0.2f;
        private const float POV_APPROACH_DURATION = 2.6f;
        // 다가옴 곡선의 끝 기울기다. 처음 기울기가 0이라 거의 멈춘 듯 떠나고, 1.3이면 마지막 0.6초에 거리의 31%를 간다.
        private const float POV_APPROACH_END_SLOPE = 1.3f;
        // 처음 거리의 운석 뒤쪽까지 그리도록 넓힌 2-2 카메라 원평면 거리(미터)다.
        private const float POV_FAR = 1400f;
        // 달아오른 공기 그림에서 투명한 안쪽 원의 반지름과 가장 진한 구간이 끝나는 반지름(그림 반지름 비율)이다.
        // 안쪽 원은 띠 판 위에서 운석 반지름에 놓인다. 판 위의 운석 윤곽은 운석 반지름의 1.01~1.10배라 1.125배까지 가장 진하게 둔다.
        // 바깥 끝은 운석 반지름의 1.47배로, 다 다가왔을 때 띠가 운석 화면 반지름의 1.33배까지 번진다.
        private const float GLOW_INNER = 0.68f;
        private const float GLOW_PLATEAU = 0.765f;
        // 하늘색 막은 구덩이까지 덮도록 운석보다 이 배율만큼 크고, 처음 투명도에서 0으로 걷힌다.
        private const float HAZE_SCALE = 1.05f;
        private const float HAZE_START = 0.425f;
        // 바람에 구름이 칸 왼쪽으로 흐르는 거리(미터)다.
        private const float CLOUD_DRIFT = 5f;
        // 띠의 뜨거운 쪽이 향할 구름 번호, 밝아지는 시작과 시간(초), 끝 투명도, 흐르며 도는 각도(도)와 시간(초)이다.
        private const int HOT_CLOUD = 2;
        private const float GLOW_HOT_DELAY = 1.5f;
        private const float GLOW_HOT_DURATION = 1f;
        private const float GLOW_HOT_ALPHA = 0.75f;
        private const float GLOW_HOT_SWEEP = 12f;
        private const float GLOW_HOT_SWEEP_DURATION = 1.3f;
        // 띠가 HOT_CLOUD 구름 윗면에 닿아 가장자리가 뜯기는 때와 시간(초)이다.
        // 왼쪽 덩이가 떨어져 나가는 방향(도, 0이 칸 오른쪽, 90이 위)과 거리(미터), 윗면 조각이 날아가는 방향과 거리다.
        private const float TEAR_DELAY = 2.32f;
        private const float TEAR_DURATION = 0.3f;
        private const float TEAR_PUSH_ANGLE = 195f;
        private const float TEAR_PUSH = 14f;
        private const float TEAR_FLING_ANGLE = 185f;
        private const float TEAR_FLING = 26f;
        // 4-1 부서지는 지구 칸의 시야각(도), 원평면(미터), 눈에서 유리까지 거리(미터)다. 지구 중심은 카메라 앞 SHATTER_EARTH_DISTANCE(미터)에서 칸 세로 SHATTER_EARTH_VIEW_Y에 놓인다.
        private const float SHATTER_FOV = 28f;
        private const float SHATTER_FAR = 800f;
        private const float SHATTER_GLASS_DISTANCE = 0.12f;
        private const float SHATTER_EARTH_DISTANCE = 321f;
        private const float SHATTER_EARTH_VIEW_Y = 0.52f;
        // 조각은 지각과 맨틀 껍질이다. 외핵 바깥 반지름(지구 반지름 비율), 구덩이 각도(도)와 깊이(반지름 비율), 충격 축 둘레로 나누는 조각 수다.
        private const float SHATTER_CORE_RATIO = 0.55f;
        private const float SHATTER_CRATER_ANGLE = 10f;
        private const float SHATTER_CRATER_DEPTH = 0.07f;
        private const int SHATTER_PIECES = 6;
        // 조각은 SHATTER_OPEN_DELAY초부터 SHATTER_OPEN_DURATION초 동안 SHATTER_OPEN_ANGLE도 돌아 구덩이 쪽 끝부터 벌어지고,
        // SHATTER_MOVE_DELAY초부터 SHATTER_MOVE_DURATION초 동안 반지름의 SHATTER_SPREAD배만큼 흩어진다. 흩어지는 곡선의 끝 기울기와 충격점 반대쪽으로 기우는 정도다.
        private const float SHATTER_OPEN_DELAY = 1.1f;
        private const float SHATTER_OPEN_DURATION = 1.5f;
        private const float SHATTER_OPEN_ANGLE = 10f;
        private const float SHATTER_MOVE_DELAY = 1.3f;
        private const float SHATTER_MOVE_DURATION = 3.1f;
        private const float SHATTER_SPREAD = 0.28f;
        private const float SHATTER_MOVE_END_SLOPE = 1.3f;
        private const float SHATTER_PUSH_BACK = 0.25f;
        // 외핵 덩어리는 SHATTER_OOZE_DELAY초에서 덩어리마다 0~SHATTER_OOZE_JITTER초 늦게 나와 SHATTER_OOZE_DURATION초 동안 흘러나온다.
        private const float SHATTER_OOZE_DELAY = 2f;
        private const float SHATTER_OOZE_JITTER = 0.9f;
        private const float SHATTER_OOZE_DURATION = 2.8f;
        private const int SHATTER_STAR_COUNT = 240;
        private const string SHATTER_MESH_PREFIX = MESH_FOLDER + "/GB_ShatterPiece";
        // 4-2 유리에 댄 손 세트다. 카메라는 주인공의 눈이고 유리는 눈앞 HAND_GLASS_DISTANCE(미터)에 있다. 부서진 지구는 4-1과 같은 거리에서 칸 세로 HAND_EARTH_VIEW_Y 자리에 둔다.
        private const float HAND_FOV = 40f;
        private const float HAND_NEAR = 0.02f;
        private const float HAND_GLASS_DISTANCE = 0.5f;
        private const float HAND_EARTH_VIEW_Y = 0.55f;
        // 세로 창틀 안쪽 모서리가 놓이는 칸 가로 자리다.
        private const float HAND_FRAME_VIEW_X = 0.9f;
        // 손이 올라와 유리 HAND_HOVER(미터) 앞에서 멈칫했다가 닿고, 손가락이 펴져 붙은 뒤 HAND_HOLD초 머물고 HAND_FIST_DURATION초 동안 주먹을 쥔다.
        private const float HAND_RISE_DELAY = 0.8f;
        private const float HAND_RISE_DURATION = 1.05f;
        private const float HAND_HOVER = 0.015f;
        private const float HAND_TOUCH_DELAY = 1.95f;
        private const float HAND_TOUCH_DURATION = 0.2f;
        private const float HAND_FLATTEN_DURATION = 0.25f;
        private const float HAND_HOLD = 1.2f;
        private const float HAND_FIST_DURATION = 1.1f;
        // 주먹 곡선은 진행을 이만큼 거듭제곱해 천천히 출발한다.
        private const float HAND_FIST_POWER = 1.3f;
        // 주먹을 쥐며 손등 윗부분이 유리 쪽으로 꺾이는 각도와 엄지 쪽이 보이게 도는 각도(도), 유리를 따라 내려가는 거리와 다 쥔 주먹과 유리 사이 틈(미터)이다.
        private const float HAND_WRIST_BEND = 35f;
        private const float HAND_WRIST_TWIST = -28f;
        private const float HAND_SLIDE = 0.01f;
        private const float HAND_FIST_GAP = 0.001f;
        // 떨림은 머묾 끝 HAND_LEAK_LEAD초 동안 HAND_LEAK_TREMBLE까지 새어 나오고, 주먹을 다 쥔 뒤 HAND_TREMBLE_GROW초 동안 HAND_TREMBLE(미터)까지 커지며 HAND_END초에 멈춘다.
        private const float HAND_LEAK_LEAD = 0.6f;
        private const float HAND_LEAK_TREMBLE = 0.0003f;
        private const float HAND_TREMBLE = 0.0012f;
        private const float HAND_TREMBLE_GROW = 1f;
        private const float HAND_END = 6.7f;
        // 떨림 1미터마다 손목이 앞뒤(X축), 좌우(Z축)로 기우는 각도(도)다.
        private const float HAND_TREMBLE_TILT_X = 240f;
        private const float HAND_TREMBLE_TILT_Z = 300f;
        // 시험판에서 고른 떨림은 초당 24장 화면이었다. 같은 모습이 되게 이 간격으로 떨림 키를 넣는다.
        private const float HAND_TREMBLE_KEYS_PER_SECOND = 24f;
        // 4-3 주인공의 눈 세트다. 2-1과 같은 머리에서 왼쪽 눈 하나를 시야각 HERO_FOV로, 눈 테 너비가 칸 가로의 HERO_EYE_SHARE가 되는 거리에서 찍는다.
        // 카메라는 귀 쪽으로 HERO_CAMERA_YAW도 비켜 있어 정면을 보는 시선이 카메라 옆을 지나고, 눈은 칸 세로 HERO_EYE_VIEW_Y 자리에 온다.
        private const float HERO_FOV = 30f;
        private const float HERO_EYE_SHARE = 0.72f;
        private const float HERO_CAMERA_YAW = 20f;
        private const float HERO_EYE_VIEW_Y = 0.58f;
        private const float HERO_NEAR = 0.02f;
        private const float HERO_FAR = 5f;
        // 눈 뜬 정도, 눈 부품의 앞뒤 자리(눈 기준), 동공에 비친 부서진 지구의 지름(세트 단위)이다.
        private const float HERO_EYE_OPEN = 0.62f;
        private const float HERO_RIM_DEPTH = 0.012f;
        private const float HERO_IRIS_DEPTH = -0.05f;
        private const float HERO_PUPIL_DEPTH = -0.066f;
        private const float HERO_REFLECTION_DEPTH = -0.084f;
        private const float HERO_REFLECTION_DIAMETER = 0.044f;
        // 홍채·동공·비친 지구를 화면에서 눈 가운데보다 오른쪽으로 옮기는 거리(눈 중심 평면 기준)다. 실제 눈보다 비친 지구를 강조하려는 게임적 허용이다.
        private const float HERO_GAZE_SHIFT = 0.02f;
        // 고인 눈물 초승달은 흰자 아래 호의 TEAR_ARC_FROM~TEAR_ARC_TO(도)에 걸쳐 눈 테를 TEAR_LID_COVER만큼, 흰자 위로 TEAR_POOL_THICKNESS만큼 덮고, 눈 겉면에서 TEAR_SURFACE_GAP 앞에 붙는다.
        private const float TEAR_ARC_FROM = 212f;
        private const float TEAR_ARC_TO = 328f;
        private const float TEAR_LID_COVER = 0.003f;
        private const float TEAR_POOL_THICKNESS = 0.04f;
        private const float TEAR_SURFACE_GAP = 0.008f;
        private const float TEAR_SHINE_WIDTH = 0.003f;
        private const float TEAR_SHINE_MARGIN = 18f;
        private const string TEAR_POOL_MESH_PATH = MESH_FOLDER + "/GB_TearPool.asset";
        private const string TEAR_SHINE_MESH_PATH = MESH_FOLDER + "/GB_TearShine.asset";
        // 방울은 호의 TEAR_SPILL_ANGLE(도) 자리에서 맺혀 TEAR_SAG만큼 처지고, 볼을 타고 TEAR_FALL만큼 내려가며 코 쪽으로 TEAR_DRIFT만큼 비낀다. 볼 겉면에서 TEAR_DROP_LIFT 앞에 뜬다.
        private const float TEAR_SPILL_ANGLE = 250f;
        private const float TEAR_FORM_DELAY = 1.9f;
        private const float TEAR_FORM_DURATION = 0.7f;
        private const float TEAR_SAG = 0.012f;
        private const float TEAR_SLIDE_DURATION = 1.8f;
        private const float TEAR_FALL = 0.32f;
        private const float TEAR_DRIFT = 0.03f;
        private const float TEAR_STRETCH = 0.012f;
        private const float TEAR_DROP_LIFT = 0.011f;
        // 흘러내리는 진행은 0.6u² + 0.4u³이다. 끝 기울기는 그 미분값이다.
        private const float TEAR_SLIDE_END_SLOPE = 2.4f;
        // 젖은 자국은 조각 TEAR_TRAIL_SEGMENTS개로 나눠 방울이 지날 때 차례로 생기고, 볼 겉면에서 TEAR_TRAIL_LIFT 앞에 붙는다.
        private const int TEAR_TRAIL_SEGMENTS = 8;
        private const float TEAR_TRAIL_WIDTH = 0.0055f;
        private const float TEAR_TRAIL_LIFT = 0.004f;

        private static readonly Color _fireColor = new Color(0.58f, 0.24f, 0.2f, 1f);

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
        // 1-3 카메라 시작 자리에서 머리 중심이 놓일 칸 안 정규화 위치다. 라벨 상자에 걸리지 않게 가운데보다 조금 아래에 둔다.
        private static readonly Vector2 _bustStartHeadViewport = new Vector2(0.5f, 0.39f);
        private static readonly Vector2 _questionSize = new Vector2(80f, 90f);
        private static readonly Vector3 _headDown = new Vector3(-8f, 0f, 0f);
        private static readonly Vector3 _headUp = new Vector3(32f, 0f, 0f);
        // 정면 구간에서 고개를 드는 최대 각도다. 카메라가 빠지는 동안 _headUp으로 내려온다.
        private static readonly Vector3 _headPeak = new Vector3(40f, 0f, 0f);

        // 새마다 다른 값이다. 시민이 보이는 높이에서 벗어난 정도, 카메라에서 떨어진 거리(미터), 출발 늦춤(초), 날갯짓 시작 자리, 1초당 날갯짓 횟수.
        private static readonly float[] _birdLanes = { 0.05f, -0.12f, 0.18f, -0.02f, 0.24f, -0.20f, 0.10f, -0.08f, 0.15f };
        private static readonly float[] _birdDepths = { 10.5f, 12.5f, 9.0f, 13.5f, 11.0f, 8.5f, 12.0f, 14.0f, 9.5f };
        private static readonly float[] _birdDelays = { 0.00f, 0.05f, 0.10f, 0.14f, 0.19f, 0.24f, 0.29f, 0.34f, 0.40f };
        private static readonly float[] _birdPhases = { 0.00f, 0.37f, 0.71f, 0.18f, 0.55f, 0.90f, 0.26f, 0.63f, 0.44f };
        private static readonly float[] _birdFrequencies = { 5.0f, 4.6f, 5.4f, 4.8f, 5.2f, 5.6f, 4.7f, 5.1f, 4.9f };

        // 2쪽 얼굴 세트와 올려다보기 세트 자리, 얼굴이 위를 보는 각도다.
        private static readonly Vector3 _faceSetPosition = new Vector3(4000f, 0f, 0f);
        private static readonly Vector3 _povSetPosition = new Vector3(5000f, 0f, 0f);
        private static readonly Vector3 _faceTilt = new Vector3(25f, 0f, 0f);
        // 3-2 지구와 로켓 세트 자리다. 2-2 카메라 원평면(1,400m)보다 멀리 둔다.
        private static readonly Vector3 _launchSetPosition = new Vector3(8000f, 0f, 0f);
        // 착탄점은 지구에서 카메라를 향한 점을 이만큼 왼쪽 위로 옮긴 방향이다. 로켓은 _rocketLaunchNormal 방향의 지구 표면 아래에서 솟아 카메라 오른쪽 위 _rocketPassPoint를 향해 난다.
        private static readonly Vector3 _impactShift = new Vector3(-0.15f, 0.3f, 0f);
        private static readonly Vector3 _rocketLaunchNormal = new Vector3(0.45f, -0.1f, -0.89f);
        private static readonly Vector3 _rocketPassPoint = new Vector3(5f, 2f, 6f);
        private static readonly Color _flameOuterColor = new Color(0.75f, 0.36f, 0.24f, 1f);
        private static readonly Color _flameCoreColor = new Color(0.95f, 0.9f, 0.78f, 1f);
        // 3-3 충격파 세트 자리다. 3-2 세트 카메라 원평면(420m)보다 멀리 둔다.
        private static readonly Vector3 _shockSetPosition = new Vector3(9000f, 0f, 0f);
        // 3-3 세트 안에서 빛이 나아가는 방향이다. 씬 방향광이 이 방향이 되도록 세트를 돌려, 카메라 쪽 위에서 비치게 한다.
        private static readonly Vector3 _shockLocalLight = new Vector3(0.35f, -0.35f, 0.87f);
        // 3-3 카메라가 진로 가운데에서 떨어진 방향이다. 옆(-Z)에서 조금 돌고 30도 아래로 내려가 있다.
        private static readonly Vector3 _shockCameraOffset = new Vector3(-0.366f, -0.5f, -0.785f);
        private static readonly Color _shockColor = new Color(1f, 0.95f, 0.88f, 1f);
        // 3-3 파편마다 다른 값이다. 로켓 진로와의 각도(도), 카메라 방위에서 벗어난 각도(도), 충격파 반지름 대비 거리 비율, 크기(미터), 초당 도는 각도(도), 도는 축.
        private static readonly float[] _debrisAngles = { 72f, 45f, 30f, 55f, 20f };
        private static readonly float[] _debrisAzimuths = { -6f, 15f, -12f, 25f, 20f };
        private static readonly float[] _debrisRatios = { 1.25f, 1.12f, 1.06f, 1.18f, 1.03f };
        private static readonly float[] _debrisSizes = { 0.5f, 1f, 0.9f, 0.8f, 0.7f };
        private static readonly float[] _debrisSpins = { 60f, 120f, 180f, 90f, 150f };
        private static readonly Vector3[] _debrisSpinAxes = { new Vector3(0.3f, 0.8f, -0.5f), new Vector3(-0.7f, 0.2f, 0.6f), new Vector3(0.5f, -0.4f, 0.75f), new Vector3(0.1f, 0.6f, 0.8f), new Vector3(-0.6f, -0.5f, 0.6f) };
        // 구름마다 다른 값이다. 시민 눈에서의 거리(미터), 칸 화면에서 운석 중심 둘레로 놓인 방향(도, 0이 오른쪽, 90이 위), 시선에서 떨어진 각도(도), 크기 배율.
        // 첫 구름은 운석 처음 자리의 왼쪽 위 가장자리를 가리고 운석이 지나는 거리에 있어, 운석이 다가오며 그 구름 앞으로 나온다.
        private static readonly float[] _cloudDistances = { 220f, 320f, 160f };
        private static readonly float[] _cloudAngles = { 135f, 50f, 270f };
        private static readonly float[] _cloudOffsets = { 9.5f, 27f, 26f };
        private static readonly float[] _cloudSizes = { 1.6f, 1.3f, 1.4f };
        // 달아오른 공기 띠의 처음 색(흰빛, 투명도 0.3)과 끝 색(옅은 살구빛, 투명도 1)이다.
        private static readonly Color _glowStartColor = new Color(1f, 1f, 1f, 0.3f);
        private static readonly Color _glowEndColor = new Color(0.93f, 0.81f, 0.7f, 1f);
        // 띠의 뜨거운 쪽 색(흰빛에 가까움)이다.
        private static readonly Color _glowHotColor = new Color(1f, 0.96f, 0.9f, 1f);
        // 떨어져 나가며 작아진 왼쪽 덩이의 크기, 윗면 조각이 숨어 있는 자리와 다 생긴 크기(구름 묶음 기준 로컬 값)다.
        private static readonly Vector3 _tornPuffScale = new Vector3(8f, 5.5f, 5.5f);
        private static readonly Vector3 _shredStart = new Vector3(-4f, 4f, 0f);
        private static readonly Vector3 _shredScale = new Vector3(5f, 3.5f, 3.5f);
        // 2-1 입 구멍의 다문 자리와 크기, 벌린 크기(입 축 기준 로컬 값)다. 다문 선을 위쪽에 두어 아래로 벌어지게 한다.
        private static readonly Vector3 _mouthClosedPosition = new Vector3(0f, 0.1f, 0f);
        private static readonly Vector3 _mouthClosedScale = new Vector3(0.28f, 0.035f, 0.1f);
        private static readonly Vector3 _mouthOpenScale = new Vector3(0.34f, 0.32f, 0.1f);
        // 눈썹의 호기심 어린 자리·각도와 놀란 자리·각도다. 값은 왼쪽, 오른쪽 순서다.
        // 처음에는 칸 오른쪽 눈썹만 조금 높고 바깥 끝이 올라가 갸웃하는 표정이고, 끝에는 두 눈썹이 높이 올라가 안쪽 끝이 더 높다. 자리의 Z는 이마 곡면을 따른다.
        private static readonly Vector3[] _browCuriousPositions = { new Vector3(-0.27f, 1.06f, -0.596f), new Vector3(0.27f, 1.11f, -0.58f) };
        private static readonly float[] _browCuriousAngles = { -3f, 6f };
        private static readonly Vector3[] _browShockedPositions = { new Vector3(-0.27f, 1.15f, -0.565f), new Vector3(0.27f, 1.15f, -0.565f) };
        private static readonly float[] _browShockedAngles = { 14f, -14f };
        // 눈 크기(흰자와 테의 세로 배율), 시선(홍채 묶음 자리), 동공 크기의 처음과 끝 값이다.
        // 처음에는 홍채가 위아래 테에 닿는 보통 눈이고, 끝에는 홍채 둘레로 흰자가 다 보인다.
        private static readonly Vector3 _eyeCuriousOpening = new Vector3(1f, 0.56f, 1f);
        private static readonly Vector3 _gazeCurious = new Vector3(0.02f, 0f, 0f);
        private static readonly Vector3 _gazeShocked = new Vector3(0.02f, 0.04f, 0f);
        private static readonly Vector3 _pupilNormal = new Vector3(0.055f, 0.055f, 0.03f);
        private static readonly Vector3 _pupilDilated = new Vector3(0.11f, 0.11f, 0.03f);
        // 동공에 비친 운석 띠의 색이다. 2-2 처음 화면의 흰 띠처럼 보이게 한다.
        private static readonly Color _reflectionBandColor = new Color(1f, 1f, 1f, 0.65f);
        // 4-1 세트 자리다. 충격점은 카메라를 향한 점을 _shatterImpactShift만큼 왼쪽 위로 옮긴 방향이다.
        private static readonly Vector3 _shatterSetPosition = new Vector3(12000f, 0f, 0f);
        private static readonly Vector3 _shatterImpactShift = new Vector3(-0.12f, 0.22f, 0f);
        // 4-1 구덩이, 잘린 맨틀 면, 외핵, 유리 반사 줄의 색이다.
        private static readonly Color _craterColor = new Color(0.85f, 0.42f, 0.2f, 1f);
        private static readonly Color _mantleColor = new Color(0.45f, 0.13f, 0.07f, 1f);
        private static readonly Color _coreColor = new Color(1f, 0.6f, 0.24f, 1f);
        private static readonly Color _glassSheenColor = new Color(0.85f, 0.9f, 1f, 0.035f);
        // 4-2 세트 자리다. 4-1 세트와 서로의 카메라 원평면(800m)보다 멀리 둔다.
        private static readonly Vector3 _handSetPosition = new Vector3(14000f, 0f, 0f);
        // 유리에 닿은 손목 자리(눈 기준 로컬 값)다. 손이 오른쪽 어깨 바로 앞에 있어 팔이 화면 아래에서 올라오고, 손바닥 앞면이 유리 바로 앞에 온다.
        private static readonly Vector3 _handWrist = new Vector3(0.17f, -0.06f, HAND_GLASS_DISTANCE - 0.0135f);
        // 손이 올라오기 시작하는 자리(유리에 닿은 손목 자리 기준)와 그때의 손목 기울기(도), 팔이 뻗어 가는 어깨 자리(눈 기준)다.
        private static readonly Vector3 _handRiseStart = new Vector3(0.03f, -0.26f, -0.16f);
        private static readonly Vector3 _handRiseTilt = new Vector3(50f, 0f, -15f);
        private static readonly Vector3 _handShoulder = new Vector3(0.2f, -0.24f, 0f);
        // 손가락은 검지·중지·약지·새끼 순서의 가로 자리와 세 마디 길이(미터)다. 마디 순서대로 올라올 때의 굽음과 주먹의 굽음(도)이 있다.
        private static readonly float[] _fingerX = { -0.03f, -0.009f, 0.012f, 0.031f };
        private static readonly float[,] _fingerLengths = { { 0.034f, 0.023f, 0.019f }, { 0.037f, 0.026f, 0.02f }, { 0.035f, 0.024f, 0.019f }, { 0.028f, 0.018f, 0.016f } };
        private static readonly float[] _fingerRelaxed = { 15f, 15f, 10f };
        private static readonly float[] _fingerCurl = { 88f, 98f, 65f };
        // 엄지 뿌리의 편 손과 주먹 회전, 엄지 끝의 주먹 회전(도)이다.
        private static readonly Vector3 _thumbOpen = new Vector3(0f, 0f, 38f);
        private static readonly Vector3 _thumbFist = new Vector3(30f, -20f, -32f);
        private static readonly Vector3 _thumbTipFist = new Vector3(0f, 0f, -40f);
        // 떨림 두 축의 물결이다. 값은 첫 물결 각속도(라디안/초), 시작 자리, 둘째 물결 각속도, 시작 자리 순서이고 두 물결을 0.6:0.4로 섞는다.
        private static readonly Vector4 _trembleWaveX = new Vector4(69f, 0f, 107f, 1f);
        private static readonly Vector4 _trembleWaveY = new Vector4(83f, 2f, 131f, 0f);
        // 4-3 세트 자리다. 4-2 세트와 서로의 카메라 원평면보다 멀리 둔다.
        private static readonly Vector3 _heroSetPosition = new Vector3(16000f, 0f, 0f);
        // 주인공 머리는 2-1과 같은 크기다. 머리 축 자리(세트 기준), 머리 중심과 크기, 찍는 왼쪽 눈 자리, 슬픈 눈썹(안쪽 끝이 올라감)의 자리와 기울기, 코의 자리와 크기(머리 축 기준)다.
        private static readonly Vector3 _heroHeadPivot = new Vector3(0f, 1.05f, 0f);
        private static readonly Vector3 _heroHeadCenter = new Vector3(0f, 0.85f, 0f);
        private static readonly Vector3 _heroHeadSize = new Vector3(1.3f, 1.55f, 1.35f);
        private static readonly Vector3 _heroEyePosition = new Vector3(0.26f, 0.85f, -0.6f);
        private static readonly Vector3 _heroBrowPosition = new Vector3(0.25f, 1.065f, -0.585f);
        private static readonly Vector3 _heroBrowTilt = new Vector3(0f, 0f, -16f);
        private static readonly Vector3 _heroNosePosition = new Vector3(0f, 0.69f, -0.68f);
        private static readonly Vector3 _heroNoseSize = new Vector3(0.17f, 0.22f, 0.17f);
        // 눈 테, 흰자, 홍채, 동공의 크기다. 동공은 어두운 곳이라 2-1 처음보다 크다.
        private static readonly Vector3 _heroRimSize = new Vector3(0.34f, 0.38f, 0.1f);
        private static readonly Vector3 _heroWhiteSize = new Vector3(0.3f, 0.34f, 0.12f);
        private static readonly Vector3 _heroIrisSize = new Vector3(0.17f, 0.17f, 0.05f);
        private static readonly Vector3 _heroPupilSize = new Vector3(0.09f, 0.09f, 0.03f);
        // 고인 눈물, 윗면의 반짝이는 선, 방울, 젖은 자국, 반짝임의 색과 방울 크기다.
        private static readonly Color _tearPoolColor = new Color(0.78f, 0.87f, 0.96f, 0.5f);
        private static readonly Color _tearShineColor = new Color(1f, 1f, 1f, 0.92f);
        private static readonly Color _tearDropColor = new Color(0.8f, 0.88f, 0.97f, 0.7f);
        private static readonly Color _tearTrailColor = new Color(0.85f, 0.92f, 1f, 0.28f);
        private static readonly Color _tearSparkColor = new Color(1f, 1f, 1f, 0.95f);
        private static readonly Vector3 _tearDropSize = new Vector3(0.028f, 0.033f, 0.02f);

        private static Sprite _circle;
        private static Sprite _triangle;
        private static Sprite _glow;
        private static Sprite _glowHot;
        private static Sprite _glassSheen;
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
        /// 도형 그림(원, 삼각형, 달아오른 공기, 띠의 뜨거운 쪽, 유리 반사 줄)이 없으면 만들고, 칸을 만들 때 쓰는 그림과 글꼴을 불러 둔다.
        /// _circle, _triangle, _glow, _glowHot, _glassSheen, _rounded, _font를 채운다.
        /// </summary>
        private static void PrepareAssets()
        {
            EnsureShapeSprites();
            _circle = AssetDatabase.LoadAssetAtPath<Sprite>(CIRCLE_PATH);
            _triangle = AssetDatabase.LoadAssetAtPath<Sprite>(TRIANGLE_PATH);
            _glow = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_PATH);
            _glowHot = AssetDatabase.LoadAssetAtPath<Sprite>(GLOW_HOT_PATH);
            _glassSheen = AssetDatabase.LoadAssetAtPath<Sprite>(SHEEN_PATH);
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
            // 운석이 부딪히기 전이라 1-2 칸 흔들림을 넣지 않는다.
            // 어두워지는 하늘을 먼저 느낀 흰 새 떼가 카메라 가까이를 지나 달아나고, 그 뒤 그림자가 들어온다.
            CreateBirdFlock(zoomSet, shadowCamera, zoomSet.TransformPoint(_citizenEnd), shadowPanel);
            CreateLabel(shadowPanel, shadowSize, "1-2", "그림자", "같은 각도로 줌인, 놀란 흰 새 떼가 앞을 지나간 뒤 그림자가 점점 빨리 덮쳐 온다");
            panels.Add(FinishPanel(shadowPanel));

            // 1-3 올려다보기: 비스듬히 선 인물을 위에서 내려다보는 상반신 구도다. 카메라는 몸 정면 쪽에서 출발해 이 구도로 온다.
            RectTransform lookUp = CreatePanel(page, "1-3", 0.5f, 0f, 1f, 0.48f, Gray(0.24f), out Vector2 lookUpSize);
            Camera bustCamera = CreateSetCamera("Camera_1-3", bustSet, _bustCameraPosition, _bustLookTarget, 50f, Gray(0.4f), EnsureRenderTexture("1-3", lookUpSize));
            RawImage lookUpView = CreateView(lookUp, bustCamera);

            // 카메라는 몸 정면 쪽 위의 가까운 자리에서 기다리다가, 고개를 다 든 뒤 머리 둘레로 돌며 뒤로 빠져 최종 구도에 멈춘다.
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
            // 정면 구간에서는 _headPeak까지 들고, 카메라가 빠지는 동안 _headUp으로 내려온다. 끝 값은 _headUp이다.
            float raiseTotal = dollyDelay + dollyDuration - raiseDelay;
            CuttoonTransformTween raise = AddTransformTween(lookUp, headPivot, raiseDelay, raiseTotal);
            TweenLocalRotation(raise, _headDown, _headUp);
            SetCurve(raise, HeadRaiseCurve(raiseDuration / raiseTotal, (dollyDelay - raiseDelay) / raiseTotal, (_headPeak.x - _headDown.x) / (_headUp.x - _headDown.x)));
            // ?는 고개를 다 든 뒤 나타난다.
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

            // 눈썹은 호기심 어린 자리에서 시작하고, 2-1이 열리면 놀란 자리로 올라간다.
            Transform browLeft = CreatePrimitive(MENU_CUBE, faceHead, "BrowLeft", _browCuriousPositions[0], new Vector3(0.3f, 0.055f, 0.07f), dark);
            browLeft.localRotation = Quaternion.Euler(0f, 0f, _browCuriousAngles[0]);
            Transform browRight = CreatePrimitive(MENU_CUBE, faceHead, "BrowRight", _browCuriousPositions[1], new Vector3(0.3f, 0.055f, 0.07f), dark);
            browRight.localRotation = Quaternion.Euler(0f, 0f, _browCuriousAngles[1]);
            CreatePrimitive(MENU_SPHERE, faceHead, "Nose", new Vector3(0f, 0.69f, -0.68f), new Vector3(0.17f, 0.22f, 0.17f), skin);

            // 입은 얼굴 아래쪽 곡면을 따라 아래로 기울인 어두운 타원이다. 처음에는 다문 선이고, 2-1이 열리면 벌어진다.
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

            // 동공 가운데 앞에 2-2 처음 운석과 같은 모습(흐린 회색 몸체와 흰 띠)의 비친 운석을 둔다.
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

                // 동공에 비친 운석이 고르게 커지고, 눈이 다 커진 뒤에도 잠시 더 커져 운석이 2-2 전부터 다가오고 있음을 보여 준다.
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
            // 운석은 반지름이 처음 각도로 보이는 먼 거리에서 출발한다. 크기는 그대로라 다가오면 화면 크기가 거리에 반비례해 커진다.
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

            // 하늘색 막은 구덩이까지 덮도록 운석보다 조금 크다. 처음 투명도로 저장하고 연출이 걷어 낸다.
            Color hazeColor = new Color(SKY_GRAY, SKY_GRAY, SKY_GRAY, HAZE_START);
            Transform haze = CreatePrimitive(MENU_SPHERE, povMeteor, "Haze", Vector3.zero, Vector3.one * HAZE_SCALE, EnsureTransparentMaterial("GB_Haze", hazeColor, null, 1));
            // 띠 판은 운석과 함께 돌아 앞면(-Z)이 시민을 본다. 띠 그림의 안쪽 원이 운석 반지름에 놓이는 크기라, 안쪽은 운석 몸체에 가려진다.
            Transform glow = CreatePrimitive(MENU_QUAD, povMeteor, "Glow", Vector3.zero, Vector3.one / GLOW_INNER, EnsureTransparentMaterial("GB_Glow", _glowStartColor, _glow.texture, 0));
            // 뜨거운 쪽 판은 띠 판과 같은 크기이고, 그림 오른쪽이 HOT_CLOUD 구름 방향을 보게 돌린다. 처음엔 투명하고 막과 띠보다 나중에 그린다.
            Color hotClear = new Color(_glowHotColor.r, _glowHotColor.g, _glowHotColor.b, 0f);
            Transform glowHot = CreatePrimitive(MENU_QUAD, povMeteor, "GlowHot", Vector3.zero, Vector3.one / GLOW_INNER, EnsureTransparentMaterial("GB_GlowHot", hotClear, _glowHot.texture, 2));
            glowHot.localRotation = Quaternion.Euler(0f, 0f, _cloudAngles[HOT_CLOUD] - GLOW_HOT_SWEEP * 0.5f);
            foreach (Transform layer in new[] { haze, glow, glowHot })
            {
                MeshRenderer layerRenderer = layer.GetComponent<MeshRenderer>();
                layerRenderer.shadowCastingMode = ShadowCastingMode.Off;
                layerRenderer.receiveShadows = false;
            }

            // 구름은 칸 화면에 똑바로 서도록 시선과 같은 방향으로 돌린다. 구름 묶음의 로컬 X가 칸 가로다.
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
                // HOT_CLOUD 구름은 운석 바로 아래에 있어 윗면이 운석을 향한다. 윗면 왼쪽에 뜯겨 나갈 조각을 크기 0으로 숨겨 둔다.
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
            // 새는 흰색이다.
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
        /// 빛을 받지 않는 색 재질 name을 MATERIAL_FOLDER에서 찾아 돌려준다. 없으면 색 color인 URP Unlit 재질로 만든다.
        /// </summary>
        private static Material EnsureUnlitColorMaterial(string name, Color color)
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
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 지름 1인 촘촘한 구 메시를 SPHERE_MESH_PATH에서 찾아 돌려준다. 없으면 위도 SPHERE_MESH_LATITUDE칸, 경도 그 두 배 칸으로 만들어 저장한다.
        /// 기본 구 메시는 칸이 성겨, 두 구가 얕은 각도로 만나는 3-2 불길 경계가 계단처럼 보이기 때문에 쓴다.
        /// </summary>
        private static Mesh EnsureSphereMesh()
        {
            EnsureFolder(MESH_FOLDER);
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(SPHERE_MESH_PATH);
            if (mesh != null)
            {
                return mesh;
            }

            int latitude = SPHERE_MESH_LATITUDE;
            int longitude = SPHERE_MESH_LATITUDE * 2;
            Vector3[] vertices = new Vector3[(latitude + 1) * (longitude + 1)];
            Vector3[] normals = new Vector3[vertices.Length];
            int[] triangles = new int[latitude * longitude * 6];
            for (int a = 0; a <= latitude; a++)
            {
                float theta = Mathf.PI * a / latitude;
                for (int b = 0; b <= longitude; b++)
                {
                    float phi = 2f * Mathf.PI * b / longitude;
                    Vector3 direction = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                    int index = a * (longitude + 1) + b;
                    vertices[index] = direction * 0.5f;
                    normals[index] = direction;
                }
            }

            // 바깥에서 볼 때 시계 방향이 되도록 위쪽 줄 두 점과 아래쪽 줄 한 점, 아래쪽 줄 두 점과 위쪽 줄 한 점으로 삼각형을 잇는다.
            int cursor = 0;
            for (int a = 0; a < latitude; a++)
            {
                for (int b = 0; b < longitude; b++)
                {
                    int top = a * (longitude + 1) + b;
                    int bottom = top + longitude + 1;
                    triangles[cursor++] = top;
                    triangles[cursor++] = top + 1;
                    triangles[cursor++] = bottom;
                    triangles[cursor++] = bottom;
                    triangles[cursor++] = top + 1;
                    triangles[cursor++] = bottom + 1;
                }
            }

            mesh = new Mesh();
            mesh.name = "GB_SphereDense";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, SPHERE_MESH_PATH);
            return mesh;
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

            // 2-1 시민의 얼굴. 위를 올려다보는 얼굴을 정면에서, 표정이 칸을 꽉 채우게 찍는다.
            RectTransform facePanel = CreatePanel(page, "2-1", 0f, 0f, 0.24f, 1f, Gray(0.14f), out Vector2 faceSize);
            FaceCameraPose(faceSet, faceHead, faceSize.x / faceSize.y, out Vector3 faceCameraPosition, out Vector3 faceLookTarget);
            Camera faceCamera = CreateSetCamera("Camera_2-1", faceSet, faceCameraPosition, faceLookTarget, FACE_FOV, Gray(SKY_GRAY), EnsureRenderTexture("2-1", faceSize));
            CreateView(facePanel, faceCamera);
            // 호기심 어린 표정이 눈썹, 눈, 동공, 입 순서로 조금씩 겹치며 경악한 표정으로 바뀐다.
            AddFaceChange(facePanel, faceHead);
            CreateLabel(facePanel, faceSize, "2-1", "시민의 얼굴", "위를 올려다본 얼굴 정면, 호기심 어린 표정이 점점 경악으로 바뀐다. 동공에 비친 운석이 점점 커진다");
            panels.Add(FinishPanel(facePanel));

            // 2-2 운석. 2-1의 시민이 길 양옆 건물 사이로 올려다본 하늘이다. 운석은 화면 가운데에 있다.
            RectTransform meteorPanel = CreatePanel(page, "2-2", 0.24f, 0f, 1f, 1f, Gray(0.16f), out Vector2 meteorSize);
            Vector3 eye = new Vector3(0f, POV_EYE_HEIGHT, 0f);
            Quaternion look = Quaternion.Euler(-POV_PITCH, POV_YAW, 0f);
            Vector3 forward = look * Vector3.forward;
            Camera povCamera = CreateSetCamera("Camera_2-2", povSet, eye, eye + forward * 10f, POV_FOV, Gray(SKY_GRAY), EnsureRenderTexture("2-2", meteorSize));
            // 처음 거리의 운석과 그 뒤의 구름까지 그린다.
            povCamera.farClipPlane = POV_FAR;
            CreateView(meteorPanel, povCamera);

            // 운석은 크기 그대로 시선 방향으로 다가온다. 거의 멈춘 듯 묵직하게 떠나 점점 빨라지고, 끝에는 칸 위아래를 넘치게 차서 하늘을 닫는다.
            CuttoonTransformTween approach = AddTransformTween(meteorPanel, povMeteor, POV_APPROACH_DELAY, POV_APPROACH_DURATION);
            TweenLocalPosition(approach, povMeteor.localPosition, eye + forward * POV_METEOR_DISTANCE);
            SetCurve(approach, new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, POV_APPROACH_END_SLOPE, 0f)));

            // 멀 때는 하늘색 막이 운석 몸체와 구덩이를 하늘빛에 묻고, 다가올수록 걷혀 운석이 짙고 또렷해진다.
            CuttoonColorTween clear = AddColorTween(meteorPanel, povMeteor.Find("Haze").GetComponent<Renderer>(), POV_APPROACH_DELAY, POV_APPROACH_DURATION);
            TweenColor(clear, new Color(SKY_GRAY, SKY_GRAY, SKY_GRAY, HAZE_START), new Color(SKY_GRAY, SKY_GRAY, SKY_GRAY, 0f));
            SetCurve(clear, EaseInCurve());

            // 운석 앞에서 짓눌려 달아오른 공기는 흰빛으로 옅게 시작해 다가올수록 진해지며 옅은 살구빛이 된다.
            CuttoonColorTween heat = AddColorTween(meteorPanel, povMeteor.Find("Glow").GetComponent<Renderer>(), POV_APPROACH_DELAY, POV_APPROACH_DURATION);
            TweenColor(heat, _glowStartColor, _glowEndColor);
            SetCurve(heat, EaseInCurve());

            // 운석이 가까워지면 띠 가운데 HOT_CLOUD 구름을 향한 쪽이 흰빛에 가깝게 세지고, 들쭉날쭉한 폭이 천천히 흐른다.
            // 달아오른 공기가 그 구름을 스치기 직전임을 먼저 보여 준다.
            Transform glowHot = povMeteor.Find("GlowHot");
            Color hotClear = new Color(_glowHotColor.r, _glowHotColor.g, _glowHotColor.b, 0f);
            CuttoonColorTween hot = AddColorTween(meteorPanel, glowHot.GetComponent<Renderer>(), GLOW_HOT_DELAY, GLOW_HOT_DURATION);
            TweenColor(hot, hotClear, new Color(_glowHotColor.r, _glowHotColor.g, _glowHotColor.b, GLOW_HOT_ALPHA));
            float hotAngle = _cloudAngles[HOT_CLOUD];
            CuttoonTransformTween flow = AddTransformTween(meteorPanel, glowHot, GLOW_HOT_DELAY, GLOW_HOT_SWEEP_DURATION);
            TweenLocalRotation(flow, new Vector3(0f, 0f, hotAngle - GLOW_HOT_SWEEP * 0.5f), new Vector3(0f, 0f, hotAngle + GLOW_HOT_SWEEP * 0.5f));
            SetCurve(flow, AnimationCurve.Linear(0f, 0f, 1f, 1f));

            // 구름은 운석에 밀리지 않고 바람에 아주 천천히 칸 왼쪽으로 흐른다. 같은 거리를 흘러 가까운 구름이 화면에서 더 빨리 움직인다.
            Vector3 wind = look * Vector3.left * CLOUD_DRIFT;
            foreach (Transform cloud in clouds)
            {
                CuttoonTransformTween drift = AddTransformTween(meteorPanel, cloud, 0f, POV_APPROACH_DELAY + POV_APPROACH_DURATION);
                TweenLocalPosition(drift, cloud.localPosition, cloud.localPosition + wind);
                SetCurve(drift, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }

            // 띠가 HOT_CLOUD 구름 윗면에 닿는 순간, 왼쪽 덩이가 운석 가장자리를 따라 옆으로 떨어져 나가며 작아지고 윗면 조각 하나가 더 멀리 날아간다.
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

            // 효과음 글자는 싸 보여서 넣지 않는다.
            // 원인이 화면에 없는 2쪽 흔들림은 넣지 않는다.
            CreateLabel(meteorPanel, meteorSize, "2-2", "운석", "시민이 올려다본 하늘, 흐릿하던 운석이 점점 빨라지며 하늘을 덮고, 달아오른 공기에 아래 구름이 뜯긴다");
            panels.Add(FinishPanel(meteorPanel));

            return FinishPage(page, panels, 0f);
        }

        /// <summary>
        /// 3-2 지구와 로켓 3D 세트를 _launchSetPosition에 만든다. 칸 크기 viewSize의 가로세로 비율로 지구를 칸 화면의 정한 자리에 둔다.
        /// 불길 구 burn과 로켓 rocket은 시작 자리에 두고 끝 자리를 burnEnd, rocketEnd로 내보낸다. 몸체 돌기 대상 rocketBody와 불꽃 떨림 대상 flameOuter, flameCore도 내보낸다.
        /// 착탄점을 중심으로 부풀 충격파 테두리 shockRing은 처음 크기로 두고 내보낸다. 세트 루트를 돌려준다. 칸 카메라는 루트 원점에서 앞(+Z)을 본다.
        /// </summary>
        private static Transform BuildLaunchSet(Vector2 viewSize, out Transform burn, out Vector3 burnEnd, out Transform shockRing, out Transform rocket, out Vector3 rocketEnd, out Transform rocketBody, out Transform flameOuter, out Transform flameCore)
        {
            Transform root = CreateFromMenu(MENU_EMPTY, null, "Set_Launch").transform;
            root.position = _launchSetPosition;
            root.rotation = Quaternion.identity;
            float tanHalf = Mathf.Tan(LAUNCH_FOV * 0.5f * Mathf.Deg2Rad);
            float aspect = viewSize.x / viewSize.y;
            Vector3 earthCenter = new Vector3((LAUNCH_EARTH_VIEW_X * 2f - 1f) * tanHalf * aspect * LAUNCH_EARTH_DISTANCE, (LAUNCH_EARTH_VIEW_Y * 2f - 1f) * tanHalf * LAUNCH_EARTH_DISTANCE, LAUNCH_EARTH_DISTANCE);
            Mesh denseSphere = EnsureSphereMesh();

            Transform earth = CreatePrimitive(MENU_SPHERE, root, "Earth", earthCenter, Vector3.one * LAUNCH_EARTH_RADIUS * 2f, EnsureMaterial("GB_Earth", 0.42f));
            earth.GetComponent<MeshFilter>().sharedMesh = denseSphere;

            // 지구보다 조금 작은 붉은 구를 착탄점 쪽으로 밀면 지구 밖으로 나온 부분만 보여, 착탄점 둘레의 둥근 불길이 된다.
            Vector3 impact = ((-earthCenter).normalized + _impactShift).normalized;
            burn = CreatePrimitive(MENU_SPHERE, root, "Burn", earthCenter + impact * LAUNCH_EARTH_RADIUS * BurnOffset(BURN_START_ANGLE), Vector3.one * LAUNCH_EARTH_RADIUS * BURN_SCALE * 2f, EnsureUnlitColorMaterial("GB_Burn", _fireColor));
            burn.GetComponent<MeshFilter>().sharedMesh = denseSphere;
            burnEnd = earthCenter + impact * LAUNCH_EARTH_RADIUS * BurnOffset(BURN_END_ANGLE);

            // 충격파 테두리는 착탄점(불길 중심의 지구 표면)을 중심으로 부푸는 껍질이다. 지구 안쪽 반은 지구에 가려 보이지 않는다.
            shockRing = CreatePrimitive(MENU_SPHERE, root, "ShockRing", earthCenter + impact * LAUNCH_EARTH_RADIUS, Vector3.one * ShockRingRadius(0f) * 2f, EnsureShockMaterial());
            shockRing.GetComponent<MeshFilter>().sharedMesh = denseSphere;

            // 별은 칸 화면에 고르게 흩어지도록 먼 거리에서 시야 안에 둔다. 씨앗을 고정해 다시 만들어도 같은 자리에 놓인다.
            Material star = EnsureUnlitMaterial("GB_Star", 0.6f);
            System.Random random = new System.Random(7);
            for (int i = 0; i < LAUNCH_STAR_COUNT; i++)
            {
                float z = 330f + (float)random.NextDouble() * 70f;
                float halfHeight = tanHalf * z;
                float x = ((float)random.NextDouble() * 2f - 1f) * halfHeight * aspect;
                float y = ((float)random.NextDouble() * 2f - 1f) * halfHeight;
                float size = 0.7f + (float)random.NextDouble() * 0.6f;
                CreatePrimitive(MENU_SPHERE, root, "Star" + i, new Vector3(x, y, z), Vector3.one * size, star);
            }

            Vector3 start = earthCenter + _rocketLaunchNormal.normalized * (LAUNCH_EARTH_RADIUS - ROCKET_START_DEPTH);
            rocketEnd = Vector3.Lerp(start, _rocketPassPoint, (start.z - ROCKET_END_DISTANCE) / (start.z - _rocketPassPoint.z));
            rocket = CreateFromMenu(MENU_EMPTY_CHILD, root, "Rocket").transform;
            rocket.localPosition = start;
            rocket.localRotation = Quaternion.LookRotation(rocketEnd - start);
            rocket.localScale = Vector3.one * ROCKET_SCALE;
            rocketBody = CreateFromMenu(MENU_EMPTY_CHILD, rocket, "Body").transform;
            rocketBody.localPosition = Vector3.zero;
            rocketBody.localRotation = Quaternion.identity;
            BuildRocket(rocketBody, out flameOuter, out flameCore);

            foreach (MeshRenderer meshRenderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            return root;
        }

        /// <summary>
        /// body 아래에 앞이 +Z인 로켓 도형(원통 몸체, 둥근 머리, 띠, 창, 날개 4장, 노즐)과 두 겹 불꽃을 만든다.
        /// 불꽃은 노즐에 붙은 받침 아래 떨림 축 flameOuter, flameCore에 달려, 두 축을 로컬 Z로 돌리면 불꽃이 노즐을 축으로 기운다.
        /// </summary>
        private static void BuildRocket(Transform body, out Transform flameOuter, out Transform flameCore)
        {
            Quaternion alongZ = Quaternion.Euler(90f, 0f, 0f);
            Material hull = EnsureMaterial("GB_RocketHull", 0.8f);
            Material dark = EnsureMaterial("GB_RocketDark", 0.3f);
            CreatePrimitive(MENU_CYLINDER, body, "Hull", Vector3.zero, new Vector3(0.9f, 2.2f, 0.9f), hull).localRotation = alongZ;
            CreatePrimitive(MENU_SPHERE, body, "Nose", new Vector3(0f, 0f, 2.2f), new Vector3(0.9f, 0.9f, 2f), hull);
            CreatePrimitive(MENU_CYLINDER, body, "Band", new Vector3(0f, 0f, 1.1f), new Vector3(0.93f, 0.12f, 0.93f), dark).localRotation = alongZ;
            CreatePrimitive(MENU_SPHERE, body, "Window", new Vector3(0f, 0.4f, 1.65f), new Vector3(0.28f, 0.2f, 0.28f), EnsureMaterial("GB_RocketWindow", 0.18f));
            Material fin = EnsureMaterial("GB_RocketFin", 0.52f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f + 45f;
                Vector3 outward = new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0f);
                CreatePrimitive(MENU_CUBE, body, "Fin" + i, outward * 0.8f + new Vector3(0f, 0f, -1.6f), new Vector3(0.08f, 0.8f, 1.3f), fin).localRotation = Quaternion.Euler(0f, 0f, angle - 90f);
            }

            CreatePrimitive(MENU_CYLINDER, body, "Nozzle", new Vector3(0f, 0f, -2.42f), new Vector3(0.62f, 0.22f, 0.62f), dark).localRotation = alongZ;
            // 받침의 로컬 +X가 몸체 뒤쪽(-Z)을 보게 돌려, 불꽃을 +X로 늘이고 떨림 축의 Z 회전이 불꽃을 위아래로 기울이게 한다.
            Transform mount = CreateFromMenu(MENU_EMPTY_CHILD, body, "FlameMount").transform;
            mount.localPosition = new Vector3(0f, 0f, -2.42f);
            mount.localRotation = Quaternion.Euler(0f, 90f, 0f);
            flameOuter = CreateFromMenu(MENU_EMPTY_CHILD, mount, "FlameOuterPivot").transform;
            flameOuter.localPosition = Vector3.zero;
            flameOuter.localRotation = Quaternion.identity;
            CreatePrimitive(MENU_SPHERE, flameOuter, "FlameOuter", new Vector3(1.5f, 0f, 0f), new Vector3(3f, 0.85f, 0.85f), EnsureUnlitColorMaterial("GB_FlameOuter", _flameOuterColor));
            flameCore = CreateFromMenu(MENU_EMPTY_CHILD, mount, "FlameCorePivot").transform;
            flameCore.localPosition = Vector3.zero;
            flameCore.localRotation = Quaternion.identity;
            CreatePrimitive(MENU_SPHERE, flameCore, "FlameCore", new Vector3(0.7f, 0f, 0f), new Vector3(1.7f, 0.45f, 0.45f), EnsureUnlitColorMaterial("GB_FlameCore", _flameCoreColor));
        }

        /// <summary>
        /// 불길이 착탄점 둘레 반경 angle도(지구 중심에서 잰 각도)까지 번져 보이도록, 불길 구 중심을 지구 중심에서 착탄점 쪽으로 미는 거리를 지구 반지름 배로 돌려준다.
        /// 지구 반지름을 1로 보면 불길 구(반지름 BURN_SCALE)와 지구 표면이 만나는 원이 angle도에 오는 거리이며, 두 해 중 지구 밖으로 덜 나오는 쪽을 쓴다.
        /// </summary>
        private static float BurnOffset(float angle)
        {
            float cosine = Mathf.Cos(angle * Mathf.Deg2Rad);
            return cosine - Mathf.Sqrt(cosine * cosine - (1f - BURN_SCALE * BURN_SCALE));
        }

        /// <summary>
        /// 불길 구 위치 연출의 곡선을 만든다. 번진 반경이 BURN_START_ANGLE도에서 BURN_END_ANGLE도까지 처음엔 빠르게 점점 느리게(이즈 아웃) 커지도록
        /// 시간마다 BurnOffset 거리를 0~1로 바꾼 값을 11개 키로 넣고 접선을 부드럽게 한 곡선을 돌려준다.
        /// </summary>
        private static AnimationCurve BurnCurve()
        {
            float from = BurnOffset(BURN_START_ANGLE);
            float to = BurnOffset(BURN_END_ANGLE);
            Keyframe[] keys = new Keyframe[11];
            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / 10f;
                float eased = 1f - (1f - t) * (1f - t);
                keys[i] = new Keyframe(t, (BurnOffset(Mathf.Lerp(BURN_START_ANGLE, BURN_END_ANGLE, eased)) - from) / (to - from));
            }

            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            return curve;
        }

        /// <summary>
        /// 3-2가 열리고 seconds초 뒤 충격파 테두리의 반지름(미터)을 돌려준다.
        /// SHOCK_RING_DURATION초 동안 이즈 아웃으로 SHOCK_RING_END_RADIUS까지 커지는 값과, 그때 불길 가장자리까지의 표면 직선거리에 SHOCK_RING_LEAD를 곱한 값 중 큰 쪽이다.
        /// </summary>
        private static float ShockRingRadius(float seconds)
        {
            float ring = Mathf.Clamp01(seconds / SHOCK_RING_DURATION);
            float burn = Mathf.Clamp01(seconds / BURN_DURATION);
            float burnAngle = Mathf.Lerp(BURN_START_ANGLE, BURN_END_ANGLE, 1f - (1f - burn) * (1f - burn));
            float burnChord = 2f * LAUNCH_EARTH_RADIUS * Mathf.Sin(burnAngle * 0.5f * Mathf.Deg2Rad);
            return Mathf.Max(SHOCK_RING_END_RADIUS * (1f - (1f - ring) * (1f - ring)), SHOCK_RING_LEAD * burnChord);
        }

        /// <summary>
        /// 3-2 충격파 테두리 크기 연출의 곡선을 만든다. 시간마다 ShockRingRadius를 처음 반지름에서 끝 반지름까지 0~1로 바꾼 값을
        /// 11개 키로 넣고 접선을 부드럽게 한 곡선을 돌려준다.
        /// </summary>
        private static AnimationCurve ShockRingCurve()
        {
            float from = ShockRingRadius(0f);
            Keyframe[] keys = new Keyframe[11];
            for (int i = 0; i < keys.Length; i++)
            {
                float t = i / 10f;
                keys[i] = new Keyframe(t, (ShockRingRadius(t * SHOCK_RING_DURATION) - from) / (SHOCK_RING_END_RADIUS - from));
            }

            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            return curve;
        }

        /// <summary>
        /// 0~1 구간에서 y = a·x + (1 − a)·x²인 곡선을 두 키로 만들어 돌려준다. a는 startSlope다.
        /// 시작 기울기 a, 끝 기울기 2 − a인 두 키의 보간이 이 2차식과 같다.
        /// </summary>
        private static AnimationCurve QuadraticCurve(float startSlope)
        {
            return new AnimationCurve(new Keyframe(0f, 0f, startSlope, startSlope), new Keyframe(1f, 1f, 2f - startSlope, 2f - startSlope));
        }

        /// <summary>
        /// 충격파 껍질 재질 GB_Shock을 MATERIAL_FOLDER에서 찾아 돌려준다.
        /// 없으면 SHOCK_SHADER 셰이더로 만들고, 씬 방향광이 나아가는 방향을 껍질 음영 방향(_LightDir)으로 넣는다.
        /// </summary>
        private static Material EnsureShockMaterial()
        {
            EnsureFolder(MATERIAL_FOLDER);
            string path = MATERIAL_FOLDER + "/GB_Shock.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null)
            {
                return material;
            }

            material = new Material(Shader.Find(SHOCK_SHADER));
            material.SetColor("_BaseColor", _shockColor);
            material.SetVector("_LightDir", Object.FindFirstObjectByType<Light>().transform.forward);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        /// <summary>
        /// 3-3 진행도 progress에서 충격파 반지름(미터)을 돌려준다. 중력에 끌려 점점 느려진다.
        /// </summary>
        private static float ShockWaveRadius(float progress)
        {
            return SHOCK_WAVE_START + SHOCK_WAVE_SPEED * progress - SHOCK_WAVE_SLOW * progress * progress;
        }

        /// <summary>
        /// 3-3 칸 카메라의 세트 기준 위치를 돌려준다. 진로 SHOCK_CAMERA_MID 지점에서 _shockCameraOffset 방향으로 SHOCK_CAMERA_DISTANCE만큼 떨어진 자리다.
        /// </summary>
        private static Vector3 ShockCameraPosition()
        {
            return new Vector3(0f, SHOCK_CAMERA_MID, 0f) + _shockCameraOffset.normalized * SHOCK_CAMERA_DISTANCE;
        }

        /// <summary>
        /// 3-3 진행도 progress에서 칸 카메라의 세트 기준 회전을 돌려준다.
        /// 진로 높이 SHOCK_LOOK_START에서 SHOCK_LOOK_END로 옮겨 가는 점을 바라보고, 로켓이 왼쪽 아래에서 오른쪽 위로 가도록 SHOCK_CAMERA_ROLL도 기울인다.
        /// </summary>
        private static Quaternion ShockCameraRotation(float progress)
        {
            Vector3 target = new Vector3(0f, Mathf.LerpUnclamped(SHOCK_LOOK_START, SHOCK_LOOK_END, progress), 0f);
            return Quaternion.LookRotation(target - ShockCameraPosition(), Vector3.up) * Quaternion.Euler(0f, 0f, SHOCK_CAMERA_ROLL);
        }

        /// <summary>
        /// 3-3 파편 index가 충돌 지점에서 날아가는 세트 기준 단위 방향을 돌려준다.
        /// 로켓 진로(+Y)에서 _debrisAngles도, 카메라 방위 cameraAzimuth에서 _debrisAzimuths도 벗어난 방향이다.
        /// </summary>
        private static Vector3 DebrisDirection(int index, float cameraAzimuth)
        {
            float tilt = _debrisAngles[index] * Mathf.Deg2Rad;
            float azimuth = (cameraAzimuth + _debrisAzimuths[index]) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(tilt) * Mathf.Cos(azimuth), Mathf.Cos(tilt), Mathf.Sin(tilt) * Mathf.Sin(azimuth));
        }

        /// <summary>
        /// 3-3 충격파 3D 세트를 _shockSetPosition에 만든다. 충돌 지점을 세트 원점에, 로켓 진로를 세트 +Y에 두고, 씬 방향광이 카메라 쪽 위에서 비치도록 세트를 돌린다.
        /// 로켓 rocket(몸체 rocketBody, 불꽃 flameOuter·flameCore), 충격파 wave, 파편의 위치 축 debris와 도는 축 debrisSpins를 처음 상태로 두고 내보낸다.
        /// 별은 칸 카메라가 보는 쪽에만 둔다. 세트 루트를 돌려준다.
        /// </summary>
        private static Transform BuildShockSet(out Transform rocket, out Transform rocketBody, out Transform flameOuter, out Transform flameCore, out Transform wave, out Transform[] debris, out Transform[] debrisSpins)
        {
            Transform root = CreateFromMenu(MENU_EMPTY, null, "Set_Shock").transform;
            root.position = _shockSetPosition;
            root.rotation = Quaternion.FromToRotation(_shockLocalLight.normalized, Object.FindFirstObjectByType<Light>().transform.forward);
            Mesh denseSphere = EnsureSphereMesh();

            // 별은 카메라가 진행도 0.5에 보는 방향 둘레에 먼 거리로 흩어 둔다. 씨앗을 고정해 다시 만들어도 같은 자리에 놓인다.
            Material star = EnsureUnlitMaterial("GB_Star", 0.6f);
            Vector3 cameraPosition = ShockCameraPosition();
            Vector3 middleForward = ShockCameraRotation(0.5f) * Vector3.forward;
            System.Random random = new System.Random(11);
            for (int i = 0; i < SHOCK_STAR_COUNT; i++)
            {
                Vector3 jitter = new Vector3((float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f, (float)random.NextDouble() * 2f - 1f) * 0.75f;
                float distance = SHOCK_STAR_DISTANCE + (float)random.NextDouble() * SHOCK_STAR_DEPTH;
                float size = 0.6f + (float)random.NextDouble() * 0.5f;
                CreatePrimitive(MENU_SPHERE, root, "Star" + i, cameraPosition + (middleForward + jitter).normalized * distance, Vector3.one * size, star);
            }

            rocket = CreateFromMenu(MENU_EMPTY_CHILD, root, "Rocket").transform;
            rocket.localPosition = new Vector3(0f, SHOCK_ROCKET_START, 0f);
            rocket.localRotation = Quaternion.LookRotation(Vector3.up, Vector3.back);
            rocket.localScale = Vector3.one * SHOCK_ROCKET_SCALE;
            rocketBody = CreateFromMenu(MENU_EMPTY_CHILD, rocket, "Body").transform;
            rocketBody.localPosition = Vector3.zero;
            rocketBody.localRotation = Quaternion.identity;
            BuildRocket(rocketBody, out flameOuter, out flameCore);

            wave = CreatePrimitive(MENU_SPHERE, root, "Shockwave", Vector3.zero, Vector3.one * ShockWaveRadius(0f) * 2f, EnsureShockMaterial());
            wave.GetComponent<MeshFilter>().sharedMesh = denseSphere;

            // 파편의 위치 축은 도는 축 방향으로 돌려 두고, 그 아래 축이 로컬 Z로만 돌아 조각마다 한 축으로 일정하게 돈다.
            Material rock = EnsureMaterial("GB_Debris", 0.36f);
            float cameraAzimuth = Mathf.Atan2(_shockCameraOffset.z, _shockCameraOffset.x) * Mathf.Rad2Deg;
            debris = new Transform[_debrisAngles.Length];
            debrisSpins = new Transform[_debrisAngles.Length];
            for (int i = 0; i < debris.Length; i++)
            {
                debris[i] = CreateFromMenu(MENU_EMPTY_CHILD, root, "Debris" + i).transform;
                debris[i].localPosition = DebrisDirection(i, cameraAzimuth) * _debrisRatios[i] * ShockWaveRadius(0f);
                debris[i].localRotation = Quaternion.LookRotation(_debrisSpinAxes[i].normalized);
                debrisSpins[i] = CreateFromMenu(MENU_EMPTY_CHILD, debris[i], "Spin").transform;
                debrisSpins[i].localPosition = Vector3.zero;
                debrisSpins[i].localRotation = Quaternion.identity;
                float size = _debrisSizes[i];
                Transform piece = CreatePrimitive(MENU_CUBE, debrisSpins[i], "Rock", Vector3.zero, new Vector3(size, size * 0.75f, size * 0.9f), rock);
                piece.localRotation = Quaternion.Euler(i * 37f, i * 61f, i * 23f);
            }

            foreach (MeshRenderer meshRenderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            return root;
        }

        /// <summary>
        /// 4-1 부서지는 지구 3D 세트를 _shatterSetPosition에 만들고, 칸 panel에 조각·외핵 덩어리 연출을 달아 칸 카메라를 돌려준다.
        /// 카메라는 루트 원점에서 앞(+Z)을 보는 주인공의 눈이고, 눈앞 SHATTER_GLASS_DISTANCE에 반사 줄이 있는 유리가 있다.
        /// 지구는 칸 크기 viewSize의 가로세로 비율로 칸 가운데에 두고, 충격 축 둘레로 나눈 조각, 외핵 공, 외핵 덩어리로 만든다.
        /// </summary>
        private static Camera BuildShatterSet(RectTransform panel, Vector2 viewSize)
        {
            Transform root = CreateFromMenu(MENU_EMPTY, null, "Set_Shatter").transform;
            root.position = _shatterSetPosition;
            root.rotation = Quaternion.identity;
            float tanHalf = Mathf.Tan(SHATTER_FOV * 0.5f * Mathf.Deg2Rad);
            float aspect = viewSize.x / viewSize.y;
            Vector3 earthCenter = new Vector3(0f, (SHATTER_EARTH_VIEW_Y * 2f - 1f) * tanHalf * SHATTER_EARTH_DISTANCE, SHATTER_EARTH_DISTANCE);
            CreateWindowStars(root, tanHalf, aspect);

            // 유리는 눈앞의 투명 판이고, 칸 화면을 넉넉히 덮는다.
            float glassHeight = 2f * SHATTER_GLASS_DISTANCE * tanHalf * 1.1f;
            CreatePrimitive(MENU_QUAD, root, "Glass", new Vector3(0f, 0f, SHATTER_GLASS_DISTANCE), new Vector3(glassHeight * aspect, glassHeight, 1f), EnsureTransparentMaterial("GB_GlassSheen", _glassSheenColor, _glassSheen.texture, 0));

            CreateShatteredEarth(root, earthCenter, Quaternion.identity, panel);

            foreach (MeshRenderer meshRenderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            Camera camera = CreateSetCamera("Camera_4-1", root, Vector3.zero, Vector3.forward * 10f, SHATTER_FOV, Gray(0.06f), EnsureRenderTexture("4-1", viewSize));
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = SHATTER_FAR;
            return camera;
        }

        /// <summary>
        /// 창밖 먼 곳에 별 SHATTER_STAR_COUNT개를 root 아래에 흩어 둔다. 시야각 절반의 탄젠트 tanHalf와 칸 가로세로 비율 aspect로 칸 화면 안에 놓는다.
        /// 씨앗을 고정해 다시 만들어도 같은 자리에 놓인다.
        /// </summary>
        private static void CreateWindowStars(Transform root, float tanHalf, float aspect)
        {
            Material star = EnsureUnlitMaterial("GB_Star", 0.6f);
            System.Random random = new System.Random(7);
            for (int i = 0; i < SHATTER_STAR_COUNT; i++)
            {
                float z = 620f + (float)random.NextDouble() * 60f;
                float x = ((float)random.NextDouble() * 2f - 1f) * tanHalf * aspect * z;
                float y = ((float)random.NextDouble() * 2f - 1f) * tanHalf * z;
                float size = 1.15f + (float)random.NextDouble() * 1.05f;
                CreatePrimitive(MENU_SPHERE, root, "Star" + i, new Vector3(x, y, z), Vector3.one * size, star);
            }
        }

        /// <summary>
        /// 부서지는 지구를 parent 아래 로컬 위치 localPosition, 로컬 회전 localRotation에 만든다. 충격 축 둘레로 나눈 조각, 외핵 공, 외핵 덩어리로 이뤄진다.
        /// panel이 있으면 조각이 벌어지며 흩어지고 덩어리가 흘러나오는 연출을 panel에 달고, null이면 그 연출이 끝난 모습으로 멈춰 둔다. 만든 지구 Transform을 돌려준다.
        /// </summary>
        private static Transform CreateShatteredEarth(Transform parent, Vector3 localPosition, Quaternion localRotation, RectTransform panel)
        {
            Vector3 impact = (Vector3.back + _shatterImpactShift).normalized;
            Vector3 axisU = Vector3.Cross(impact, Vector3.up).normalized;
            Vector3 axisV = Vector3.Cross(impact, axisU).normalized;
            Transform earth = CreateFromMenu(MENU_EMPTY_CHILD, parent, "Earth").transform;
            earth.localPosition = localPosition;
            earth.localRotation = localRotation;
            Material[] pieceMaterials = { EnsureMaterial("GB_Earth", 0.42f), EnsureUnlitColorMaterial("GB_ImpactCrater", _craterColor), EnsureUnlitColorMaterial("GB_Burn", _fireColor), EnsureUnlitColorMaterial("GB_Mantle", _mantleColor) };
            Mesh[] pieceMeshes = EnsureShatterPieceMeshes(impact, axisU, axisV);
            AnimationCurve moveCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, SHATTER_MOVE_END_SLOPE, SHATTER_MOVE_END_SLOPE));
            float step = 2f * Mathf.PI / SHATTER_PIECES;
            for (int i = 0; i < SHATTER_PIECES; i++)
            {
                Transform piece = CreatePrimitive(MENU_SPHERE, earth, "Piece" + i, Vector3.zero, Vector3.one, pieceMaterials[0]);
                piece.GetComponent<MeshFilter>().sharedMesh = pieceMeshes[i];
                piece.GetComponent<MeshRenderer>().sharedMaterials = pieceMaterials;

                // 조각은 충격 축에서 바깥으로, 운석이 민 쪽(충격점 반대)으로 조금 기울어 흩어진다.
                // 충격 축과 조각 방향에 수직인 축으로 돌아 구덩이 쪽 끝이 먼저 벌어진다.
                float phi = (i + 0.5f) * step;
                Vector3 side = axisU * Mathf.Cos(phi) + axisV * Mathf.Sin(phi);
                Vector3 outward = (side - impact * SHATTER_PUSH_BACK).normalized;
                Vector3 open = Quaternion.AngleAxis(SHATTER_OPEN_ANGLE, Vector3.Cross(impact, side).normalized).eulerAngles;
                open = new Vector3(Mathf.DeltaAngle(0f, open.x), Mathf.DeltaAngle(0f, open.y), Mathf.DeltaAngle(0f, open.z));
                Vector3 spreadEnd = outward * (SHATTER_SPREAD * LAUNCH_EARTH_RADIUS);
                if (panel == null)
                {
                    piece.localRotation = Quaternion.Euler(open);
                    piece.localPosition = spreadEnd;
                }
                else
                {
                    CuttoonTransformTween turn = AddTransformTween(panel, piece, SHATTER_OPEN_DELAY, SHATTER_OPEN_DURATION);
                    TweenLocalRotation(turn, Vector3.zero, open);
                    SetCurve(turn, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
                    CuttoonTransformTween spread = AddTransformTween(panel, piece, SHATTER_MOVE_DELAY, SHATTER_MOVE_DURATION);
                    TweenLocalPosition(spread, Vector3.zero, spreadEnd);
                    SetCurve(spread, moveCurve);
                }
            }

            // 액체 외핵은 조각에 붙어 가지 않고 가운데 빛나는 공으로 남는다. 내핵은 그 안에 있어 보이지 않는다.
            Material core = EnsureUnlitColorMaterial("GB_Core", _coreColor);
            Transform coreBall = CreatePrimitive(MENU_SPHERE, earth, "Core", Vector3.zero, Vector3.one * LAUNCH_EARTH_RADIUS * SHATTER_CORE_RATIO * 2f * 0.985f, core);
            coreBall.GetComponent<MeshFilter>().sharedMesh = EnsureSphereMesh();

            // 외핵 덩어리는 조각 사이 틈 쪽으로 흘러나온다. 줄지어 보이지 않게 덩어리마다 방향, 크기, 나오는 때, 가는 거리를 다르게 한다.
            AnimationCurve oozeCurve = new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f));
            System.Random blobRandom = new System.Random(3);
            for (int i = 0; i < SHATTER_PIECES; i++)
            {
                float phi = (i + ((float)blobRandom.NextDouble() - 0.5f) * 0.5f) * step;
                Vector3 direction = (axisU * Mathf.Cos(phi) + axisV * Mathf.Sin(phi) + impact * (0.15f + 0.5f * (float)blobRandom.NextDouble())).normalized;
                float size = LAUNCH_EARTH_RADIUS * (0.05f + 0.08f * (float)blobRandom.NextDouble()) * 1.5f;
                float delay = SHATTER_OOZE_DELAY + SHATTER_OOZE_JITTER * (float)blobRandom.NextDouble();
                float reach = 0.5f + 0.7f * (float)blobRandom.NextDouble();
                Vector3 start = direction * LAUNCH_EARTH_RADIUS * SHATTER_CORE_RATIO * 0.85f;
                Vector3 end = start + direction * LAUNCH_EARTH_RADIUS * (0.35f + SHATTER_SPREAD * 0.6f) * reach;
                Vector3 endScale = new Vector3(size, size * 1.8f, size);
                Transform blob = CreatePrimitive(MENU_SPHERE, earth, "Blob" + i, start, Vector3.zero, core);
                blob.localRotation = Quaternion.FromToRotation(Vector3.up, direction);
                if (panel == null)
                {
                    blob.localPosition = end;
                    blob.localScale = endScale;
                }
                else
                {
                    CuttoonTransformTween flow = AddTransformTween(panel, blob, delay, SHATTER_OOZE_DURATION);
                    TweenLocalPosition(flow, start, end);
                    TweenLocalScale(flow, Vector3.zero, endScale);
                    SetCurve(flow, oozeCurve);
                }
            }

            return earth;
        }

        /// <summary>
        /// 4-2 유리에 댄 손 3D 세트를 _handSetPosition에 만들고, 칸 panel에 손이 올라와 유리에 닿았다가 주먹을 쥐고 떠는 연출을 달아 칸 카메라를 돌려준다.
        /// 카메라는 루트 원점에서 앞(+Z)을 보는 주인공의 눈이다. 유리 너머 칸 가운데에 4-1 끝 모습으로 멈춘 지구가 있고, 칸 크기 viewSize의 비율로 별과 창틀을 둔다.
        /// 주먹을 다 쥐었을 때 주먹이 유리에 닿도록 손목이 물러날 거리를 끝 자세에서 재어 쓰고, 다 만든 뒤 손을 연출의 처음 자세로 돌려 둔다.
        /// </summary>
        private static Camera BuildHandSet(RectTransform panel, Vector2 viewSize)
        {
            Transform root = CreateFromMenu(MENU_EMPTY, null, "Set_Hand").transform;
            root.position = _handSetPosition;
            root.rotation = Quaternion.identity;
            float tanHalf = Mathf.Tan(HAND_FOV * 0.5f * Mathf.Deg2Rad);
            float aspect = viewSize.x / viewSize.y;
            CreateWindowStars(root, tanHalf, aspect);

            // 부서진 지구는 4-1이 끝난 모습으로 멈춰 있다. 충격점 쪽 면이 4-1처럼 눈을 향하게 돌린다.
            Vector3 earthCenter = new Vector3(0f, (HAND_EARTH_VIEW_Y * 2f - 1f) * tanHalf, 1f) * SHATTER_EARTH_DISTANCE;
            CreateShatteredEarth(root, earthCenter, Quaternion.LookRotation(earthCenter.normalized, Vector3.up), null);

            // 유리는 4-1과 같은 반사 줄 재질이고, 칸 오른쪽에 세로 창틀이 걸친다.
            float glassHeight = 2f * HAND_GLASS_DISTANCE * tanHalf * 1.1f;
            CreatePrimitive(MENU_QUAD, root, "Glass", new Vector3(0f, 0f, HAND_GLASS_DISTANCE), new Vector3(glassHeight * aspect, glassHeight, 1f), EnsureTransparentMaterial("GB_GlassSheen", _glassSheenColor, _glassSheen.texture, 0));
            float frameDepth = HAND_GLASS_DISTANCE + 0.03f;
            float frameX = (HAND_FRAME_VIEW_X * 2f - 1f) * tanHalf * aspect * frameDepth;
            CreatePrimitive(MENU_CUBE, root, "WindowFrame", new Vector3(frameX + 0.04f, 0f, frameDepth), new Vector3(0.08f, 0.6f, 0.06f), EnsureMaterial("GB_WindowFrame", 0.3f));

            // 손목 자리는 올라오기, 닿기, 주먹 쥘 때의 내려감과 물러남, 두 축 떨림을 한 겹씩 맡는다. 겹마다 끝 자세로 만들어 두고 연출을 단 뒤 처음 자세로 돌린다.
            // 손만 꺾이고 팔뚝은 따라 돌지 않도록 팔과 손을 마지막 겹 아래에 나란히 둔다.
            Transform wrist = CreatePivot(root, "Wrist", _handWrist + Vector3.back * HAND_HOVER);
            Transform touch = CreatePivot(wrist, "Touch", Vector3.forward * HAND_HOVER);
            Transform grip = CreatePivot(touch, "Grip", Vector3.down * HAND_SLIDE);
            Transform shakeX = CreatePivot(grip, "ShakeX", Vector3.zero);
            Transform shakeY = CreatePivot(shakeX, "ShakeY", Vector3.zero);

            // 손은 손목이 뿌리이고, 손바닥이 위(+Y)로 서서 유리(+Z)를 본다. 오른손 손등을 보므로 엄지는 왼쪽(-X)에 있다.
            Material skin = EnsureMaterial("GB_Skin", 0.8f);
            Transform hand = CreatePivot(shakeY, "Hand", Vector3.zero);
            hand.localRotation = Quaternion.Euler(HAND_WRIST_BEND, HAND_WRIST_TWIST, 0f);
            CreatePrimitive(MENU_CUBE, hand, "Palm", new Vector3(0f, 0.05f, 0f), new Vector3(0.082f, 0.1f, 0.026f), skin);
            int segments = _fingerRelaxed.Length;
            Transform[] joints = new Transform[_fingerX.Length * segments];
            Transform[] curls = new Transform[joints.Length];
            for (int f = 0; f < _fingerX.Length; f++)
            {
                Transform parent = hand;
                Vector3 jointPosition = new Vector3(_fingerX[f], 0.1f, 0f);
                for (int s = 0; s < segments; s++)
                {
                    // 마디마다 펴기와 주먹 쥐기를 따로 맡는 두 겹을 둔다. 같은 축으로 돌아 두 각도가 더해진다.
                    float length = _fingerLengths[f, s];
                    Transform joint = CreatePivot(parent, "Finger" + f + "_" + s, jointPosition);
                    Transform curl = CreatePivot(joint, "Curl", Vector3.zero);
                    curl.localRotation = Quaternion.Euler(_fingerCurl[s], 0f, 0f);
                    CreatePrimitive(MENU_CAPSULE, curl, "Bone", new Vector3(0f, length * 0.5f, 0f), new Vector3(0.018f, length * 0.5f + 0.004f, 0.018f), skin);
                    joints[f * segments + s] = joint;
                    curls[f * segments + s] = curl;
                    parent = curl;
                    jointPosition = new Vector3(0f, length, 0f);
                }
            }

            Transform thumb = CreatePivot(hand, "Thumb", new Vector3(-0.038f, 0.025f, 0.004f));
            thumb.localRotation = Quaternion.Euler(_thumbFist);
            CreatePrimitive(MENU_CAPSULE, thumb, "Bone", new Vector3(0f, 0.02f, 0f), new Vector3(0.022f, 0.024f, 0.022f), skin);
            Transform thumbTip = CreatePivot(thumb, "ThumbTip", new Vector3(0f, 0.04f, 0f));
            thumbTip.localRotation = Quaternion.Euler(_thumbTipFist);
            CreatePrimitive(MENU_CAPSULE, thumbTip, "Bone", new Vector3(0f, 0.015f, 0f), new Vector3(0.02f, 0.019f, 0.02f), skin);

            // 팔은 손목에서 어깨(눈 오른쪽 아래) 쪽으로 곧게 뻗는다. 손목 조금 뒤부터 제복 소매가 덮고, 소매 끝에 어두운 띠가 있다.
            Transform arm = CreatePivot(shakeY, "Arm", Vector3.zero);
            arm.localRotation = Quaternion.FromToRotation(Vector3.up, (_handShoulder - _handWrist).normalized);
            CreatePrimitive(MENU_CYLINDER, arm, "Forearm", new Vector3(0f, 0.03f, 0f), new Vector3(0.05f, 0.03f, 0.04f), skin);
            CreatePrimitive(MENU_CYLINDER, arm, "Cuff", new Vector3(0f, 0.07f, 0f), new Vector3(0.082f, 0.012f, 0.082f), EnsureMaterial("GB_Dark", 0.15f));
            CreatePrimitive(MENU_CYLINDER, arm, "Sleeve", new Vector3(0f, 0.315f, 0f), new Vector3(0.082f, 0.235f, 0.082f), EnsureMaterial("GB_Uniform", 0.48f));

            // 끝 자세에서 주먹 앞끝이 유리에서 HAND_FIST_GAP 앞에 오도록 손목이 물러날 거리를 잰다.
            float retreat = HAND_GLASS_DISTANCE - HAND_FIST_GAP - HandFrontDepth(hand, root);
            float holdEnd = HAND_TOUCH_DELAY + HAND_TOUCH_DURATION + HAND_HOLD;
            AnimationCurve smooth = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
            AnimationCurve fistCurve = FistCurve();

            // 0.8초까지는 지구만 보인다. 손은 아래에서 빠르게 올라와 천천히 멈추며 유리 앞에서 멈칫하고, 조심스럽게 닿는다.
            CuttoonTransformTween rise = AddTransformTween(panel, wrist, HAND_RISE_DELAY, HAND_RISE_DURATION);
            TweenLocalPosition(rise, _handWrist + _handRiseStart, wrist.localPosition);
            TweenLocalRotation(rise, _handRiseTilt, Vector3.zero);
            SetCurve(rise, new AnimationCurve(new Keyframe(0f, 0f, 3f, 3f), new Keyframe(1f, 1f, 0f, 0f)));
            CuttoonTransformTween reach = AddTransformTween(panel, touch, HAND_TOUCH_DELAY, HAND_TOUCH_DURATION);
            TweenLocalPosition(reach, Vector3.zero, touch.localPosition);
            SetCurve(reach, smooth);

            // 손가락은 닿으며 펴져 유리에 붙는다. 머묾이 끝나면 천천히 출발해 빨라지며 주먹을 쥔다.
            for (int i = 0; i < joints.Length; i++)
            {
                CuttoonTransformTween flatten = AddTransformTween(panel, joints[i], HAND_TOUCH_DELAY, HAND_FLATTEN_DURATION);
                TweenLocalRotation(flatten, new Vector3(_fingerRelaxed[i % segments], 0f, 0f), Vector3.zero);
                SetCurve(flatten, smooth);
                CuttoonTransformTween fold = AddTransformTween(panel, curls[i], holdEnd, HAND_FIST_DURATION);
                TweenLocalRotation(fold, Vector3.zero, new Vector3(_fingerCurl[i % segments], 0f, 0f));
                SetCurve(fold, fistCurve);
            }

            CuttoonTransformTween thumbFold = AddTransformTween(panel, thumb, holdEnd, HAND_FIST_DURATION);
            TweenLocalRotation(thumbFold, _thumbOpen, _thumbFist);
            SetCurve(thumbFold, fistCurve);
            CuttoonTransformTween thumbTipFold = AddTransformTween(panel, thumbTip, holdEnd, HAND_FIST_DURATION);
            TweenLocalRotation(thumbTipFold, Vector3.zero, _thumbTipFist);
            SetCurve(thumbTipFold, fistCurve);

            // 주먹을 쥐며 손등 윗부분이 유리 쪽으로 꺾이고 엄지 쪽이 보이게 돈다. 손은 유리를 따라 조금 내려가고, 꺾인 주먹이 유리에 닿도록 꺾임에 맞춰 물러난다.
            CuttoonTransformTween bend = AddTransformTween(panel, hand, holdEnd, HAND_FIST_DURATION);
            TweenLocalRotation(bend, Vector3.zero, new Vector3(HAND_WRIST_BEND, HAND_WRIST_TWIST, 0f));
            SetCurve(bend, fistCurve);
            CuttoonTransformTween press = AddTransformTween(panel, grip, holdEnd, HAND_FIST_DURATION);
            TweenLocalPosition(press, Vector3.zero, new Vector3(0f, -HAND_SLIDE, retreat));
            SetCurve(press, fistCurve);

            // 떨림은 머묾 끝에 아주 작게 새어 나와 주먹을 다 쥔 뒤 커진다. 두 축이 다른 물결로 떨고, 떨리는 만큼 손목이 조금 기운다.
            float trembleStart = holdEnd - HAND_LEAK_LEAD;
            float fistEnd = holdEnd + HAND_FIST_DURATION;
            CuttoonTransformTween trembleX = AddTransformTween(panel, shakeX, trembleStart, HAND_END - trembleStart);
            TweenLocalPosition(trembleX, Vector3.zero, Vector3.right * HAND_TREMBLE);
            TweenLocalRotation(trembleX, Vector3.zero, Vector3.right * (HAND_TREMBLE * HAND_TREMBLE_TILT_X));
            SetCurve(trembleX, TrembleCurve(_trembleWaveX, holdEnd, fistEnd));
            CuttoonTransformTween trembleY = AddTransformTween(panel, shakeY, trembleStart, HAND_END - trembleStart);
            TweenLocalPosition(trembleY, Vector3.zero, Vector3.up * HAND_TREMBLE);
            TweenLocalRotation(trembleY, Vector3.zero, Vector3.forward * (HAND_TREMBLE * HAND_TREMBLE_TILT_Z));
            SetCurve(trembleY, TrembleCurve(_trembleWaveY, holdEnd, fistEnd));

            // 끝 자세로 만들어 둔 손을 연출의 처음 자세로 돌린다. 겹마다 연출이 하나씩이라 순서와 상관없이 같은 자세가 된다.
            foreach (CuttoonEffect effect in panel.GetComponents<CuttoonEffect>())
            {
                effect.ResetEffect();
            }

            foreach (MeshRenderer meshRenderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            Camera camera = CreateSetCamera("Camera_4-2", root, Vector3.zero, Vector3.forward * 10f, HAND_FOV, Gray(0.06f), EnsureRenderTexture("4-2", viewSize));
            camera.nearClipPlane = HAND_NEAR;
            camera.farClipPlane = SHATTER_FAR;
            return camera;
        }

        /// <summary>
        /// 빈 자식 오브젝트 만들기 메뉴로 parent 아래 로컬 위치 localPosition에 회전 없는 빈 오브젝트 name을 만든다. 만든 Transform을 돌려준다.
        /// </summary>
        private static Transform CreatePivot(Transform parent, string name, Vector3 localPosition)
        {
            Transform pivot = CreateFromMenu(MENU_EMPTY_CHILD, parent, name).transform;
            pivot.localPosition = localPosition;
            pivot.localRotation = Quaternion.identity;
            return pivot;
        }

        /// <summary>
        /// 4-2 주먹 곡선을 만든다. 진행 u를 HAND_FIST_POWER제곱한 값을 부드럽게 출발하고 멈추는 3p² − 2p³에 넣어, 천천히 출발해 빨라지다가 꽉 쥐며 멈춘다.
        /// 21개 키에 값과 기울기를 넣은 곡선을 돌려준다.
        /// </summary>
        private static AnimationCurve FistCurve()
        {
            Keyframe[] keys = new Keyframe[21];
            for (int i = 0; i < keys.Length; i++)
            {
                float u = i / 20f;
                float p = Mathf.Pow(u, HAND_FIST_POWER);
                float slope = u > 0f ? 6f * p * (1f - p) * HAND_FIST_POWER * p / u : 0f;
                keys[i] = new Keyframe(u, p * p * (3f - 2f * p), slope, slope);
            }

            return new AnimationCurve(keys);
        }

        /// <summary>
        /// 4-2 손 떨림 한 축의 곡선을 만든다. 시각마다 떨림 폭(HAND_TREMBLE에 견준 비율)에 wave의 두 사인 물결(각속도 x·z, 시작 자리 y·w)을 0.6:0.4로 섞은 값을 곱한다.
        /// 떨림 폭은 머묾이 끝나는 holdEnd 전 HAND_LEAK_LEAD초 동안 HAND_LEAK_TREMBLE까지, 주먹을 다 쥔 fistEnd부터 HAND_TREMBLE_GROW초 동안 HAND_TREMBLE까지 부드럽게 커진다.
        /// 떨림 시작부터 HAND_END까지 HAND_TREMBLE_KEYS_PER_SECOND 간격으로 키를 넣고 키 사이를 곧게 이은 곡선을 돌려준다. 마지막 키는 0이라 연출이 끝나면 손이 제자리에 선다.
        /// </summary>
        private static AnimationCurve TrembleCurve(Vector4 wave, float holdEnd, float fistEnd)
        {
            float start = holdEnd - HAND_LEAK_LEAD;
            float duration = HAND_END - start;
            int count = Mathf.CeilToInt(duration * HAND_TREMBLE_KEYS_PER_SECOND);
            Keyframe[] keys = new Keyframe[count + 1];
            for (int i = 0; i < count; i++)
            {
                float seconds = start + i / HAND_TREMBLE_KEYS_PER_SECOND;
                float leak = Mathf.SmoothStep(0f, 1f, (seconds - start) / HAND_LEAK_LEAD) * HAND_LEAK_TREMBLE;
                float grow = Mathf.SmoothStep(0f, 1f, (seconds - fistEnd) / HAND_TREMBLE_GROW) * (HAND_TREMBLE - HAND_LEAK_TREMBLE);
                float ripple = Mathf.Sin(seconds * wave.x + wave.y) * 0.6f + Mathf.Sin(seconds * wave.z + wave.w) * 0.4f;
                keys[i] = new Keyframe((seconds - start) / duration, (leak + grow) / HAND_TREMBLE * ripple);
            }

            keys[count] = new Keyframe(1f, 0f);
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i].inTangent = i > 0 ? (keys[i].value - keys[i - 1].value) / (keys[i].time - keys[i - 1].time) : 0f;
                keys[i].outTangent = i < keys.Length - 1 ? (keys[i + 1].value - keys[i].value) / (keys[i + 1].time - keys[i].time) : 0f;
            }

            return new AnimationCurve(keys);
        }

        /// <summary>
        /// hand 아래 도형 중 유리 쪽(root 기준 +Z)으로 가장 앞선 끝의 깊이를 잰다. 캡슐은 양 끝 반구의 앞끝, 상자는 여덟 모서리로 잰다.
        /// root 기준 로컬 Z 값을 돌려준다.
        /// </summary>
        private static float HandFrontDepth(Transform hand, Transform root)
        {
            float front = float.MinValue;
            foreach (MeshFilter meshFilter in hand.GetComponentsInChildren<MeshFilter>())
            {
                Transform part = meshFilter.transform;
                if (meshFilter.sharedMesh.name.StartsWith("Capsule"))
                {
                    float radius = 0.5f * part.lossyScale.x;
                    Vector3 axis = part.up * Mathf.Max(0f, part.lossyScale.y - radius);
                    front = Mathf.Max(front, root.InverseTransformPoint(part.position + axis).z + radius, root.InverseTransformPoint(part.position - axis).z + radius);
                    continue;
                }

                for (int corner = 0; corner < 8; corner++)
                {
                    Vector3 local = new Vector3((corner & 1) == 0 ? -0.5f : 0.5f, (corner & 2) == 0 ? -0.5f : 0.5f, (corner & 4) == 0 ? -0.5f : 0.5f);
                    front = Mathf.Max(front, root.InverseTransformPoint(part.TransformPoint(local)).z);
                }
            }

            return front;
        }

        /// <summary>
        /// 4-3 주인공의 눈 3D 세트를 _heroSetPosition에 만들고, 칸 panel에 고인 눈물이 넘쳐 한 방울이 볼을 타고 흐르는 연출을 달아 칸 카메라를 돌려준다.
        /// 머리는 2-1과 같은 크기와 재질이고, 찍는 왼쪽 눈 하나와 코, 슬픈 눈썹만 둔다. 동공 앞에는 4-1이 끝난 모습의 부서진 지구가 작게 비친다.
        /// 세트는 얼굴이 해가 있는 쪽을 보게 돌리고, 카메라는 칸 크기 viewSize의 비율로 눈 테 너비가 칸 가로의 HERO_EYE_SHARE가 되는 거리에서 귀 쪽으로 HERO_CAMERA_YAW도 비켜 눈을 본다.
        /// </summary>
        private static Camera BuildHeroEyeSet(RectTransform panel, Vector2 viewSize)
        {
            Transform root = CreateFromMenu(MENU_EMPTY, null, "Set_HeroEye").transform;
            root.position = _heroSetPosition;
            root.rotation = Quaternion.Euler(0f, SunYaw() - 180f, 0f);
            Material skin = EnsureMaterial("GB_Skin", 0.8f);
            Material dark = EnsureMaterial("GB_Dark", 0.15f);

            Transform head = CreatePivot(root, "HeadPivot", _heroHeadPivot);
            Transform headSkin = CreatePrimitive(MENU_SPHERE, head, "Head", _heroHeadCenter, _heroHeadSize, skin);
            Transform nose = CreatePrimitive(MENU_SPHERE, head, "Nose", _heroNosePosition, _heroNoseSize, skin);
            Transform brow = CreatePrimitive(MENU_CUBE, head, "Brow", _heroBrowPosition, new Vector3(0.3f, 0.055f, 0.07f), dark);
            brow.localRotation = Quaternion.Euler(_heroBrowTilt);

            // 눈은 2-1 눈과 같은 짜임이다. 흰자와 테는 눈 뜬 정도(Opening) 아래, 홍채와 동공은 시선(Gaze) 아래에 둔다.
            // 카메라가 비켜 있으므로 홍채, 동공, 비친 지구를 모두 카메라와 눈 중심을 잇는 선 위에 두어, 화면에서 눈 가운데에 한 중심으로 겹쳐 보이게 한다.
            Vector3 cameraOffset = HeroCameraOffset(viewSize);
            Vector2 gazeTarget = Vector2.right * HERO_GAZE_SHIFT;
            Transform eye = CreatePivot(head, "EyeLeft", _heroEyePosition);
            Transform opening = CreatePivot(eye, "Opening", Vector3.zero);
            opening.localScale = new Vector3(1f, HERO_EYE_OPEN, 1f);
            Transform rim = CreatePrimitive(MENU_SPHERE, opening, "Rim", new Vector3(0f, 0f, HERO_RIM_DEPTH), _heroRimSize, dark);
            Transform white = CreatePrimitive(MENU_SPHERE, opening, "White", Vector3.zero, _heroWhiteSize, EnsureMaterial("GB_EyeWhite", 0.95f));
            Transform gaze = CreatePivot(eye, "Gaze", Vector3.zero);
            CreatePrimitive(MENU_SPHERE, gaze, "Iris", HeroSightPoint(cameraOffset, gazeTarget, HERO_IRIS_DEPTH), _heroIrisSize, EnsureMaterial("GB_Iris", 0.3f));
            CreatePrimitive(MENU_SPHERE, gaze, "Pupil", HeroSightPoint(cameraOffset, gazeTarget, HERO_PUPIL_DEPTH), _heroPupilSize, EnsureMaterial("GB_Pupil", 0.04f));
            // 비친 지구는 조각이 벌어진 지름(지구 반지름의 1.3배)이 HERO_REFLECTION_DIAMETER가 되게 줄이고, 충격점 쪽 면이 카메라를 본다.
            Transform reflection = CreateShatteredEarth(gaze, HeroSightPoint(cameraOffset, gazeTarget, HERO_REFLECTION_DEPTH), Quaternion.identity, null);
            reflection.localScale = Vector3.one * (HERO_REFLECTION_DIAMETER / (2f * LAUNCH_EARTH_RADIUS * 1.3f));

            // 고인 눈물은 아래 눈꺼풀을 따라 눈 겉면에 붙은 초승달 물과 그 윗면의 반짝이는 선, 반짝임 한 점이다. 모두 카메라에서 본 흰자 테두리 안에 들어오게 놓는다.
            // 흰자 테두리는 실제로 그려지는 구 메시(흰자, 머리, 코, 눈 테)의 삼각형에 카메라 광선을 쏘아 찾는다. 매끈한 타원체로 계산하면 흰자와 피부가 만나는 선이 화면과 어긋난다.
            List<Vector3> whiteTriangles = new List<Vector3>();
            AddEyeLocalTriangles(eye, white, whiteTriangles);
            List<Vector3> coverTriangles = new List<Vector3>();
            AddEyeLocalTriangles(eye, headSkin, coverTriangles);
            AddEyeLocalTriangles(eye, nose, coverTriangles);
            AddEyeLocalTriangles(eye, rim, coverTriangles);
            Vector2[] whiteEdge = HeroWhiteEdges(cameraOffset, whiteTriangles, coverTriangles);
            EnsureTearMeshes(cameraOffset, whiteEdge, out Mesh poolMesh, out Mesh shineMesh);
            Material sparkMaterial = EnsureTransparentMaterial("GB_TearSpark", _tearSparkColor, null, 4);
            Transform pool = CreatePrimitive(MENU_QUAD, eye, "TearPool", Vector3.zero, Vector3.one, EnsureTransparentMaterial("GB_TearPool", _tearPoolColor, null, 0));
            pool.GetComponent<MeshFilter>().sharedMesh = poolMesh;
            Transform shine = CreatePrimitive(MENU_QUAD, eye, "TearShine", Vector3.zero, Vector3.one, EnsureTransparentMaterial("GB_TearShine", _tearShineColor, null, 2));
            shine.GetComponent<MeshFilter>().sharedMesh = shineMesh;
            Vector2 sparkPoint = TearLidPoint(whiteEdge, 238f, out float sparkTaper) + new Vector2(0f, TEAR_POOL_THICKNESS * sparkTaper * 0.55f);
            CreatePrimitive(MENU_SPHERE, eye, "TearSpark", HeroOnSurface(cameraOffset, sparkPoint, point => TearSurfaceZ(point, cameraOffset) - 0.005f), new Vector3(0.009f, 0.005f, 0.002f), sparkMaterial);

            // 방울은 맺힘(Tear), 흘러내림(Slide), 볼 겉면을 따라가는 앞뒤(Depth), 늘어남(Bead)을 한 겹씩 맡는다.
            float slideDelay = TEAR_FORM_DELAY + TEAR_FORM_DURATION;
            AnimationCurve slideCurve = new AnimationCurve(new Keyframe(0f, 0f, 0f, 0f), new Keyframe(1f, 1f, TEAR_SLIDE_END_SLOPE, TEAR_SLIDE_END_SLOPE));
            // 방울이 맺히는 자리와 처진 자리는 카메라에서 본 아래 눈꺼풀 가장자리에 오게 피부 겉면 위로 옮긴다.
            Vector2 lidEdge = TearLidPoint(whiteEdge, TEAR_SPILL_ANGLE, out _) + Vector2.down * 0.004f;
            Vector2 spill = HeroOnSurface(cameraOffset, lidEdge, TearDropDepth);
            Vector2 formed = HeroOnSurface(cameraOffset, lidEdge + Vector2.down * TEAR_SAG, TearDropDepth);
            float formedDepth = TearDropDepth(formed);
            Transform tear = CreatePivot(eye, "Tear", new Vector3(spill.x, spill.y, formedDepth));
            Transform slide = CreatePivot(tear, "Slide", Vector3.zero);
            Transform depth = CreatePivot(slide, "Depth", Vector3.zero);
            Transform bead = CreatePrimitive(MENU_SPHERE, depth, "Bead", Vector3.zero, _tearDropSize, EnsureTransparentMaterial("GB_Tear", _tearDropColor, null, 3));
            CreatePrimitive(MENU_SPHERE, bead, "Spark", new Vector3(-0.22f, 0.25f, -0.5f), new Vector3(0.3f, 0.22f, 0.1f), sparkMaterial);

            // 방울은 눈꺼풀 가장자리에서 부풀며 맺히고, 처음엔 천천히 출발해 점점 빨리 볼을 타고 칸 밖으로 내려가며 조금 늘어난다.
            CuttoonTransformTween form = AddTransformTween(panel, tear, TEAR_FORM_DELAY, TEAR_FORM_DURATION);
            TweenLocalPosition(form, tear.localPosition, new Vector3(formed.x, formed.y, formedDepth));
            TweenLocalScale(form, Vector3.zero, Vector3.one);
            SetCurve(form, AnimationCurve.EaseInOut(0f, 0f, 1f, 1f));
            Vector2 slideOffset = new Vector2(-TEAR_DRIFT, -TEAR_FALL);
            CuttoonTransformTween fall = AddTransformTween(panel, slide, slideDelay, TEAR_SLIDE_DURATION);
            TweenLocalPosition(fall, Vector3.zero, slideOffset);
            SetCurve(fall, slideCurve);
            float depthEnd = TearDropDepth(formed + slideOffset) - formedDepth;
            CuttoonTransformTween follow = AddTransformTween(panel, depth, slideDelay, TEAR_SLIDE_DURATION);
            TweenLocalPosition(follow, Vector3.zero, Vector3.forward * depthEnd);
            SetCurve(follow, TearDepthCurve(formed, slideOffset, formedDepth, depthEnd));
            CuttoonTransformTween stretch = AddTransformTween(panel, bead, slideDelay, TEAR_SLIDE_DURATION);
            TweenLocalScale(stretch, _tearDropSize, _tearDropSize + Vector3.up * TEAR_STRETCH);
            SetCurve(stretch, slideCurve);

            // 젖은 자국은 넘친 자리에서 방울 위끝까지 이어진다. 조각마다 볼 겉면에 붙어 방울이 지나는 동안 위에서부터 자란다.
            Material trailMaterial = EnsureTransparentMaterial("GB_TearTrail", _tearTrailColor, null, 1);
            Vector3 previous = HeroOnSurface(cameraOffset, lidEdge, point => TearTrailPoint(point).z);
            for (int i = 0; i < TEAR_TRAIL_SEGMENTS; i++)
            {
                float from = (float)i / TEAR_TRAIL_SEGMENTS;
                float to = (float)(i + 1) / TEAR_TRAIL_SEGMENTS;
                Vector3 next = TearTrailPoint(formed + slideOffset * to + Vector2.up * 0.008f);
                Transform segment = CreatePivot(eye, "Trail" + i, previous);
                segment.localRotation = Quaternion.FromToRotation(Vector3.down, (next - previous).normalized);
                CreatePrimitive(MENU_QUAD, segment, "Streak", Vector3.down * 0.5f, Vector3.one, trailMaterial);
                float start = slideDelay + TearSlideTime(from) * TEAR_SLIDE_DURATION;
                CuttoonTransformTween grow = AddTransformTween(panel, segment, start, (TearSlideTime(to) - TearSlideTime(from)) * TEAR_SLIDE_DURATION);
                TweenLocalScale(grow, new Vector3(TEAR_TRAIL_WIDTH, 0f, 1f), new Vector3(TEAR_TRAIL_WIDTH, (next - previous).magnitude, 1f));
                SetCurve(grow, AnimationCurve.Linear(0f, 0f, 1f, 1f));
                previous = next;
            }

            // 다 만든 방울과 젖은 자국을 연출의 처음 상태로 둔다.
            foreach (CuttoonEffect effect in panel.GetComponents<CuttoonEffect>())
            {
                effect.ResetEffect();
            }

            foreach (MeshRenderer meshRenderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
            }

            Quaternion turn = Quaternion.Euler(0f, -HERO_CAMERA_YAW, 0f);
            Vector3 cameraPosition = _heroHeadPivot + _heroEyePosition + cameraOffset;
            Camera camera = CreateSetCamera("Camera_4-3", root, cameraPosition, cameraPosition + turn * Vector3.forward, HERO_FOV, Gray(0.14f), EnsureRenderTexture("4-3", viewSize));
            camera.nearClipPlane = HERO_NEAR;
            camera.farClipPlane = HERO_FAR;
            return camera;
        }

        /// <summary>
        /// 4-3 카메라 자리를 눈 기준으로 구한다. 칸 크기 viewSize의 비율로 눈 테 너비가 칸 가로의 HERO_EYE_SHARE가 되는 거리에서 귀 쪽으로 HERO_CAMERA_YAW도 돌고,
        /// 눈이 칸 세로 HERO_EYE_VIEW_Y 자리에 오게 조금 아래로 옮긴 자리를 돌려준다.
        /// </summary>
        private static Vector3 HeroCameraOffset(Vector2 viewSize)
        {
            float tanHalf = Mathf.Tan(HERO_FOV * 0.5f * Mathf.Deg2Rad);
            float viewHeight = _heroRimSize.x / HERO_EYE_SHARE / (viewSize.x / viewSize.y);
            return Quaternion.Euler(0f, -HERO_CAMERA_YAW, 0f) * new Vector3(0f, (0.5f - HERO_EYE_VIEW_Y) * viewHeight, -viewHeight * 0.5f / tanHalf);
        }

        /// <summary>
        /// 카메라 자리 cameraOffset(눈 기준)에서 눈 중심 평면(Z 0)의 점 target을 지나는 선 위에서 앞뒤 자리가 depth인 점을 돌려준다.
        /// 이 점은 카메라 화면에서 target과 같은 자리에 보인다.
        /// </summary>
        private static Vector3 HeroSightPoint(Vector3 cameraOffset, Vector2 target, float depth)
        {
            float t = (depth - cameraOffset.z) / -cameraOffset.z;
            return cameraOffset + ((Vector3)target - cameraOffset) * t;
        }

        /// <summary>
        /// 카메라 자리 cameraOffset에서 눈 중심 평면의 점 target을 지나는 선이 겉면 surfaceZ(눈 기준 점의 앞뒤 자리)와 만나는 점을 두 번 고쳐 구해 돌려준다.
        /// 겉면에 붙은 물체가 카메라 화면에서 target 자리에 보이게 할 때 쓴다.
        /// </summary>
        private static Vector3 HeroOnSurface(Vector3 cameraOffset, Vector2 target, System.Func<Vector2, float> surfaceZ)
        {
            Vector3 point = HeroSightPoint(cameraOffset, target, surfaceZ(target));
            point = HeroSightPoint(cameraOffset, target, surfaceZ(point));
            return HeroSightPoint(cameraOffset, target, surfaceZ(point));
        }

        /// <summary>
        /// 4-3 눈 기준 점 (x, y)에서 가장 앞(카메라 쪽, -Z)에 있는 겉면의 앞뒤 자리를 구한다. 머리, 눈 테, 흰자, 홍채, 동공, 비친 지구를 타원체로 본다.
        /// 눈 뜬 정도 HERO_EYE_OPEN, 눈 부품 크기, 홍채·동공·비친 지구를 놓는 카메라 자리 cameraOffset을 쓰고, 눈 기준 Z 값을 돌려준다. 어느 겉면에도 들지 않으면 1을 돌려준다.
        /// </summary>
        private static float HeroSurfaceZ(float x, float y, Vector3 cameraOffset)
        {
            Vector3 opened = new Vector3(0.5f, 0.5f * HERO_EYE_OPEN, 0.5f);
            float z = EllipsoidFrontZ(x, y, _heroHeadCenter - _heroEyePosition, _heroHeadSize * 0.5f);
            z = Mathf.Min(z, EllipsoidFrontZ(x, y, new Vector3(0f, 0f, HERO_RIM_DEPTH), Vector3.Scale(_heroRimSize, opened)));
            z = Mathf.Min(z, EllipsoidFrontZ(x, y, Vector3.zero, Vector3.Scale(_heroWhiteSize, opened)));
            Vector2 gazeTarget = Vector2.right * HERO_GAZE_SHIFT;
            z = Mathf.Min(z, EllipsoidFrontZ(x, y, HeroSightPoint(cameraOffset, gazeTarget, HERO_IRIS_DEPTH), _heroIrisSize * 0.5f));
            z = Mathf.Min(z, EllipsoidFrontZ(x, y, HeroSightPoint(cameraOffset, gazeTarget, HERO_PUPIL_DEPTH), _heroPupilSize * 0.5f));
            Vector3 reflectionCenter = HeroSightPoint(cameraOffset, gazeTarget, HERO_REFLECTION_DEPTH);
            float reflectionRadius = HERO_REFLECTION_DIAMETER * 0.6f;
            if ((x - reflectionCenter.x) * (x - reflectionCenter.x) + (y - reflectionCenter.y) * (y - reflectionCenter.y) < reflectionRadius * reflectionRadius)
            {
                z = Mathf.Min(z, HERO_REFLECTION_DEPTH - 0.003f);
            }

            return z;
        }

        /// <summary>
        /// 고인 눈물이 붙을 앞뒤 자리를 구한다. 점 point 둘레 TEAR_SURFACE_GAP 안 아홉 점의 눈 겉면(HeroSurfaceZ, 카메라 자리 cameraOffset) 중 가장 앞에서 TEAR_SURFACE_GAP만큼 앞이라,
        /// 홍채 가장자리처럼 가파르게 솟은 곳에서도 눈물 면이 그 뒤로 파고들지 않는다. 눈 기준 Z를 돌려준다.
        /// </summary>
        private static float TearSurfaceZ(Vector2 point, Vector3 cameraOffset)
        {
            float front = 1f;
            for (int i = -1; i <= 1; i++)
            {
                for (int j = -1; j <= 1; j++)
                {
                    front = Mathf.Min(front, HeroSurfaceZ(point.x + i * TEAR_SURFACE_GAP, point.y + j * TEAR_SURFACE_GAP, cameraOffset));
                }
            }

            return front - TEAR_SURFACE_GAP;
        }

        /// <summary>
        /// 중심 center, 반지름 radii인 타원체를 앞(-Z)에서 볼 때 점 (x, y)에 있는 앞면의 Z를 돌려준다. 점이 타원체 밖이면 1을 돌려준다.
        /// </summary>
        private static float EllipsoidFrontZ(float x, float y, Vector3 center, Vector3 radii)
        {
            float u = (x - center.x) / radii.x;
            float v = (y - center.y) / radii.y;
            float inside = 1f - u * u - v * v;
            return inside > 0f ? center.z - radii.z * Mathf.Sqrt(inside) : 1f;
        }

        /// <summary>
        /// 화면에 보이는 흰자 아래 테두리 whiteEdge(HeroWhiteEdges 결과)에서 흰자 타원 각도 angle(도, 180~360) 방향의 점(눈 중심 평면, 눈 기준)을 1도 사이를 이어 구해 돌려준다.
        /// 그 자리의 고인 눈물 두께 비율(가운데 1, TEAR_ARC_FROM·TEAR_ARC_TO 끝 0)을 taper로 내보낸다.
        /// </summary>
        private static Vector2 TearLidPoint(Vector2[] whiteEdge, float angle, out float taper)
        {
            float t = Mathf.Clamp01((angle - TEAR_ARC_FROM) / (TEAR_ARC_TO - TEAR_ARC_FROM));
            taper = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(Mathf.PI * t)), 0.7f);
            float index = Mathf.Clamp(angle - 180f, 0f, whiteEdge.Length - 1.001f);
            int lower = Mathf.FloorToInt(index);
            return Vector2.Lerp(whiteEdge[lower], whiteEdge[lower + 1], index - lower);
        }

        /// <summary>
        /// 카메라 자리 cameraOffset에서 보이는 흰자 아래 테두리를 180~360도까지 1도마다 구해 돌려준다. 흰자 타원의 각 방향으로 눈 중심에서 바깥으로 가며,
        /// 카메라 광선이 흰자 삼각형 white에 가리는 삼각형 covers(머리, 코, 눈 테)보다 먼저 닿는 마지막 자리를 반씩 좁혀 찾는다. 점은 눈 중심 평면의 눈 기준 자리다.
        /// </summary>
        private static Vector2[] HeroWhiteEdges(Vector3 cameraOffset, List<Vector3> white, List<Vector3> covers)
        {
            Vector2[] edge = new Vector2[181];
            for (int degree = 0; degree < edge.Length; degree++)
            {
                float radians = (180f + degree) * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(_heroWhiteSize.x * 0.5f * Mathf.Cos(radians), _heroWhiteSize.y * 0.5f * HERO_EYE_OPEN * Mathf.Sin(radians));
                float inside = 0f;
                float outside = 1.6f;
                for (int i = 0; i < 20; i++)
                {
                    float middle = (inside + outside) * 0.5f;
                    Vector3 ray = (Vector3)(direction * middle) - cameraOffset;
                    float whiteHit = RayTrianglesHit(cameraOffset, ray, white);
                    if (whiteHit < float.MaxValue && whiteHit < RayTrianglesHit(cameraOffset, ray, covers))
                    {
                        inside = middle;
                    }
                    else
                    {
                        outside = middle;
                    }
                }

                edge[degree] = direction * inside;
            }

            return edge;
        }

        /// <summary>
        /// 부품 part의 메시 삼각형 꼭짓점을 눈 eye 기준 자리로 바꿔 triangles에 세 개씩 더한다.
        /// </summary>
        private static void AddEyeLocalTriangles(Transform eye, Transform part, List<Vector3> triangles)
        {
            Mesh mesh = part.GetComponent<MeshFilter>().sharedMesh;
            Matrix4x4 toEye = eye.worldToLocalMatrix * part.localToWorldMatrix;
            Vector3[] vertices = mesh.vertices;
            foreach (int index in mesh.triangles)
            {
                triangles.Add(toEye.MultiplyPoint3x4(vertices[index]));
            }
        }

        /// <summary>
        /// 점 origin에서 방향 ray로 가는 광선이 삼각형 목록 triangles(세 점씩)에 처음 닿는 거리 비율(ray 길이를 1로 본 값)을 돌려준다. 닿지 않으면 float.MaxValue를 돌려준다.
        /// </summary>
        private static float RayTrianglesHit(Vector3 origin, Vector3 ray, List<Vector3> triangles)
        {
            float nearest = float.MaxValue;
            for (int i = 0; i < triangles.Count; i += 3)
            {
                Vector3 edgeA = triangles[i + 1] - triangles[i];
                Vector3 edgeB = triangles[i + 2] - triangles[i];
                Vector3 p = Vector3.Cross(ray, edgeB);
                float determinant = Vector3.Dot(edgeA, p);
                if (Mathf.Abs(determinant) < 1e-12f)
                {
                    continue;
                }

                Vector3 offset = origin - triangles[i];
                float u = Vector3.Dot(offset, p) / determinant;
                if (u < 0f || u > 1f)
                {
                    continue;
                }

                Vector3 q = Vector3.Cross(offset, edgeA);
                float v = Vector3.Dot(ray, q) / determinant;
                if (v < 0f || u + v > 1f)
                {
                    continue;
                }

                float hit = Vector3.Dot(edgeB, q) / determinant;
                if (hit > 0f && hit < nearest)
                {
                    nearest = hit;
                }
            }

            return nearest;
        }

        /// <summary>
        /// 고인 눈물 초승달과 윗면 반짝이는 선 메시를 카메라 자리 cameraOffset과 화면에 보이는 흰자 테두리 whiteEdge에 맞춰 만들고, MESH_FOLDER의 메시 파일에 써서 pool, shine으로 내보낸다.
        /// 만들 때마다 다시 쓰므로 카메라나 눈 값을 바꿔도 눈물이 새 구도를 따른다.
        /// </summary>
        private static void EnsureTearMeshes(Vector3 cameraOffset, Vector2[] whiteEdge, out Mesh pool, out Mesh shine)
        {
            EnsureFolder(MESH_FOLDER);
            pool = SaveMeshAsset(BuildTearPoolMesh(cameraOffset, whiteEdge), TEAR_POOL_MESH_PATH);
            shine = SaveMeshAsset(BuildTearShineMesh(cameraOffset, whiteEdge), TEAR_SHINE_MESH_PATH);
        }

        /// <summary>
        /// 메시 mesh를 path에 저장하고 저장된 메시를 돌려준다. 파일이 이미 있으면 그 메시의 꼭짓점, 법선, 삼각형을 mesh 것으로 바꿔 써서 씬이 가리키는 GUID를 유지한다.
        /// </summary>
        private static Mesh SaveMeshAsset(Mesh mesh, string path)
        {
            Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (saved == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            saved.Clear();
            saved.vertices = mesh.vertices;
            saved.normals = mesh.normals;
            saved.triangles = mesh.triangles;
            saved.RecalculateBounds();
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(saved);
            AssetDatabase.SaveAssetIfDirty(saved);
            return saved;
        }

        /// <summary>
        /// 고인 눈물 초승달 메시를 만든다. 화면에 보이는 흰자 아래 테두리 whiteEdge를 따라 아래로는 눈 테를 TEAR_LID_COVER만큼, 위로는 흰자를 TEAR_POOL_THICKNESS만큼 덮고(호 가운데 최대, 양 끝 0),
        /// 각 점은 카메라 자리 cameraOffset에서 그 자리에 보이도록 눈 겉면(TEAR_SURFACE_GAP 앞) 위로 옮기고, 가운데가 조금 더 부푼다. 만든 메시를 돌려준다.
        /// </summary>
        private static Mesh BuildTearPoolMesh(Vector3 cameraOffset, Vector2[] whiteEdge)
        {
            const int COLUMNS = 60;
            const int ROWS = 6;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int i = 0; i <= COLUMNS; i++)
            {
                Vector2 lid = TearLidPoint(whiteEdge, Mathf.Lerp(TEAR_ARC_FROM, TEAR_ARC_TO, (float)i / COLUMNS), out float taper);
                for (int j = 0; j <= ROWS; j++)
                {
                    float v = (float)j / ROWS;
                    Vector2 point = lid + Vector2.up * ((-TEAR_LID_COVER + (TEAR_POOL_THICKNESS + TEAR_LID_COVER) * v) * taper);
                    float bulge = 0.004f * Mathf.Sin(Mathf.PI * v) * taper;
                    vertices.Add(HeroOnSurface(cameraOffset, point, onEye => TearSurfaceZ(onEye, cameraOffset) - bulge));
                }
            }

            for (int i = 0; i < COLUMNS; i++)
            {
                for (int j = 0; j < ROWS; j++)
                {
                    int a = i * (ROWS + 1) + j;
                    int c = a + ROWS + 1;
                    triangles.AddRange(new[] { a, a + 1, c, a + 1, c + 1, c });
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "GB_TearPool";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 고인 눈물 윗면의 반짝이는 선 메시를 만든다. 초승달 윗선을 따라 양 끝 TEAR_SHINE_MARGIN도를 빼고 이어지며, 가운데가 TEAR_SHINE_WIDTH로 가장 굵다. 화면에 보이는 흰자 테두리 whiteEdge로 정한 초승달 윗선이 카메라 자리 cameraOffset에서 그 자리에 보이도록 눈 겉면 위로 옮긴다. 만든 메시를 돌려준다.
        /// </summary>
        private static Mesh BuildTearShineMesh(Vector3 cameraOffset, Vector2[] whiteEdge)
        {
            const int COLUMNS = 40;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int i = 0; i <= COLUMNS; i++)
            {
                Vector2 lid = TearLidPoint(whiteEdge, Mathf.Lerp(TEAR_ARC_FROM + TEAR_SHINE_MARGIN, TEAR_ARC_TO - TEAR_SHINE_MARGIN, (float)i / COLUMNS), out float taper);
                Vector2 point = lid + Vector2.up * (TEAR_POOL_THICKNESS * taper);
                float halfWidth = TEAR_SHINE_WIDTH * 0.5f * Mathf.Sin(Mathf.PI * i / COLUMNS);
                Vector3 onEye = HeroOnSurface(cameraOffset, point, surface => TearSurfaceZ(surface, cameraOffset) - 0.003f);
                vertices.Add(onEye + Vector3.down * halfWidth);
                vertices.Add(onEye + Vector3.up * halfWidth);
            }

            for (int i = 0; i < COLUMNS; i++)
            {
                int a = i * 2;
                triangles.AddRange(new[] { a, a + 1, a + 2, a + 2, a + 1, a + 3 });
            }

            Mesh mesh = new Mesh();
            mesh.name = "GB_TearShine";
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 4-3 눈 기준 점 point에 있는 피부 겉면의 앞뒤 자리를 구한다. 볼(머리 겉면)과, 그 자리에 눈 테가 있으면 눈 테 앞면 중 앞쪽이다. 눈 기준 Z를 돌려준다.
        /// </summary>
        private static float HeroSkinZ(Vector2 point)
        {
            float cheek = EllipsoidFrontZ(point.x, point.y, _heroHeadCenter - _heroEyePosition, _heroHeadSize * 0.5f);
            float rim = EllipsoidFrontZ(point.x, point.y, new Vector3(0f, 0f, HERO_RIM_DEPTH), Vector3.Scale(_heroRimSize, new Vector3(0.5f, 0.5f * HERO_EYE_OPEN, 0.5f)));
            return Mathf.Min(cheek, rim);
        }

        /// <summary>
        /// 눈 기준 점 point에 있는 방울 중심의 앞뒤 자리를 돌려준다. 피부 겉면(HeroSkinZ)에서 TEAR_DROP_LIFT만큼 앞이라 방울이 볼을 따라간다.
        /// </summary>
        private static float TearDropDepth(Vector2 point)
        {
            return HeroSkinZ(point) - TEAR_DROP_LIFT;
        }

        /// <summary>
        /// 젖은 자국이 지나는 눈 기준 점 point를 피부 겉면(HeroSkinZ)에서 TEAR_TRAIL_LIFT만큼 앞에 놓은 3D 점으로 돌려준다.
        /// </summary>
        private static Vector3 TearTrailPoint(Vector2 point)
        {
            return new Vector3(point.x, point.y, HeroSkinZ(point) - TEAR_TRAIL_LIFT);
        }

        /// <summary>
        /// 흘러내림 진행 0.6u² + 0.4u³이 progress가 되는 시간 비율 u(0~1)를 반으로 나눠 찾는 방식으로 구해 돌려준다.
        /// </summary>
        private static float TearSlideTime(float progress)
        {
            float low = 0f;
            float high = 1f;
            for (int i = 0; i < 30; i++)
            {
                float middle = (low + high) * 0.5f;
                if (middle * middle * (0.6f + 0.4f * middle) < progress)
                {
                    low = middle;
                }
                else
                {
                    high = middle;
                }
            }

            return (low + high) * 0.5f;
        }

        /// <summary>
        /// 방울 앞뒤(Depth) 연출의 곡선을 만든다. 방울이 formed에서 offset만큼 흘러내리는 동안 볼 겉면을 따라가도록, 시간마다 TearDropDepth에서 처음 자리 formedDepth를 뺀 값을
        /// 끝 값 depthEnd로 나눠 21개 키로 넣고 접선을 부드럽게 한 곡선을 돌려준다.
        /// </summary>
        private static AnimationCurve TearDepthCurve(Vector2 formed, Vector2 offset, float formedDepth, float depthEnd)
        {
            Keyframe[] keys = new Keyframe[21];
            for (int i = 0; i < keys.Length; i++)
            {
                float u = i / 20f;
                float progress = u * u * (0.6f + 0.4f * u);
                keys[i] = new Keyframe(u, (TearDropDepth(formed + offset * progress) - formedDepth) / depthEnd);
            }

            AnimationCurve curve = new AnimationCurve(keys);
            for (int i = 0; i < keys.Length; i++)
            {
                curve.SmoothTangents(i, 0f);
            }

            return curve;
        }

        /// <summary>
        /// 4-1 조각 메시 SHATTER_PIECES개를 SHATTER_MESH_PREFIX 경로에서 찾아 돌려준다. 하나라도 없으면 모두 새로 만들어 저장한다.
        /// 충격 방향 impact와 그 둘레 방위각 기준 axisU, axisV로 촘촘한 구를 나눈다. 값을 바꾸면 저장된 메시를 지워야 다시 만든다.
        /// </summary>
        private static Mesh[] EnsureShatterPieceMeshes(Vector3 impact, Vector3 axisU, Vector3 axisV)
        {
            EnsureFolder(MESH_FOLDER);
            Mesh[] meshes = new Mesh[SHATTER_PIECES];
            bool complete = true;
            for (int i = 0; i < SHATTER_PIECES; i++)
            {
                meshes[i] = AssetDatabase.LoadAssetAtPath<Mesh>(SHATTER_MESH_PREFIX + i + ".asset");
                complete &= meshes[i] != null;
            }

            if (complete)
            {
                return meshes;
            }

            Mesh sphere = EnsureSphereMesh();
            Vector3[] vertices = sphere.vertices;
            int[] triangles = sphere.triangles;
            int triangleCount = triangles.Length / 3;
            int[] owner = new int[triangleCount];
            for (int t = 0; t < triangleCount; t++)
            {
                Vector3 center = (vertices[triangles[t * 3]] + vertices[triangles[t * 3 + 1]] + vertices[triangles[t * 3 + 2]]).normalized;
                owner[t] = ShatterPieceOf(center, impact, axisU, axisV);
            }

            // 모서리마다 붙은 삼각형의 조각 번호를 모아, 다른 조각과 맞닿은 모서리(금)를 찾는다. 구 이음매의 겹친 꼭짓점은 위치로 같은 점으로 본다.
            Dictionary<(Vector3Int, Vector3Int), List<int>> edgeOwners = new Dictionary<(Vector3Int, Vector3Int), List<int>>();
            for (int t = 0; t < triangleCount; t++)
            {
                for (int e = 0; e < 3; e++)
                {
                    Vector3Int a = VertexKey(vertices[triangles[t * 3 + e]]);
                    Vector3Int b = VertexKey(vertices[triangles[t * 3 + (e + 1) % 3]]);
                    if (a == b)
                    {
                        continue;
                    }

                    (Vector3Int, Vector3Int) key = EdgeKey(a, b);
                    if (!edgeOwners.TryGetValue(key, out List<int> owners))
                    {
                        owners = new List<int>();
                        edgeOwners[key] = owners;
                    }

                    owners.Add(owner[t]);
                }
            }

            for (int i = 0; i < SHATTER_PIECES; i++)
            {
                if (meshes[i] != null)
                {
                    AssetDatabase.DeleteAsset(SHATTER_MESH_PREFIX + i + ".asset");
                }

                meshes[i] = BuildShatterPieceMesh(i, vertices, triangles, owner, edgeOwners, impact);
                AssetDatabase.CreateAsset(meshes[i], SHATTER_MESH_PREFIX + i + ".asset");
            }

            return meshes;
        }

        /// <summary>
        /// 조각 piece의 메시를 만든다. 구 꼭짓점 vertices, 삼각형 triangles, 삼각형별 조각 owner, 모서리별 조각 목록 edgeOwners, 충격 방향 impact를 쓴다.
        /// 하위 메시 0은 겉면, 1은 구덩이, 2는 불길, 3은 잘린 면과 외핵을 향한 안쪽 면(달아오른 맨틀)이다. 만든 Mesh를 돌려준다.
        /// </summary>
        private static Mesh BuildShatterPieceMesh(int piece, Vector3[] vertices, int[] triangles, int[] owner, Dictionary<(Vector3Int, Vector3Int), List<int>> edgeOwners, Vector3 impact)
        {
            List<Vector3> positions = new List<Vector3>();
            List<Vector3> normals = new List<Vector3>();
            List<int>[] subMeshes = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
            Dictionary<int, int> outerIndex = new Dictionary<int, int>();
            Dictionary<int, int> innerIndex = new Dictionary<int, int>();
            Vector3 firstNormal = Vector3.Cross(vertices[triangles[1]] - vertices[triangles[0]], vertices[triangles[2]] - vertices[triangles[0]]);
            // 구 메시는 바깥을 앞면으로 둔다. 이 부호로 새 면의 감김 방향을 맞춘다.
            float winding = Mathf.Sign(Vector3.Dot(firstNormal, vertices[triangles[0]] + vertices[triangles[1]] + vertices[triangles[2]]));
            float coreRadius = LAUNCH_EARTH_RADIUS * SHATTER_CORE_RATIO;
            for (int t = 0; t < owner.Length; t++)
            {
                if (owner[t] != piece)
                {
                    continue;
                }

                int ia = triangles[t * 3];
                int ib = triangles[t * 3 + 1];
                int ic = triangles[t * 3 + 2];
                float angle = Vector3.Angle((vertices[ia] + vertices[ib] + vertices[ic]).normalized, impact);
                int surface = angle < SHATTER_CRATER_ANGLE ? 1 : angle < BURN_END_ANGLE ? 2 : 0;
                foreach (int index in new[] { ia, ib, ic })
                {
                    if (!outerIndex.TryGetValue(index, out int outer))
                    {
                        outer = positions.Count;
                        outerIndex[index] = outer;
                        positions.Add(ShatterSurface(vertices[index], impact));
                        normals.Add(vertices[index].normalized);
                    }

                    subMeshes[surface].Add(outer);
                }

                // 안쪽 면은 외핵 바깥 반지름에서 가운데를 향하도록 감김을 뒤집는다.
                foreach (int index in new[] { ia, ic, ib })
                {
                    if (!innerIndex.TryGetValue(index, out int inner))
                    {
                        inner = positions.Count;
                        innerIndex[index] = inner;
                        positions.Add(vertices[index].normalized * coreRadius);
                        normals.Add(-vertices[index].normalized);
                    }

                    subMeshes[3].Add(inner);
                }

                for (int e = 0; e < 3; e++)
                {
                    Vector3 pa = vertices[triangles[t * 3 + e]];
                    Vector3 pb = vertices[triangles[t * 3 + (e + 1) % 3]];
                    Vector3 pc = vertices[triangles[t * 3 + (e + 2) % 3]];
                    Vector3Int ka = VertexKey(pa);
                    Vector3Int kb = VertexKey(pb);
                    if (ka == kb || !edgeOwners[EdgeKey(ka, kb)].Exists(other => other != piece))
                    {
                        continue;
                    }

                    // 잘린 면은 겉면 모서리에서 외핵 바깥까지 내려가는 사각형이고, 조각 안쪽(pc 쪽)의 반대를 앞면으로 둔다.
                    Vector3 sa = ShatterSurface(pa, impact);
                    Vector3 sb = ShatterSurface(pb, impact);
                    Vector3 da = pa.normalized * coreRadius;
                    Vector3 db = pb.normalized * coreRadius;
                    bool flip = Mathf.Sign(Vector3.Dot(Vector3.Cross(sb - sa, db - sa), sa - ShatterSurface(pc, impact))) != winding;
                    Vector3[] quad = flip ? new[] { sb, sa, da, db } : new[] { sa, sb, db, da };
                    Vector3 faceNormal = Vector3.Cross(quad[1] - quad[0], quad[2] - quad[0]).normalized * winding;
                    foreach (int corner in new[] { 0, 1, 2, 0, 2, 3 })
                    {
                        subMeshes[3].Add(positions.Count);
                        positions.Add(quad[corner]);
                        normals.Add(faceNormal);
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "GB_ShatterPiece" + piece;
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(positions);
            mesh.SetNormals(normals);
            mesh.subMeshCount = subMeshes.Length;
            for (int s = 0; s < subMeshes.Length; s++)
            {
                mesh.SetTriangles(subMeshes[s], s);
            }

            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// 단위 방향 direction이 속한 4-1 조각 번호를 돌려준다. 충격 방향 impact 둘레의 방위각(axisU, axisV 기준)을 SHATTER_PIECES칸으로 나누고,
        /// 충격점에서 떨어진 각도에 따라 경계를 흔들어 금이 구불구불하게 한다.
        /// </summary>
        private static int ShatterPieceOf(Vector3 direction, Vector3 impact, Vector3 axisU, Vector3 axisV)
        {
            float polar = Vector3.Angle(direction, impact) * Mathf.Deg2Rad;
            float azimuth = Mathf.Atan2(Vector3.Dot(direction, axisV), Vector3.Dot(direction, axisU));
            float wavy = azimuth + 0.1f * Mathf.Sin(3f * polar + 2f * azimuth) + 0.06f * Mathf.Sin(7f * polar - azimuth);
            float step = 2f * Mathf.PI / SHATTER_PIECES;
            return Mathf.FloorToInt(Mathf.Repeat(wavy, 2f * Mathf.PI) / step) % SHATTER_PIECES;
        }

        /// <summary>
        /// 지름 1인 구의 꼭짓점 vertex를 지구 겉면 위치로 바꿔 돌려준다. 충격 방향 impact에서 SHATTER_CRATER_ANGLE도 안은 안쪽으로 파인 구덩이다.
        /// </summary>
        private static Vector3 ShatterSurface(Vector3 vertex, Vector3 impact)
        {
            Vector3 direction = vertex.normalized;
            float ratio = Vector3.Angle(direction, impact) / SHATTER_CRATER_ANGLE;
            float depth = ratio < 1f ? SHATTER_CRATER_DEPTH * (1f - ratio * ratio) : 0f;
            return direction * LAUNCH_EARTH_RADIUS * (1f - depth);
        }

        /// <summary>
        /// 꼭짓점 위치 position을 반올림한 정수 좌표로 바꿔 돌려준다. 구 이음매에서 겹친 꼭짓점을 같은 점으로 보기 위해 쓴다.
        /// </summary>
        private static Vector3Int VertexKey(Vector3 position)
        {
            return new Vector3Int(Mathf.RoundToInt(position.x * 20000f), Mathf.RoundToInt(position.y * 20000f), Mathf.RoundToInt(position.z * 20000f));
        }

        /// <summary>
        /// 두 꼭짓점 좌표 a, b를 순서와 관계없이 같은 모서리 열쇠로 묶어 돌려준다.
        /// </summary>
        private static (Vector3Int, Vector3Int) EdgeKey(Vector3Int a, Vector3Int b)
        {
            bool aFirst = a.x < b.x || (a.x == b.x && (a.y < b.y || (a.y == b.y && a.z < b.z)));
            return aFirst ? (a, b) : (b, a);
        }

        /// <summary>
        /// 3쪽(3-1 충돌, 3-2 멸망과 출발, 3-3 뒤쫓는 충격파)을 pagesRoot 아래에 깔고 쪽 컴포넌트를 돌려준다.
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
            // 3D 지구와 로켓 세트를 칸 카메라로 찍어 보여 준다. 운석이 떨어진 한 점에서 불길과 충격파가 번지고, 지구에서 솟은 로켓이 거의 정면으로 다가온다.
            Transform launchSet = BuildLaunchSet(departureSize, out Transform burn, out Vector3 burnEnd, out Transform shockRing, out Transform rocket, out Vector3 rocketEnd, out Transform rocketBody, out Transform flameOuter, out Transform flameCore);
            Camera launchCamera = CreateSetCamera("Camera_3-2", launchSet, Vector3.zero, Vector3.forward * 10f, LAUNCH_FOV, Gray(0.06f), EnsureRenderTexture("3-2", departureSize));
            launchCamera.farClipPlane = LAUNCH_FAR;
            CreateView(departure, launchCamera);
            CuttoonTransformTween spread = AddTransformTween(departure, burn, 0f, BURN_DURATION);
            TweenLocalPosition(spread, burn.localPosition, burnEnd);
            SetCurve(spread, BurnCurve());
            // 충격파 테두리는 불길과 함께 생겨 불길보다 앞서 퍼지고, 끝 무렵 지구 가장자리 밖으로 부풀어 나와 3-3으로 이어진다.
            CuttoonTransformTween ringGrow = AddTransformTween(departure, shockRing, 0f, SHOCK_RING_DURATION);
            TweenLocalScale(ringGrow, shockRing.localScale, Vector3.one * SHOCK_RING_END_RADIUS * 2f);
            SetCurve(ringGrow, ShockRingCurve());
            CuttoonTransformTween liftOff = AddTransformTween(departure, rocket, ROCKET_DELAY, ROCKET_DURATION);
            TweenLocalPosition(liftOff, rocket.localPosition, rocketEnd);
            SetCurve(liftOff, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            CuttoonTransformTween roll = AddTransformTween(departure, rocketBody, ROCKET_DELAY, ROCKET_DURATION);
            TweenLocalRotation(roll, Vector3.zero, new Vector3(0f, 0f, ROCKET_ROLL));
            SetCurve(roll, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            CuttoonFlap flicker = departure.gameObject.AddComponent<CuttoonFlap>();
            SetRef(flicker, "_leftWing", flameOuter);
            SetRef(flicker, "_rightWing", flameCore);
            SetFloat(flicker, "_delay", ROCKET_DELAY);
            SetFloat(flicker, "_duration", ROCKET_DURATION);
            SetFloat(flicker, "_frequency", FLAME_FLICKER_CYCLES / ROCKET_DURATION);
            SetFloat(flicker, "_amplitude", FLAME_FLICKER_AMPLITUDE);
            CreateLabel(departure, departureSize, "3-2", "멸망과 출발", "운석이 떨어진 자리에서 불길과 충격파가 번지고, 지구에서 솟은 로켓이 정면으로 다가온다");
            panels.Add(FinishPanel(departure));

            // 3-3 뒤쫓는 충격파
            RectTransform voyage = CreatePanel(page, "3-3", 0.7f, 0f, 1f, 0.8f, Gray(0.06f), out Vector2 voyageSize);
            // 3D 세트를 옆 아래에서 비스듬히 올려다보는 칸 카메라로 찍는다. 로켓이 점점 빨리 달아나고, 충돌에서 퍼진 충격파와 파편이 뒤를 쫓다가 뒤처진다.
            Transform shockSet = BuildShockSet(out Transform shockRocket, out Transform shockRocketBody, out Transform shockFlameOuter, out Transform shockFlameCore, out Transform wave, out Transform[] debris, out Transform[] debrisSpins);
            Camera shockCamera = CreateSetCamera("Camera_3-3", shockSet, ShockCameraPosition(), new Vector3(0f, SHOCK_LOOK_START, 0f), SHOCK_FOV, Gray(0.06f), EnsureRenderTexture("3-3", voyageSize));
            shockCamera.farClipPlane = SHOCK_FAR;
            shockCamera.transform.localRotation = ShockCameraRotation(0f);
            CreateView(voyage, shockCamera);
            float progressEnd = SHOCK_DURATION / SHOCK_PROGRESS_SECONDS;
            float waveGrowth = ShockWaveRadius(progressEnd) - ShockWaveRadius(0f);
            AnimationCurve waveCurve = QuadraticCurve(SHOCK_WAVE_SPEED * progressEnd / waveGrowth);
            CuttoonTransformTween flee = AddTransformTween(voyage, shockRocket, 0f, SHOCK_DURATION);
            TweenLocalPosition(flee, shockRocket.localPosition, new Vector3(0f, SHOCK_ROCKET_START + SHOCK_ROCKET_RUN * progressEnd * progressEnd, 0f));
            SetCurve(flee, QuadraticCurve(0f));
            CuttoonTransformTween shockRoll = AddTransformTween(voyage, shockRocketBody, 0f, SHOCK_DURATION);
            TweenLocalRotation(shockRoll, Vector3.zero, new Vector3(0f, 0f, SHOCK_ROCKET_ROLL * progressEnd));
            SetCurve(shockRoll, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            CuttoonFlap shockFlicker = voyage.gameObject.AddComponent<CuttoonFlap>();
            SetRef(shockFlicker, "_leftWing", shockFlameOuter);
            SetRef(shockFlicker, "_rightWing", shockFlameCore);
            SetFloat(shockFlicker, "_duration", SHOCK_DURATION);
            // 3-2와 같은 빠르기로 떨되, 떨림 수를 정수로 맞춰 끝에 불꽃이 곧게 선다.
            SetFloat(shockFlicker, "_frequency", Mathf.Round(FLAME_FLICKER_CYCLES / ROCKET_DURATION * SHOCK_DURATION) / SHOCK_DURATION);
            SetFloat(shockFlicker, "_amplitude", FLAME_FLICKER_AMPLITUDE);
            CuttoonTransformTween grow = AddTransformTween(voyage, wave, 0f, SHOCK_DURATION);
            TweenLocalScale(grow, wave.localScale, Vector3.one * ShockWaveRadius(progressEnd) * 2f);
            SetCurve(grow, waveCurve);
            CuttoonColorTween thin = AddColorTween(voyage, wave.GetComponent<Renderer>(), 0f, SHOCK_DURATION);
            TweenColor(thin, _shockColor, new Color(_shockColor.r, _shockColor.g, _shockColor.b, 1f - SHOCK_WAVE_FADE * progressEnd));
            SetCurve(thin, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            for (int i = 0; i < debris.Length; i++)
            {
                CuttoonTransformTween fly = AddTransformTween(voyage, debris[i], 0f, SHOCK_DURATION);
                TweenLocalPosition(fly, debris[i].localPosition, debris[i].localPosition * (ShockWaveRadius(progressEnd) / ShockWaveRadius(0f)));
                SetCurve(fly, waveCurve);
                CuttoonTransformTween spin = AddTransformTween(voyage, debrisSpins[i], 0f, SHOCK_DURATION);
                TweenLocalRotation(spin, Vector3.zero, new Vector3(0f, 0f, _debrisSpins[i] * SHOCK_DURATION));
                SetCurve(spin, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            }

            // 카메라는 진행도 1(SHOCK_PROGRESS_SECONDS초)까지만 고개를 돌리고 멈춰, 로켓이 칸 오른쪽 위 끝으로 빠져나가게 한다.
            // 회전은 오일러 각으로 보간되므로, 각 축이 가까운 쪽으로 돌도록 끝 각도를 시작 각도에 맞춘다.
            Vector3 lookFrom = ShockCameraRotation(0f).eulerAngles;
            Vector3 lookTo = ShockCameraRotation(1f).eulerAngles;
            lookTo = lookFrom + new Vector3(Mathf.DeltaAngle(lookFrom.x, lookTo.x), Mathf.DeltaAngle(lookFrom.y, lookTo.y), Mathf.DeltaAngle(lookFrom.z, lookTo.z));
            CuttoonTransformTween pan = AddTransformTween(voyage, shockCamera.transform, 0f, SHOCK_PROGRESS_SECONDS);
            TweenLocalRotation(pan, lookFrom, lookTo);
            SetCurve(pan, AnimationCurve.Linear(0f, 0f, 1f, 1f));
            CreateLabel(voyage, voyageSize, "3-3", "뒤쫓는 충격파", "충돌에서 퍼진 충격파와 파편이 로켓 뒤를 쫓다가, 점점 빨라지는 로켓에게 뒤처진다");
            panels.Add(FinishPanel(voyage));

            return FinishPage(page, panels, 0f);
        }

        /// <summary>
        /// 4쪽(4-1 창 너머 지구, 4-2 유리에 댄 손, 4-3 주인공의 눈)을 pagesRoot 아래에 깔고 쪽 컴포넌트를 돌려준다.
        /// </summary>
        private static CuttoonPage BuildPage4(RectTransform pagesRoot)
        {
            RectTransform page = CreatePage(pagesRoot, "Page4");
            List<CuttoonPanel> panels = new List<CuttoonPanel>();

            // 4-1 창 너머 지구
            RectTransform window = CreatePanel(page, "4-1", 0f, 0.45f, 1f, 1f, Gray(0.06f), out Vector2 windowSize);
            // 주인공 시야로 유리 바로 앞에서 본 3D 지구를 칸 카메라로 찍는다. 구덩이에서 방사형으로 갈라진 조각이 벌어지며 흩어지고, 틈으로 외핵이 드러난다.
            CreateView(window, BuildShatterSet(window, windowSize));
            CreateLabel(window, windowSize, "4-1", "창 너머 지구", "유리 너머 지구가 구덩이에서 방사형으로 갈라져 흩어지고, 틈으로 빛나는 외핵이 흘러나온다");
            panels.Add(FinishPanel(window));

            // 4-2 유리에 댄 손
            RectTransform handPanel = CreatePanel(page, "4-2", 0f, 0f, 0.62f, 0.45f, Gray(0.06f), out Vector2 handSize);
            // 4-1과 같은 주인공 시야로, 오른손이 아래에서 올라와 부서진 지구 옆 유리에 닿았다가 주먹을 쥐고 떠는 3D 세트를 칸 카메라로 찍는다.
            CreateView(handPanel, BuildHandSet(handPanel, handSize));
            CreateLabel(handPanel, handSize, "4-2", "유리에 댄 손", "유리에 손바닥을 댔다가 손목이 꺾이며 주먹을 꽉 쥐고 떤다");
            panels.Add(FinishPanel(handPanel));

            // 4-3 주인공의 눈
            RectTransform heroEye = CreatePanel(page, "4-3", 0.62f, 0f, 1f, 0.45f, Gray(0.14f), out Vector2 heroEyeSize);
            // 눈물이 가득 고인 주인공의 왼쪽 눈을 귀 쪽으로 조금 비켜 찍는다. 동공에 부서진 지구가 비치고, 고인 눈물이 넘쳐 한 방울이 볼을 타고 흐른다.
            CreateView(heroEye, BuildHeroEyeSet(heroEye, heroEyeSize));
            CreateLabel(heroEye, heroEyeSize, "4-3", "주인공의 눈", "고인 눈물이 넘쳐 흐르고, 동공에 부서진 지구");
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

            // 2-2 운석 둘레의 달아오른 공기 그림을 더했다.
            if (!File.Exists(GLOW_PATH))
            {
                WriteShapeTexture(GLOW_PATH, GlowCoverage);
            }

            // 띠 가운데 구름을 향한 뜨거운 쪽 그림을 더했다.
            if (!File.Exists(GLOW_HOT_PATH))
            {
                WriteShapeTexture(GLOW_HOT_PATH, GlowHotCoverage);
            }

            if (!File.Exists(SHEEN_PATH))
            {
                WriteShapeTexture(SHEEN_PATH, SheenCoverage);
            }
        }

        /// <summary>
        /// size 크기 그림에서 점 (x, y)의 유리 반사 줄 진하기를 돌려준다.
        /// 왼쪽 위에서 오른쪽 아래로 비스듬한 넓은 띠와 좁은 띠에 아주 옅은 바탕을 더한 값이며, 재질 색의 알파와 곱해 그려진다.
        /// </summary>
        private static float SheenCoverage(float x, float y, float size)
        {
            float along = x / size + y / size * 0.35f;
            float wide = Mathf.Exp(-Mathf.Pow((along - 0.38f) / 0.05f, 2f));
            float narrow = Mathf.Exp(-Mathf.Pow((along - 0.5f) / 0.018f, 2f));
            return wide + 0.57f * narrow + 0.17f;
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

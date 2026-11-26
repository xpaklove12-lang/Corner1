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
        private const string SCENE_PATH = "Assets/IntroCuttoon/Scenes/IntroCuttoon.unity";
        private const string SCENE_FOLDER = "Assets/IntroCuttoon/Scenes";
        private const string SPRITE_FOLDER = "Assets/IntroCuttoon/Sprites";
        private const string CIRCLE_PATH = "Assets/IntroCuttoon/Sprites/GB_Circle.png";
        private const string TRIANGLE_PATH = "Assets/IntroCuttoon/Sprites/GB_Triangle.png";
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
        private const string MATERIAL_FOLDER = "Assets/IntroCuttoon/Materials";
        private const string RENDER_TEXTURE_FOLDER = "Assets/IntroCuttoon/RenderTextures";
        private const string LIT_SHADER = "Universal Render Pipeline/Lit";
        private const string UI_LAYER = "UI";
        private const int SHAPE_TEXTURE_SIZE = 256;
        private const int LABEL_FONT_SIZE = 22;
        private const float PAGE_WIDTH = 1800f;
        private const float PAGE_HEIGHT = 940f;
        private const float GUTTER = 24f;
        private const int QUESTION_FONT_SIZE = 60;
        private const float QUESTION_GAP = 26f;
        private const float QUESTION_HEIGHT_RATIO = 0.75f;

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
        // 1-3 카메라가 빠지기 시작하는 곳을 최종 카메라 기준 오른쪽(x), 위(y), 앞(z) 거리로 적는다.
        private static readonly Vector3 _bustDollyOffset = new Vector3(0.2f, 0.1f, 0.5f);
        private static readonly Vector2 _questionSize = new Vector2(80f, 90f);
        private static readonly Vector3 _headDown = new Vector3(-8f, 0f, 0f);
        private static readonly Vector3 _headUp = new Vector3(32f, 0f, 0f);

        private static Sprite _circle;
        private static Sprite _triangle;
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
                    "이미 있는 IntroCuttoon 씬을 덮어씁니다. 씬에서 고친 칸 배치와 연출 값이 사라집니다.",
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
        /// 도형 그림을 준비하고 새 씬에 카메라, 캔버스, 5쪽 14칸, 번쩍임과 암전 화면, 하단 안내, 진행 오브젝트를 깔아 SCENE_PATH로 저장한다.
        /// 저장한 씬이 열린 채로 끝난다.
        /// </summary>
        public static void Build()
        {
            EnsureShapeSprites();
            EnsureFolder(SCENE_FOLDER);
            _circle = AssetDatabase.LoadAssetAtPath<Sprite>(CIRCLE_PATH);
            _triangle = AssetDatabase.LoadAssetAtPath<Sprite>(TRIANGLE_PATH);
            _rounded = AssetDatabase.GetBuiltinExtraResource<Sprite>(ROUNDED_SPRITE);
            _font = AssetDatabase.LoadAssetAtPath<Font>(FONT_PATH);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Gray(0.45f);
            CreateLight();
            Transform citySet = BuildCitySet("Set_City", _citySetPosition, _citizenStart, out Transform citizen);
            Transform zoomSet = BuildCitySet("Set_City_Zoom", _zoomSetPosition, _citizenEnd, out _);
            Transform meteor = CreateMeteor(zoomSet);
            Transform bustSet = BuildBustSet(out Transform headPivot);
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
                BuildPage2(pagesRoot),
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
            CuttoonTransformTween approach = AddTransformTween(shadowPanel, meteor, 0.25f, 2.2f);
            TweenLocalPosition(approach, _meteorStart, _meteorEnd);
            SetCurve(approach, EaseInCurve());

            // 그림자가 오는 동안 칸 전체를 어둡게 하는 막은 라벨보다 먼저 만들어 라벨 아래에 그려지게 한다.
            Image dim = CreateImage(shadowPanel, "Dim", null, Color.black);
            SetAnchors(dim.rectTransform, 0f, 0f, 1f, 1f);
            CanvasGroup dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            dimGroup.alpha = 0f;
            dimGroup.blocksRaycasts = false;
            dimGroup.interactable = false;
            CuttoonTween darken = AddTween(shadowPanel, dim.rectTransform, 0.25f, 2.2f);
            TweenAlpha(darken, dimGroup, 0f, 0.35f);
            SetCurve(darken, EaseInCurve());
            // 흔들림은 칸이 다 나타난 뒤 준다. 운석은 칸 밖에서 미리 출발해, 흔들림이 시작된 직후 그림자가 칸에 들어오기 시작한다.
            CuttoonShake hit = shadowPanel.gameObject.AddComponent<CuttoonShake>();
            SetRef(hit, "_target", shadowPanel);
            SetFloat(hit, "_delay", 0.6f);
            SetFloat(hit, "_duration", 0.35f);
            SetFloat(hit, "_amplitude", 10f);
            CreateLabel(shadowPanel, shadowSize, "1-2", "그림자", "같은 각도로 줌인, 오른쪽 위 모서리에서 그림자가 점점 빨리 덮쳐 온다");
            panels.Add(FinishPanel(shadowPanel));

            // 1-3 올려다보기: 비스듬히 선 인물을 앞쪽 조금 위의 가까운 카메라로 내려다보는 상반신 구도다.
            RectTransform lookUp = CreatePanel(page, "1-3", 0.5f, 0f, 1f, 0.48f, Gray(0.24f), out Vector2 lookUpSize);
            Camera bustCamera = CreateSetCamera("Camera_1-3", bustSet, _bustCameraPosition, _bustLookTarget, 50f, Gray(0.4f), EnsureRenderTexture("1-3", lookUpSize));
            RawImage lookUpView = CreateView(lookUp, bustCamera);

            // 카메라는 고개를 다 든 뒤, 최종 구도보다 가깝고 오른쪽 위인 곳에서 각도를 유지한 채 왼쪽 아래 뒤로 빠져 최종 위치에 멈춘다.
            float dollyDelay = 1.5f;
            float dollyDuration = 1.6f;
            Transform bustCameraTransform = bustCamera.transform;
            Vector3 dollyStartWorld = bustCameraTransform.position
                + bustCameraTransform.right * _bustDollyOffset.x
                + bustCameraTransform.up * _bustDollyOffset.y
                + bustCameraTransform.forward * _bustDollyOffset.z;
            Vector3 dollyStart = bustSet.InverseTransformPoint(dollyStartWorld);
            CuttoonTransformTween pullBack = AddTransformTween(lookUp, bustCameraTransform, dollyDelay, dollyDuration);
            TweenLocalPosition(pullBack, dollyStart, _bustCameraPosition);

            // ?는 씬에서는 고개를 든 머리의 오른쪽 옆에 둔다. 카메라가 빠지기 전과 뒤의 머리 크기로 ?가 작아지는 비율을 구한다.
            MeshFilter[] headParts = headPivot.GetComponentsInChildren<MeshFilter>();
            bustCamera.aspect = (float)bustCamera.targetTexture.width / bustCamera.targetTexture.height;
            Rect headAtEnd = ViewportBounds(bustCamera, headParts);
            bustCameraTransform.localPosition = dollyStart;
            Rect headAtStart = ViewportBounds(bustCamera, headParts);
            bustCameraTransform.localPosition = _bustCameraPosition;
            bustCamera.ResetAspect();
            Vector2 questionAtEnd = QuestionPoint(headAtEnd, lookUpSize);

            Text question = CreateText(lookUp, "Question", "?", QUESTION_FONT_SIZE, Gray(0.95f), TextAnchor.MiddleLeft);
            SetPoint(question.rectTransform, questionAtEnd, _questionSize, new Vector2(0f, 0.5f));
            CanvasGroup questionGroup = question.gameObject.AddComponent<CanvasGroup>();
            questionGroup.blocksRaycasts = false;
            questionGroup.interactable = false;
            CuttoonTransformTween raise = AddTransformTween(lookUp, headPivot, 0.4f, 1f);
            TweenLocalRotation(raise, _headDown, _headUp);
            CuttoonTween fadeIn = AddTween(lookUp, question.rectTransform, 0.4f, 0.9f);
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
        /// 2쪽(2-1 시민의 눈, 2-2 운석)을 pagesRoot 아래에 깔고 쪽 컴포넌트를 돌려준다.
        /// 2-2에는 2쪽 전체를 흔드는 연출을 단다.
        /// </summary>
        private static CuttoonPage BuildPage2(RectTransform pagesRoot)
        {
            RectTransform page = CreatePage(pagesRoot, "Page2");
            List<CuttoonPanel> panels = new List<CuttoonPanel>();

            // 2-1 시민의 눈
            RectTransform eyePanel = CreatePanel(page, "2-1", 0f, 0f, 0.24f, 1f, Gray(0.14f), out Vector2 eyeSize);
            CreateEye(eyePanel, eyeSize, 0.06f, 0.38f, 0.94f, 0.62f, Gray(0.62f));
            CreateLabel(eyePanel, eyeSize, "2-1", "시민의 눈", "크게 뜬 눈, 동공에 운석이 비친다");
            panels.Add(FinishPanel(eyePanel));

            // 2-2 운석
            RectTransform meteorPanel = CreatePanel(page, "2-2", 0.24f, 0f, 1f, 1f, Gray(0.16f), out Vector2 meteorSize);
            Image meteor = CreateImage(meteorPanel, "Meteor", _circle, Gray(0.48f));
            SetPoint(meteor.rectTransform, new Vector2(0.55f, 0.98f), new Vector2(1050f, 1050f));
            float[] craters = { 0.32f, 0.35f, 170f, 0.62f, 0.27f, 120f, 0.5f, 0.55f, 90f, 0.72f, 0.5f, 60f };
            for (int i = 0; i < craters.Length / 3; i++)
            {
                Image crater = CreateImage(meteor.transform, "Crater" + i, _circle, Gray(0.4f));
                SetPoint(crater.rectTransform, new Vector2(craters[i * 3], craters[i * 3 + 1]), Vector2.one * craters[i * 3 + 2]);
            }

            SetAnchors(CreateImage(meteorPanel, "Ground", null, Gray(0.22f)).rectTransform, 0f, 0f, 1f, 0.03f);
            float[] cityHeights = { 0.06f, 0.1f, 0.05f, 0.12f, 0.08f, 0.04f, 0.11f, 0.07f, 0.09f, 0.05f, 0.13f, 0.06f, 0.08f, 0.1f, 0.05f };
            for (int i = 0; i < cityHeights.Length; i++)
            {
                float x = 0.01f + i * 0.066f;
                Image block = CreateImage(meteorPanel, "City" + i, null, Gray(0.1f));
                SetAnchors(block.rectTransform, x, 0.03f, x + 0.05f, 0.03f + cityHeights[i]);
            }

            CuttoonTween approach = AddTween(meteorPanel, meteor.rectTransform, 0f, 3.2f);
            TweenPosition(approach, new Vector2(0f, 80f), Vector2.zero);
            TweenScale(approach, Vector3.one * 0.82f, Vector3.one);
            CuttoonShake shake = meteorPanel.gameObject.AddComponent<CuttoonShake>();
            SetRef(shake, "_target", page);
            CreateLabel(meteorPanel, meteorSize, "2-2", "운석", "하늘을 가득 채운 운석, 그 아래 작은 도시");
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
        /// 원과 삼각형 도형 그림이 SPRITE_FOLDER에 없을 때만 만든다. 이미 있으면 그대로 둔다.
        /// </summary>
        private static void EnsureShapeSprites()
        {
            EnsureFolder(SPRITE_FOLDER);
            if (!File.Exists(CIRCLE_PATH))
            {
                WriteShapeTexture(CIRCLE_PATH, false);
            }

            if (!File.Exists(TRIANGLE_PATH))
            {
                WriteShapeTexture(TRIANGLE_PATH, true);
            }
        }

        /// <summary>
        /// 흰색 도형(triangle이면 오른쪽을 가리키는 삼각형, 아니면 원) PNG를 path에 쓰고 스프라이트로 가져온다.
        /// 가장자리는 1픽셀 폭으로 부드럽게 만든다.
        /// </summary>
        private static void WriteShapeTexture(string path, bool triangle)
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
                    float coverage = triangle ? TriangleCoverage(px, py, size) : CircleCoverage(px, py, size);
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
    }
}

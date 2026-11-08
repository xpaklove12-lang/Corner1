using System;

using UnityEngine;
using UnityEngine.Rendering;

using Object = UnityEngine.Object;

namespace EyeMaze
{
    /// <summary>런타임 코드가 만들고 지우는 오브젝트를 에디터 Undo에 연결하기 위한 훅.</summary>
    public static class EyeHooks
    {
        public static Action<GameObject> OnCreated { get; set; }
        public static Action<GameObject> OnDestroyRequest { get; set; }

        /// <summary>
        /// 새로 만든 오브젝트를 에디터 훅에 알린다.
        /// go를 사용하며, 등록된 OnCreated 훅을 호출한다.
        /// </summary>
        public static void Created(GameObject go)
        {
            if (go != null && OnCreated != null) OnCreated(go);
        }

        /// <summary>
        /// 오브젝트를 지운다. Play 모드에서는 Destroy, 에디트 모드에서는 Undo 훅 또는 DestroyImmediate를 사용한다.
        /// go를 사용하며, 반환값은 없다.
        /// </summary>
        public static void Destroy(GameObject go)
        {
            if (go == null) return;
            if (Application.isPlaying)
            {
                Object.Destroy(go);
                return;
            }
            if (OnDestroyRequest != null) OnDestroyRequest(go);
            else Object.DestroyImmediate(go);
        }

        /// <summary>
        /// 에디트 모드에서 씬 뷰와 게임 뷰를 다시 그리도록 요청한다.
        /// 입력값은 없으며, Play 모드에서는 아무 일도 하지 않는다.
        /// </summary>
        public static void Repaint()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
#endif
        }
    }

    /// <summary>모든 그림은 LineRenderer로 그린다 (선, 원호, 원, 둥근 점, 사각형).</summary>
    public static class Shapes
    {
        /// <summary>
        /// 오브젝트에 LineRenderer를 붙이거나 찾아서 기본 설정을 적용한다.
        /// go, mat, caps를 사용하며, 설정된 LineRenderer를 반환한다.
        /// </summary>
        public static LineRenderer Ensure(GameObject go, Material mat, int caps)
        {
            LineRenderer lr = go.GetComponent<LineRenderer>();
            if (lr == null) lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            if (mat != null) lr.sharedMaterial = mat;
            lr.numCapVertices = caps;
            lr.numCornerVertices = 2;
            lr.alignment = LineAlignment.View;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            lr.widthMultiplier = 0.05f;
            return lr;
        }

        /// <summary>
        /// 반지름과 각도로 평면 위의 점을 계산한다.
        /// r, deg를 사용하며, 로컬 좌표를 반환한다.
        /// </summary>
        public static Vector3 Polar(float r, float deg)
        {
            float a = deg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0f);
        }

        /// <summary>
        /// LineRenderer의 점들을 한 번에 설정한다.
        /// lr, pts, loop를 사용하며, lr의 점 목록과 loop 여부를 바꾼다.
        /// </summary>
        public static void SetPoints(LineRenderer lr, Vector3[] pts, bool loop)
        {
            lr.loop = loop;
            lr.positionCount = pts.Length;
            lr.SetPositions(pts);
        }

        /// <summary>
        /// 원호를 그린다.
        /// 반지름 r, 시작각 a0, 끝각 a1, 분할 수 seg를 사용하며, lr의 점 목록을 바꾼다.
        /// </summary>
        public static void SetArc(LineRenderer lr, float r, float a0, float a1, int seg)
        {
            Vector3[] p = new Vector3[seg + 1];
            for (int i = 0; i <= seg; i++) p[i] = Polar(r, Mathf.Lerp(a0, a1, i / (float)seg));
            SetPoints(lr, p, false);
        }

        /// <summary>
        /// 닫힌 원을 그린다.
        /// 반지름 r과 분할 수 seg를 사용하며, lr의 점 목록을 바꾼다.
        /// </summary>
        public static void SetCircle(LineRenderer lr, float r, int seg)
        {
            Vector3[] p = new Vector3[seg];
            for (int i = 0; i < seg; i++) p[i] = Polar(r, 360f * i / seg);
            SetPoints(lr, p, true);
        }

        /// <summary>
        /// 아주 짧은 선과 둥근 캡으로 꽉 찬 원을 그린다.
        /// diameter를 사용하며, lr의 점 목록과 두께를 바꾼다.
        /// </summary>
        public static void SetBlob(LineRenderer lr, float diameter)
        {
            SetPoints(lr, new[] { Vector3.zero, new Vector3(0f, 0.001f, 0f) }, false);
            lr.widthMultiplier = diameter;
        }

        /// <summary>
        /// 가로로 긴 선을 높이만큼 두껍게 그려 꽉 찬 직사각형을 만든다.
        /// w, h를 사용하며, lr의 점 목록과 두께를 바꾼다.
        /// </summary>
        public static void SetFilledRect(LineRenderer lr, float w, float h)
        {
            lr.numCapVertices = 0;
            SetPoints(lr, new[] { new Vector3(-w * 0.5f, 0f, 0f), new Vector3(w * 0.5f, 0f, 0f) }, false);
            lr.widthMultiplier = h;
        }

        /// <summary>
        /// 선 전체를 한 색으로 칠한다.
        /// c를 사용하며, lr의 시작색과 끝색을 바꾼다.
        /// </summary>
        public static void SetColor(LineRenderer lr, Color c)
        {
            lr.startColor = c;
            lr.endColor = c;
        }
    }
}

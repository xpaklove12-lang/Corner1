using System;

using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace EyeMaze
{
    /// <summary>
    /// 도트 느낌: 월드 카메라를 낮은 해상도 RenderTexture에 그린 뒤, 화면 전체에 포인트 필터로 키워서 보여준다.
    /// height를 낮출수록 도트가 굵어진다 (270 = 1080p 기준 4배). pixelate를 끄면 원래대로 선명하게 보인다.
    /// 에디트 모드에서도 Game 뷰에 바로 반영된다.
    /// </summary>
    [ExecuteAlways]
    public class PixelCamera : MonoBehaviour
    {
        [Header("연결")]
        [SerializeField, FormerlySerializedAs("worldCamera")] private Camera _worldCamera;
        [Tooltip("화면 전체를 덮는 RawImage (Canvas 맨 뒤)")]
        [SerializeField, FormerlySerializedAs("target")] private RawImage _target;

        [Header("해상도")]
        [SerializeField, FormerlySerializedAs("pixelate")] private bool _pixelate = true;
        [Tooltip("세로 해상도 (낮을수록 도트가 굵음)")]
        [SerializeField, FormerlySerializedAs("height"), Range(90, 1080)] private int _height = 270;
        [Tooltip("가로세로 비율 (16:9)")]
        [SerializeField, FormerlySerializedAs("aspect")] private Vector2 _aspect = new Vector2(16f, 9f);

        [NonSerialized] private RenderTexture _rt;

        void OnEnable() => Apply();
        void OnDisable() => Release();
        void OnValidate() => EyeHooks.Repaint();
        void Update() => Apply();

        /// <summary>
        /// 설정에 맞는 RenderTexture를 준비하고 카메라와 RawImage에 연결한다.
        /// _pixelate, _height, _aspect를 사용하며, 카메라의 targetTexture와 RawImage의 texture를 바꾼다.
        /// </summary>
        private void Apply()
        {
            if (!_pixelate || _worldCamera == null || _target == null)
            {
                Release();
                if (_target != null && _target.enabled) _target.enabled = false;
                return;
            }
            int h = Mathf.Max(16, _height);
            int w = Mathf.Max(16, Mathf.RoundToInt(h * _aspect.x / Mathf.Max(0.01f, _aspect.y)));
            if (_rt == null || _rt.width != w || _rt.height != h)
            {
                Release();
                _rt = new RenderTexture(w, h, 16, RenderTextureFormat.ARGB32)
                {
                    filterMode = FilterMode.Point,
                    antiAliasing = 1,
                    useMipMap = false,
                    name = "EyeMaze Pixel View",
                    hideFlags = HideFlags.DontSave,
                };
                _rt.Create();
            }
            if (_worldCamera.targetTexture != _rt) _worldCamera.targetTexture = _rt;
            if (_target.texture != _rt) _target.texture = _rt;
            if (!_target.enabled) _target.enabled = true;
        }

        /// <summary>
        /// 만들어 둔 RenderTexture를 카메라에서 떼고 해제한다.
        /// 입력값은 없으며, _rt를 null로 만든다.
        /// </summary>
        private void Release()
        {
            if (_worldCamera != null && _worldCamera.targetTexture == _rt) _worldCamera.targetTexture = null;
            if (_rt != null)
            {
                _rt.Release();
                if (Application.isPlaying) Destroy(_rt);
                else DestroyImmediate(_rt);
                _rt = null;
            }
        }
    }
}

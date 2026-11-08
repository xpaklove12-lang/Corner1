using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>눈을 들여다보는 상대. 신뢰는 문을 열고, 동공 열림은 마지막 입구를 연다.</summary>
    public class Person : MonoBehaviour
    {
        [Header("상태")]
        [SerializeField, FormerlySerializedAs("displayName")] private string _displayName = "은재";
        [Tooltip("층 사이의 문을 열고, 감을 수 있는 태엽 양을 늘린다")]
        [SerializeField, FormerlySerializedAs("trust")] private int _trust;
        [Tooltip("동공 크기. 높을수록 마음이 열려 있음")]
        [SerializeField, FormerlySerializedAs("dilation"), Range(0f, 1f)] private float _dilation = 0.2f;

        public string DisplayName { get => _displayName; set => _displayName = value; }
        public int Trust { get => _trust; set => _trust = value; }
        public float Dilation { get => _dilation; set => _dilation = Mathf.Clamp01(value); }

        void OnValidate() => EyeHooks.Repaint();
    }
}

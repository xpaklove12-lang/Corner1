using UnityEngine;
using UnityEngine.Serialization;

namespace EyeMaze
{
    /// <summary>눈을 들여다보는 상대. 신뢰는 문을 열고, 동공 열림은 마지막 입구를 연다.</summary>
    public class Person : MonoBehaviour
    {
        [Header("상태")]
        [SerializeField, FormerlySerializedAs("displayName")] private string _displayName = "은재";
        [Tooltip("층 사이의 문을 연다")]
        [SerializeField, FormerlySerializedAs("trust")] private int _trust;
        [Tooltip("동공 크기. 높을수록 마음이 열려 있음")]
        [SerializeField, FormerlySerializedAs("dilation"), Range(0f, 1f)] private float _dilation = 0.2f;
        // 스펙 004: 옳은 대답으로 얻은 신뢰 누적값 추가
        [System.NonSerialized] private int _earnedTrust;

        public string DisplayName { get => _displayName; set => _displayName = value; }
        public int Trust { get => _trust; set => _trust = value; }
        /// <summary>옳은 대답으로 얻은 신뢰의 합. 안 좋은 말의 감점이 들어가지 않는다.</summary>
        public int EarnedTrust { get => _earnedTrust; set => _earnedTrust = value; }
        /// <summary>문을 여는 데 쓰는 신뢰. Inspector에서 올린 Trust로도 문을 시험할 수 있다.</summary>
        public int GateTrust => Mathf.Max(_trust, _earnedTrust);
        public float Dilation { get => _dilation; set => _dilation = Mathf.Clamp01(value); }

        void OnValidate() => EyeHooks.Repaint();
    }
}

using System;
using System.Collections.Generic;

using UnityEngine;

namespace EyeMaze
{
    public class FragmentPortalSystem : MonoBehaviour
    {
        [Serializable]
        private class LayerDestination
        {
            [Header("층별 목적지")]
            [SerializeField] private MazeLayer _layer;
            [SerializeField] private Transform _destination;

            public MazeLayer Layer => _layer;
            public Transform Destination => _destination;
        }

        [Header("연결")]
        [SerializeField] private EyeMazeDirector _director;
        [SerializeField] private FragmentPortal _portalPrefab;
        [SerializeField] private LayerDestination[] _destinations = Array.Empty<LayerDestination>();

        [Header("생성")]
        [SerializeField] private Vector2 _spawnOffset = new Vector2(0.14f, 0f);
        private readonly Dictionary<MazeLayer, MemoryFragment> _latestFragments = new Dictionary<MazeLayer, MemoryFragment>();
        private readonly Dictionary<MazeLayer, FragmentPortal> _portals = new Dictionary<MazeLayer, FragmentPortal>();
        private readonly Dictionary<MazeLayer, FragmentPortal> _returnPortals = new Dictionary<MazeLayer, FragmentPortal>();
        private readonly HashSet<MazeLayer> _warnedMissing = new HashSet<MazeLayer>();
        private ClientCase _case;

        void OnEnable()
        {
            MemoryFragment.PickedUp += RecordFragment;
            MemoryFragment.FeedbackCleared += ClearPortals;
        }

        void OnDisable()
        {
            MemoryFragment.PickedUp -= RecordFragment;
            MemoryFragment.FeedbackCleared -= ClearPortals;
            ClearPortals();
        }

        void LateUpdate()
        {
            if (_case != _director.ActiveCase)
            {
                ClearPortals();
                _case = _director.ActiveCase;
            }
            foreach (LayerDestination binding in _destinations)
            {
                MazeLayer layer = binding.Layer;
                if (layer == null || _portals.ContainsKey(layer) || !_latestFragments.TryGetValue(layer, out MemoryFragment fragment)) continue;
                if (fragment == null || !fragment.Collected || !fragment.gameObject.activeInHierarchy || !IsLayerOpen(layer)) continue;
                if (binding.Destination == null)
                {
                    if (_warnedMissing.Add(layer)) Debug.LogWarning($"FragmentPortal: {layer.name}의 potal_destination을 연결하세요.", this);
                    continue;
                }
                FragmentPortal portal = Instantiate(_portalPrefab, layer.transform);
                portal.transform.position = fragment.transform.position + layer.transform.TransformVector((Vector3)_spawnOffset);
                FragmentPortal returnPortal = Instantiate(_portalPrefab, binding.Destination);
                returnPortal.transform.localPosition = Vector3.zero;
                portal.Initialize(_director, returnPortal);
                returnPortal.Initialize(_director, portal);
                _portals.Add(layer, portal);
                _returnPortals.Add(layer, returnPortal);
            }
        }

        /// <summary>
        /// fragment가 현재 미로와 케이스에 속하면 해당 층의 최근 수집 파편으로 기록한다.
        /// 수집된 파편 참조를 사용하며 층별 기록과 케이스 상태를 변경한다.
        /// </summary>
        private void RecordFragment(MemoryFragment fragment)
        {
            if (fragment.GetComponentInParent<IrisMaze>() != _director.Maze || !fragment.gameObject.activeInHierarchy) return;
            if (_case != _director.ActiveCase) { ClearPortals(); _case = _director.ActiveCase; }
            MazeLayer layer = fragment.Layer;
            if (layer != null) _latestFragments[layer] = fragment;
        }

        /// <summary>
        /// layer가 소유한 문의 현재 통과 조건을 조회한다.
        /// 디렉터의 미로와 인물 상태를 사용하며 통과 가능한 문이 하나라도 있으면 true를 반환한다.
        /// </summary>
        private bool IsLayerOpen(MazeLayer layer)
        {
            foreach (MazeGate gate in _director.Maze.GetComponentsInChildren<MazeGate>(false))
                if (_director.Maze.LayerOfRing(gate.Ring) == layer.Index && gate.CanPass(_director.Person)) return true;
            return false;
        }

        /// <summary>
        /// 입력값 없이 생성된 포탈과 파편 기록을 제거한다.
        /// 파편 초기화, 케이스 변경과 비활성화 시 층별 상태를 비운다.
        /// </summary>
        private void ClearPortals()
        {
            foreach (FragmentPortal portal in _portals.Values)
            {
                if (portal == null) continue;
                portal.gameObject.SetActive(false);
                Destroy(portal.gameObject);
            }
            foreach (FragmentPortal portal in _returnPortals.Values)
            {
                if (portal == null) continue;
                portal.gameObject.SetActive(false);
                Destroy(portal.gameObject);
            }
            _portals.Clear();
            _returnPortals.Clear();
            _latestFragments.Clear();
            _warnedMissing.Clear();
        }
    }
}

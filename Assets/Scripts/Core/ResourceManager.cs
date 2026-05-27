// =============================================================================
// ResourceManager.cs
// 역할  : 씬의 모든 ResourceNode를 등록/관리하고, GathererFSM에 비예약 노드를 제공한다.
// 사용법: GameManager와 동일한 GameObject에 추가(자동). 직접 접근은
//         GameManager.Instance.ResourceManager 를 사용할 것.
// 의존성: GameManager (같은 GameObject), ResourceNode (AIVillage.Resources)
// GDD   : §11 ResourceManager / Week 2 노드 목록 관리 / Week 4 GathererFSM 연동
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using AIVillage.Resources;

namespace AIVillage.Core
{
    /// <summary>
    /// ResourceNode 목록을 관리하는 매니저.
    /// 비예약 노드 목록 제공 및 가장 가까운 비예약 노드 반환 기능을 담당한다.
    /// Week 4에서 GathererFSM이 이 API를 호출한다.
    /// </summary>
    public sealed class ResourceManager : MonoBehaviour
    {
        #region Private State

        /// <summary>씬에 등록된 전체 ResourceNode 목록.</summary>
        private readonly List<ResourceNode> _allNodes = new List<ResourceNode>();

        #endregion

        #region Node Registration

        /// <summary>
        /// ResourceNode를 목록에 등록한다.
        /// Week 3에서 ResourceNode.Awake()가 자동 호출하도록 연결 예정.
        /// </summary>
        /// <param name="node">등록할 노드</param>
        public void RegisterNode(ResourceNode node)
        {
            if (node == null)
            {
                Debug.LogWarning("[ResourceManager] RegisterNode — null 노드는 등록할 수 없습니다.");
                return;
            }

            if (_allNodes.Contains(node))
            {
                Debug.LogWarning($"[ResourceManager] RegisterNode — 이미 등록된 노드: {node.name}");
                return;
            }

            _allNodes.Add(node);

            // MessageBus 주입: GameManager.Awake → CacheComponents에서 이미 생성됨
            if (GameManager.Instance?.MessageBus != null)
                node.InjectMessageBus(GameManager.Instance.MessageBus);

            Debug.Log($"[ResourceManager] 노드 등록: {node.name} ({node.GetResourceType()}) | 총 {_allNodes.Count}개");
        }

        /// <summary>
        /// ResourceNode를 목록에서 제거한다.
        /// 노드 오브젝트가 파괴될 때 호출.
        /// </summary>
        /// <param name="node">제거할 노드</param>
        public void UnregisterNode(ResourceNode node)
        {
            if (node == null)
            {
                Debug.LogWarning("[ResourceManager] UnregisterNode — null 노드입니다.");
                return;
            }

            if (!_allNodes.Remove(node))
            {
                Debug.LogWarning($"[ResourceManager] UnregisterNode — 등록되지 않은 노드: {node.name}");
                return;
            }

            Debug.Log($"[ResourceManager] 노드 제거: {node.name} | 총 {_allNodes.Count}개");
        }

        #endregion

        #region Node Query API

        /// <summary>
        /// 예약되지 않고 사용 가능한(IsAvailable) 노드 목록을 반환한다.
        /// Week 4에서 GathererFSM이 채집 대상 노드를 선택할 때 사용한다.
        /// </summary>
        /// <param name="type">필터링할 자원 종류. null이면 전체 타입 반환.</param>
        /// <returns>사용 가능한 노드 읽기 전용 리스트</returns>
        public IReadOnlyList<ResourceNode> GetAvailableNodes(ResourceType? type = null)
        {
            var result = new List<ResourceNode>();

            foreach (ResourceNode node in _allNodes)
            {
                if (node == null) continue;
                if (!node.IsAvailable()) continue;
                if (type.HasValue && node.GetResourceType() != type.Value) continue;

                result.Add(node);
            }

            return result;
        }

        /// <summary>
        /// 지정한 월드 좌표에서 가장 가까운 비예약 노드를 반환한다.
        /// GDD V01-15: 비예약 노드 중 가장 가까운 노드 선택.
        /// </summary>
        /// <remarks>
        /// TODO: Week 3 — Vector2.Distance 를 A* 경로 비용으로 교체
        /// </remarks>
        /// <param name="from">기준 월드 좌표 (Gatherer 현재 위치)</param>
        /// <param name="type">필터링할 자원 종류. null이면 전체 타입 대상.</param>
        /// <returns>가장 가까운 사용 가능 노드. 없으면 null.</returns>
        public ResourceNode GetNearestAvailableNode(Vector2 from, ResourceType? type = null)
        {
            IReadOnlyList<ResourceNode> candidates = GetAvailableNodes(type);

            if (candidates.Count == 0)
                return null;

            ResourceNode nearest     = null;
            float        minDistance = float.MaxValue;

            foreach (ResourceNode node in candidates)
            {
                // TODO: Week 3 — Vector2.Distance → A* 경로 비용으로 교체
                float dist = Vector2.Distance(from, node.GetWorldPosition());

                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearest     = node;
                }
            }

            return nearest;
        }

        /// <summary>현재 등록된 전체 노드 수 (디버그/UI 용도).</summary>
        public int TotalNodeCount => _allNodes.Count;

        /// <summary>현재 사용 가능한 노드 수 (디버그/UI 용도).</summary>
        public int AvailableNodeCount => GetAvailableNodes().Count;

        #endregion

        #region Lifecycle

        private void OnDestroy()
        {
            _allNodes.Clear();
        }

        #endregion
    }
}

// =============================================================================
// Gatherer.cs
// 역할  : 자원 수집 전담 유닛. Idle → Moving → Gathering → Returning → Idle 루프.
//         ResourceManager에서 가장 가까운 비예약 노드를 자동으로 찾아 채집 후 귀환.
// 의존성: AIUnit, GameManager, ResourceManager, ResourceNode
// GDD   : §5 GathererFSM / Week 4
// =============================================================================

using System.Collections;
using UnityEngine;
using AIVillage.Resources;
using AIVillage.Core;

namespace AIVillage.Units
{
    /// <summary>
    /// 자원 노드를 자율적으로 찾아 채집하고 기지로 귀환하는 유닛.
    /// AIUnit의 비동기 이동 + Coroutine 기반 채집 타이머를 결합한 FSM.
    /// </summary>
    public class Gatherer : AIUnit
    {
        #region Inspector Fields

        [Header("채집 설정 (GDD §5)")]
        [SerializeField, Range(1, 10), Tooltip("1회 채집량. 노드 잔량보다 크면 잔량만 수집됨.")]
        private int _gatherAmountPerTrip = 1;

        [SerializeField, Tooltip("자원 노드를 찾지 못했을 때 재탐색 대기 시간 (초).")]
        private float _retryDelay = 2f;

        #endregion

        #region Private FSM State

        private ResourceNode _targetNode;
        private int          _gatheredAmount;
        private bool         _isReturning;
        private bool         _isInGatherCycle; // OnIdle 재진입 방지 플래그
        private Coroutine    _gatherCoroutine;

        #endregion

        #region Unity Lifecycle

        protected override void Start()
        {
            base.Start(); // PopulationManager 등록
            SearchAndGo();
        }

        #endregion

        #region AIUnit Callbacks

        /// <summary>
        /// 목적지 도착 시 호출. 노드 도착이면 채집 시작, 기지 도착이면 자원 반납.
        /// </summary>
        protected override void OnReachDestination()
        {
            if (!_isReturning && _targetNode != null)
            {
                // 자원 노드에 도착 — 채집 코루틴 시작
                _gatherCoroutine = StartCoroutine(GatherRoutine());
            }
            else if (_isReturning)
            {
                // 기지에 도착 — 채집한 자원 반납
                if (_gatheredAmount > 0 && _targetNode != null)
                {
                    GameManager.Instance.AddResource(_targetNode.GetResourceType(), _gatheredAmount);
                    Debug.Log($"[Gatherer] '{name}' — {_targetNode.GetResourceType()} {_gatheredAmount} 반납 완료.");
                }

                _targetNode?.ReleaseReservation();
                _targetNode       = null;
                _gatheredAmount   = 0;
                _isReturning      = false;
                _isInGatherCycle  = false;
            }
        }

        /// <summary>
        /// Idle 상태 진입 시 호출. 채집 사이클 중간이 아닐 때만 다음 노드를 탐색한다.
        /// </summary>
        protected override void OnIdle()
        {
            if (!_isInGatherCycle)
                SearchAndGo();
        }

        /// <summary>
        /// 경로 실패 시 예약을 해제하고 FSM을 초기화한 뒤 재탐색한다.
        /// </summary>
        protected override void OnPathFailed()
        {
            base.OnPathFailed();

            if (_gatherCoroutine != null)
            {
                StopCoroutine(_gatherCoroutine);
                _gatherCoroutine = null;
            }

            _targetNode?.ReleaseReservation();
            _targetNode      = null;
            _gatheredAmount  = 0;
            _isReturning     = false;
            _isInGatherCycle = false;

            // 이미 예약된 Invoke가 있을 수 있으므로 취소 후 재등록
            CancelInvoke(nameof(SearchAndGo));
            Invoke(nameof(SearchAndGo), _retryDelay);
        }

        #endregion

        #region FSM Logic

        /// <summary>
        /// 가장 가까운 비예약 노드를 찾아 이동을 시작한다.
        /// 노드가 없으면 _retryDelay 후 재시도한다.
        /// </summary>
        private void SearchAndGo()
        {
            if (GameManager.Instance == null) return;

            ResourceNode node = GameManager.Instance.ResourceManager
                .GetNearestAvailableNode(transform.position);

            if (node == null)
            {
                // 사용 가능한 노드 없음 — 잠시 후 재탐색
                Invoke(nameof(SearchAndGo), _retryDelay);
                return;
            }

            if (!node.TryReserve(gameObject))
            {
                // 예약 경합 발생 (멀티 Gatherer) — 짧게 재시도
                Invoke(nameof(SearchAndGo), 0.5f);
                return;
            }

            _targetNode      = node;
            _isReturning     = false;
            _isInGatherCycle = true;

            Debug.Log($"[Gatherer] '{name}' — {node.GetResourceType()} 노드 '{node.name}' 예약. 이동 시작.");
            SetDestination(node.transform.position);
        }

        /// <summary>
        /// 채집 타이머 대기 후 자원을 수집하고 기지로 귀환한다.
        /// enabled=false일 때도 코루틴은 계속 실행된다.
        /// </summary>
        private IEnumerator GatherRoutine()
        {
            yield return new WaitForSeconds(_targetNode.GatherDuration);

            if (_targetNode == null) yield break; // 노드가 씬에서 제거된 경우

            int gathered = _targetNode.Gather(_gatherAmountPerTrip);

            if (gathered == 0)
            {
                // 다른 유닛에 의해 이미 고갈된 엣지 케이스 — 노드는 Depleted 처리됨
                _targetNode      = null;
                _isInGatherCycle = false;
                _gatherCoroutine = null;
                Invoke(nameof(SearchAndGo), _retryDelay);
                yield break;
            }

            _gatheredAmount  = gathered;
            _isReturning     = true;
            _gatherCoroutine = null;

            Debug.Log($"[Gatherer] '{name}' — {_targetNode.GetResourceType()} {gathered} 채집 완료. 기지로 귀환.");

            Vector2 home = GameManager.Instance.BasePosition;
            SetDestination(new Vector3(home.x, home.y, 0f));
        }

        #endregion
    }
}

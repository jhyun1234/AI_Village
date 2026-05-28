// =============================================================================
// Gatherer.cs
// 역할  : 자원 수집 전담 유닛. Idle → Moving → Gathering → Returning → Idle 루프.
//         ResourceManager에서 가장 가까운 비예약 노드를 자동으로 찾아 채집 후 귀환.
//         Week 8: OnFleeingEnter/Exit 구현으로 Fleeing 진입/복귀 시 상태 정리.
// 의존성: AIUnit, GameManager, ResourceManager, ResourceNode
// GDD   : §5 GathererFSM / Week 4 / §8-2 Fleeing 연동
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
    ///
    /// Week 8 추가:
    ///   - OnFleeingEnter(): 채집 코루틴 중단, 노드 예약 해제, Invoke 취소, 상태 초기화
    ///   - OnFleeingExit(): SearchAndGo() 재호출로 채집 사이클 재개
    /// </summary>
    public class Gatherer : AIUnit
    {
        #region ── Serialized Fields ──

        [Header("채집 설정 (GDD §5)")]
        [Tooltip("1회 채집량. 노드 잔량보다 크면 잔량만 수집됨.")]
        [SerializeField, Range(1, 10)] private int _gatherAmountPerTrip = 1;

        [Tooltip("자원 노드를 찾지 못했을 때 재탐색 대기 시간 (초).")]
        [SerializeField] private float _retryDelay = 2f;

        [Tooltip("노드 주변 이 반경 내에 몬스터가 있으면 해당 노드를 선택하지 않는다. GDD: 몬스터 감지 범위(3f)보다 크게 설정 권장.")]
        [SerializeField] private float _safeNodeRadius = 4f;

        #endregion

        #region ── Private FSM State ──

        private ResourceNode _targetNode;
        private int          _gatheredAmount;
        private bool         _isReturning;
        private bool         _isInGatherCycle; // OnIdle 재진입 방지 플래그
        private Coroutine    _gatherCoroutine;

        #endregion

        #region ── Unity Lifecycle ──

        protected override void Start()
        {
            base.Start(); // PopulationManager 등록 (AIUnit.Start)
            SearchAndGo();
        }

        #endregion

        #region ── AIUnit Callbacks ──

        /// <summary>
        /// 목적지 도착 시 호출. 노드 도착이면 채집 시작, 기지 도착이면 자원 반납.
        /// Fleeing 상태에서는 AIUnit.OnArrival 분기로 인해 호출되지 않는다.
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
                _targetNode      = null;
                _gatheredAmount  = 0;
                _isReturning     = false;
                _isInGatherCycle = false;
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
        /// Fleeing 상태의 경로 실패는 base.OnPathFailed()에서 직선 폴백으로 처리된다.
        /// </summary>
        protected override void OnPathFailed()
        {
            base.OnPathFailed(); // Fleeing이면 직선 폴백 활성화, 아니면 Idle 복귀

            // Fleeing 경로 실패는 base에서 처리됨 — 아래 정리는 일반 이동 실패에만 적용
            if (_currentState == UnitState.Fleeing) return;

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

        /// <summary>
        /// Fleeing 상태 진입 시 호출 (AIUnit.SetFleeing → OnFleeingEnter 경유).
        /// 진행 중인 채집 작업을 모두 정리하여 깨끗한 상태로 도주를 시작한다.
        ///
        /// 정리 순서:
        ///   1. Invoke 예약 취소: SearchAndGo 지연 재시도가 예약됐을 경우 취소
        ///   2. 채집 코루틴 중단: null 체크 후 StopCoroutine
        ///   3. 노드 예약 해제: 다른 Gatherer가 즉시 사용할 수 있도록
        ///   4. FSM 상태 변수 초기화: 복귀 후 SearchAndGo에서 깨끗하게 시작
        /// </summary>
        protected override void OnFleeingEnter()
        {
            // ── 1. Invoke 예약 취소 ──
            // SearchAndGo가 _retryDelay / 0.5f 지연으로 Invoke 예약된 경우 취소
            CancelInvoke(nameof(SearchAndGo));

            // ── 2. 채집 코루틴 중단 ──
            if (_gatherCoroutine != null)
            {
                StopCoroutine(_gatherCoroutine);
                _gatherCoroutine = null;
            }

            // ── 3. 노드 예약 해제 ──
            // 도주 중 다른 Gatherer가 이 노드를 즉시 사용할 수 있도록 해제
            _targetNode?.ReleaseReservation();

            // ── 4. FSM 상태 초기화 ──
            _targetNode      = null;
            _gatheredAmount  = 0;
            _isReturning     = false;
            _isInGatherCycle = false;

            Debug.Log($"[Gatherer] '{name}' — OnFleeingEnter: 채집 상태 정리 완료.");
        }

        /// <summary>
        /// 기지 안전 구역 도달 후 Fleeing에서 복귀할 때 호출 (AIUnit.UpdateFleeing 경유).
        /// SearchAndGo()를 호출하여 채집 사이클을 재개한다.
        /// </summary>
        protected override void OnFleeingExit()
        {
            Debug.Log($"[Gatherer] '{name}' — OnFleeingExit: 채집 재개.");
            SearchAndGo();
        }

        #endregion

        #region ── FSM Logic ──

        /// <summary>
        /// 가장 가까운 비예약 노드를 찾아 이동을 시작한다.
        /// 노드가 없으면 _retryDelay 후 재시도한다.
        /// Fleeing 상태이거나 GameManager가 없으면 즉시 반환한다.
        /// </summary>
        private void SearchAndGo()
        {
            if (GameManager.Instance == null) return;

            // ── Fleeing 상태에서는 탐색 금지 ──
            // Invoke 지연으로 인해 OnFleeingEnter 이후에 이 메서드가 호출될 수 있음
            if (_currentState == UnitState.Fleeing) return;

            ResourceNode node = GameManager.Instance.ResourceManager
                .GetNearestAvailableNode(transform.position);

            if (node == null)
            {
                // 사용 가능한 노드 없음 — 잠시 후 재탐색
                Invoke(nameof(SearchAndGo), _retryDelay);
                return;
            }

            // 내 현재 위치 주변에 몬스터가 있으면 이동 자체를 금지
            // 기지까지 쫓아온 몬스터가 바로 옆에 있는데 바로 출발하는 루프 방지
            ThreatManager tm = GameManager.Instance.ThreatManager;
            if (tm != null && tm.GetNearestMonster(transform.position, _safeNodeRadius) != null)
            {
                Invoke(nameof(SearchAndGo), _retryDelay);
                return;
            }

            // 목적지 노드 주변에 몬스터가 있으면 해당 노드를 선택하지 않고 대기
            if (tm != null && tm.GetNearestMonster(node.transform.position, _safeNodeRadius) != null)
            {
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
        /// enabled=false 상태(Idle/Gathering)에서도 코루틴은 계속 실행된다.
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

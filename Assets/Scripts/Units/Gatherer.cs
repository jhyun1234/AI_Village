// =============================================================================
// Gatherer.cs
// 역할  : 자원 수집 전담 유닛. Idle → Moving → Gathering → Returning → Idle 루프.
//         ResourceManager에서 가장 가까운 비예약 노드를 자동으로 찾아 채집 후 귀환.
//         Week 8: OnFleeingEnter/Exit 구현으로 Fleeing 진입/복귀 시 상태 정리.
// 의존성: AIUnit, GameManager, ResourceManager, ResourceNode
// GDD   : §5 GathererFSM / Week 4 / §8-2 Fleeing 연동
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using AIVillage.Resources;
using AIVillage.Core;
using AIVillage.GOAP;

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
        private GoapAgent    _goapAgent; // GOAP 1단계: null이면 기존 동작 그대로

        #endregion

        #region ── Unity Lifecycle ──

        protected override void Start()
        {
            base.Start(); // PopulationManager 등록 (AIUnit.Start)
            _goapAgent = GetComponent<GoapAgent>(); // null 허용: 미부착 시 기존 동작

            // [GOAP 2단계] GoapAgent가 있으면 SearchAndGo 호출 안 함.
            // GoapAgent의 첫 번째 Tick(game.tick 구독)에서 플래닝이 시작되어 Execute()가 호출된다.
            if (_goapAgent == null)
                SearchAndGo();
        }

        #endregion

        #region ── AIUnit Callbacks ──

        /// <summary>
        /// 목적지 도착 시 호출. 노드 도착이면 채집 시작, 기지 도착이면 자원 반납.
        /// Fleeing 상태에서는 AIUnit.OnArrival 분기로 인해 호출되지 않는다.
        ///
        /// [GOAP 2단계] GoapAgent 부착 시:
        ///   - 기지 귀환(_isReturning)이면 자원 반납 처리 후 NotifyArrival()로 신호 전달.
        ///   - 노드 도착이면 NotifyArrival()만 전달 (채집 시작은 GoapAgent.NotifyArrival이 담당).
        /// </summary>
        protected override void OnReachDestination()
        {
            if (_goapAgent != null)
            {
                // [GOAP 2단계] 기지 귀환 시에만 자원 반납 처리 (기존 로직 그대로)
                if (_isReturning)
                {
                    if (_gatheredAmount > 0 && _targetNode != null)
                    {
                        GameManager.Instance.AddResource(_targetNode.GetResourceType(), _gatheredAmount);
#if UNITY_EDITOR
                        Debug.Log($"[Gatherer] '{name}' — {_targetNode.GetResourceType()} {_gatheredAmount} 반납 완료 (GOAP).");
#endif
                    }
                    _targetNode?.ReleaseReservation();
                    _targetNode      = null;
                    _gatheredAmount  = 0;
                    _isReturning     = false;
                    _isInGatherCycle = false;
                }
                // 도착 신호 전달 (SearchNodeAction → GatherAction 전환은 GoapAgent가 처리)
                _goapAgent.NotifyArrival();
                return;
            }

            // ── Legacy FSM (GoapAgent 미부착 시 기존 동작 그대로) ──
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
#if UNITY_EDITOR
                    Debug.Log($"[Gatherer] '{name}' — {_targetNode.GetResourceType()} {_gatheredAmount} 반납 완료.");
#endif
                }

                _targetNode?.ReleaseReservation();
                _targetNode      = null;
                _gatheredAmount  = 0;
                _isReturning     = false;
                _isInGatherCycle = false;
            }
        }

        /// <summary>
        /// Idle 상태 진입 시 호출.
        /// [GOAP 2단계] GoapAgent가 있으면 모든 행동 결정을 GoapAgent에 위임 — FSM은 대기만 한다.
        /// Legacy FSM은 채집 사이클 중간이 아닐 때만 다음 노드를 탐색한다.
        /// </summary>
        protected override void OnIdle()
        {
            // [GOAP 2단계] GoapAgent가 모든 결정을 담당 — FSM은 대기만
            if (_goapAgent != null) return;

            // Legacy FSM (GoapAgent 미부착 시 기존 동작 그대로)
            if (!_isInGatherCycle)
                SearchAndGo();
        }

        /// <summary>
        /// 경로 실패 시 예약을 해제하고 FSM을 초기화한 뒤 재탐색한다.
        /// Fleeing 상태의 경로 실패는 base.OnPathFailed()에서 직선 폴백으로 처리된다.
        /// [GOAP 2단계] GoapAgent 있으면 NotifyPathFailed()로 즉시 재플래닝한다.
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

            // 이미 예약된 Invoke가 있을 수 있으므로 취소
            CancelInvoke(nameof(SearchAndGo));

            if (_goapAgent != null)
            {
                // [GOAP 2단계] GoapAgent가 재플래닝 담당 — Invoke(SearchAndGo) 불필요
                _goapAgent.NotifyPathFailed();
                return;
            }

            // Legacy FSM: 지연 후 재탐색
            Invoke(nameof(SearchAndGo), _retryDelay);
        }

        /// <summary>
        /// 위협 감지 시 호출 (GameManager.CheckThreatForAllUnits → OnThreatDetected 경유).
        /// 위협 강도를 평가하여 CombatAlertQueue에 경보를 등록한 뒤 기본 도주 동작을 실행한다.
        /// </summary>
        public override void OnThreatDetected(Monster monster)
        {
            _goapAgent?.ForceReplanOnThreat(); // [GOAP 1단계] 즉시 재플래닝 (Tick 주기 우회)

            if (monster != null)
            {
                AIVillage.Core.ThreatLevel level = EvaluateThreatLevel(monster);
                AIVillage.Core.CombatAlertQueue.Instance?.EnqueueAlert(monster, level, transform.position);
            }
            base.OnThreatDetected(monster); // SetFleeing() 호출
        }

        /// <summary>Monster HP 기준으로 위협 강도를 4단계로 평가한다.</summary>
        private AIVillage.Core.ThreatLevel EvaluateThreatLevel(Monster monster)
        {
            float hp = monster.Hp;
            if (hp >= 40f) return AIVillage.Core.ThreatLevel.AllNeeded;
            if (hp >= 25f) return AIVillage.Core.ThreatLevel.VeryStrong;
            if (hp >= 15f) return AIVillage.Core.ThreatLevel.Strong;
            return AIVillage.Core.ThreatLevel.Weak;
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

#if UNITY_EDITOR
            Debug.Log($"[Gatherer] '{name}' — OnFleeingEnter: 채집 상태 정리 완료.");
#endif
        }

        /// <summary>
        /// 기지 안전 구역 도달 후 Fleeing에서 복귀할 때 호출 (AIUnit.UpdateFleeing 경유).
        /// [GOAP 2단계] GoapAgent 있으면 NotifyFleeingExit()로 즉시 재플래닝 요청.
        /// Legacy: SearchAndGo()를 호출하여 채집 사이클을 재개한다.
        /// </summary>
        protected override void OnFleeingExit()
        {
#if UNITY_EDITOR
            Debug.Log($"[Gatherer] '{name}' — OnFleeingExit: 채집 재개.");
#endif
            if (_goapAgent != null)
            {
                _goapAgent.NotifyFleeingExit();
                return;
            }
            SearchAndGo();
        }

        #endregion

        #region ── FSM Logic ──

        /// <summary>
        /// Week 9 개선: 전체 후보 노드를 순회하여 DangerRegistry 및 ThreatManager 이중 필터를
        /// 통과한 노드 중 가장 가까운 노드를 선택하고 이동을 시작한다.
        ///
        /// 선택 알고리즘:
        ///   1. 내 현재 위치 주변 직접 위협 체크 (출발 자체를 막는 가드)
        ///   2. GetAvailableNodes()로 전체 후보 취득
        ///   3. 각 노드에 대해:
        ///      a. DangerRegistry 위험 구역 필터 (과거 위협 좌표 회피)
        ///      b. ThreatManager 직접 위협 필터 (현재 몬스터 위치 회피)
        ///   4. 필터 통과 노드 중 거리 최솟값으로 bestNode 결정
        ///   5. 예약 성공 시 이동 시작
        /// </summary>
        private void SearchAndGo()
        {
            // ── 가드 1: GameManager 참조 ──
            if (GameManager.Instance == null) return;

            // ── 가드 2: Fleeing 상태 ──
            // Invoke 지연으로 인해 OnFleeingEnter 이후에도 이 메서드가 호출될 수 있음
            if (_currentState == UnitState.Fleeing) return;

            // [PR Fix P-004] GOAP 경로에서 현재 Action이 SearchNodeAction이 아니면 Invoke 발화를 무시.
            // Goal이 바뀐 뒤 지연 발화된 Invoke가 잘못된 노드 탐색을 시작하는 것을 방지한다.
            if (_goapAgent != null && !(_goapAgent.CurrentAction is SearchNodeAction))
                return;

            // ── 로컬 매니저 참조 캐싱 ──
            ThreatManager  tm = GameManager.Instance.ThreatManager;
            DangerRegistry dr = GameManager.Instance.DangerRegistry; // null 가능 (구형 씬 대비)

            // ── 가드 3: 내 현재 위치 주변 직접 위협 ──
            // 기지까지 쫓아온 몬스터가 바로 옆에 있는 상태에서 출발하는 루프를 방지
            if (tm != null && tm.GetNearestMonster(transform.position, _safeNodeRadius) != null)
            {
                Invoke(nameof(SearchAndGo), _retryDelay);
                return;
            }

            // ── 후보 노드 전체 취득 (Week 9 변경 핵심) ──
            // GetNearestAvailableNode() 대신 GetAvailableNodes()를 사용하여
            // 모든 후보를 직접 평가한다.
            IReadOnlyList<ResourceNode> candidates =
                GameManager.Instance.ResourceManager.GetAvailableNodes();

            if (candidates.Count == 0)
            {
                Invoke(nameof(SearchAndGo), _retryDelay);
                return;
            }

            // ── 후보 필터링 + 최근접 노드 선택 (우선순위 2-패스) ──
            // 1패스: GameManager.PreferredGatherType 노드 우선 탐색
            // 2패스: 선호 타입 노드가 없으면 임의 타입으로 폴백
            ResourceType preferred = GameManager.Instance?.PreferredGatherType ?? ResourceType.WOOD;
            ResourceNode bestNode  = null;
            float        bestDist  = float.MaxValue;

            // ── 1패스: 선호 자원 타입 ──
            foreach (ResourceNode candidate in candidates)
            {
                if (candidate == null) continue;
                if (candidate.GetResourceType() != preferred) continue;

                Vector2 nodePos = candidate.transform.position;
                if (dr != null && dr.IsDangerousArea(nodePos, _safeNodeRadius)) continue;
                if (tm != null && tm.GetNearestMonster(nodePos, _safeNodeRadius) != null) continue;

                float dist = Vector2.Distance(transform.position, nodePos);
                if (dist < bestDist) { bestDist = dist; bestNode = candidate; }
            }

            // ── 2패스: 선호 타입 없으면 임의 타입 ──
            if (bestNode == null)
            {
                bestDist = float.MaxValue;
                foreach (ResourceNode candidate in candidates)
                {
                    if (candidate == null) continue;

                    Vector2 nodePos = candidate.transform.position;
                    if (dr != null && dr.IsDangerousArea(nodePos, _safeNodeRadius)) continue;
                    if (tm != null && tm.GetNearestMonster(nodePos, _safeNodeRadius) != null) continue;

                    float dist = Vector2.Distance(transform.position, nodePos);
                    if (dist < bestDist) { bestDist = dist; bestNode = candidate; }
                }
            }

            // ── 가드 4: 안전한 노드가 없음 ──
            if (bestNode == null)
            {
#if UNITY_EDITOR
                Debug.Log($"[Gatherer] '{name}' — 모든 노드가 위험 구역. {_retryDelay}초 후 재탐색.");
#endif
                Invoke(nameof(SearchAndGo), _retryDelay);
                return;
            }

            // ── 예약 시도 ──
            if (!bestNode.TryReserve(gameObject))
            {
                // 멀티 Gatherer 예약 경합 — 짧게 재시도
                Invoke(nameof(SearchAndGo), 0.5f);
                return;
            }

            // ── 예약 성공 → 이동 시작 ──
            _targetNode      = bestNode;
            _isReturning     = false;
            _isInGatherCycle = true;

#if UNITY_EDITOR
            Debug.Log($"[Gatherer] '{name}' — {bestNode.GetResourceType()} 노드 '{bestNode.name}' 예약. 이동 시작. (dist: {bestDist:F1})");
#endif
            SetDestination(bestNode.transform.position);
        }

        /// <summary>
        /// 채집 타이머 대기 후 자원을 수집한다.
        /// enabled=false 상태(Idle/Gathering)에서도 코루틴은 계속 실행된다.
        ///
        /// [GOAP 2단계] 채집만 담당. 귀환은 ReturnToBaseAction.Execute() → StartReturnToBase()가 처리.
        /// Legacy(GoapAgent 없음): 채집 완료 후 귀환 이동까지 처리 (기존 동작 유지).
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

                // [GOAP 2단계] 다음 Tick에서 GoapAgent가 노드 없음(IsNodeReserved=false) 감지 → Replan
                if (_goapAgent == null)
                    Invoke(nameof(SearchAndGo), _retryDelay);
                yield break;
            }

            _gatheredAmount  = gathered;
            _gatherCoroutine = null;

#if UNITY_EDITOR
            Debug.Log($"[Gatherer] '{name}' — {_targetNode.GetResourceType()} {gathered} 채집 완료.");
#endif

            // [GOAP 2단계] 귀환은 ReturnToBaseAction.Execute() → StartReturnToBase()가 담당.
            // 다음 Tick에서 IsInventoryFull=true → GatherAction.IsComplete → TryAdvanceAction → ReturnToBaseAction.Execute().
            // Legacy: GoapAgent 없으면 기존대로 귀환 이동 시작.
            if (_goapAgent == null)
            {
                _isReturning = true;
                Vector2 home = GameManager.Instance.BasePosition;
                SetDestination(new Vector3(home.x, home.y, 0f));
#if UNITY_EDITOR
                Debug.Log($"[Gatherer] '{name}' — 기지로 귀환 (Legacy FSM).");
#endif
            }
            // GOAP 경로: GoapAgent의 다음 Tick이 IsInventoryFull을 감지하여 ReturnToBaseAction을 실행함
        }

        #endregion

        #region ── GOAP 1단계: GoapAgent 전용 internal getter ──

        // GoapAgent.UpdateWorldState()에서 Gatherer 내부 상태를 읽기 위한 접근자.
        // internal: 같은 어셈블리(AIVillage)에서만 접근 가능 — 외부 노출 최소화.

        /// <summary>현재 채집한 자원 수량 (GoapAgent용).</summary>
        internal int GatheredAmount => _gatheredAmount;

        /// <summary>1회 채집 한도 (GoapAgent용). IsInventoryFull 판정에 사용.</summary>
        internal int GatherAmountPerTrip => _gatherAmountPerTrip;

        /// <summary>안전 노드 선택 반경 (GoapAgent용). 위협 필터링 거리 기준.</summary>
        internal float SafeNodeRadius => _safeNodeRadius;

        /// <summary>현재 예약된 목표 노드 (GoapAgent용). null이면 미예약 상태.</summary>
        internal AIVillage.Resources.ResourceNode TargetNode => _targetNode;

        /// <summary>현재 도주 중 여부 (GoapAgent용). IsFleeing WorldState 키에 반영.</summary>
        internal bool IsFleeing => _currentState == UnitState.Fleeing;

        #endregion

        #region ── GOAP 2단계: GoapAction 전용 실행 메서드 ──

        /// <summary>
        /// SearchNodeAction.Execute()가 호출.
        /// 안전 노드 탐색 + 예약 + 이동을 시작한다.
        /// SearchAndGo()는 private을 유지 — 이 메서드가 delegate wrapper 역할.
        /// </summary>
        internal void ExecuteSearch() => SearchAndGo();

        /// <summary>
        /// GoapAgent.NotifyArrival()이 호출.
        /// 예약된 노드에서 채집 코루틴을 시작한다.
        /// GatherAction.Execute()는 no-op이므로 GoapAgent가 이 메서드를 직접 호출한다 (설계 결정 2).
        /// </summary>
        internal void StartGatherCoroutine()
        {
            if (_targetNode == null)
            {
                Debug.LogWarning($"[Gatherer] '{name}' StartGatherCoroutine — _targetNode가 null입니다. 채집을 시작할 수 없습니다.");
                return;
            }
            // 중복 코루틴 방지: 기존 코루틴이 있으면 먼저 중단
            if (_gatherCoroutine != null)
            {
                StopCoroutine(_gatherCoroutine);
                _gatherCoroutine = null;
            }
            _gatherCoroutine = StartCoroutine(GatherRoutine());
        }

        /// <summary>
        /// ReturnToBaseAction.Execute()가 호출.
        /// 귀환 플래그를 세우고 기지로 이동을 시작한다.
        /// </summary>
        internal void StartReturnToBase()
        {
            GameManager gm = GameManager.Instance;
            if (gm == null)
            {
                Debug.LogWarning($"[Gatherer] '{name}' StartReturnToBase — GameManager.Instance가 null입니다.");
                return;
            }
            _isReturning = true;
            // Warehouse가 완공돼 있으면 창고로, 아직 없으면 BasePosition(House)으로 귀환
            Vector2 target = gm.HasWarehouse ? gm.WarehousePosition : gm.BasePosition;
            SetDestination(new Vector3(target.x, target.y, 0f));
#if UNITY_EDITOR
            string dest = gm.HasWarehouse ? "Warehouse" : "BasePosition";
            Debug.Log($"[Gatherer] '{name}' — ReturnToBase 시작 (GOAP) → {dest} {target}");
#endif
        }

        #endregion

#if UNITY_EDITOR
        #region ── Editor Gizmos ──

        /// <summary>
        /// Scene 뷰에서 선택 시 Gatherer의 범위와 상태를 시각적으로 표시한다.
        ///
        /// 표시 항목:
        ///   1. 상태 마커 (중심 점): 상태별 색상 (회색=Idle, 노랑=이동, 초록=채집, 하늘=귀환, 빨강=도주)
        ///   2. 위험 회피 반경 (주황 원): 이 반경 내 Monster가 있으면 출발하지 않는다
        ///   3. 목표 노드 선: 현재 이동 중인 노드까지 직선 표시
        ///   4. 목표 노드 위험 반경 (주황 원): 노드 주변에도 동일 반경으로 안전 체크
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            // ── 1. 현재 상태 색상 마커 ──
            Color stateColor;
            if (_currentState == UnitState.Fleeing)
                stateColor = new Color(1f, 0.2f, 0.2f, 1f);   // 빨강: 도주
            else if (_isReturning)
                stateColor = new Color(0.2f, 0.9f, 1f, 1f);   // 하늘: 귀환
            else if (_currentState == UnitState.Moving)
                stateColor = new Color(1f, 0.95f, 0.1f, 1f);  // 노랑: 이동 중
            else if (_isInGatherCycle)
                stateColor = new Color(0.2f, 1f, 0.3f, 1f);   // 초록: 채집 중
            else
                stateColor = new Color(0.6f, 0.6f, 0.6f, 1f); // 회색: 대기

            Gizmos.color = stateColor;
            Gizmos.DrawSphere(transform.position, 0.18f);

            // ── 2. 내 위치 기준 위험 회피 반경 ──
            // SearchAndGo()에서 출발 전 이 반경 내 Monster 유무를 체크한다
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.12f); // 주황 채움
            Gizmos.DrawSphere(transform.position, _safeNodeRadius);
            Gizmos.color = new Color(1f, 0.6f, 0f, 0.85f); // 주황 테두리
            Gizmos.DrawWireSphere(transform.position, _safeNodeRadius);

            // ── 3. 목표 노드 표시 ──
            if (_targetNode != null)
            {
                // Gatherer → 목표 노드 연결선
                Gizmos.color = stateColor;
                Gizmos.DrawLine(transform.position, _targetNode.transform.position);

                // 목표 노드 주변 위험 회피 반경
                // SearchAndGo()에서 노드 선택 전 이 반경 내 Monster 유무를 체크한다
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.06f); // 주황 채움 (연하게)
                Gizmos.DrawSphere(_targetNode.transform.position, _safeNodeRadius);
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.5f);  // 주황 테두리
                Gizmos.DrawWireSphere(_targetNode.transform.position, _safeNodeRadius);
            }
        }

        #endregion
#endif
    }
}

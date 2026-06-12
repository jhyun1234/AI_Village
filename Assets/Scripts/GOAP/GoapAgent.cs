// =============================================================================
// GoapAgent.cs
// 역할(Role)  : Gatherer에 붙이는 GOAP 의사결정 컴포넌트.
//               매 game.tick 이벤트마다 WorldState를 갱신하고 GoapPlanner를 호출하여
//               CurrentGoalType을 결정한다. Gatherer FSM은 이 값을 읽어 행동을 조정한다.
// 사용법(Usage): GoapAgent를 Gatherer와 동일한 GameObject에 추가한다.
//               GoapAgent 미부착 시 Gatherer는 기존 FSM 동작 그대로 유지된다 (Regression 없음).
// 의존성(Dependencies): Gatherer, GameManager, ThreatManager, DangerRegistry,
//                      ResourceManager, WorldState, GoapGoal, GoapAction, GoapPlanner
//
// Author: Senior Unity Programmer
// Last Updated: 2026-06-10
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using AIVillage.Units;
using AIVillage.Core;

namespace AIVillage.GOAP
{
    /// <summary>
    /// Gatherer의 GOAP 의사결정 컴포넌트 (2단계).
    ///
    /// 아키텍처:
    ///   - GoapAgent는 Gatherer의 "두뇌" 역할을 한다.
    ///   - [2단계] GoapAction.Execute()가 Gatherer 메서드를 직접 호출하여 행동을 개시한다.
    ///   - TryAdvanceAction()이 Tick마다 현재 Action 완료를 감지하여 다음 Action으로 진행한다.
    ///   - NotifyArrival()이 Gatherer 도착 신호를 받아 SearchNode → Gather 전환을 처리한다.
    ///   - GoapAgent가 없으면 Gatherer는 기존 FSM 로직 그대로 동작한다 (Regression 없음).
    ///
    /// GC-free 설계 원칙:
    ///   - Start()에서 모든 배열/리스트를 1회 할당한다.
    ///   - Tick 루프(OnTick)에서 new 할당 없음.
    ///   - WorldState는 bool[] 공유 참조 구조체로 복사 없이 접근한다.
    /// </summary>
    [RequireComponent(typeof(Gatherer))]
    public sealed class GoapAgent : MonoBehaviour
    {
        #region ── Public Output ──

        /// <summary>
        /// Gatherer FSM이 읽는 유일한 출력.
        /// GoapPlanner.Plan()의 결과가 여기에 기록된다.
        /// GoapAgent가 없거나 Replan 전에는 None 상태.
        /// </summary>
        public GoapGoalType CurrentGoalType { get; private set; } = GoapGoalType.None;

        // [GOAP 2단계] 현재 실행 중인 Action 인덱스 (-1 = 미활성)
        private int _currentActionIndex = -1;

        /// <summary>
        /// 현재 실행 중인 Action (외부 조회용 — AI 정보 패널용).
        /// 플랜이 없거나 인덱스가 범위를 벗어나면 null 반환.
        /// </summary>
        public GoapAction CurrentAction =>
            (_currentActionIndex >= 0 && _currentActionIndex < _planBuffer.Count)
            ? _planBuffer[_currentActionIndex] : null;

        #endregion

        #region ── GC-free 버퍼 필드 (Start에서 1회 할당) ──

        // WorldState 내부 배열 — WorldState 구조체가 이 배열을 직접 참조한다
        private bool[] _stateData;

        // 이전 Tick의 상태 스냅샷 — 변화 감지(diff)에 사용
        private bool[] _prevStateData;

        // GoapPlanner DFS 깊이별 전용 시뮬레이션 버퍼 [MAX_DEPTH][WorldStateKey.Count]
        // 각 재귀 깊이가 독립 배열을 사용하므로 state._data 오염 없이 올바른 복사가 보장된다.
        private bool[][] _depthBuffers;

        // 플래닝 결과 Action 시퀀스 — Clear() 후 재사용 (GC-free)
        private List<GoapAction> _planBuffer;

        // WorldState 구조체 (bool[] 공유 참조)
        private WorldState _worldState;

        // 등록된 Goal 배열 (Priority 오름차순 정렬)
        private GoapGoal[] _goals;

        // 등록된 Action 배열
        private GoapAction[] _actions;

        #endregion

        #region ── 컴포넌트 참조 (Start에서 캐싱) ──

        private Gatherer    _gatherer;
        private GameManager _gm;

        #endregion

        #region ── Unity Lifecycle ──

        /// <summary>
        /// Start: 컴포넌트 캐싱, GC-free 버퍼 1회 할당, MessageBus 구독.
        /// Awake 대신 Start를 사용하는 이유: GameManager.MessageBus가
        /// GameManager.Awake()에서 생성되므로 Start 시점에 안전하게 접근 가능.
        /// </summary>
        private void Start()
        {
            // ── 컴포넌트 캐싱 ──
            _gatherer = GetComponent<Gatherer>();
            _gm       = GameManager.Instance;

            if (_gatherer == null)
            {
                Debug.LogError("[GoapAgent] Gatherer 컴포넌트를 찾지 못했습니다. GoapAgent를 비활성화합니다.");
                enabled = false;
                return;
            }

            if (_gm == null)
            {
                Debug.LogWarning("[GoapAgent] GameManager.Instance가 null입니다. GoapAgent가 제한적으로 동작합니다.");
            }

            // ── GC-free 버퍼 1회 할당 ──
            int keyCount   = (int)WorldStateKey.Count;
            _stateData     = new bool[keyCount];
            _prevStateData = new bool[keyCount];
            _planBuffer    = new List<GoapAction>(8); // 초기 용량 8 (Action 수의 여유)

            // 깊이별 전용 시뮬레이션 버퍼: GoapPlanner가 각 재귀 깊이에서 독립 배열을 사용한다.
            _depthBuffers = new bool[GoapPlanner.MAX_DEPTH][];
            for (int d = 0; d < GoapPlanner.MAX_DEPTH; d++)
                _depthBuffers[d] = new bool[keyCount];

            // WorldState 구조체: _stateData 배열을 공유 참조
            _worldState = new WorldState(_stateData);

            // ── Goal / Action 초기화 ──
            InitializeGoalsAndActions();

            // ── MessageBus 구독: game.tick 이벤트 수신 ──
            // Tick마다 WorldState 갱신 + 재플래닝을 수행한다
            _gm?.MessageBus?.Subscribe("game.tick", OnTick);
        }

        /// <summary>OnDestroy: MessageBus 구독 해제 (메모리 누수 방지).</summary>
        private void OnDestroy()
        {
            _gm?.MessageBus?.Unsubscribe("game.tick", OnTick);
        }

        #endregion

        #region ── Tick 처리 ──

        /// <summary>
        /// game.tick 이벤트 핸들러.
        /// WorldState를 갱신하고 현재 Action 완료 여부를 먼저 체크한다.
        /// Action 완료가 없으면 상태 변화를 감지하여 재플래닝을 수행한다.
        /// </summary>
        private void OnTick(object _)
        {
            // null 가드: Gatherer가 파괴됐거나 GoapAgent가 비활성화된 경우
            if (_gatherer == null)
            {
                Debug.LogWarning("[GoapAgent] OnTick — _gatherer가 null입니다. Tick을 건너뜁니다.");
                return;
            }

            // ── WorldState 갱신 ──
            UpdateWorldState();

            // ── [GOAP 2단계] 현재 Action 완료 체크 (SearchNodeAction은 제외 — NotifyArrival 경로) ──
            // TryAdvanceAction이 true를 반환하면 다음 Action 실행이 완료된 것 — 변화 감지 루프 스킵
            if (TryAdvanceAction())
            {
                // Execute() 직후 WorldState를 재반영: Execute가 즉각 변화시킨 상태(IsNodeReserved 등)를
                // prevState에 담아 다음 Tick에서 false "changed" 감지로 인한 불필요한 Replan을 방지한다.
                UpdateWorldState();
                _worldState.CopyTo(_prevStateData);
                return;
            }

            // ── 변화 감지: 이전 스냅샷과 비교 ──
            bool changed = false;
            for (int i = 0; i < (int)WorldStateKey.Count; i++)
            {
                if (_stateData[i] != _prevStateData[i])
                {
                    changed = true;
                    break;
                }
            }

            if (changed)
            {
                // 활성 플랜 실행 중에는 상태 변화(IsAtBase, HasAvailableNode 등)로 인한
                // 불필요한 Replan을 방지한다.
                //
                // 근본 원인:
                //   - Gatherer가 기지에서 이동하면 IsAtBase: true→false 변화 발생
                //   - 노드 예약 시 ResourceManager.GetAvailableNodes()에서 제외되어 HasAvailableNode: true→false 변화 발생
                //   - 이 변화들이 Replan을 발동시켜 플랜을 [GatherAction] 단독 또는 WaitForResource로 교체함
                //   - GatherAction.Execute()는 no-op이므로 채집 코루틴이 영원히 시작되지 않음
                //
                // 해결: 플랜이 실행 중일 때는 TryAdvanceAction + 명시적 Notify 메서드만이 Replan을 유발한다.
                // 플랜이 없을 때(초기 상태, 플랜 소진 후)만 changed detection Replan을 허용한다.
                bool planActive = _currentActionIndex >= 0 && _currentActionIndex < _planBuffer.Count;
                if (!planActive)
                {
                    Replan();
                    UpdateWorldState();
                    _worldState.CopyTo(_prevStateData);
                }
            }
        }

        #endregion

        #region ── WorldState 갱신 ──

        /// <summary>
        /// Gatherer와 GameManager의 현재 런타임 상태를 WorldState에 반영한다.
        ///
        /// 갱신 항목:
        ///   HasThreat:        주변 _safeNodeRadius 이내에 몬스터가 있는가
        ///   IsInventoryFull:  수집량 >= 1회 채집 한도
        ///   IsInventoryEmpty: 수집량 == 0
        ///   IsNodeReserved:   현재 목표 노드 예약 여부
        ///   IsAtBase:         기지 안전 구역 내 위치 여부
        ///   IsFleeing:        Gatherer.IsFleeing 상태
        ///   HasAvailableNode: DangerRegistry + ThreatManager 이중 필터 통과 노드 존재 여부
        /// </summary>
        private void UpdateWorldState()
        {
            // 모든 상태를 false로 초기화 (오래된 상태 제거)
            _worldState.Reset();

            ThreatManager  tm = _gm?.ThreatManager;
            DangerRegistry dr = _gm?.DangerRegistry;

            // ── HasThreat: 주변 위협 감지 ──
            _worldState.Set(WorldStateKey.HasThreat,
                tm != null && tm.GetNearestMonster(transform.position, _gatherer.SafeNodeRadius) != null);

            // ── IsInventoryFull: 인벤토리 가득 참 ──
            _worldState.Set(WorldStateKey.IsInventoryFull,
                _gatherer.GatheredAmount >= _gatherer.GatherAmountPerTrip);

            // ── IsInventoryEmpty: 인벤토리 비어있음 ──
            _worldState.Set(WorldStateKey.IsInventoryEmpty,
                _gatherer.GatheredAmount == 0);

            // ── IsNodeReserved: 노드 예약 완료 ──
            _worldState.Set(WorldStateKey.IsNodeReserved,
                _gatherer.TargetNode != null);

            // ── IsAtBase: 납품 지점 도착 여부 ──
            // Warehouse 완공 후 → Warehouse 반경 내 여부
            // Warehouse 미완공  → House 안전 구역 내 여부 (폴백)
            bool isAtBase = _gm != null &&
                (_gm.HasWarehouse
                    ? _gm.IsAtWarehouse(transform.position)
                    : _gm.IsInSafeZone(transform.position));
            _worldState.Set(WorldStateKey.IsAtBase, isAtBase);

            // ── IsFleeing: 도주 중 ──
            _worldState.Set(WorldStateKey.IsFleeing, _gatherer.IsFleeing);

            // ── HasAvailableNode: 안전한 자원 노드 존재 여부 ──
            // DangerRegistry + ThreatManager 이중 필터를 통과한 노드가 1개라도 있으면 true
            bool hasNode = false;
            if (_gm?.ResourceManager != null)
            {
                var nodes = _gm.ResourceManager.GetAvailableNodes();
                for (int i = 0; i < nodes.Count; i++)
                {
                    var n = nodes[i];
                    if (n == null) continue;

                    Vector2 nodePos = n.transform.position;

                    // 필터 1: DangerRegistry 위험 구역 회피
                    if (dr != null && dr.IsDangerousArea(nodePos, _gatherer.SafeNodeRadius)) continue;

                    // 필터 2: ThreatManager 직접 위협 회피
                    if (tm != null && tm.GetNearestMonster(nodePos, _gatherer.SafeNodeRadius) != null) continue;

                    // 두 필터를 통과한 안전한 노드 발견
                    hasNode = true;
                    break;
                }
            }
            _worldState.Set(WorldStateKey.HasAvailableNode, hasNode);
        }

        #endregion

        #region ── 플래닝 ──

        /// <summary>
        /// GoapPlanner.Plan()을 호출하여 최적 Goal을 계산하고 CurrentGoalType을 갱신한다.
        /// [GOAP 2단계] Goal이 변경됐거나 Action 인덱스가 초기화됐을 때 플랜 첫 Action을 Execute한다.
        /// </summary>
        private void Replan()
        {
            GoapGoalType newGoal = GoapPlanner.Plan(
                _worldState,
                _goals,
                _actions,
                _planBuffer,
                _depthBuffers);

            bool goalChanged = (newGoal != CurrentGoalType);

#if UNITY_EDITOR
            if (goalChanged)
                Debug.Log($"[GoapAgent] '{name}' Goal 변경: {CurrentGoalType} → {newGoal}");
#endif

            CurrentGoalType = newGoal;

            // [PR Fix P-001] 플랜 소진(_currentActionIndex >= Count) 조건 추가:
            // 동일 Goal 사이클 반복 시 첫 Action Execute()가 호출되지 않아 영구 정지하는 버그 수정.
            // [GOAP 2단계] Goal 변경 시, Action 인덱스 미활성 시, 또는 플랜 소진 시 플랜 실행 재시작
            if (goalChanged || _currentActionIndex < 0 || _currentActionIndex >= _planBuffer.Count)
            {
                _currentActionIndex = 0;
                if (_planBuffer.Count > 0)
                    _planBuffer[0].Execute();
            }
        }

        /// <summary>
        /// OnThreatDetected에서 호출 — Tick 주기를 우회하여 즉시 재플래닝한다.
        /// 위협 감지는 즉각적인 반응이 필요하므로 다음 Tick을 기다리지 않는다.
        /// </summary>
        public void ForceReplanOnThreat()
        {
            UpdateWorldState();
            Replan();
            // 스냅샷 즉시 갱신 (ForceReplan 후 다음 Tick에서 중복 재플래닝 방지)
            _worldState.CopyTo(_prevStateData);
        }

        #endregion

        #region ── GOAP 2단계: Action 진행 및 브릿지 메서드 ──

        /// <summary>
        /// 현재 Action 완료 여부를 감지하여 다음 Action으로 진행한다.
        /// SearchNodeAction은 조기 완료 리스크가 있으므로 여기서 제외한다.
        /// (SearchNode → Gather 전환은 오직 NotifyArrival() 경로에서만 발생)
        ///
        /// 반환값: true면 Action이 진행됐으므로 호출자는 변화 감지 루프를 스킵한다.
        /// </summary>
        private bool TryAdvanceAction()
        {
            if (_currentActionIndex < 0 || _currentActionIndex >= _planBuffer.Count) return false;

            GoapAction current = _planBuffer[_currentActionIndex];

            // SearchNodeAction: 조기 완료 리스크 → NotifyArrival만으로 전환 (설계 결정 4)
            if (current is SearchNodeAction) return false;

            if (!current.IsComplete(_worldState)) return false;

#if UNITY_EDITOR
            Debug.Log($"[GoapAgent] '{name}' Action 완료: {current.Name}");
#endif

            _currentActionIndex++;

            if (_currentActionIndex < _planBuffer.Count)
                // 다음 Action 개시 (One-shot 신호)
                _planBuffer[_currentActionIndex].Execute();
            else
                // 플랜 소진 → 재플래닝으로 새 플랜을 수립
                Replan();

            return true;
        }

        /// <summary>
        /// Gatherer.OnReachDestination()에서 도착 신호를 전달받는다.
        ///
        /// 역할:
        ///   - SearchNodeAction 완료 시: GatherAction으로 전환하고 StartGatherCoroutine() 직접 호출.
        ///     (GatherAction.Execute()는 no-op이므로 GoapAgent가 직접 채집 코루틴을 시작한다)
        ///   - ReturnToBaseAction 도착 시: 자원 반납은 OnReachDestination에서 이미 완료됨.
        ///     IsAtBase + IsInventoryEmpty가 true가 되어 다음 Tick의 TryAdvanceAction이 처리한다.
        /// </summary>
        internal void NotifyArrival()
        {
            if (_currentActionIndex < 0 || _currentActionIndex >= _planBuffer.Count) return;

            GoapAction current = _planBuffer[_currentActionIndex];

            // SearchNodeAction → GatherAction 전환 (유일한 전환 경로 — 설계 결정 4)
            if (current is SearchNodeAction)
            {
                _currentActionIndex++;

                // [PR Fix P-002] bounds 초과 및 예상치 못한 Action 타입 방어:
                // 인덱스가 범위를 벗어나거나 다음 Action이 GatherAction이 아닌 경우
                // 플랜이 손상된 것으로 판단하여 안전하게 재플래닝한다.
                if (_currentActionIndex >= _planBuffer.Count ||
                    !(_planBuffer[_currentActionIndex] is GatherAction))
                {
                    Debug.LogWarning($"[GoapAgent] '{name}' NotifyArrival: GatherAction을 찾을 수 없습니다. Replan 실행.");
                    _currentActionIndex = -1;
                    Replan();
                    return;
                }

#if UNITY_EDITOR
                Debug.Log($"[GoapAgent] '{name}' NotifyArrival: SearchNode 완료 → Gather 시작");
#endif
                // GatherAction.Execute()는 no-op이므로 직접 채집 코루틴 시작 (설계 결정 2)
                if (_gatherer != null && _gatherer.TargetNode != null)
                    _gatherer.StartGatherCoroutine();
            }
            // ReturnToBaseAction 도착: 자원 반납은 Gatherer.OnReachDestination에서 처리 완료.
            // 다음 Tick에서 IsAtBase + IsInventoryEmpty → TryAdvanceAction이 완료 감지함.
        }

        /// <summary>
        /// Gatherer.OnFleeingExit()에서 호출 — 도주 해제 후 즉시 재플래닝한다.
        /// Tick 주기를 기다리지 않고 새 플랜을 즉시 수립한다.
        /// </summary>
        internal void NotifyFleeingExit()
        {
            UpdateWorldState();
            // -1로 리셋하여 Replan에서 goalChanged || _currentActionIndex < 0 조건 충족 보장
            _currentActionIndex = -1;
            Replan();
            _worldState.CopyTo(_prevStateData);
        }

        /// <summary>
        /// Gatherer.OnPathFailed()에서 호출 — 경로 실패 후 즉시 재플래닝한다.
        /// 상태 초기화는 Gatherer.OnPathFailed()에서 이미 수행됨.
        /// </summary>
        internal void NotifyPathFailed()
        {
            _currentActionIndex = -1;
            UpdateWorldState();
            Replan();
            _worldState.CopyTo(_prevStateData);
        }

        #endregion

        #region ── 초기화 ──

        /// <summary>
        /// Goal 배열과 Action 배열을 초기화한다.
        /// Goal은 Priority 오름차순 정렬이 보장된 순서로 등록한다.
        /// Action은 각각 Initialize(this)를 호출하여 Gatherer 참조를 주입한다.
        /// </summary>
        private void InitializeGoalsAndActions()
        {
            // Goal 등록: Priority 오름차순 (0=Flee, 1=Deposit, 2=Collect, 3=Wait)
            _goals = new GoapGoal[]
            {
                GoapGoal.CreateFlee(),             // Priority 0
                GoapGoal.CreateDepositResource(),  // Priority 1
                GoapGoal.CreateCollectResource(),  // Priority 2
                GoapGoal.CreateWaitForResource()   // Priority 3
            };

            // Action 등록
            // GatherAction을 SearchNodeAction 앞에 두어야 DFS가 [SearchNode, Gather] 2단 플랜을 반환한다.
            // SearchNodeAction이 앞에 오면 DFS가 depth 1에서 SearchNode를 재시도하여
            // [SearchNode, SearchNode, Gather] 3단 플랜을 반환하고 NotifyArrival에서 GatherAction을 찾지 못한다.
            _actions = new GoapAction[]
            {
                new FleeAction(),
                new GatherAction(),
                new SearchNodeAction(),
                new ReturnToBaseAction(),
                new WaitAction()
            };

            // 각 Action에 GoapAgent 참조 주입 (Gatherer 캐싱 포함)
            foreach (GoapAction action in _actions)
                action.Initialize(this);
        }

        #endregion

        #region ── Internal Gatherer Accessor (Actions 전용) ──

        /// <summary>
        /// FleeAction 등 GOAP Action 클래스가 Gatherer에 접근하기 위한 내부 프로퍼티.
        /// GoapAgent와 같은 어셈블리 내에서만 접근 가능 (internal).
        /// </summary>
        internal Gatherer Gatherer => _gatherer;

        #endregion
    }
}

// =============================================================================
// SearchNodeAction.cs
// 역할(Role)  : 가장 가까운 안전한 자원 노드를 탐색하여 예약하는 GOAP Action.
//               CollectResource Goal의 첫 번째 단계.
// 사용법(Usage): GoapAgent.InitializeGoalsAndActions()에서 자동으로 등록된다.
//               Execute()는 1단계 위임 (FSM이 처리).
// 의존성(Dependencies): GoapAction, GoapAgent, WorldState, WorldStateKey
//
// Author: Senior Unity Programmer
// Last Updated: 2026-06-10
// =============================================================================

namespace AIVillage.GOAP
{
    /// <summary>
    /// 안전한 자원 노드를 탐색하고 예약하는 Action.
    ///
    /// Precondition:
    ///   IsInventoryEmpty = true   (인벤토리가 비어있어야 노드를 찾아 나선다)
    ///   HasAvailableNode = true   (안전한 노드가 존재해야 한다)
    ///   IsFleeing = false         (도주 중에는 노드를 탐색하지 않는다)
    ///
    /// Effect:
    ///   IsNodeReserved = true     (이 Action 완료 후 노드가 예약된 상태)
    ///
    /// IsComplete: IsNodeReserved == true (Gatherer.TargetNode != null)
    /// </summary>
    public sealed class SearchNodeAction : GoapAction
    {
        // ── Private Fields ──
        private GoapAgent _agent; // GoapAgent 참조 (Initialize에서 주입)

        /// <summary>
        /// 생성자: Precondition / Effect / Cost를 설정한다.
        /// 배열을 여기서 할당하고 이후 변경하지 않는다 (GC-free 유지).
        /// </summary>
        public SearchNodeAction()
        {
            Name = "SearchNodeAction";
            Cost = 1.0f; // 기획서 수치: SearchNode Cost 1.0f

            // ── Precondition 설정 ──
            PreconditionKeys = new WorldStateKey[]
            {
                WorldStateKey.IsInventoryEmpty,
                WorldStateKey.HasAvailableNode,
                WorldStateKey.IsFleeing
            };
            PreconditionValues = new bool[] { true, true, false };

            // ── Effect 설정 ──
            EffectKeys   = new WorldStateKey[] { WorldStateKey.IsNodeReserved };
            EffectValues = new bool[] { true };
        }

        /// <summary>GoapAgent 참조를 주입한다. 1단계에서는 별도 캐싱 불필요.</summary>
        public override void Initialize(GoapAgent agent)
        {
            _agent = agent;
        }

        /// <summary>사전조건 충족 여부를 반환한다.</summary>
        public override bool CanExecute(in WorldState state)
        {
            return ArePreconditionsMet(state);
        }

        /// <summary>
        /// [GOAP 2단계] Gatherer.ExecuteSearch()를 호출하여 안전 노드 탐색 + 예약 + 이동을 시작한다.
        /// Execute()는 One-shot 신호 — 실행 개시만 담당하고 완료 감지는 GoapAgent.NotifyArrival()이 처리한다.
        /// </summary>
        public override void Execute()
        {
            // [GOAP 2단계] Gatherer.ExecuteSearch()로 안전 노드 탐색 + 예약 + 이동 시작
            _agent.Gatherer.ExecuteSearch();
        }

        /// <summary>IsNodeReserved == true 이면 완료 (Gatherer.TargetNode가 예약됨).</summary>
        public override bool IsComplete(in WorldState state)
        {
            return state.Get(WorldStateKey.IsNodeReserved);
        }
    }
}

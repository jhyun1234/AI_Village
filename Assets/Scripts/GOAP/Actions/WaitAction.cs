// =============================================================================
// WaitAction.cs
// 역할(Role)  : 안전한 자원 노드가 없을 때 노드가 생길 때까지 기다리는 GOAP Action.
//               WaitForResource Goal의 단독 처리 Action.
//               Cost=3.0f — 모든 Action 중 가장 높아 최후 수단으로만 선택된다.
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
    /// 안전한 자원 노드가 없을 때 대기하는 Action.
    ///
    /// Precondition:
    ///   HasAvailableNode = false  (안전한 노드가 없어야 대기한다)
    ///   IsFleeing = false         (도주 중에는 대기하지 않는다)
    ///
    /// Effect:
    ///   HasAvailableNode = true   (대기 후 노드가 생성될 것이라는 낙관적 예측)
    ///                              실제 노드 생성은 ResourceNode 재생성 시스템이 담당한다.
    ///
    /// Cost: 3.0f — 가장 비싼 Action (최후 수단)
    ///
    /// IsComplete: HasAvailableNode == true
    ///             (노드가 재생성되거나 위험 구역이 해제되어 안전한 노드가 생긴 경우)
    /// </summary>
    public sealed class WaitAction : GoapAction
    {
        // ── Private Fields ──
        private GoapAgent _agent; // GoapAgent 참조 (Initialize에서 주입)

        /// <summary>
        /// 생성자: Precondition / Effect / Cost를 설정한다.
        /// Cost=3.0f: 가장 높은 비용 — 다른 모든 Option이 없을 때만 선택.
        /// </summary>
        public WaitAction()
        {
            Name = "WaitAction";
            Cost = 3.0f; // 기획서 수치: Wait Cost 3.0f (최후 수단)

            // ── Precondition 설정 ──
            PreconditionKeys = new WorldStateKey[]
            {
                WorldStateKey.HasAvailableNode,
                WorldStateKey.IsFleeing
            };
            PreconditionValues = new bool[] { false, false };

            // ── Effect 설정 ──
            // 낙관적 예측: 대기 완료 후 노드가 생성될 것이라고 가정
            // 실제 Effect는 ResourceNode 재생성 시스템이 담당한다
            EffectKeys   = new WorldStateKey[] { WorldStateKey.HasAvailableNode };
            EffectValues = new bool[] { true };
        }

        /// <summary>GoapAgent 참조를 주입한다.</summary>
        public override void Initialize(GoapAgent agent)
        {
            _agent = agent;
        }

        /// <summary>안전한 노드가 없고 도주 중이 아닐 때 실행 가능.</summary>
        public override bool CanExecute(in WorldState state)
        {
            return ArePreconditionsMet(state);
        }

        /// <summary>
        /// GOAP 1단계: 실제 대기는 Gatherer.OnIdle()에서 WaitForResource Goal 감지 후 처리한다.
        /// (SearchAndGo 호출을 건너뛰고 Idle 상태를 유지한다)
        /// 이 메서드는 위임(no-op).
        /// </summary>
        public override void Execute()
        {
            // [GOAP 1단계] FSM 위임 — Gatherer.OnIdle()이 WaitForResource Goal 시 대기를 처리한다.
        }

        /// <summary>안전한 노드가 하나라도 생기면 완료.</summary>
        public override bool IsComplete(in WorldState state)
        {
            return state.Get(WorldStateKey.HasAvailableNode);
        }
    }
}

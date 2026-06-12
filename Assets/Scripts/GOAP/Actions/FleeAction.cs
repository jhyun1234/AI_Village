// =============================================================================
// FleeAction.cs
// 역할(Role)  : 위협 감지 시 기지로 도주하는 GOAP Action.
//               Flee Goal의 단독 처리 Action. Priority=0 (최고 우선순위).
//               Cost=0.0f — 항상 다른 Action보다 먼저 선택된다.
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
    /// 위협 감지 시 기지로 도주하는 Action.
    ///
    /// Precondition:
    ///   HasThreat = true          (위협이 있어야 도주한다)
    ///
    /// Effect:
    ///   IsFleeing = true          (도주 상태로 전환됨)
    ///   IsAtBase = true           (도주 목적지는 기지)
    ///
    /// Cost: 0.0f — 위협 상황에서 가장 먼저 선택되도록 비용 0으로 설정
    ///
    /// IsComplete: IsFleeing == false AND IsAtBase == true
    ///             (도주 완료: 기지에 도착하고 도주 상태가 해제된 경우)
    /// </summary>
    public sealed class FleeAction : GoapAction
    {
        // ── Private Fields ──
        private GoapAgent _agent; // GoapAgent 참조 (Initialize에서 주입)

        /// <summary>
        /// 생성자: Precondition / Effect / Cost를 설정한다.
        /// Cost=0.0f: 위협 감지 시 항상 최우선 선택되도록 설정.
        /// </summary>
        public FleeAction()
        {
            Name = "FleeAction";
            Cost = 0.0f; // 기획서 수치: Flee Cost 0.0f (즉각 도주 최우선)

            // ── Precondition 설정 ──
            PreconditionKeys   = new WorldStateKey[] { WorldStateKey.HasThreat };
            PreconditionValues = new bool[] { true };

            // ── Effect 설정 ──
            EffectKeys = new WorldStateKey[]
            {
                WorldStateKey.IsFleeing,
                WorldStateKey.IsAtBase
            };
            EffectValues = new bool[] { true, true };
        }

        /// <summary>GoapAgent 참조를 주입한다.</summary>
        public override void Initialize(GoapAgent agent)
        {
            _agent = agent;
        }

        /// <summary>위협이 존재하면 이 Action을 실행할 수 있다.</summary>
        public override bool CanExecute(in WorldState state)
        {
            return ArePreconditionsMet(state);
        }

        /// <summary>
        /// GOAP 1단계: 실제 도주는 Gatherer FSM(SetFleeing → OnFleeingEnter)이 처리한다.
        /// 이 메서드는 위임(no-op).
        /// </summary>
        public override void Execute()
        {
            // [GOAP 1단계] FSM 위임 — AIUnit.SetFleeing()이 도주 처리를 담당한다.
        }

        /// <summary>
        /// 도주 중이 아니고 기지에 도착했으면 완료.
        /// (IsFleeing==false: AIUnit.UpdateFleeing에서 안전 구역 도달 후 자동 해제)
        /// </summary>
        public override bool IsComplete(in WorldState state)
        {
            return !state.Get(WorldStateKey.IsFleeing)
                && state.Get(WorldStateKey.IsAtBase);
        }
    }
}

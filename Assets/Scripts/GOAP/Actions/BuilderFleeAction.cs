// =============================================================================
// BuilderFleeAction.cs
// 역할  : Builder용 도주 Action. 위협 감지 시 SetFleeing()을 호출한다.
//         FleeAction(Gatherer)과 동일한 구조, BuilderGoapAgent 참조 사용.
// =============================================================================

namespace AIVillage.GOAP
{
    /// <summary>
    /// Builder 위협 감지 시 도주하는 Action.
    ///
    /// Precondition: HasThreat = true
    /// Effect:       IsFleeing = true
    /// </summary>
    public sealed class BuilderFleeAction : GoapAction
    {
        private BuilderGoapAgent _agent;

        public BuilderFleeAction()
        {
            Name = "BuilderFleeAction";
            Cost = 0f;

            PreconditionKeys   = new WorldStateKey[] { WorldStateKey.HasThreat };
            PreconditionValues = new bool[]          { true };

            EffectKeys   = new WorldStateKey[] { WorldStateKey.IsFleeing };
            EffectValues = new bool[]          { true };
        }

        public override void Initialize(GoapAgent agent)          { }
        public override void InitializeForBuilder(BuilderGoapAgent agent) { _agent = agent; }
        public override bool CanExecute(in WorldState state)      => ArePreconditionsMet(state);

        /// <summary>Execute는 no-op — SetFleeing은 CheckThreatForAllUnits가 이미 호출.</summary>
        public override void Execute() { }

        /// <summary>도주 상태가 되면 완료.</summary>
        public override bool IsComplete(in WorldState state)
            => state.Get(WorldStateKey.IsFleeing);
    }
}

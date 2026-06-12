// =============================================================================
// SearchBuildingAction.cs
// 역할  : 가장 가까운 대기 중인 건설지를 탐색하여 예약하고 이동하는 GOAP Action.
//         BuildBuilding Goal의 첫 번째 단계.
//         SearchNodeAction(Gatherer)과 동일한 구조.
// =============================================================================

namespace AIVillage.GOAP
{
    /// <summary>
    /// 건설 대기 중인 건물을 찾아 예약하고 이동하는 Action.
    ///
    /// Precondition:
    ///   HasPendingBuilding  = true   (건설 대기 건물이 존재해야 한다)
    ///   IsBuildingReserved  = false  (아직 건물을 예약하지 않은 상태)
    ///   IsFleeing           = false  (도주 중에는 탐색하지 않는다)
    ///
    /// Effect:
    ///   IsBuildingReserved  = true   (이 Action 완료 후 건물 예약 완료)
    ///
    /// IsComplete: NotifyArrival() 경로만 허용 (TryAdvanceAction 제외 — 조기 완료 리스크)
    /// </summary>
    public sealed class SearchBuildingAction : GoapAction
    {
        private BuilderGoapAgent _agent;

        public SearchBuildingAction()
        {
            Name = "SearchBuildingAction";
            Cost = 1f;

            PreconditionKeys = new WorldStateKey[]
            {
                WorldStateKey.HasPendingBuilding,
                WorldStateKey.IsBuildingReserved,
                WorldStateKey.IsFleeing
            };
            PreconditionValues = new bool[] { true, false, false };

            EffectKeys   = new WorldStateKey[] { WorldStateKey.IsBuildingReserved };
            EffectValues = new bool[]          { true };
        }

        public override void Initialize(GoapAgent agent)          { }
        public override void InitializeForBuilder(BuilderGoapAgent agent) { _agent = agent; }
        public override bool CanExecute(in WorldState state)      => ArePreconditionsMet(state);

        /// <summary>Builder.ExecuteSearchBuilding() → SearchAndBuild() 실행.</summary>
        public override void Execute()
        {
            _agent?.Builder.ExecuteSearchBuilding();
        }

        /// <summary>IsBuildingReserved=true이면 완료 (플래너 시뮬레이션용).</summary>
        public override bool IsComplete(in WorldState state)
            => state.Get(WorldStateKey.IsBuildingReserved);
    }
}

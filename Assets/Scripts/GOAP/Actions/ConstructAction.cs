// =============================================================================
// ConstructAction.cs
// 역할  : 예약된 건설지에서 건물을 완공하는 GOAP Action.
//         BuildBuilding Goal의 두 번째 단계 (SearchBuildingAction 이후).
//         Execute는 no-op — BuildRoutine 코루틴은 OnReachDestination에서 시작됨.
// =============================================================================

namespace AIVillage.GOAP
{
    /// <summary>
    /// 예약된 건설지에서 건물을 완공하는 Action.
    ///
    /// Precondition:
    ///   IsBuildingReserved = true    (건물이 예약되어 있어야 한다)
    ///   IsFleeing          = false   (도주 중에는 건설하지 않는다)
    ///
    /// Effect:
    ///   IsBuildingReserved  = false  (완공 후 예약 해제)
    ///   HasPendingBuilding  = false  (이 건물은 더 이상 대기 상태 아님)
    ///
    /// IsComplete: IsBuildingReserved == false
    ///             (_targetBuilding == null — BuildRoutine 완료 또는 포기)
    /// </summary>
    public sealed class ConstructAction : GoapAction
    {
        public ConstructAction()
        {
            Name = "ConstructAction";
            Cost = 2f;

            PreconditionKeys = new WorldStateKey[]
            {
                WorldStateKey.IsBuildingReserved,
                WorldStateKey.IsFleeing
            };
            PreconditionValues = new bool[] { true, false };

            EffectKeys = new WorldStateKey[]
            {
                WorldStateKey.IsBuildingReserved,
                WorldStateKey.HasPendingBuilding
            };
            EffectValues = new bool[] { false, false };
        }

        public override void Initialize(GoapAgent agent)          { }
        public override void InitializeForBuilder(BuilderGoapAgent agent) { }
        public override bool CanExecute(in WorldState state)      => ArePreconditionsMet(state);

        /// <summary>Execute는 no-op — BuildRoutine은 OnReachDestination에서 이미 시작됨.</summary>
        public override void Execute() { }

        /// <summary>IsBuildingReserved=false (_targetBuilding=null)이면 완료.</summary>
        public override bool IsComplete(in WorldState state)
            => !state.Get(WorldStateKey.IsBuildingReserved);
    }
}

// =============================================================================
// BuilderWaitAction.cs
// 역할  : 건설 대기 건물이 없을 때 건물이 생길 때까지 기다리는 GOAP Action.
//         WaitForBuilding Goal의 단독 처리 Action.
//         WaitAction(Gatherer)과 동일한 구조, HasPendingBuilding 키 사용.
// =============================================================================

namespace AIVillage.GOAP
{
    /// <summary>
    /// 건설 대기 건물이 없을 때 대기하는 Action.
    ///
    /// Precondition:
    ///   HasPendingBuilding = false  (건설 대기 건물이 없어야 대기한다)
    ///   IsFleeing          = false
    ///
    /// Effect:
    ///   HasPendingBuilding = true   (낙관적 예측: 대기 후 건물이 배치될 것)
    ///
    /// IsComplete: HasPendingBuilding == true (플레이어가 새 건물을 배치한 경우)
    /// </summary>
    public sealed class BuilderWaitAction : GoapAction
    {
        public BuilderWaitAction()
        {
            Name = "BuilderWaitAction";
            Cost = 3f;

            PreconditionKeys = new WorldStateKey[]
            {
                WorldStateKey.HasPendingBuilding,
                WorldStateKey.IsFleeing
            };
            PreconditionValues = new bool[] { false, false };

            EffectKeys   = new WorldStateKey[] { WorldStateKey.HasPendingBuilding };
            EffectValues = new bool[]          { true };
        }

        public override void Initialize(GoapAgent agent)          { }
        public override void InitializeForBuilder(BuilderGoapAgent agent) { }
        public override bool CanExecute(in WorldState state)      => ArePreconditionsMet(state);
        public override void Execute()                            { }
        public override bool IsComplete(in WorldState state)
            => state.Get(WorldStateKey.HasPendingBuilding);
    }
}

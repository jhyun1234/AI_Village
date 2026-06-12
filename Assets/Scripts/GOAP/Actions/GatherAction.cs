// =============================================================================
// GatherAction.cs
// 역할(Role)  : 예약된 자원 노드에서 자원을 채집하는 GOAP Action.
//               CollectResource Goal의 두 번째 단계 (SearchNodeAction 이후).
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
    /// 예약된 노드에서 자원을 채집하는 Action.
    ///
    /// Precondition:
    ///   IsNodeReserved = true     (노드가 이미 예약되어 있어야 한다)
    ///   IsInventoryEmpty = true   (인벤토리가 비어있어야 채집한다)
    ///   IsFleeing = false         (도주 중에는 채집하지 않는다)
    ///
    /// Effect:
    ///   IsInventoryFull = true    (채집 완료 후 인벤토리가 가득 참)
    ///   IsInventoryEmpty = false  (채집 완료 후 인벤토리가 비어있지 않음)
    ///
    /// IsComplete: IsInventoryFull == true OR IsNodeReserved == false
    ///             (채집 완료 또는 노드 소진/해제로 인한 중단)
    /// </summary>
    public sealed class GatherAction : GoapAction
    {
        // ── Private Fields ──
        private GoapAgent _agent; // GoapAgent 참조 (Initialize에서 주입)

        /// <summary>
        /// 생성자: Precondition / Effect / Cost를 설정한다.
        /// </summary>
        public GatherAction()
        {
            Name = "GatherAction";
            Cost = 2.0f; // 기획서 수치: Gather Cost 2.0f (시간이 걸리는 작업)

            // ── Precondition 설정 ──
            PreconditionKeys = new WorldStateKey[]
            {
                WorldStateKey.IsNodeReserved,
                WorldStateKey.IsInventoryEmpty,
                WorldStateKey.IsFleeing
            };
            PreconditionValues = new bool[] { true, true, false };

            // ── Effect 설정 ──
            EffectKeys = new WorldStateKey[]
            {
                WorldStateKey.IsInventoryFull,
                WorldStateKey.IsInventoryEmpty
            };
            EffectValues = new bool[] { true, false };
        }

        /// <summary>GoapAgent 참조를 주입한다.</summary>
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
        /// GOAP 1단계: 실제 채집은 Gatherer FSM(GatherRoutine)이 처리한다.
        /// 이 메서드는 위임(no-op).
        /// </summary>
        public override void Execute()
        {
            // [GOAP 1단계] FSM 위임 — Gatherer.GatherRoutine()이 채집을 처리한다.
        }

        /// <summary>
        /// 인벤토리가 가득 찼거나(채집 완료) 노드 예약이 해제됐으면(노드 소진) 완료.
        /// </summary>
        public override bool IsComplete(in WorldState state)
        {
            // 정상 완료: 채집 후 인벤토리 가득 참
            // 비정상 완료: 노드가 소진되어 예약 해제됨 (다음 사이클로 넘어감)
            return state.Get(WorldStateKey.IsInventoryFull)
                || !state.Get(WorldStateKey.IsNodeReserved);
        }
    }
}

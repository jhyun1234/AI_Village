// =============================================================================
// ReturnToBaseAction.cs
// 역할(Role)  : 채집한 자원을 들고 기지로 귀환하여 반납하는 GOAP Action.
//               DepositResource Goal의 단독 처리 Action.
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
    /// 채집 완료 후 기지로 귀환하여 자원을 반납하는 Action.
    ///
    /// Precondition:
    ///   IsInventoryFull = true    (인벤토리가 가득 차있어야 귀환한다)
    ///   IsFleeing = false         (도주 중에는 이 Action이 아닌 FleeAction이 처리한다)
    ///
    /// Effect:
    ///   IsInventoryEmpty = true   (반납 완료 후 인벤토리 비어있음)
    ///   IsInventoryFull = false   (반납 완료 후 인벤토리 더 이상 가득 차지 않음)
    ///   IsAtBase = true           (기지에 도착한 상태)
    ///
    /// IsComplete: IsAtBase == true AND IsInventoryEmpty == true
    ///             (기지에 도착하고 인벤토리를 모두 반납한 경우)
    /// </summary>
    public sealed class ReturnToBaseAction : GoapAction
    {
        // ── Private Fields ──
        private GoapAgent _agent; // GoapAgent 참조 (Initialize에서 주입)

        /// <summary>
        /// 생성자: Precondition / Effect / Cost를 설정한다.
        /// </summary>
        public ReturnToBaseAction()
        {
            Name = "ReturnToBaseAction";
            Cost = 1.0f; // 기획서 수치: Return Cost 1.0f (이동 비용)

            // ── Precondition 설정 ──
            PreconditionKeys = new WorldStateKey[]
            {
                WorldStateKey.IsInventoryFull,
                WorldStateKey.IsFleeing
            };
            PreconditionValues = new bool[] { true, false };

            // ── Effect 설정 ──
            EffectKeys = new WorldStateKey[]
            {
                WorldStateKey.IsInventoryEmpty,
                WorldStateKey.IsInventoryFull,
                WorldStateKey.IsAtBase
            };
            EffectValues = new bool[] { true, false, true };
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
        /// [GOAP 2단계] Gatherer.StartReturnToBase()를 호출하여 귀환 이동을 시작한다.
        /// Execute()는 One-shot 신호 — 이동 개시만 담당하고 완료 감지는 TryAdvanceAction()이 처리한다.
        /// 완료 조건: IsAtBase == true AND IsInventoryEmpty == true (다음 Tick에서 판정).
        /// </summary>
        public override void Execute()
        {
            // [GOAP 2단계] Gatherer.StartReturnToBase()로 귀환 이동 시작
            _agent.Gatherer.StartReturnToBase();
        }

        /// <summary>기지에 도착하고 인벤토리가 비어있으면 완료.</summary>
        public override bool IsComplete(in WorldState state)
        {
            return state.Get(WorldStateKey.IsAtBase)
                && state.Get(WorldStateKey.IsInventoryEmpty);
        }
    }
}

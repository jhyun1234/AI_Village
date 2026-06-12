// =============================================================================
// GoapGoal.cs
// 역할(Role)  : GOAP 에이전트가 달성하려는 목표(Goal)를 정의한다.
//               각 Goal은 활성화 조건(IsRelevant)과 우선순위(Priority)를 가지며,
//               GoapPlanner.Plan()에서 현재 WorldState와 비교해 최우선 Goal을 선택한다.
// 사용법(Usage): GoapGoal.CreateFlee() 등 정적 팩토리로 인스턴스를 생성한다.
//               GoapAgent.InitializeGoalsAndActions()에서 배열로 등록한다.
// 의존성(Dependencies): WorldState, WorldStateKey
//
// Author: Senior Unity Programmer
// Last Updated: 2026-06-10
// =============================================================================

namespace AIVillage.GOAP
{
    // ───────────────────────────────────────────────────────────────────────────
    // GoapGoalType: 목표 종류를 나타내는 열거형.
    // Priority 수치가 낮을수록 먼저 실행된다 (0 = 최고 우선순위).
    // ───────────────────────────────────────────────────────────────────────────
    public enum GoapGoalType
    {
        None             = 0, // 목표 없음 (초기 상태)
        // ── Gatherer 목표 ──
        Flee             = 1, // 도주 — Priority 0 (최고, Gatherer + Builder 공용)
        DepositResource  = 2, // 자원 반납 — Priority 1
        CollectResource  = 3, // 자원 채집 — Priority 2
        WaitForResource  = 4, // 노드 대기 — Priority 3
        // ── Builder 목표 ──
        BuildBuilding    = 5, // 건물 건설 — Priority 1
        WaitForBuilding  = 6  // 건설지 대기 — Priority 2
    }

    /// <summary>
    /// GOAP 에이전트의 목표 데이터.
    ///
    /// Priority: 숫자가 낮을수록 우선순위가 높다 (Flee=0, Deposit=1, Collect=2, Wait=3).
    /// IsRelevant(): 현재 WorldState에서 이 목표가 의미 있는지 판단한다.
    ///              GoapPlanner는 IsRelevant==true인 Goal 중 Priority가 가장 낮은 것을 선택한다.
    /// </summary>
    public struct GoapGoal
    {
        // ── Public Fields ──
        public GoapGoalType Type;
        public int          Priority;

        // ───────────────────────────────────────────────────────────────────────
        // IsRelevant: 현재 WorldState에서 이 Goal이 활성화되어야 하는지 판단한다.
        //
        // 활성화 조건 (기획서):
        //   Flee:            HasThreat == true
        //   DepositResource: IsInventoryFull == true  AND  HasThreat == false
        //   CollectResource: HasAvailableNode == true AND  IsInventoryEmpty == true  AND  HasThreat == false
        //   WaitForResource: HasAvailableNode == false AND HasThreat == false
        // ───────────────────────────────────────────────────────────────────────
        public bool IsRelevant(in WorldState state)
        {
            switch (Type)
            {
                case GoapGoalType.Flee:
                    // 위협이 있으면 무조건 도주 Goal 활성화
                    return state.Get(WorldStateKey.HasThreat);

                case GoapGoalType.DepositResource:
                    // 인벤토리가 가득 찼고 위협이 없을 때 반납
                    return state.Get(WorldStateKey.IsInventoryFull)
                        && !state.Get(WorldStateKey.HasThreat);

                case GoapGoalType.CollectResource:
                    // 안전한 노드가 있고, 인벤토리가 비어있고, 위협 없을 때 채집
                    return state.Get(WorldStateKey.HasAvailableNode)
                        && state.Get(WorldStateKey.IsInventoryEmpty)
                        && !state.Get(WorldStateKey.HasThreat);

                case GoapGoalType.WaitForResource:
                    return !state.Get(WorldStateKey.HasAvailableNode)
                        && !state.Get(WorldStateKey.HasThreat);

                // ── Builder 목표 ──
                case GoapGoalType.BuildBuilding:
                    // 건설 대기 중인 건물이 있고 위협이 없을 때 건설
                    return state.Get(WorldStateKey.HasPendingBuilding)
                        && !state.Get(WorldStateKey.HasThreat);

                case GoapGoalType.WaitForBuilding:
                    // 건설 대기 건물이 없고 위협도 없을 때 대기
                    return !state.Get(WorldStateKey.HasPendingBuilding)
                        && !state.Get(WorldStateKey.HasThreat);

                default:
                    return false;
            }
        }

        // ───────────────────────────────────────────────────────────────────────
        // 정적 팩토리 메서드: 미리 정의된 Goal 인스턴스를 생성한다.
        // Priority 수치가 낮을수록 먼저 실행된다 (0 = 최고 우선순위).
        // ───────────────────────────────────────────────────────────────────────

        /// <summary>Flee Goal 생성. Priority=0 (최고 우선순위).</summary>
        public static GoapGoal CreateFlee()
        {
            return new GoapGoal { Type = GoapGoalType.Flee, Priority = 0 };
        }

        /// <summary>DepositResource Goal 생성. Priority=1.</summary>
        public static GoapGoal CreateDepositResource()
        {
            return new GoapGoal { Type = GoapGoalType.DepositResource, Priority = 1 };
        }

        /// <summary>CollectResource Goal 생성. Priority=2.</summary>
        public static GoapGoal CreateCollectResource()
        {
            return new GoapGoal { Type = GoapGoalType.CollectResource, Priority = 2 };
        }

        /// <summary>WaitForResource Goal 생성. Priority=3 (최저 우선순위).</summary>
        public static GoapGoal CreateWaitForResource()
        {
            return new GoapGoal { Type = GoapGoalType.WaitForResource, Priority = 3 };
        }

        // ── Builder 전용 팩토리 ──

        /// <summary>BuildBuilding Goal 생성 (Builder). Priority=1.</summary>
        public static GoapGoal CreateBuildBuilding()
        {
            return new GoapGoal { Type = GoapGoalType.BuildBuilding, Priority = 1 };
        }

        /// <summary>WaitForBuilding Goal 생성 (Builder). Priority=2.</summary>
        public static GoapGoal CreateWaitForBuilding()
        {
            return new GoapGoal { Type = GoapGoalType.WaitForBuilding, Priority = 2 };
        }
    }
}

// =============================================================================
// GoapAction.cs
// 역할(Role)  : GOAP 플래너가 시퀀싱하는 개별 행동(Action)의 추상 기반 클래스.
//               각 구체 Action은 사전조건(Precondition), 효과(Effect), 비용(Cost)을 선언하고
//               Execute() / IsComplete() 를 구현한다.
// 사용법(Usage): 이 클래스를 상속하여 구체 Action 클래스를 만든다.
//               GoapAgent.InitializeGoalsAndActions()에서 배열로 등록하고
//               Initialize(agent)를 호출해 Gatherer 참조를 주입한다.
// 의존성(Dependencies): WorldState, WorldStateKey, GoapAgent
//
// Author: Senior Unity Programmer
// Last Updated: 2026-06-10
// =============================================================================

namespace AIVillage.GOAP
{
    /// <summary>
    /// GOAP 플래너용 행동 추상 기반 클래스.
    ///
    /// PreconditionKeys / PreconditionValues: 이 Action이 실행 가능한 전제 조건.
    /// EffectKeys / EffectValues:             이 Action이 완료됐을 때 WorldState에 미치는 효과.
    /// Cost:                                  플래너가 최적 경로 탐색 시 사용하는 비용 값.
    ///                                        낮을수록 선호된다.
    /// </summary>
    public abstract class GoapAction
    {
        #region ── Public Properties ──

        /// <summary>Action 식별 이름 (디버그 로그용).</summary>
        public string Name { get; protected set; }

        /// <summary>
        /// 플래너 비용. 낮을수록 선호된다.
        /// FleeAction=0, SearchNode/Return=1, Gather=2, Wait=3.
        /// </summary>
        public float Cost { get; protected set; }

        /// <summary>사전조건 키 배열. PreconditionValues와 인덱스가 1:1 대응한다.</summary>
        public WorldStateKey[] PreconditionKeys { get; protected set; }

        /// <summary>사전조건 값 배열. PreconditionKeys[i]의 기대값이 PreconditionValues[i].</summary>
        public bool[] PreconditionValues { get; protected set; }

        /// <summary>효과 키 배열. EffectValues와 인덱스가 1:1 대응한다.</summary>
        public WorldStateKey[] EffectKeys { get; protected set; }

        /// <summary>효과 값 배열. EffectKeys[i]에 EffectValues[i]를 적용한다.</summary>
        public bool[] EffectValues { get; protected set; }

        #endregion

        #region ── Abstract Methods (구체 클래스에서 반드시 구현) ──

        /// <summary>
        /// GoapAgent 참조를 주입받아 필요한 컴포넌트를 캐싱한다.
        /// GoapAgent.InitializeGoalsAndActions()에서 1회 호출된다.
        /// </summary>
        public abstract void Initialize(GoapAgent agent);

        /// <summary>
        /// 현재 WorldState에서 이 Action을 실행할 수 있는지 반환한다.
        /// 일반적으로 ArePreconditionsMet(state)를 호출한다.
        /// 추가 런타임 조건이 있을 때 오버라이드한다.
        /// </summary>
        public abstract bool CanExecute(in WorldState state);

        /// <summary>
        /// Action의 실제 실행 로직.
        /// GOAP 1단계: FSM이 처리하므로 이 메서드는 위임(no-op)이다.
        /// 향후 단계에서 구체 행동 로직을 여기에 추가한다.
        /// </summary>
        public abstract void Execute();

        /// <summary>
        /// 현재 WorldState에서 이 Action이 완료됐는지 반환한다.
        /// 완료 판정은 Effect가 실제 WorldState에 반영됐는지 확인한다.
        /// </summary>
        public abstract bool IsComplete(in WorldState state);

        #endregion

        #region ── Concrete Helper Methods ──

        /// <summary>
        /// 현재 WorldState에서 사전조건이 모두 충족됐는지 확인한다.
        /// PreconditionKeys[i] 의 상태값 == PreconditionValues[i] 이어야 한다.
        /// </summary>
        public bool ArePreconditionsMet(in WorldState state)
        {
            if (PreconditionKeys == null) return true;

            for (int i = 0; i < PreconditionKeys.Length; i++)
            {
                // 사전조건 불일치 — 이 Action은 현재 실행 불가
                if (state.Get(PreconditionKeys[i]) != PreconditionValues[i])
                    return false;
            }
            return true;
        }

        /// <summary>
        /// 이 Action의 효과를 output 배열에 기록한다 (GC-free 시뮬레이션용).
        /// GoapPlanner의 DFS 탐색에서 상태 시뮬레이션에 사용된다.
        /// output 배열은 호출자(GoapPlanner._simBuffer)가 소유하며 재사용된다.
        /// </summary>
        public void ApplyEffects(bool[] output)
        {
            if (EffectKeys == null) return;

            for (int i = 0; i < EffectKeys.Length; i++)
                output[(int)EffectKeys[i]] = EffectValues[i];
        }

        #endregion
    }
}

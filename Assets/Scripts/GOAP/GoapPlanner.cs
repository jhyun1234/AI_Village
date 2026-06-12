// =============================================================================
// GoapPlanner.cs
// 역할(Role)  : 현재 WorldState와 등록된 Goal/Action 목록을 받아
//               최적 Goal과 Action 시퀀스를 계산하는 정적 플래너.
//               DFS(깊이 우선 탐색) 기반, 최대 깊이 5로 실용적 성능을 보장한다.
// 사용법(Usage): GoapPlanner.Plan(...)을 GoapAgent.Replan()에서 호출한다.
//               planBuffer / depthBuffers는 호출자(GoapAgent)가 소유하고 재사용한다 (GC-free).
// 의존성(Dependencies): WorldState, WorldStateKey, GoapGoal, GoapAction
//
// Author: Senior Unity Programmer
// Last Updated: 2026-06-12
// =============================================================================

using System.Collections.Generic;

namespace AIVillage.GOAP
{
    /// <summary>
    /// GOAP 플래너 정적 클래스.
    ///
    /// Plan() 흐름:
    ///   1. availableGoals 를 Priority 오름차순으로 순회
    ///   2. 각 Goal에 대해 IsRelevant(currentState) 확인
    ///   3. 최초로 IsRelevant==true인 Goal에 대해 FindActionSequence() 호출
    ///   4. Action 시퀀스 탐색 성공 시 planBuffer에 채우고 Goal 타입 반환
    ///   5. 모든 Goal이 실패하면 GoapGoalType.None 반환
    /// </summary>
    public static class GoapPlanner
    {
        // ── 상수 ──
        // DFS 최대 탐색 깊이. 너무 깊으면 성능 저하, 너무 얕으면 복잡한 플랜 생성 불가.
        // 현재 Action 수(5개) 기준 깊이 5는 충분히 여유 있다.
        public const int MAX_DEPTH = 5;

        /// <summary>
        /// 최적 Goal을 선택하고 대응하는 Action 시퀀스를 planBuffer에 채운다.
        ///
        /// 알고리즘:
        ///   - availableGoals는 Priority 오름차순 정렬을 기대한다
        ///     (GoapAgent.InitializeGoalsAndActions에서 정렬 보장)
        ///   - 첫 번째 IsRelevant+실행가능 Action 시퀀스 발견 즉시 반환 (최적 우선)
        ///
        /// GC-free 설계:
        ///   - planBuffer:    List.Clear() 후 재사용 (재할당 없음)
        ///   - depthBuffers:  깊이별 전용 bool[][] 배열 (GoapAgent.Start에서 1회 할당)
        ///                    각 재귀 깊이가 독립 버퍼를 사용하므로 상위 state._data 오염 없음.
        /// </summary>
        /// <param name="currentState">현재 세계 상태</param>
        /// <param name="availableGoals">등록된 Goal 배열 (Priority 오름차순)</param>
        /// <param name="availableActions">등록된 Action 배열</param>
        /// <param name="planBuffer">결과 Action 시퀀스를 담을 재사용 리스트</param>
        /// <param name="depthBuffers">깊이별 시뮬레이션 버퍼 배열 [MAX_DEPTH][WorldStateKey.Count]</param>
        /// <returns>선택된 GoapGoalType. 실패 시 GoapGoalType.None.</returns>
        public static GoapGoalType Plan(
            in WorldState      currentState,
            GoapGoal[]         availableGoals,
            GoapAction[]       availableActions,
            List<GoapAction>   planBuffer,
            bool[][]           depthBuffers)
        {
            // ── null 가드 ──
            if (availableGoals == null || availableActions == null || planBuffer == null || depthBuffers == null)
                return GoapGoalType.None;

            // planBuffer 초기화 (Clear는 내부 배열을 재사용하므로 GC-free)
            planBuffer.Clear();

            // ── Goal 순회: Priority 오름차순 (낮을수록 먼저) ──
            for (int g = 0; g < availableGoals.Length; g++)
            {
                GoapGoal goal = availableGoals[g];

                // 현재 WorldState에서 이 Goal이 관련 없으면 건너뜀
                if (!goal.IsRelevant(currentState))
                    continue;

                // ── Action 시퀀스 탐색 (DFS) ──
                planBuffer.Clear();

                if (FindActionSequence(goal, availableActions, currentState, depthBuffers, planBuffer, 0))
                {
                    // 탐색 성공 — 이 Goal을 선택
                    return goal.Type;
                }

                // 이 Goal은 실행 가능한 Action 시퀀스가 없음 — 다음 Goal 시도
            }

            // 모든 Goal 실패
            planBuffer.Clear();
            return GoapGoalType.None;
        }

        /// <summary>
        /// 재귀 DFS로 Goal 달성을 위한 Action 시퀀스를 탐색한다.
        ///
        /// 탐색 원리:
        ///   현재 State에서 precondition이 충족된 Action을 찾아
        ///   그 Effect를 적용한 시뮬레이션 State에서 다시 탐색한다.
        ///   단일 Action으로 Goal의 IsRelevant를 false로 만들 수 있으면 성공.
        ///
        /// GC-free 깊이 격리:
        ///   각 재귀 깊이는 depthBuffers[depth]를 전용 작업 버퍼로 사용한다.
        ///   상위 깊이의 state._data는 depthBuffers[depth-1]이며, 하위 깊이에서
        ///   절대 수정되지 않으므로 state.CopyTo(myBuffer)가 항상 올바른 스냅샷을 만든다.
        /// </summary>
        /// <param name="goal">달성 목표</param>
        /// <param name="actions">사용 가능한 Action 목록</param>
        /// <param name="state">현재 시뮬레이션 상태 (이 깊이의 입력 상태, 수정 금지)</param>
        /// <param name="depthBuffers">깊이별 전용 작업 버퍼 배열</param>
        /// <param name="result">Action 시퀀스 결과 리스트</param>
        /// <param name="depth">현재 재귀 깊이</param>
        /// <returns>시퀀스 탐색 성공 여부</returns>
        private static bool FindActionSequence(
            GoapGoal         goal,
            GoapAction[]     actions,
            in WorldState    state,
            bool[][]         depthBuffers,
            List<GoapAction> result,
            int              depth)
        {
            // ── 깊이 초과 — 탐색 중단 ──
            if (depth >= depthBuffers.Length)
                return false;

            // 이 깊이 전용 작업 버퍼.
            // state._data는 depthBuffers[depth-1](또는 _stateData)이며, myBuffer와 항상 다른 배열이다.
            // 따라서 state.CopyTo(myBuffer)가 진짜 복사를 수행하여 상태 격리가 보장된다.
            bool[] myBuffer = depthBuffers[depth];

            // ── 각 Action을 후보로 순회 ──
            for (int i = 0; i < actions.Length; i++)
            {
                GoapAction action = actions[i];

                // 현재 상태에서 사전조건 미충족 Action 건너뜀
                if (!action.ArePreconditionsMet(state))
                    continue;

                // 이 깊이의 입력 상태를 전용 버퍼에 복사한 뒤 Effect 적용.
                // state._data != myBuffer 이므로 no-op이 되지 않는다 (깊이 격리의 핵심).
                state.CopyTo(myBuffer);
                action.ApplyEffects(myBuffer);

                // 임시 WorldState: myBuffer를 감싸서 Goal.IsRelevant 판정에 사용.
                // 추가 할당 없음 (GC-free).
                WorldState simState = new WorldState(myBuffer);

                // ── Action을 결과에 추가 ──
                result.Add(action);

                // ── Goal 달성 판정: Effect 적용 후 Goal의 IsRelevant가 false이면 달성 ──
                if (!simState.IsRelevant(goal, state))
                {
                    // Goal이 달성됐거나 더 이상 필요하지 않음 — 성공
                    return true;
                }

                // ── 재귀: 다음 단계 탐색 ──
                // simState는 myBuffer를 참조하므로 depth+1에서는 depthBuffers[depth+1]을 사용한다.
                if (FindActionSequence(goal, actions, simState, depthBuffers, result, depth + 1))
                    return true;

                // 이 Action 경로 실패 — 결과에서 제거 후 다른 Action 시도
                // 다음 반복에서 state.CopyTo(myBuffer)가 myBuffer를 깨끗하게 덮어쓰므로
                // 명시적 복원 불필요.
                result.RemoveAt(result.Count - 1);
            }

            // 이 깊이에서 유효한 Action 없음
            return false;
        }
    }

    /// <summary>
    /// GoapPlanner 내부 DFS에서 Goal 달성 판정에 사용하는 확장 메서드.
    /// WorldState의 구조체 메서드로 두면 GoapGoal에 대한 역참조가 생기므로
    /// static 확장으로 분리하여 단방향 의존성을 유지한다.
    /// </summary>
    internal static class WorldStateGoalExtension
    {
        /// <summary>
        /// Effect 적용 후 simState에서 이 Goal이 더 이상 IsRelevant하지 않으면
        /// "달성됐다"고 간주한다.
        ///
        /// 의미: Goal이 IsRelevant==true가 된 이유(조건)가 simState에서 사라졌다 = 해소됨.
        /// 예) Flee: HasThreat==false → 도주 필요 없음 = Flee Goal 달성
        ///     DepositResource: IsInventoryFull==false → 반납 완료 = 달성
        /// </summary>
        internal static bool IsRelevant(this WorldState simState, GoapGoal goal, in WorldState originalState)
        {
            // originalState에서 Goal이 관련 있었으나
            // simState에서는 더 이상 관련 없음 = 달성
            if (originalState.IsRelevantForGoal(goal) && !simState.IsRelevantForGoal(goal))
                return false; // 달성됨 — "관련 없음" 반환

            // 아직 달성 안 됨 — "관련 있음" 반환
            return true;
        }

        /// <summary>WorldState에서 GoapGoal의 IsRelevant를 호출하는 헬퍼.</summary>
        private static bool IsRelevantForGoal(this WorldState state, GoapGoal goal)
        {
            return goal.IsRelevant(state);
        }
    }
}

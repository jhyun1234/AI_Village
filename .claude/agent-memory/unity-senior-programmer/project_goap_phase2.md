---
name: project_goap_phase2
description: GOAP 2단계 구현 완료: GoapAction.Execute()가 Gatherer 메서드를 직접 호출하는 One-shot 실행 모델 (2026-06-12)
metadata:
  type: project
---

## GOAP 2단계: FSM 내부 로직 → GoapAction.Execute() 교체 (2026-06-12)

수정 파일 4개:
- `Assets/Scripts/GOAP/GoapAgent.cs`
- `Assets/Scripts/Units/Gatherer.cs`
- `Assets/Scripts/GOAP/Actions/SearchNodeAction.cs`
- `Assets/Scripts/GOAP/Actions/ReturnToBaseAction.cs`

**Why:** 1단계에서 GoapAction.Execute()가 모두 no-op이었고 Gatherer FSM이 직접 행동을 결정했다.
2단계부터 GoapAgent가 Action을 직접 개시하고, Tick 루프가 완료를 폴링하는 구조로 전환.

**How to apply:** 앞으로 새 GoapAction 추가 시 아래 5가지 설계 결정을 기준으로 Execute/IsComplete 구현.

---

### 확정된 5가지 설계 결정

1. **One-shot 실행 모델**: Execute()는 행동 개시 신호 1회만. 완료 감지는 GoapAgent.OnTick → TryAdvanceAction() 폴링.

2. **NotifyArrival 브릿지**: Gatherer.OnReachDestination() → `_goapAgent?.NotifyArrival()` 호출.
   - SearchNodeAction 완료 시: NotifyArrival에서 직접 `_gatherer.StartGatherCoroutine()` 호출
   - GatherAction.Execute()는 no-op 유지 (설계 결정 2)

3. **GatherRoutine 채집 전담**: GOAP 경로에서 GatherRoutine은 채집만(`WaitForSeconds → Gather()`). 귀환은 ReturnToBaseAction.Execute() → StartReturnToBase()가 담당. Legacy(GoapAgent 없음)는 GatherRoutine이 귀환까지 처리(기존 동작 유지).

4. **SearchNodeAction 조기 완료 리스크 방지**: TryAdvanceAction()에서 SearchNodeAction은 제외(`if (current is SearchNodeAction) return false`). SearchNode → Gather 전환은 오직 NotifyArrival() 경로에서만.

5. **하위 호환성**: `_goapAgent == null`인 Gatherer는 기존 FSM 동작 100% 유지.

---

### GoapAgent에 추가된 신규 메서드

- `TryAdvanceAction()` private — Tick마다 현재 Action 완료 체크, 다음 Action Execute()
- `NotifyArrival()` internal — Gatherer 도착 신호 수신 → SearchNode→Gather 전환
- `NotifyFleeingExit()` internal — 도주 해제 후 즉시 재플래닝
- `NotifyPathFailed()` internal — 경로 실패 후 즉시 재플래닝
- `CurrentAction` public property — 현재 실행 중 GoapAction (AI 정보 패널용)
- `_currentActionIndex` private int — 현재 플랜 내 Action 인덱스 (-1=미활성)

### Gatherer에 추가된 신규 메서드

- `ExecuteSearch()` internal — SearchNodeAction.Execute()용 delegate wrapper (SearchAndGo()는 private 유지)
- `StartGatherCoroutine()` internal — GoapAgent.NotifyArrival()이 직접 호출
- `StartReturnToBase()` internal — ReturnToBaseAction.Execute()가 호출

---

### 변경하지 않은 파일 (no-op 유지)

- `GatherAction.cs` — Execute() no-op 유지
- `FleeAction.cs` — Execute() no-op 유지
- `WaitAction.cs` — Execute() no-op 유지
- `GoapPlanner.cs`, `WorldState.cs`, `GoapGoal.cs`, `GoapAction.cs`

관련 메모리: [[project_goap_phase1]]

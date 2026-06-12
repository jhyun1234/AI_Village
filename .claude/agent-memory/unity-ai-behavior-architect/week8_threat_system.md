---
name: week8-threat-system
description: Week 8 ThreatManager/Monster FSM/AIUnit Fleeing 설계 핵심 결정 — 감지 방식, enabled 토글 유지, 코루틴 인터럽트 패턴
metadata:
  type: project
---

Week 8 설계 핵심 결정 (2026-05-29):

**감지 방식: ThreatManager 폴링 (Physics2D 아님)**
- AIUnit이 ThreatManager.GetNearestMonster(pos, radius)를 GameManager Tick(0.5s)에 호출
- Physics2D.OverlapCircle은 Monster 측에서만 사용 (추적 대상 찾기용)
- Why: Physics2D.OverlapCircle을 AIUnit마다 매 Update 호출하면 유닛 수 × 프레임 횟수만큼 Physics 연산 발생. Tick 기반 폴링으로 0.5초 1회로 축소.

**Monster 이동: Vector3.MoveTowards (A* 아님)**
- 순찰/추적 경로가 단순하므로 A* 비동기 경로 불필요
- Monster는 AIUnit 상속 안 함. 독립 MonoBehaviour.
- enabled 토글 패턴은 Monster에 적용 안 함 (Update가 항상 필요한 구조)

**SetState 가시성 변경: private → protected**
- Fleeing 진입을 위해 AIUnit.SetState()를 protected로 변경 필요
- SetFleeing() public 메서드가 내부에서 SetState(UnitState.Fleeing) + enabled=true 호출

**enabled 토글 확장**
- 기존: enabled = (state == Moving)
- 변경: enabled = (state == Moving || state == Fleeing)
- Fleeing 중 매 Update에서 기지 반경 체크 + 체력 회복이 필요하기 때문

**Gatherer/Builder 코루틴 인터럽트 패턴**
- SetFleeing() 호출 시 파생 클래스의 OnFleeingEnter() 호출
- Gatherer: StopCoroutine(_gatherCoroutine) + _targetNode.ReleaseReservation() + 상태 초기화
- Builder: StopCoroutine(_buildCoroutine) + _targetBuilding.ReleaseConstruction() + 상태 초기화
- CancelInvoke() 반드시 포함 (Invoke(nameof(SearchAndGo), delay) 예약이 살아있을 수 있음)

**체력 회복 위치: AIUnit.Update (Fleeing 상태 중)**
- 기지 반경 5타일 내 AND Fleeing 상태일 때만 회복
- Fleeing → Idle 전환 이후에는 회복 중단 (Idle은 enabled=false)
- 코루틴 대신 Update 사용 — Fleeing이 enabled=true 보장이 있으므로 안전

**Monster 추적 포기 이중 조건**
- 조건 A: 거리 > 8타일 (월드 단위: 8 * tileSize)
- 조건 B: 유닛이 기지 반경 내 진입 (GameManager.BasePosition 기준 반경 5타일)
- 두 조건 중 하나라도 충족 시 Chasing → Patrolling

**[[week9_danger_registry]]** — Week 9에서 DangerRegistry 연동 예정

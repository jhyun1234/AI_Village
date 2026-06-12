---
name: project-goap-phase1
description: GOAP 1단계 구현 완료: WorldState/GoapGoal/GoapAction/GoapPlanner/GoapAgent + 5개 Action + GameManager/Gatherer 최소 수정 (2026-06-10)
metadata:
  type: project
---

GOAP 1단계가 `Assets/Scripts/GOAP/` 경로에 구현 완료됐다.

**Why:** Gatherer FSM을 GOAP 아키텍처로 점진적으로 전환하기 위한 1단계. GoapAgent 미부착 Gatherer는 기존과 완전히 동일하게 동작하도록 regression-free 설계.

**How to apply:** GOAP 2단계 작업 시 이 파일 구조와 네임스페이스(`AIVillage.GOAP`)를 기준으로 확장한다.

## 신규 파일
- `Assets/Scripts/GOAP/WorldState.cs` — bool[] 공유 참조 GC-free 상태 컨테이너
- `Assets/Scripts/GOAP/GoapGoal.cs` — GoapGoalType enum + IsRelevant 조건 정의
- `Assets/Scripts/GOAP/GoapAction.cs` — 추상 기반 클래스 (Precondition/Effect/Cost)
- `Assets/Scripts/GOAP/GoapPlanner.cs` — DFS 기반 정적 플래너 (최대 깊이 5)
- `Assets/Scripts/GOAP/GoapAgent.cs` — Gatherer에 붙이는 MonoBehaviour 의사결정 컴포넌트
- `Assets/Scripts/GOAP/Actions/SearchNodeAction.cs` — Cost 1.0f
- `Assets/Scripts/GOAP/Actions/GatherAction.cs` — Cost 2.0f
- `Assets/Scripts/GOAP/Actions/ReturnToBaseAction.cs` — Cost 1.0f
- `Assets/Scripts/GOAP/Actions/FleeAction.cs` — Cost 0.0f (최고 우선)
- `Assets/Scripts/GOAP/Actions/WaitAction.cs` — Cost 3.0f (최후 수단)

## 수정된 파일
- `Assets/Scripts/Core/GameManager.cs` — OnTick() 끝에 `MessageBus?.Publish("game.tick", null)` 1줄 추가
- `Assets/Scripts/Units/Gatherer.cs` — using GOAP 추가, _goapAgent 캐싱, OnIdle/OnThreatDetected 패치, internal getter 5개 추가

## 설계 핵심 결정
- Tick 주기는 `game.tick` MessageBus 이벤트로 GoapAgent.OnTick을 트리거
- WorldState는 GC-free: bool[] 1회 할당 후 구조체 공유 참조
- GoapAgent 미부착 시 Gatherer는 기존 FSM 로직 100% 유지
- 1단계 Execute()는 모두 no-op 위임 (FSM이 실제 실행 담당)
- `IsFleeing` internal getter는 `_currentState == UnitState.Fleeing` 으로 구현

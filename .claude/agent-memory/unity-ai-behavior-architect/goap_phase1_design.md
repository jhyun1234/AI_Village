---
name: goap-phase1-design
description: GOAP 레이어 1단계 설계 핵심 결정 — WorldState 표현, GoapAgent↔Gatherer 연동, 4단계 마이그레이션 전략
metadata:
  type: project
---

## GOAP 마이그레이션 전략 (4단계, 확정)

1단계: GoapAgent 컴포넌트 추가, FSM은 그대로 — CurrentGoal만 전달
2단계: Gatherer FSM 내부를 GoapAction으로 하나씩 교체
3단계: Builder, Warrior 동일 적용
4단계: GOAP 래퍼 제거 (FSM 완전 소멸)

**핵심 원칙: 각 단계마다 게임이 실행 가능한 상태 유지**

## 1단계 핵심 결정

### WorldState 표현 방식
- `struct WorldStateSnapshot` (GC-free, value type) + `enum WorldStateKey` 조합
- Dictionary<string,bool> 기각: Tick마다 박싱/언박싱 발생, string 비교 비용
- enum flags 기각: 키-값 독립 표현 불가 (예: HasResource=true + NodeAvailable=false 동시 표현)
- 실제 구현: `bool[]` 배열로 enum int 인덱싱 → GC Zero, 읽기 O(1)

### GoapGoal 우선순위
P0: Flee (생존) > P1: DepositResource (자원 반납, 꽉 참) > P2: CollectResource (정상 채집) > P3: WaitForResource (노드 없음)

### GoapAgent ↔ Gatherer 연동 방식
- property 방식 채택 (event 기각: 이벤트 연결/해제 관리 복잡성)
- `GoapAgent.CurrentGoalType`을 Gatherer.OnIdle()에서 읽기
- 1단계에서 Gatherer FSM 수정 최소화: OnIdle() 진입부에 1개 조건 추가

### 재플래닝 정책
- 매 GameManager Tick(0.5s)마다 WorldState 업데이트
- WorldState 변화 있을 때만 GoapPlanner.Plan() 호출 (변화 없으면 스킵)
- Flee는 즉시 재플래닝 (OnThreatDetected에서 직접 호출)

### 네임스페이스: AIVillage.GOAP

## 신규 파일 목록
- Assets/Scripts/GOAP/WorldState.cs
- Assets/Scripts/GOAP/GoapGoal.cs
- Assets/Scripts/GOAP/GoapAction.cs (추상 기반 클래스)
- Assets/Scripts/GOAP/GoapPlanner.cs
- Assets/Scripts/GOAP/GoapAgent.cs (MonoBehaviour)
- Assets/Scripts/GOAP/Actions/SearchNodeAction.cs
- Assets/Scripts/GOAP/Actions/GatherAction.cs
- Assets/Scripts/GOAP/Actions/ReturnToBaseAction.cs
- Assets/Scripts/GOAP/Actions/FleeAction.cs

## AIUnit.cs 수정 사항 (최소화)
- 수정 없음 — GoapAgent는 Gatherer 컴포넌트만 참조
- Gatherer.cs 수정: OnIdle() 진입부에 GoapAgent.CurrentGoalType 체크 추가 (2줄)

**Why:** 4단계 마이그레이션에서 FSM을 단계적으로 제거하기 위해 GoapAgent를 FSM 외부에 완전히 분리
**How to apply:** 2단계 설계 시 GoapAction.Execute()가 FSM 상태 전환을 직접 호출하는 구조로 전환

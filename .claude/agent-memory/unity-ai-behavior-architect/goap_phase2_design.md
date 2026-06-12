---
name: goap-phase2-design
description: GOAP 2단계 설계 핵심 결정 — One-shot 실행 모델, NotifyArrival 브릿지, GatherRoutine 분리, 인터럽트 처리, 하위 호환 가드 패턴
metadata:
  type: project
---

## GOAP 2단계 목표
Gatherer FSM 내부 로직을 GoapAction.Execute()로 교체.
FSM은 수동 실행자(passive executor)로 역할 분리. 결정 주체는 GoapAgent.

## 핵심 결정

### 실행 모델: One-shot
- Execute() 1회 호출로 작업 개시 (kick-off)
- 완료 감지는 GoapAgent.OnTick()에서 IsComplete() 폴링
- Tick-driven 기각: 코루틴 기반 GatherAction에서 중복 실행 방어 비용 과다

### 플랜 실행 추적 필드 (GoapAgent 추가)
- _currentActionIndex: int — planBuffer 내 현재 Action 인덱스
- _currentAction: GoapAction — O(1) 접근용 캐시
- _activeGoalType: GoapGoalType — 인터럽트 비교용

### 인터럽트 처리
- ForceReplanOnThreat() 에서 _currentAction?.Interrupt() 호출 후 Replan()
- GoapAction 기반 클래스에 virtual void Interrupt() {} 추가 (기본 no-op)
- OnFleeingEnter()가 코루틴 정리를 이미 담당하므로 GatherAction.Interrupt() 오버라이드 불필요

### Gatherer 노출 메서드 (internal 추가)
- SearchAndGo(): private → internal (SearchNodeAction 호출)
- StartGatherCoroutine(): 신규 — 이중 시작 방어 가드 포함
- StartReturnToBase(): 신규 — _isReturning 중복 가드 포함
- DepositResource(): 신규 — IsInSafeZone 가드 포함, OnReachDestination에서 분리

### GatherRoutine() 역할 축소
- 기존: WaitForSeconds → Gather → SetDestination(base) [귀환 포함]
- 변경: WaitForSeconds → Gather → 종료 (_goapAgent != null 시 SetDestination 제거)
- 귀환은 ReturnToBaseAction.Execute() → StartReturnToBase()가 담당

### OnReachDestination 브릿지 (NotifyArrival)
- `if (_goapAgent != null) { _goapAgent.NotifyArrival(); return; }`
- NotifyArrival()에서 현재 Action 타입별 분기:
  - GatherAction 진행 중이면 StartGatherCoroutine()
  - ReturnToBaseAction 진행 중이면 DepositResource()

### FleeAction.Execute(): no-op 유지
- 도주는 OnThreatDetected → ForceReplanOnThreat → SetFleeing() 경로가 이미 처리
- FleeAction을 통해 SetFleeing() 재호출하면 이중 호출 발생

### 하위 호환 가드 패턴 (모든 분기)
- OnIdle(): `if (_goapAgent != null) return;`
- OnReachDestination(): `if (_goapAgent != null) { _goapAgent.NotifyArrival(); return; }`
- OnFleeingExit(): `if (_goapAgent != null) return;`
- GatherRoutine(): _goapAgent != null 분기로 SetDestination 제거

## 주요 리스크
1. SearchNodeAction.IsComplete()가 이동 완료 전 GatherAction으로 조기 전진
   → 방어: GatherAction.Execute() no-op, NotifyArrival 경로에서만 StartGatherCoroutine()
2. Flee 인터럽트 시 코루틴 미정리 → OnFleeingEnter()가 이미 StopCoroutine 처리
3. ReturnToBaseAction.DepositResource() 기지 미도착 시 호출 → IsInSafeZone 가드

## 신규 파일
없음. 기존 파일만 수정.

**Why:** GoapAction.Execute() no-op에서 실제 로직으로 전환하면서 FSM을 단계적으로 수동 실행자로 전환. 4단계 FSM 완전 제거를 위한 중간 단계.
**How to apply:** 3단계 설계 시 Builder/Warrior FSM도 동일 패턴 적용 가능. GoapAgent 필드 구조는 재사용.

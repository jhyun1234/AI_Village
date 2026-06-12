---
name: warrior-fsm-design
description: Warrior FSM 설계 핵심 결정 — Standby은신/WarriorHealHelper/PopulationManager미등록/SetFleeing무력화/CombatAlertQueue인터페이스
metadata:
  type: project
---

Warrior FSM 설계 핵심 결정 (2026-05-31):

**상태 구조: 4개 상태 (Fleeing 없음)**
- Standby(은신 대기) → Moving-Dispatch → Fighting → Moving-Return → Standby
- Moving은 단일 UnitState.Moving을 사용하며 _isReturning 플래그로 방향 구분
- UnitState.Fighting은 이미 enum에 추가되어 있음

**Standby 은신 구현 방식**
- _spriteRenderer.enabled = false (시각 은신)
- _collider.enabled = false (Monster 감지 차단)
- this.enabled = false (AIUnit.SetState가 자동 처리, Moving/Fleeing 아닐 때)
- GameObject.SetActive(false) 절대 금지 (코루틴 멈춤)

**WarriorHealHelper: 별도 MonoBehaviour로 HP 회복**
- Warrior가 disabled일 때도 HP 회복이 필요하므로 별도 컴포넌트 사용
- Warrior와 동일 GameObject에 [RequireComponent]로 강제 추가
- Warrior.ApplyHeal(float deltaTime)을 internal 메서드로 호출
- EnterStandby에서 StartHealing(), ExitStandby에서 StopHealing()

**AIUnit.SetState() 수정 필요 사항**
- 기존: enabled = (Moving || Fleeing)
- Fighting 진입 시 Update() 필요 → Fighting도 enabled=true 필요
- 방법 A: AIUnit.SetState() 조건에 Fighting 추가 (권장)
- 방법 B: EnterFighting()에서 SetState() 후 명시적 this.enabled = true 추가
- senior-programmer에게 선택 위임, 설계서에 경고 명시함

**SetFleeing() 무력화**
- AIUnit.SetFleeing()이 virtual이 아님 → new 또는 virtual 추가 필요
- 권장: AIUnit.SetFleeing()에 virtual 추가 후 Warrior에서 override (빈 메서드)
- GameManager.CheckThreatForAllUnits()가 Warrior에게도 SetFleeing() 호출하므로 반드시 무력화 필요

**PopulationManager 미등록 결정**
- Warrior는 base.Start() 호출 금지 → PopulationManager 미등록
- 이유: Warrior만 남아있을 때 패배 조건이 올바르게 판정되어야 함
- OnDestroy()에서 base.OnDestroy() 호출은 유지 (UnregisterUnit은 Contains 체크로 무해)

**CombatAlertQueue 인터페이스 (Warrior 측)**
- public bool TryDispatch(Vector2 targetPosition) — 파견 명령 수신
- public bool IsAvailableForDispatch { get; } — 파견 가능 여부 조회
  조건: _currentState == Standby && Hp >= MaxHp * _dispatchHpThreshold(0.8f)

**Warrior 목록 관리: Barracks에서 보유**
- PopulationManager 미등록이므로 CombatAlertQueue가 직접 Warrior를 찾을 수 없음
- Barracks가 List<Warrior>를 보유, OnDestroy 시 HomeBarracks.ReleaseSlot()에서 제거

**MessageBus 채널**
- "warrior.dispatched" — Standby → Moving 전환 시 발행
- "warrior.returned"   — Moving-Return → Standby 전환 시 발행
- "unit.died"          — AIUnit 기존 채널 그대로 사용

**주요 엣지케이스**
- AttackRoutine 재진입 방지: EnterFighting()에서 Fighting 상태 가드 필수
- OnPathFailed() override: Idle 대신 EnterStandby()로 폴백
- 최초 스폰: Barracks 위치에서 Standby 시작, 파견 명령 전까지 invisible 유지

[[week8_threat_system]] — SetFleeing/enabled 토글 패턴 참조

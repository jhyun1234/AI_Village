---
name: weapon-system-design
description: v0.2-5 무기 시스템 설계 핵심 결정 — Forge/Blacksmith/WeaponType/Warrior 무장 아키텍처
metadata:
  type: project
---

무기 시스템 설계 핵심 결정 (2026-06-09):

**신규 파일 3개**
- WeaponType.cs — namespace AIVillage.Resources (ResourceType과 동일 네임스페이스)
  값: None=0, AlloyBlade=1
- Forge.cs — Building 상속, WOOD=15/STONE=10, OnBuilt→BuildingManager.RegisterBuiltForge(this)
- Blacksmith.cs — Building 상속, WOOD=20/STONE=15, TryCraftWeapon() 보유

**수정 파일 5개**
- GameManager.cs — SILVER/COPPER 자원 switch 케이스 추가 (현재 미지원 버그 있음)
  _currentSilver, _currentCopper 필드 추가 + InitializeResources() 초기화
- BuildingManager.cs — _builtForge, _builtBlacksmith 캐시 멤버 + Register/Unregister/Get 메서드
- PlayerController.cs — digit5(Forge)/digit6(Blacksmith) 키, HandleCraftOrder() 추가
  6번 선택 + F키 조합으로 Blacksmith.TryCraftWeapon() 호출
  HandleConstructionOrder()에 Forge 선행 체크 조건 추가
- Warrior.cs — WeaponType _equippedWeapon 필드, IsEquipped 프로퍼티, Equip(type, bonus) 추가
- AIUnit.cs — TODO 주석 업데이트만 (기능 변경 없음)

**핵심 설계 결정**
- 제작 키: 6번(Blacksmith) 선택 중 F키 — 전역 F키 반응 아님
- 제작 시간: 없음 (즉시 무장) — 제작 중 건물 파괴/Warrior 사망 엣지케이스 방지
- Blacksmith는 Update 없음 — 키 입력은 PlayerController가 처리, Blacksmith는 TryCraftWeapon() 비즈니스 로직만
- FindCraftTarget(): IsAvailableForDispatch(Standby + HP 80%) 조건의 비무장 Warrior 탐색
  CombatAlertQueue → Barracks.CollectAvailableWarriors() 사용 (FindObjectsOfType 금지)
- _reusableWarriorList (readonly List) + 매 탐색 시작 시 Clear() 필수
- 자원 차감 원자성: FindCraftTarget() 성공 후에만 SpendCraftResources() 호출

**주요 리스크**
- R1 (Critical): GameManager SILVER/COPPER 미지원 — 구현 1순위
- R2: Forge 중복 건설 시 UnregisterForge가 두 번째 Forge도 null로 만드는 버그
  방어: PlayerController에서 Forge 이미 존재 시 건설 거부
- R3: _reusableWarriorList.Clear() 누락 시 사망 Warrior 참조 포함

**Warrior.Equip() 설계**
- _isEquipped == true이면 즉시 return (중복 방지)
- _attackDamage += attackBonus (15f → 25f)
- MessageBus.Publish("warrior.equipped", this)
- 상태 전이 없음 — Standby 유지

**구현 순서**
1. WeaponType.cs + GameManager SILVER/COPPER 지원
2. BuildingManager 캐시 멤버 + Warrior.Equip()
3. Forge.cs + Blacksmith.cs
4. PlayerController 확장

[[warrior_fsm_design]] — Warrior 상태/Barracks 구조 참조

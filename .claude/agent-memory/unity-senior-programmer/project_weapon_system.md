---
name: project-weapon-system
description: v0.2-5 무기 시스템 전체 파일 Write 완료 현황 (2026-06-09)
metadata:
  type: project
---

v0.2-5 무기 시스템 9개 파일 전체 작성 완료 (2026-06-09).

**Why:** GDD §v0.2-5 — 비무장 Warrior(15 ATK) → AlloyBlade 장착 시 25 ATK (+10).

**How to apply:** Inspector 연결 및 플레이 테스트 진행 가능 상태.

## 작성 완료 파일 목록
- `Assets/Scripts/WeaponType.cs` — `AIVillage.Resources.WeaponType` enum (None=0, AlloyBlade=1)
- `Assets/Scripts/Buildings/Forge.cs` — 선행 건물 (나무15+돌10), static Instance, OnBuilt에서 등록
- `Assets/Scripts/Buildings/Blacksmith.cs` — 제작 건물 (나무20+돌15), TryCraftWeapon() 구현, 비용 구리5+은3
- `Assets/Scripts/Core/GameManager.cs` — _currentCopper/_currentSilver, AddResource/SpendResource/GetResource COPPER/SILVER case 추가
- `Assets/Scripts/Core/BuildingManager.cs` — HasBuiltForge / HasBuiltBlacksmith 프로퍼티 추가 (is 패턴 매칭)
- `Assets/Scripts/Core/PlayerController.cs` — 5번=Forge/6번=Blacksmith 키, HandleWeaponCraft() (F키) 추가, Blacksmith 선행 조건 체크
- `Assets/Scripts/Units/Warrior.cs` — _isEquipped 필드(언더스코어 포함), _armedAttackDamage=25f, Equip(), IsEquipped/CurrentWeapon/CurrentState 프로퍼티, WarriorEquippedEvent 이벤트 구조체
- `Assets/Scripts/Buildings/Barracks.cs` — CollectUnequippedWarriors() 추가 (Standby + !IsEquipped 조건)
- `Assets/Scripts/Core/CombatAlertQueue.cs` — CollectUnequippedWarriors() public 메서드 추가 (모든 Barracks 위임)

## ResourceType 확인
`Assets/Scripts/ResourceType.cs`에 COPPER/SILVER 이미 존재 — 추가 수정 불필요.

## 핵심 설계 결정
- `_isEquipped` 필드는 Warrior.cs 내부 private 필드 (언더스코어 포함), `IsEquipped` public 프로퍼티로 노출
- `CurrentState` 프로퍼티 추가 — Barracks.CollectUnequippedWarriors()가 UnitState.Standby 조건 확인에 사용
- `_craftBuffer` (List<Warrior>)를 Blacksmith 멤버로 보유하여 GC 최적화
- Forge는 static Instance 패턴, Blacksmith는 Instance 없이 PlayerController가 FindObjectsByType으로 1회 탐색 (F키 입력 시점만)
- 무기 제작 1회 비용은 Warrior 수와 무관 (전원 일괄 무장, 비용 1회 소비)
- warrior.equipped 이벤트: WarriorEquippedEvent 구조체를 Warrior.cs 파일 하단에 정의

## Inspector 연결 필요 항목
- PlayerController._forgePrefab → Forge 프리팹 할당
- PlayerController._blacksmithPrefab → Blacksmith 프리팹 할당
- GameManager Inspector에서 _startingCopper=0, _startingSilver=0 확인 (기본값)

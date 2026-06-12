---
name: week9-danger-registry
description: Week 9 DangerRegistry/PlayerController 설계 핵심 결정 — 자료구조 선택, 만료 방식, 순수 쿼리 채택 이유, 체력 프로퍼티 노출 패턴
metadata:
  type: project
---

Week 9 설계 핵심 결정 (2026-05-29):

**DangerRegistry 자료구조: List<DangerRecord> (Dictionary 아님)**
- DangerRecord는 동일 위치에 중복 기록 허용 (몬스터가 같은 위치에서 여러 유닛 도주 유발)
- IsDangerousArea는 좌표 기반 거리 검색 → Dictionary key가 Vector2라면 오차로 key miss 발생
- 레코드 수가 많아야 수십 개 → List 선형 탐색 비용 무시 가능
- Why: Vector2를 Dictionary key로 쓰면 float 오차로 정확히 같은 key를 만들기 어려움

**만료 정리: 자체 코루틴 (OnTick 연동 아님)**
- DangerRegistry가 독립 싱글톤이므로 GameManager Tick에 의존하지 않는 것이 결합도 낮음
- 코루틴 주기 = 1초. 만료 기준 = Time.time - record.timestamp > _recordLifetime
- Why: OnTick()에 넣으면 Week 10에서 OnTick 순서 변경 시 만료 타이밍이 틀어질 위험

**PathfindingGrid 연동: 순수 쿼리 방식 (walkable 맵 수정 안 함)**
- PathfindingGrid.SetWalkable()을 호출하면 다른 유닛의 A* 경로도 영향받음
- 임시 오버레이 방식(GetWalkableMapCopy 후 수정)은 GetWalkableMapCopy() 호출비용 × 유닛 수
- Gatherer.SearchAndGo()가 노드 선택 전 IsDangerousArea()를 직접 쿼리하는 방식이 더 단순
- Why: 노드 선택 레벨에서 필터링하면 충분. A* 경로 자체를 바꿀 필요 없음

**ResourceManager.GetAvailableNodes() 반환값 활용**
- GetNearestAvailableNode()는 단일 노드만 반환 → DangerRegistry 필터 불가
- 해결: GetAvailableNodes() 전체 목록 받아서 DangerRegistry.IsDangerousArea() 필터 후 거리 기준 정렬
- SearchAndGo()에 GetNearestSafeNode() 로직을 인라인으로 구현 (별도 메서드 신설)
- 기존 GetNearestAvailableNode() 호출은 SearchAndGo() 전용 private 로직으로 대체

**AIUnit._hp 접근: public 프로퍼티 추가**
- PlayerController가 외부에서 _hp와 _maxHp를 읽어야 함
- _hp는 protected → public float Hp { get; } 프로퍼티 AIUnit에 추가
- _maxHp도 public float MaxHp { get; } 추가
- Why: reflection이나 GetComponent 우회 없이 타입 안전하게 접근

**building.reserved MessageBus 채널**
- PlayerController 우클릭 → MessageBus.Publish("building.reserved", building)
- Builder.SearchAndBuild()가 구독 → 수신 즉시 해당 건물 우선 처리
- 기존 SearchAndBuild 루프와 충돌 방지: _isInBuildCycle 체크로 현재 작업 중이면 무시

**[[week8-threat-system]]** — Fleeing MessageBus 채널 "unit.fleeing" 이미 구현됨

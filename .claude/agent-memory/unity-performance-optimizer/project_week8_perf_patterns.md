---
name: project_week8_perf_patterns
description: Week 8 ThreatManager/Monster/Fleeing 성능 분석 결과: 주요 GC 패턴 및 스케일 기준
metadata:
  type: project
---

# Week 8 성능 분석 요약 (2026-05-29)

## 게임 스케일
- Monster 최대 3~5마리, AIUnit 최대 20마리 (v0.1)
- Tick 간격: 0.5초 (GDD R-002)
- 60fps 타겟, Unity 2D URP

## 확인된 GC 할당 패턴

### CRITICAL
1. **PopulationManager.GetAllUnitsSnapshot() — `_units.ToArray()`**
   - GameManager.CheckThreatForAllUnits()에서 매 Tick(0.5s) 호출
   - ToArray()가 매번 새 AIUnit[] 배열 힙 할당 생성
   - 20유닛 기준 20 * 8bytes = 160bytes GC Alloc per Tick
   - 수정: 재사용 가능한 AIUnit[] _snapshotBuffer 캐시 도입

2. **MessageBus.Publish() — `list.ToArray()`**
   - 메시지 발행마다 Action<object>[] 스냅샷 배열 힙 할당
   - unit.fleeing 이벤트: 20유닛 Fleeing 시 20회 연속 Publish 가능
   - 수정: List<T> index for loop + 역방향 순회로 Unsubscribe 안전성 유지

### WARNING (현재 스케일에서는 무해, 확장 시 문제)
3. **Monster.UpdatePatrolling() — Physics2D.OverlapCircle 매 프레임**
   - 5마리 * 60fps = 초당 300회 Physics 쿼리
   - 현재 스케일에서는 <0.05ms/frame으로 무해
   - 10마리 이상 확장 시 타이머 기반 감지 주기(0.2s) 권장

4. **AIUnit.SetDestination() — `new CancellationTokenSource()`**
   - CancellationTokenSource는 IDisposable 관리 객체 (힙 할당)
   - Fleeing 시 SetDestination 재호출로 생성/해제 반복
   - 현재 Fleeing은 1회성이므로 위험도 낮음
   - async Task 자체는 ValueTask 전환 없이도 현 스케일에서 무해

5. **ThreatManager.GetNearestMonster() — `RemoveAll(m => m == null)`**
   - 람다 캡처 없음(m만 참조) → GC 할당 없음 (컴파일러 static 최적화)
   - foreach List<Monster> 순회: Enumerator는 struct이므로 박싱 없음
   - 현재 5마리 * 20유닛 * 2회/s = 200회/s 호출 — 무해

6. **AIUnit.UpdateFleeing() — 매 프레임 GameManager.Instance 접근**
   - static 프로퍼티 접근이므로 GC 없음
   - Vector2 거리 계산: float 연산만, GC 없음
   - 현재 스케일에서 무해

## 안전 확인 항목
- Monster FSM: Update 내 GetComponent 없음 (캐싱 완료)
- AIUnit: enabled 토글로 Idle 상태에서 Update 비활성화 (설계 우수)
- ThreatManager.GetNearestMonster: 제곱 거리 비교로 sqrt 제거 (최적화 완료)
- MessageBus: Dictionary<string, List<Action>> 구조 (O(1) 채널 조회)

## 결론
v0.1 스케일(Monster 5, AIUnit 20)에서 60fps 달성 가능.
ToArray() GC 패턴 2개만 수정하면 GC Spike 완전 제거 가능.
Monster 10마리 이상 확장 전 OverlapCircle 타이머화 필요.

**Why:** Week 8 코드 리뷰 후 성능 분석 의뢰 (코드 리뷰어 감지 주기 타이머 0.2f 제안 포함)
**How to apply:** Week 9 이상 설계 시 ToArray() 패턴 재발생 방지 기준으로 활용

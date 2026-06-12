---
name: project-codebase-patterns
description: "AI Village 코드베이스 패턴, 아키텍처 규약, Week 8 리뷰에서 발견된 반복 패턴 및 주의 사항"
metadata:
  type: project
---

## 아키텍처 규약 (확정)

- 모든 Manager는 GameManager.CacheComponents()에서 GetComponent + AddComponent 패턴으로 자동 등록
- AIUnit 파생 클래스: Start()에서 base.Start() 먼저 호출 (PopulationManager 등록 보장)
- 컬렉션 순회 중 변경 방어: ToArray() 스냅샷 패턴 (PopulationManager.GetAllUnitsSnapshot)
- Coroutine 참조 필드 보유 + null 체크 후 StopCoroutine — 고아 코루틴 방지 표준 패턴
- CancelInvoke(nameof(특정메서드)) 사용 — 전체 CancelInvoke() 금지 (다른 Invoke 취소 방지)
- enabled 토글: Moving || Fleeing 상태만 true (Idle/Gathering/Building/Returning은 false)
- SetDestination(): Fleeing 상태 시 SetState(Moving) 호출 안 함 — 상태 덮어쓰기 방지
- Fleeing 경로 실패 폴백: _useDirectMoveToCamp = true → Vector3.MoveTowards 직선 이동

## Week 8 코드 품질 평가

**코드 전반 품질: 높음** — 설계 명세를 대부분 정확하게 구현. 주니어 레벨치고 안전 패턴 습득 양호.

**발견된 Critical 이슈:**
1. Monster.UpdateAttacking — _target == null 시 TransitionToChasing() 호출: 타겟 없이 Chasing 상태가 됨. TransitionToPatrolling()이 올바름.
2. AIUnit.TakeDamage — Destroy(gameObject) 호출 후에도 동일 프레임에 Monster.AttackRoutine이 null 체크 없이 추가 TakeDamage를 호출할 수 있음 (Destroy는 프레임 끝에 실행, _hp <= 0 가드로 방어 중이므로 실제 Critical은 아님 — 이미 방어됨).
3. Monster 순찰 중 UpdatePatrolling에서 Physics2D.OverlapCircle을 매 프레임 호출 — GDD 감지 범위 3f는 올바름, 단 모든 Monster가 매 프레임 Physics2D 쿼리를 실행하는 비용 주의.

**발견된 Warning 이슈:**
1. AIUnit.UpdateFleeing — 기지 도달 시 _pathCts?.Cancel() 후 Dispose()를 호출하지 않음 → CancellationTokenSource 메모리 누수
2. Monster.AttackRoutine — TakeDamage 후 _target?.name 에서 ?. 연산자 사용 (이미 null 체크 후이므로 불필요한 방어적 코드, 로직 이상은 없음)
3. GatherRoutine — Fleeing 진입 후 GatherRoutine이 이미 실행 중인 경우 OnFleeingEnter에서 StopCoroutine으로 중단하므로 안전. 다만 Invoke된 SearchAndGo가 GatherRoutine 종료 후에도 호출될 수 있으나 Fleeing 체크로 방어됨.

## 팀 패턴 (Coder 에이전트)

- **잘하는 것**: null 가드, 스냅샷 순회, 코루틴 참조 관리, 제곱거리 최적화, const/SerializeField 분리
- **주의할 패턴**: FSM 전환 시 null 타겟 처리 로직 오류 가능성 (UpdateAttacking → Chasing 버그)
- **CancellationTokenSource Dispose 누락**: _pathCts Cancel 후 Dispose 빠뜨리는 경향 있음

## GDD 수치 검증 (Week 8)

- Monster 감지 범위: 3f (코드 일치)
- Monster 공격력: 10f (코드 일치)
- Monster 공격 간격: 1f (코드 일치)
- Monster 추적 포기: 8f (코드 일치)
- AIUnit 기지 안전 반경: 5f (코드 일치)
- AIUnit 체력 회복: 5f HP/초 (코드 일치)
- Monster 이동 속도: 2f (코드 일치, GDD에 명시)

## GDD 수치 검증 (Week 9 — Warrior 시스템)

- Warrior 공격력: 15f (Warrior._attackDamage)
- Warrior 공격 범위: 1.2f (Warrior._attackRange)
- Warrior 공격 간격: 1f (Warrior._attackInterval)
- Warrior 탐지 반경: 5f (Warrior._detectionRadius)
- Warrior 파견 최소 HP: 80% (Warrior._dispatchHpThreshold = 0.8f)
- Warrior 대기 회복 속도: 5f HP/초 (WarriorHealHelper._healRate)
- Barracks 건설 비용: 나무 15, 돌 10 (Barracks.Reset())
- Barracks 훈련 비용: 나무 5, 돌 5 (Barracks._trainCostWood/Stone)
- Barracks 훈련 시간: 10초 (Barracks._trainDuration)
- Barracks 최대 슬롯: 3 (Barracks._maxSlots)

## GDD 수치 검증 (Week v0.2-5 — 무기 시스템)

- Forge 건설 비용: 나무 15, 돌 10 (코드 일치)
- Blacksmith 건설 비용: 나무 20, 돌 15 (코드 일치)
- AlloyBlade 제작 비용: 구리 5, 은 3 (코드 일치)
- 비무장 Warrior 공격력: 15f (코드 일치, _attackDamage)
- 무장 Warrior 공격력: 25f (코드 일치, _armedAttackDamage)
- 시작 구리/은: 0 (코드 일치, _startingCopper/_startingSilver)

## Week v0.2-5 코드 품질 평가

**코드 전반 품질: 양호** — GDD 수치 전부 일치. 자원 케이스 누락 없음.

**발견된 Critical 이슈 (v0.2-5):**
1. PlayerController.FindBuiltBlacksmith() — Blacksmith.Instance 패턴이 없어 매 F키 입력 시 FindObjectsByType<Blacksmith>() 호출. 코드 주석에서 "TODO: Blacksmith에도 static Instance 패턴 도입"이라고 명시되어 있으나 도입하지 않음. 특수 체크 항목 2번 위반: Blacksmith에 Instance 패턴이 없음.
2. Blacksmith.TryCraftWeapon() — Debug.Log(Forge 미충족 메시지, L102)가 #if UNITY_EDITOR 래핑 없이 빌드에 포함됨. 프로젝트 규칙 위반.
3. Blacksmith.TryCraftWeapon() — Debug.Log(자원 부족 메시지, L119~123)가 #if UNITY_EDITOR 래핑 없이 빌드에 포함됨. 프로젝트 규칙 위반.
4. Blacksmith.TryCraftWeapon() — Debug.Log(비무장 없음 메시지, L140)가 #if UNITY_EDITOR 래핑 없이 빌드에 포함됨.
5. CombatAlertQueue.DisplayCurrentAlert() — Debug.Log(경보 표시, L187~193) 두 개 모두 #if UNITY_EDITOR 래핑 없음.
6. AIUnit._isEquipped 필드(protected, AIUnit.cs L62)와 Warrior._isEquipped 필드(private, Warrior.cs L67)가 이름이 동일한 필드 숨김(field hiding) 문제 발생. Warrior에서 _isEquipped 접근 시 AIUnit의 필드가 아닌 Warrior 자신의 필드를 사용하므로 기능은 작동하지만, 설계 의도(AIUnit의 _isEquipped를 재활용)와 괴리가 있으며 혼란 유발.

**발견된 Warning 이슈 (v0.2-5):**
1. Blacksmith — Instance 패턴 미구현: PlayerController가 FindObjectsByType<Blacksmith>()에 의존. 씬에 미완공 Blacksmith가 있을 경우 IsBuilt 체크로 방어되지만 FindObjectsByType 자체는 씬 전체 탐색으로 성능 비용 발생.
2. Barracks.TrainingLoop — OnBuilt()에서 StartCoroutine(TrainingLoop()) 참조를 보관하지 않음. OnDestroy()에서 StopCoroutine으로 명시적 중단 불가. 프로젝트 규칙 "Coroutine 참조 필드 보관" 위반.

**팀 패턴 업데이트 (v0.2-5):**
- Debug.Log를 #if UNITY_EDITOR 래핑 없이 작성하는 패턴 반복됨 (Blacksmith 4건, CombatAlertQueue 2건). 향후 모든 Debug.Log에 대해 우선 체크 필요.
- AIUnit에 선언된 protected 필드를 파생 클래스에서 같은 이름으로 재선언하는 패턴 주의 (_isEquipped).
- Singleton 패턴을 일부 건물에만 적용하고 일부는 FindObjectsByType으로 대체하는 불일치 발생. 설계 명세(특수 체크 항목 1번)에서 Blacksmith도 Instance 패턴 요구.

## Week 9 코드 품질 평가

**코드 전반 품질: 높음** — Warrior FSM 설계 의도를 명세대로 정확히 구현. 가드 주석, CancellationToken 처리, null 안전성 모두 양호.

**발견된 Critical 이슈:**
1. Warrior.AttackRoutine — AttackRoutine 내 _attackRoutineRunning 플래그가 코루틴 자연 종료 경로에서 StartReturning()을 호출하지만, UpdateFighting()이 같은 프레임 또는 이후 프레임에 _targetMonster == null을 감지하여 StartReturning()을 중복 호출할 수 있음. 결과: SetDestination(home)이 두 번 호출 → 불필요하지만 치명적이지는 않음. 단, _isReturning 플래그가 두 번 설정되므로 무해. 실질적 Critical 없음.

**발견된 Warning 이슈 (Week 9):**
1. CombatAlertQueue.GetAllAvailableWarriors() / CountAvailableWarriors() — FindObjectsOfType<Barracks>()를 매 프레임이 아니라 입력/표시 시점에만 호출하지만, DisplayCurrentAlert()가 Update() 내에서 _alertDisplayed 플래그로 제어됨. DispatchWarriors() 시점의 1회 호출은 허용 범위. 그러나 CountAvailableWarriors()와 GetAllAvailableWarriors()가 각자 FindObjectsOfType를 호출하는 중복 탐색 문제 있음.
2. CombatAlertQueue — Barracks 목록을 캐싱하지 않고 매 호출마다 FindObjectsOfType<Barracks>() 실행. DisplayCurrentAlert() 호출 시마다 씬 전체 탐색 발생.
3. Barracks.GetAvailableWarriors() — 매 호출마다 new List<Warrior>() 할당. CombatAlertQueue가 CountAvailableWarriors()와 GetAllAvailableWarriors()에서 각각 호출하므로 한 번의 파견 처리에 두 번 할당.
4. Warrior.TryDispatch에서 EnterStandby() 이전 ExitStandby() 호출 순서는 올바름. SetDestination() 이전 ExitStandby()가 호출되므로 Moving 상태 전환 전 시각화 정상 복원 확인됨.
5. Warrior.OnPathFailed() — base 미호출 의도적, Fleeing 없으므로 정상. 단 주석이 이미 명시되어 있음.

**팀 패턴 업데이트 (Week 9):**
- FindObjectsOfType 중복 호출 패턴: CombatAlertQueue에서 CountAvailableWarriors + GetAllAvailableWarriors가 각각 FindObjectsOfType를 호출 → 향후 리뷰에서 주시
- Barracks 목록 캐싱 없음: 씬 구조 변경 시 캐시 무효화 처리가 없으므로 런타임 동적 추가/제거에 주의
- Warrior 파견 후 귀환 중복 호출 방지: StartReturning() 중복 호출 가능성은 _isReturning 플래그로 무해하게 방어됨

## GOAP 2단계 코드 품질 평가 (2026-06-12)

**코드 전반 품질: 양호** — 아키텍처 설계 의도가 코드에 잘 반영됨. 하위 호환성 분기도 전 경로 정상. 단 Critical 버그 1건 존재.

**발견된 Critical 이슈:**
1. GoapAgent.Replan() — 플랜 사이클(SearchNode→Gather→Return) 완료 후 Replan()에서 동일 Goal로 재개 시 `goalChanged=false`, `_currentActionIndex >= _planBuffer.Count`(소진 상태)이므로 `_currentActionIndex < 0` 조건도 false → `_planBuffer[0].Execute()`가 호출되지 않아 Gatherer가 영구 정지. 수정: `_currentActionIndex >= _planBuffer.Count` 조건을 `_currentActionIndex < 0` 또는 조건에 추가.

**발견된 Warning 이슈:**
1. GoapAgent.NotifyArrival() — SearchNodeAction → GatherAction 인덱스 전환 후 `_planBuffer[_currentActionIndex]`가 GatherAction이 아닐 수 있는 엣지케이스 (플랜 버퍼가 SearchNode만 있는 경우). bounds 체크 없음.
2. Gatherer.GatherRoutine() — `gathered==0` (노드 고갈) 케이스에서 GOAP 경로에서 `_isInGatherCycle = false`를 설정하지 않음. 다음 Tick 재플래닝은 가능하나 내부 상태 불일치.
3. SearchAndGo() 내 Invoke — GOAP 재플래닝으로 Goal이 바뀌어도 CancelInvoke가 호출되지 않는 경로 존재 (NotifyPathFailed 없는 단순 Goal 전환 시).

**팀 패턴 업데이트 (GOAP 2단계):**
- Replan() 재진입 조건에서 플랜 인덱스 소진 상태(_currentActionIndex >= Count)를 별도 체크하지 않는 패턴 → 향후 GOAP 관련 코드 리뷰에서 "플랜 소진 후 동일 Goal 재개" 케이스 우선 확인
- GOAP 경로에서 내부 FSM 플래그(_isInGatherCycle 등) 동기화 누락 경향

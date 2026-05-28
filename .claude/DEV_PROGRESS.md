# 📝 AI Village — 개발 진행 로그

> 세션별 개발 내용, 트러블슈팅, 구현 완료 파일 목록을 기록한 문서.
> 최종 수정: 2026-05-29

---

## 개발 환경

| 항목 | 내용 |
|------|------|
| 엔진 | Unity 2D URP |
| 언어 | C# |
| 경로탐색 | AStar 2D Grid Pathfinding (Asset Store) |
| Input | New Input System |
| 개발 도구 | Claude Code (AI 페어 프로그래밍) |
| 에이전트 파이프라인 | Architect → Programmer → Reviewer → Optimizer → Revision-Coder |

---

## 구현된 파일 목록

```
Assets/Scripts/
├── ResourceType.cs               Week 1  — ResourceType enum (WOOD/STONE)
├── ResourceNode.cs               Week 1  — 자원 노드 상태/채집/재생/예약 관리
├── Core/
│   ├── GameManager.cs            Week 2  — 싱글톤, 시작 자원, Tick 루프, 자원 API, 자동 스폰
│   ├── ResourceManager.cs        Week 2  — 노드 등록/관리, 최근접 노드 조회
│   ├── PathfindingGrid.cs        Week 3  — 60x60 그리드 싱글톤, 좌표 변환
│   ├── MessageBus.cs             Week 5  — string 채널 이벤트 버스 싱글톤
│   ├── BuildingManager.cs        Week 6  — 건물 등록/관리, 미완공 건설지 조회
│   ├── PopulationManager.cs      Week 7  — 인구 추적, HasRoom 체크
│   └── ThreatManager.cs          Week 8  — Monster 등록/해제, GetNearestMonster() API
├── Data/
│   ├── UnitState.cs              Week 3  — FSM 상태 열거형 (6개)
│   └── IMessageBus.cs            Week 5  — 이벤트 버스 인터페이스
├── Units/
│   ├── AIUnit.cs                 Week 3  — 공통 기반 클래스, A* 비동기 이동, FSM
│   ├── Gatherer.cs               Week 4  — 자원 수집 FSM (Idle→Moving→Gathering→Returning)
│   ├── Builder.cs                Week 6  — 건설 FSM (Idle→Moving→Building→Idle)
│   └── Monster.cs                Week 8  — 독립 MonoBehaviour, Patrolling/Chasing/Attacking FSM
├── Buildings/
│   ├── Building.cs               Week 6  — 건물 기반 클래스, 예약/건설 상태 관리
│   └── House.cs                  Week 6  — 완공 시 BasePosition 설정
└── Tests/
    └── Week3Test.cs              Week 3  — Gatherer 이동 테스트 (스페이스바 트리거)
```

---

## Week별 구현 내용 및 트러블슈팅

---

### ✅ Week 1 — ResourceNode + 기본 씬 (완료)

**구현 내용:**
- `ResourceType.cs`: WOOD/STONE enum 정의
- `ResourceNode.cs`: Available → Reserved → Depleted → Available 순환 상태 관리
  - `TryReserve()`, `ReleaseReservation()`, `Gather()`, `IsAvailable()` API
  - `OnMouseDown()` 클릭 테스트 (콘솔 상태 출력)
  - `OnDrawGizmosSelected()` Scene 뷰 시각화 (색상: 초록=Available, 노랑=Reserved, 갈색=Depleted)

**트러블슈팅:**
- `[RequireComponent(typeof(Collider))]` → Unity가 3D Collider 요구 → `Collider2D`로 수정

**테스트 결과:** ✅ 노드 클릭 시 콘솔에 상태 출력 확인

---

### ✅ Week 2 — GameManager + ResourceManager (완료)

**구현 내용:**
- `GameManager.cs`: 싱글톤 + DontDestroyOnLoad, 시작 자원(나무15/돌8), Tick 루프(0.5s)
  - `AddResource()`, `SpendResource()`, `GetResource()` API
  - `BasePosition` 프로퍼티 (Week 6 House가 설정)
  - `ResourceManager` 자동 추가
- `ResourceManager.cs`: 노드 등록/해제, `GetAvailableNodes()`, `GetNearestAvailableNode()`

**테스트 결과:** ✅ Play 시 콘솔에 시작 자원 출력 확인

---

### ✅ Week 3 — A* 이동 + PathfindingGrid (완료)

**구현 내용:**
- `PathfindingGrid.cs`: 60x60 그리드 싱글톤, `WorldToGrid()`, `GridToWorld()`, `SetWalkable()`
- `AIUnit.cs` (전면 재작성): `using AStar` 기반 비동기 경로 탐색
  - `CancellationTokenSource`로 Race Condition 방지
  - `enabled = (newState == Moving)` 토글로 Update 비용 절감
  - `ARRIVAL_THRESHOLD_SQ = 0.04f` (0.2f² 거리 판정)
- `UnitState.cs`: 6개 상태 열거형 (Idle/Moving/Gathering/Returning/Building/Fleeing)
- `Gatherer.cs`: AIUnit 파생, `OnReachDestination()` / `OnIdle()` 구현
- `Week3Test.cs`: 스페이스바로 목적지 설정 테스트

**트러블슈팅:**
- 설치된 패키지가 `AStar 2D Grid Pathfinding`임을 확인 (`using Pathfinding` 아님)
- `AIUnit.Awake()`에서 `enabled=false` 설정 시 파생 클래스의 `Start()` 호출되지 않음
  → Awake에서 enabled 조작 제거로 해결 (FollowPath guard가 Idle 중 비용 처리)
- New Input System 사용 중 → `Input.GetKeyDown` → `Keyboard.current.spaceKey.wasPressedThisFrame`으로 교체

**테스트 결과:** ✅ Gatherer가 ResourceNode 방향으로 정상 이동 확인

---

### ✅ Week 4 — GathererFSM (완료)

**구현 내용:**
- `ResourceNode.cs` 수정: `Start()`에서 ResourceManager 자동 등록, `OnDestroy()`에서 해제
- `Gatherer.cs` 전면 재작성:
  - `SearchAndGo()` → 최근접 비예약 노드 탐색 → `TryReserve()` → `SetDestination()`
  - `GatherRoutine()` Coroutine → `WaitForSeconds(gatherDuration)` → `Gather()` → `SetDestination(home)`
  - `OnReachDestination()` → 노드 도착: 채집 시작 / 기지 도착: 자원 반납 + 예약 해제
  - `OnIdle()` → `_isInGatherCycle` 플래그로 재진입 방지
  - `OnPathFailed()` → `CancelInvoke` + 예약 해제 + 재탐색

**핵심 설계 결정:**
- 채집 타이머에 Update 대신 **Coroutine** 채택 (enabled=false 상태에서도 실행)
- `_isInGatherCycle` 플래그로 `OnIdle()` 중복 트리거 방지

**트러블슈팅:**
- Gatherer가 움직이지 않음 → `AIUnit.Awake()`의 `enabled=false`가 `Gatherer.Start()` 실행 차단
  → `AIUnit.Awake()`에서 `enabled=false` 제거

**테스트 결과:** ✅ Gatherer 자동 수집 → 귀환 루프 정상 동작

---

### ✅ Week 5 — MessageBus + Multi-Gatherer (완료)

**구현 내용:**
- `IMessageBus.cs` 신규: `AIVillage.Core` 네임스페이스, Subscribe/Unsubscribe/Publish 정의
- `ResourceType.cs` 수정: IMessageBus 인터페이스 제거 (독립 파일로 이동)
- `MessageBus.cs` 신규: Dictionary 기반 채널 구독/발행 싱글톤
  - `list.ToArray()` 스냅샷 순회 (핸들러 내 Unsubscribe 안전)
- `GameManager.cs` 수정:
  - `MessageBus` 컴포넌트 자동 추가
  - `Start()`에서 `resource.node.depleted`, `resource.node.regenerated` 이벤트 구독
  - `AddResource()`에서 `resource.deposited` 이벤트 발행
  - `ResourceDepositedEvent` 페이로드 구조체 추가
- `ResourceManager.cs` 수정: `RegisterNode()` 시 MessageBus 자동 주입

**테스트 결과:** ✅ Gatherer 3개가 서로 다른 노드를 예약하여 분산 채집, MessageBus 이벤트 로그 출력 확인

---

### ✅ Week 6 — BuilderFSM + BuildingManager (완료)

**구현 내용:**
- `Building.cs` 신규: 3단계 상태(Unbuilt/UnderConstruction/Built), 예약 시스템, 자원 차감
  - `StartConstruction()`: 나무+돌 동시 확인 후 일괄 차감 (부분 차감 방지)
  - `CompleteConstruction()` → `virtual OnBuilt()` 콜백
- `House.cs` 신규: `OnBuilt()`에서 `GameManager.BasePosition` 자동 설정
- `BuildingManager.cs` 신규: 건물 등록/해제, `GetNearestPendingBuilding()`
- `Builder.cs` 신규: Gatherer와 동일한 FSM 패턴
  - `BuildRoutine()` Coroutine (건설 타이머)
  - `ResetCycle()` 내 `StopCoroutine` 자기 호출 방지 (코루틴 내 직접 필드 초기화)
- `GameManager.cs` 수정: `BuildingManager` 컴포넌트 자동 추가

**테스트 결과:** ✅ Builder가 House_Site 발견 → 이동 → 자원 차감 → 건설 완료 → GameManager.BasePosition 업데이트

---

### ✅ Week 7 — PopulationManager + 자동 스폰 (완료)

**구현 내용:**
- `PopulationManager.cs` 신규: `CurrentPop`, `MaxPop`, `HasRoom`, `RegisterUnit()`, `UnregisterUnit()`
- `AIUnit.cs` 수정:
  - `protected virtual Start()` 추가 → PopulationManager 자동 등록
  - `OnDestroy()` 수정 → PopulationManager 자동 해제
- `Gatherer.cs` 수정: `Start()` → `protected override Start()` + `base.Start()` 호출
- `Builder.cs` 수정: 동일
- `GameManager.cs` 수정:
  - `PopulationManager` 컴포넌트 자동 추가
  - 스폰 필드: `_gathererPrefab`, `_gathererSpawnCostWood=10`, `_spawnRadius=1f`, `_spawnCooldown=5f`
  - `CheckAutoSpawn()`: HasRoom + 자원 + 쿨다운 조건 충족 시 Gatherer 프리팹 스폰

**핵심 설계 결정:**
- 스폰 쿨다운(`_spawnCooldown=5f`) 추가 — Tick(0.5s)마다 검사하므로 쿨다운 없으면 자원 충족 시 폭발적 스폰 발생

**테스트 결과:** ✅ 나무 10개 이상 보유 시 5초 간격으로 Gatherer 자동 스폰, 인구 상한 도달 시 스폰 중단

---

### ✅ Week 8 — ThreatManager + Monster + Fleeing (완료)

**구현 내용:**
- `ThreatManager.cs` 신규: Monster 등록/해제, `GetNearestMonster(Vector2, float)` API
  - 순회 전 `RemoveAll(m => m == null)` 방어 처리
- `Monster.cs` 신규: 독립 MonoBehaviour (AIUnit 상속 없음), Vector3.MoveTowards 이동
  - `MonsterState` enum: Patrolling / Chasing / Attacking
  - 히스테리시스(_attackRange * 1.2f)로 Chasing ↔ Attacking Flicker 방지
  - `ResetToNearestWaypoint()`: 추적 포기 후 가장 가까운 웨이포인트부터 순찰 재개
  - `OnDrawGizmosSelected()`: 감지/공격/추적포기 반경 시각화
- `AIUnit.cs` 수정:
  - `SetState` private → protected, enabled 조건에 `Fleeing` 추가
  - `SetFleeing()` public: 재진입 방지 + 기지 반경 내 체크 + OnFleeingEnter 콜백
  - `TakeDamage(float)` public: Destroy 지연 재진입 방어 포함
  - `UpdateFleeing()`: 기지 도달 판정 + 체력 회복 + 직선 이동 폴백(_useDirectMoveToCamp)
  - `OnFleeingEnter()` abstract, `OnFleeingExit()` virtual 콜백 추가
  - `SetDestination()`: Fleeing 중 Moving으로 덮어쓰지 않는 분기 추가
- `Gatherer.cs` 수정: `OnFleeingEnter/Exit` 구현 (코루틴 중단, 노드 예약 해제, 재탐색 재개)
- `Builder.cs` 수정: `OnFleeingEnter/Exit` 구현 (코루틴 중단, 건물 예약 해제, 재탐색 재개)
- `GameManager.cs` 수정:
  - `ThreatManager` 프로퍼티 + `CacheComponents()`에 자동 추가
  - `CheckThreatForAllUnits()`: Tick마다 snapshot 순회로 위협 감지 → `SetFleeing()` 호출
  - `_threatDetectionRadius = 3f` Inspector 노출
- `PopulationManager.cs` 수정:
  - `GetAllUnitsSnapshot()`: `_snapshotBuffer` + `_isDirty` 캐시 패턴으로 GC 최적화

**PR 리뷰 수정 내역:**
- [Critical] Monster.UpdateAttacking: `_target == null` 시 TransitionToChasing → TransitionToPatrolling 수정
- [Warning] AIUnit.UpdateFleeing: `_pathCts?.Dispose() + _pathCts = null` 누락 추가
- [Warning] PopulationManager.GetAllUnitsSnapshot: ToArray() → _snapshotBuffer 캐시로 GC 최적화
- [Suggestion] Monster.AttackRoutine: `_target?.name` → `_target.name`
- [Suggestion] AIUnit.TakeDamage: Destroy 지연 방어 주석 추가

**런타임 버그 수정 (테스트 후 발견):**

**Bug 1: Gatherer-Monster 겹침 반복 루프**
- 현상: Gatherer가 도망 후 OnFleeingExit()에서 SearchAndGo() 호출 시 위험 노드로 즉시 복귀 → 몬스터와 계속 겹침
- 원인: 노드 위치의 몬스터만 체크했고, 기지까지 쫓아온 몬스터(Gatherer 자신 주변)는 체크하지 않았음
- 수정: `SearchAndGo()`에 두 가지 안전 체크 추가
  1. `GetNearestMonster(transform.position, _safeNodeRadius)` — Gatherer 자신 주변 몬스터 체크
  2. `GetNearestMonster(node.transform.position, _safeNodeRadius)` — 목적지 노드 주변 몬스터 체크
  → 둘 중 하나라도 있으면 `_retryDelay`(2초) 후 재시도

**Bug 2: Monster 기지 경계 진동 현상 (Oscillation)**
- 현상: Gatherer가 House(기지) 안에 있으면 Monster가 감지→추적 시작→IsTargetNearBase() 판정→Patrolling 복귀→즉시 재감지 루프 반복, 몬스터가 웨이포인트와 기지 사이에서 떨림
- 원인: UpdatePatrolling() 감지 단계에서 기지 안 유닛도 추적 시도했고, 추적 시작 후에야 IsTargetNearBase()로 포기 판정이 이루어짐
- 수정: `UpdatePatrolling()`의 감지 단계에서 `IsUnitNearBase(unit)` 사전 필터링 추가
  → 기지 안전 구역 내 유닛은 처음부터 추적 대상에서 제외

**_baseAbandonRadius Inspector 노출 (테스트 후 사용자 요청):**
- 기존: `private const float BASE_ABANDON_RADIUS = 5f` (코드 수정 없이 조정 불가)
- 변경: `[SerializeField] private float _baseAbandonRadius = 5f` (Inspector에서 Monster별 조정 가능)
- 위치: Monster Inspector의 "기지 안전 구역 설정" 헤더 아래
- 주의: `_detectionRange`(3f)보다 크게 유지해야 감지/포기 루프 방지 조건이 성립함

**핵심 설계 결정:**
- Fleeing 감지: GameManager Tick(0.5s) 기반 ThreatManager 폴링 (Physics2D.OverlapCircle 매 프레임 대신)
- Monster는 AIUnit 상속 없음 — 단순 MoveTowards로 성능 예산 절약
- Fleeing 경로 실패 시 직선 이동 폴백 (_useDirectMoveToCamp) — 생존 우선
- 기지 안전 구역: UpdatePatrolling 감지 단계 + UpdateChasing 추적 포기 단계 양쪽에서 IsUnitNearBase() 체크 (이중 방어)

**Unity 에디터 설정 (테스트 전 필수):**
1. GameManager GameObject에 ThreatManager 자동 추가됨 (CacheComponents)
2. Monster 프리팹: Collider2D 추가, _waypoints에 씬 Transform 2~3개 연결
3. AIUnit(Gatherer/Builder) 프리팹에 'Unit' 레이어 지정
4. Monster Inspector의 _unitLayerMask에 'Unit' 레이어 선택
5. Monster Inspector의 _baseAbandonRadius: _detectionRange(3f)보다 크게 유지 (기본값 5f 권장)

**테스트 결과:** ✅ Gatherer 도망/복귀 루프 정상, Monster 기지 경계 진동 없음, _baseAbandonRadius Inspector 조정 확인

---

## 다음 단계 (Week 9~10 예정)

| 주차 | 목표 | 핵심 작업 |
|------|------|---------|
| Week 9 | DangerRegistry + 플레이어 지시 2가지 | 위험 좌표 기록, 파견 거부 로직, PlayerController |
| Week 10 | TownHall + 승리/패배 + 폴리싱 | CheckWinLoseCondition, UI, 첫 플레이어블 빌드 |

---

*이 문서는 매 개발 세션 종료 시 업데이트됩니다.*

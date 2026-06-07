# 📝 AI Village — 개발 진행 로그

> 세션별 개발 내용, 트러블슈팅, 구현 완료 파일 목록을 기록한 문서.
> 최종 수정: 2026-05-30

---

## 개발 환경

| 항목 | 내용 |
|------|------|
| 엔진 | Unity 2D URP |
| 언어 | C# |
| 경로탐색 | AStar 2D Grid Pathfinding (Asset Store) |
| Input | New Input System (UnityEngine.InputSystem) |
| 개발 도구 | Claude Code (AI 페어 프로그래밍) |
| 에이전트 파이프라인 | Architect → Programmer → Reviewer → Optimizer → Revision-Coder |

---

## 구현된 파일 목록 (최신 상태)

```
Assets/Scripts/
├── ResourceType.cs               Week 1      — ResourceType enum (WOOD/STONE)
├── ResourceNode.cs               Week 1,4    — 자원 노드 상태/채집/재생/예약 관리
├── Core/
│   ├── GameManager.cs            Week 2,7,8,9,10 — 싱글톤, Tick, 자원 API, 자동 스폰
│   ├── ResourceManager.cs        Week 2      — 노드 등록/관리, 최근접 노드 조회
│   ├── PathfindingGrid.cs        Week 3      — 60x60 그리드 싱글톤, 좌표 변환
│   ├── MessageBus.cs             Week 5      — string 채널 이벤트 버스 싱글톤
│   ├── BuildingManager.cs        Week 6      — 건물 등록/관리, 미완공 건설지 조회
│   ├── PopulationManager.cs      Week 7      — 인구 추적, HasRoom 체크, GC 최적화 스냅샷
│   ├── ThreatManager.cs          Week 8      — Monster 등록/해제, GetNearestMonster() API
│   ├── DangerRegistry.cs         Week 9      — 위험 좌표 기록/쿼리/만료 싱글톤
│   └── PlayerController.cs       Week 9,10   — 좌클릭 파견 / 우클릭 건설 (다중 건물)
├── Data/
│   ├── UnitState.cs              Week 3      — FSM 상태 열거형 (6개)
│   └── IMessageBus.cs            Week 5      — 이벤트 버스 인터페이스
├── Units/
│   ├── AIUnit.cs                 Week 3,8,9  — 공통 기반 클래스, A* 비동기 이동, FSM
│   ├── Gatherer.cs               Week 4,8,9  — 자원 수집 FSM, DangerRegistry 이중 필터
│   ├── Builder.cs                Week 6,8,9  — 건설 FSM, building.reserved 구독
│   └── Monster.cs                Week 8      — 독립 MonoBehaviour, 3상태 FSM
├── Buildings/
│   ├── Building.cs               Week 6,10   — 건물 기반 클래스 (BuildCostWood/Stone getter 추가)
│   ├── House.cs                  Week 6,8    — _safeZoneRadius + OnDrawGizmos()
│   ├── TownHall.cs               Week 10     — 승리 조건 건물
│   └── Quarry.cs                 Week 10     — 돌 자동생산 건물 (신규)
└── Tests/
    └── Week3Test.cs              Week 3      — Gatherer 이동 테스트
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
- `Week3Test.cs`: 스페이스바로 목적지 설정 테스트

**트러블슈팅:**
- 설치된 패키지: `AStar 2D Grid Pathfinding` (`using Pathfinding` 아님, `using AStar` 사용)
- `AIUnit.Awake()`에서 `enabled=false` 설정 시 파생 클래스의 `Start()` 호출되지 않음 → Awake에서 enabled 조작 제거
- New Input System: `Input.GetKeyDown` → `Keyboard.current.spaceKey.wasPressedThisFrame`으로 교체

**테스트 결과:** ✅ Gatherer가 ResourceNode 방향으로 정상 이동 확인

---

### ✅ Week 4 — GathererFSM (완료)

**구현 내용:**
- `ResourceNode.cs` 수정: `Start()`에서 ResourceManager 자동 등록
- `Gatherer.cs` 전면 재작성:
  - `SearchAndGo()` → 최근접 비예약 노드 탐색 → `TryReserve()` → `SetDestination()`
  - `GatherRoutine()` Coroutine → 채집 타이머 → 기지 귀환
  - `_isInGatherCycle` 플래그로 `OnIdle()` 중복 트리거 방지

**핵심 설계 결정:**
- 채집 타이머에 Update 대신 **Coroutine** 채택 (enabled=false 상태에서도 실행)

**테스트 결과:** ✅ Gatherer 자동 수집 → 귀환 루프 정상 동작

---

### ✅ Week 5 — MessageBus + Multi-Gatherer (완료)

**구현 내용:**
- `IMessageBus.cs` 신규: Subscribe/Unsubscribe/Publish 인터페이스
- `MessageBus.cs` 신규: Dictionary 기반 채널 구독/발행 싱글톤
  - `list.ToArray()` 스냅샷 순회 (핸들러 내 Unsubscribe 안전)
- `GameManager.cs` 수정: MessageBus 자동 추가, 이벤트 구독

**테스트 결과:** ✅ Gatherer 3개 분산 채집, MessageBus 이벤트 로그 출력 확인

---

### ✅ Week 6 — BuilderFSM + BuildingManager (완료)

**구현 내용:**
- `Building.cs` 신규: Unbuilt/UnderConstruction/Built 3단계 상태, 예약 시스템
  - `StartConstruction()`: 나무+돌 동시 확인 후 일괄 차감 (부분 차감 방지)
- `House.cs` 신규: `OnBuilt()`에서 `GameManager.BasePosition` 자동 설정
- `BuildingManager.cs` 신규: 건물 등록/해제, `GetNearestPendingBuilding()`
- `Builder.cs` 신규: Gatherer와 동일한 FSM 패턴, `BuildRoutine()` Coroutine

**테스트 결과:** ✅ Builder가 House_Site 발견 → 이동 → 자원 차감 → 건설 완료

---

### ✅ Week 7 — PopulationManager + 자동 스폰 (완료)

**구현 내용:**
- `PopulationManager.cs` 신규: `CurrentPop`, `MaxPop`, `HasRoom`, 자동 등록/해제
- `AIUnit.cs` 수정: `protected virtual Start()` → PopulationManager 자동 등록
- `GameManager.cs` 수정: `CheckAutoSpawn()` — HasRoom + 자원 + 쿨다운 조건 충족 시 스폰

**핵심 설계 결정:**
- 스폰 쿨다운(`_spawnCooldown=5f`) 추가 (Tick 0.5s마다 검사 → 쿨다운 없으면 폭발적 스폰)

**테스트 결과:** ✅ 나무 10개 이상 보유 시 5초 간격 Gatherer 자동 스폰, 인구 상한 도달 시 중단

---

### ✅ Week 8 — ThreatManager + Monster + Fleeing (완료)

**구현 내용:**
- `ThreatManager.cs` 신규: Monster 등록/해제, `GetNearestMonster(Vector2, float)` API
- `Monster.cs` 신규: 독립 MonoBehaviour (AIUnit 상속 없음), Vector3.MoveTowards 이동
  - `MonsterState` enum: Patrolling / Chasing / Attacking (Monster.cs 내 선언)
  - 히스테리시스(_attackRange * 1.2f)로 Chasing ↔ Attacking Flicker 방지
  - `ResetToNearestWaypoint()`: 추적 포기 후 순찰 재개
  - `OnDrawGizmosSelected()`: 노랑=감지(3f), 빨강=공격(0.6f), 파랑=추적포기(8f)
- `AIUnit.cs` 수정: `SetFleeing()`, `TakeDamage()`, `UpdateFleeing()`, `OnFleeingEnter/Exit` 추가
- `Gatherer.cs` / `Builder.cs` 수정: `OnFleeingEnter/Exit` 구현
- `GameManager.cs` 수정: `CheckThreatForAllUnits()` Tick 연동
- `PopulationManager.cs` 수정: GC 최적화 스냅샷 캐시 (`_snapshotBuffer` + `_isDirty`)

**PR 리뷰 수정 내역:**
- [Critical] Monster.UpdateAttacking: `_target == null` 시 TransitionToPatrolling으로 수정
- [Warning] AIUnit.UpdateFleeing: `_pathCts?.Dispose() + _pathCts = null` 누락 추가
- [Warning] PopulationManager.GetAllUnitsSnapshot: ToArray() → _snapshotBuffer 캐시로 GC 최적화

**런타임 버그 수정 (테스트 후 발견):**

**Bug 1: Gatherer-Monster 겹침 반복 루프**
- 원인: 노드 위치 몬스터만 체크, 기지까지 따라온 몬스터(자신 주변)는 체크 안 함
- 수정: `SearchAndGo()`에 내 위치 + 목적지 노드 위치 두 가지 안전 체크 추가

**Bug 2: Monster 기지 경계 진동 현상 (Oscillation)**
- 원인: UpdatePatrolling() 감지 단계에서 기지 안 유닛도 추적 시도
- 수정: `UpdatePatrolling()` 감지 단계에 `IsUnitNearBase(unit)` 사전 필터링 추가

**테스트 결과:** ✅ Gatherer 도망/복귀 루프 정상 동작, Monster 기지 경계 진동 없음

---

### 🔧 Week 8 사후 버그 수정 + 아키텍처 개선 (2026-05-29)

**GIF 분석으로 발견한 추가 버그 3가지:**

**Bug 3 — AIUnit 경로 계산 중 멈춤 (GIF 1 확인)**
- 현상: Monster를 만나면 Gatherer가 즉시 멈추고 공격받아 사망
- 원인: `SetFleeing()` → A* 계산 시작 → `_isPathReady=false` 동안 `FollowPath()` early return → 완전 정지
- 수정 (`AIUnit.cs - UpdateFleeing`): `_useDirectMoveToCamp || !_isPathReady` 조건 → 경로 계산 중에도 즉시 기지 방향 직선 이동

**Bug 4 — SetFleeing() 기지 근처 early return (GIF 2 확인)**
- 현상: `Monster → Chasing → Patrolling 복귀` 무한 반복, Gatherer 완전 고정
- 원인: 기지 5f 이내 유닛에게 `SetFleeing()` 무조건 return → Monster 바로 옆에도 도주 불가
- 수정 (`AIUnit.cs - SetFleeing`): 기지 5f 이내에서도 Monster가 1.5f 이내 접근 시 Fleeing 허용

**Bug 5 — Monster Attacking 중 기지 진입 시 공격 계속**
- 원인: `UpdateAttacking()`에서 `IsTargetNearBase()` 체크 없음
- 수정 (`Monster.cs - UpdateAttacking`): `IsTargetNearBase()` 체크 추가

**기지 안전 구역 단일화 — House로 통합:**
- 삭제: `Monster._baseAbandonRadius`, `AIUnit._baseSafeWorldRadius` 필드 제거
- 추가: `House._safeZoneRadius(5f)` 단일 필드 → `GameManager.RegisterSafeZone()` / `IsInSafeZone()` 경유
- 모든 판정(`Monster.IsUnitNearBase`, `AIUnit.SetFleeing`, `AIUnit.UpdateFleeing`)이 `gm.IsInSafeZone()` 사용
- **안전 구역 변경 시 House Inspector의 `_safeZoneRadius` 하나만 수정**

**Gizmo 시각화 추가:**

| 오브젝트 | 메서드 | 표시 내용 |
|----------|--------|----------|
| House | `OnDrawGizmos()` (항상) | 완공 전: 회색 원 / 완공 후: 하늘색 원 (안전 구역) |
| Monster | `OnDrawGizmosSelected()` | 노랑=감지(3f), 빨강=공격(0.6f), 파랑=추적포기(8f) |
| Gatherer | `OnDrawGizmosSelected()` | 상태 색상 마커 + 주황=_safeNodeRadius(4f) + 목표노드 선 |

**Gatherer 상태 색상:** 회색=Idle / 노랑=이동 / 초록=채집 / 하늘=귀환 / 빨강=도주

**Unity 에디터 설정 변경사항:**
- Monster Inspector: `_baseAbandonRadius` 항목 **삭제됨** (House에서 통합 관리)
- AIUnit Inspector: `_baseSafeWorldRadius` 항목 **삭제됨** (House에서 통합 관리)
- House Inspector: `_safeZoneRadius`(기본 5f) **신규 추가** — 이 값 하나만 조정

---

### ✅ Week 10 — TownHall + 승리/패배 조건 (진행 중)

**구현 내용:**

**`TownHall.cs` 신규 (`Assets/Scripts/Buildings/`):**
- `Building` 상속, 추가 SerializeField 없음
- `Reset()`: 컴포넌트 추가 시 나무 30 / 돌 20 자동 세팅 (Unity Editor 콜백)
- `OnBuilt()`: `GameManager.Instance.RegisterTownHall()` 호출
- `OnDrawGizmos()`: 완공 전 회색, 완공 후 금색 WireSphere (반경 1.5f)

**`Building.cs` 수정:**
- `_buildCostWood`, `_buildCostStone` private → protected (TownHall Reset() 접근용)

**`GameManager.cs` 수정:**
- `_townHallBuilt`, `_hadUnits` 필드 추가
- `IsTownHallBuilt` 프로퍼티 추가
- `RegisterTownHall()` public 메서드 추가
- `CheckWinLoseCondition()` 구현:
  - 승리: `_townHallBuilt && CurrentPop >= _victoryPopulation`
  - 패배: `_hadUnits && CurrentPop == 0` (`_hadUnits` 가드로 시작 직후 오인 방지)
- `TriggerGameOver()`: `Time.timeScale = 0f` 추가 → 게임 완전 정지
- `OnApplicationQuit()`: `Time.timeScale = 1f` 복구 (Play 재시작 정상화)
- `#if UNITY_EDITOR` ContextMenu 테스트 버튼 3개:
  - `[테스트] 자원 충전 (나무+100 돌+100)`
  - `[테스트] 승리 조건 시뮬레이션`
  - `[테스트] 패배 조건 시뮬레이션`

**테스트 결과:** ✅ 패배 시 모든 유닛 제거 후 0.5초 내 게임 정지 확인 / ✅ 승리 시 즉시 게임 정지 확인

**Quarry.cs 신규 (`Assets/Scripts/Buildings/`):**
- `Building` 상속, `Reset()`: 나무 5 / 돌 10 자동 세팅
- `OnBuilt()`: `ProductionLoop()` 코루틴 시작
- `ProductionLoop()`: 45초마다 `GameManager.AddResource(STONE, 1)` 호출
- GameManager null 시 루프 종료 (yield break)
- Gizmo: 완공 전 회색 / 완공 후 갈색 WireSphere (반경 1f)
- 빌드 전용: `Debug.Log` → `#if UNITY_EDITOR` 내부

**PlayerController.cs 수정 — 다중 건물 배치 지원:**
- `_housePrefab`, `_quarryPrefab`, `_townHallPrefab` 3개 Inspector 필드
- `_selectedBuildingIndex` (0=House, 1=Quarry, 2=TownHall)
- `UpdateBuildingSelection()`: 1/2/3 키로 건물 종류 선택
- `HandleConstructionOrder()`: 선택된 프리팹의 `Building.BuildCostWood/BuildCostStone` 읽어 사전 비용 체크 (하드코딩 제거)
- 모든 `Debug.Log` → `#if UNITY_EDITOR` 래핑

**Building.cs 수정:**
- `public int BuildCostWood => _buildCostWood` getter 추가
- `public int BuildCostStone => _buildCostStone` getter 추가
- PlayerController가 프리팹에서 비용 읽기 위해 필요

**Debug.Log 정리 (전체 18개 파일):**
- 정보성 `Debug.Log(...)` 전부 `#if UNITY_EDITOR/#endif` 래핑
- `Debug.LogWarning(...)` / `Debug.LogError(...)` 유지 (에러 감지용)
- 대상: GameManager, Building, House, TownHall, Quarry, AIUnit, Gatherer, Builder, Monster, BuildingManager, PopulationManager, ThreatManager, DangerRegistry, ResourceNode, ResourceManager, PathfindingGrid

**Unity 에디터 설정 (Week 10 완료 후 필수):**
1. `Quarry` 프리팹 생성 → Quarry 컴포넌트 추가 (나무 5 / 돌 10 자동 세팅 확인)
2. `PlayerController` Inspector: `_quarryPrefab`, `_townHallPrefab` 추가 할당
3. 플레이 중 1/2/3 키로 건물 종류 선택 후 우클릭으로 배치

**테스트 체크포인트:**
- [ ] 2번 키 → 우클릭 → Quarry 생성 + Builder 이동 확인
- [ ] Quarry 완공 후 45초 대기 → 돌 +1 확인
- [ ] 3번 키 → 우클릭 → TownHall 생성 + Builder 이동 확인
- [ ] Debug.Log가 빌드 콘솔에 출력되지 않음 확인

**미완료 (Week 10 남은 작업):**
- [ ] 첫 플레이어블 빌드

---

### ✅ Week 9 — DangerRegistry + PlayerController (완료)

**구현 내용:**

**`DangerRegistry.cs` 신규 (`Assets/Scripts/Core/`):**
- `DangerRecord` readonly struct: `Location(Vector2)`, `DangerLevel(int)`, `Timestamp(float)`
- `RecordDanger(Vector2, int)`: 위험 좌표 + 위험도 기록 (몬스터 위험도 = 2)
- `IsDangerousArea(Vector2, float)`: `_dangerRadius(4f) + checkRadius` 합산 반경 판정
- `CleanupRoutine()`: 1초 주기 코루틴 → 120초 만료 기록 자동 삭제
- `GetAllActiveRecords()`: 활성 기록 스냅샷 반환 (Week 10 UI용)
- `OnDrawGizmos()`: 항상 표시 — 위험도2=주황, 기타=노랑
- MessageBus 구독: `Start()`에서만 수행 (Awake에서 구독 금지 — MessageBus 초기화 순서 충돌)
- 싱글톤: `GameManager.CacheComponents()`에서 ThreatManager 다음에 자동 AddComponent

**`PlayerController.cs` 신규 (`Assets/Scripts/Core/`):**
- 좌클릭 `HandleDangerousDispatch()`:
  - `FindNearestDispatchableUnit()` → IsFleeing=false 유닛 중 클릭 좌표 최근접 반환
  - 체력 80% 미만이면 거부 로그 출력 (명령 거부, 유닛 상태 그대로 유지)
  - 조건 충족 시 `AIUnit.SetDestination(clickedWorldPos)` 호출
- 우클릭 `HandleConstructionOrder()`:
  - 자원 사전 체크 (나무 10 + 돌 5). 부족 시 Instantiate 하지 않음
  - 조건 충족 시 `Instantiate(_housePrefab, worldPos)` → `MessageBus.Publish("building.reserved", building)`
- `_mainCamera`: Awake에서 캐싱 (Update 내 `Camera.main` 호출 = FindWithTag 비용 방지)
- `using AIVillage.Resources` 필수 (ResourceType.WOOD/STONE 사용)

**`AIUnit.cs` 수정 (Week 9 추가):**
- `public float Hp => _hp` — PlayerController 파견 체력 체크용
- `public float MaxHp => _maxHp` — 80% 계산 기준
- `public bool IsFleeing => _currentState == UnitState.Fleeing` — protected 필드 캡슐화

**`GameManager.cs` 수정:**
- `public DangerRegistry DangerRegistry { get; private set; }` 프로퍼티 추가
- `CacheComponents()`: ThreatManager 다음에 DangerRegistry 자동 AddComponent

**`Gatherer.cs` 수정 (SearchAndGo 전면 교체):**
- `GetNearestAvailableNode()` → `GetAvailableNodes()` 전체 순회로 교체
- 이중 필터: `DangerRegistry.IsDangerousArea()` + `ThreatManager.GetNearestMonster()`
- 필터 통과 노드 중 최근접 선택 → 모두 위험 구역이면 `_retryDelay` 재탐색
- `DangerRegistry` null 체크 포함 (구형 씬 대비)

**`Builder.cs` 수정:**
- `Start()`: `Subscribe("building.reserved", OnBuildingReserved)` 추가
- `OnDestroy()` override 추가: `Unsubscribe` → `base.OnDestroy()` 순서
- `OnBuildingReserved(object)` 신규: Fleeing/작업중 가드 → `CancelInvoke + SearchAndBuild` 재트리거

**트러블슈팅:**

**컴파일 오류 — "Can't add script component 'PlayerController'"**
- 원인: `PlayerController.cs`에서 `ResourceType.WOOD`/`STONE`을 사용하지만 `using AIVillage.Resources;` 누락
- 증상: 프로젝트 전체 컴파일 실패 → 모든 스크립트 컴포넌트 추가 불가
- 수정: `PlayerController.cs` 상단에 `using AIVillage.Resources;` 추가
- **교훈: 새 스크립트에서 ResourceType 사용 시 반드시 `using AIVillage.Resources;` 추가 필요**

**핵심 설계 결정:**
- DangerRegistry는 PathfindingGrid.SetWalkable()을 직접 수정하지 않음 (순수 쿼리 방식)
  → walkable 맵 직접 수정 시 비동기 A* Task와 Race Condition 위험
- PlayerController 실제 자원 차감: `Building.StartConstruction()`에서 수행 (Builder 도착 시)
  → PlayerController는 사전 체크만. 즉시 차감 시 Builder 이동 취소 시 자원 복구 로직 필요
- `"building.reserved"` 발행 시 Builder는 payload의 Building을 직접 배정하지 않고 재탐색만
  → 자율성 원칙 유지 (특정 Builder 강제 지정 금지)

**Unity 에디터 설정 (Week 9 테스트 전 필수):**
1. 빈 GameObject 생성 → `PlayerController` 컴포넌트 추가
2. PlayerController Inspector: `_housePrefab` 할당 (Building 컴포넌트 포함된 House 프리팹)
3. DangerRegistry: `GameManager.CacheComponents()`에서 자동 추가 (별도 설정 불필요)
4. 씬에 `MainCamera` 태그가 달린 카메라 필수
5. Project Settings → Player → Active Input Handling: `Input System Package (New)` 확인

**테스트 체크포인트:**
- [ ] 유닛 도주 발생 → DangerRegistry Scene 뷰에 주황 원 표시 확인
- [ ] `_recordLifetime`을 5초로 임시 변경 → 5초 후 Gizmo 원 사라짐 확인
- [ ] Gatherer가 DangerRegistry 위험 구역 노드 skip하고 다른 노드 선택 확인
- [ ] 좌클릭 → 체력 80% 미만 유닛에서 거부 로그 출력 확인
- [ ] 우클릭 → 자원 부족 시 House Instantiate 안 됨 확인
- [ ] 우클릭 → 자원 충족 시 House 생성 + Builder 이동 시작 확인

---

## v0.2 구현 현황 (2026-05-31)

### ✅ v0.2-1: 카메라 엣지 스크롤링 (완료)

**구현 내용:**
- `CameraController.cs` 신규: 마우스 엣지(_edgeThreshold=20px) 감지 → 카메라 이동
- 이동 속도 Inspector 조정 가능 (_scrollSpeed=8f)
- 카메라 이동 범위: 0~60 클램핑 (맵 내부)
- New Input System: `Mouse.current.position.ReadValue()` 사용

**Unity 에디터 설정:**
- MainCamera GameObject에 CameraController 컴포넌트 추가

---

### ✅ v0.2-2: ResourceType 확장 (완료)

**구현 내용:**
- `ResourceType.cs`: SILVER, COPPER enum 추가
- 채집 시간 5초, 재생 시간 90초 (GDD 확정)

---

### ✅ v0.2-3: 훈련소(Barracks) + Warrior (완료)

**구현 내용:**

**`UnitState.cs` 수정:**
- `Standby` (Warrior 대기/숨김), `Fighting` (Warrior 전투 중) 추가

**`Barracks.cs` 신규 (`Assets/Scripts/Buildings/`):**
- Building 상속, Reset(): 나무 15 / 돌 10 자동 세팅
- `List<Warrior> _warriors` 보유 (CombatAlertQueue 조회용, FindObjectsOfType 없이)
- `OnBuilt()`: CombatAlertQueue.Instance.RegisterBarracks(this) + TrainingLoop() 시작
- `OnDestroy()`: CombatAlertQueue.Instance.UnregisterBarracks(this) + base.OnDestroy()
- `TrainingLoop()`: 슬롯 대기 → 자원 대기 → 자원차감 → 10초 대기 → Warrior 스폰
- `CollectAvailableWarriors(List<Warrior> result)`: 중간 List 할당 없이 직접 추가 (GC 최적화)
- `ReleaseSlot(Warrior warrior)`: 사망 시 목록에서 제거

**`Warrior.cs` 신규 (`Assets/Scripts/Units/`):**
- AIUnit 상속, `[RequireComponent(typeof(WarriorHealHelper))]`
- `base.Start()` 미호출 → PopulationManager 미등록 (패배 조건 분리 의도)
- `SetFleeing()` override → 빈 메서드 (도주 없음)
- `OnThreatDetected()` override → 빈 메서드 (위협 반응 없음)
- Standby: SpriteRenderer.enabled=false + Collider2D.enabled=false (SetActive 금지)
- Fighting: `_attackCoroutine` 필드로 코루틴 참조 보관 → EnterStandby/StartReturning에서 명시적 StopCoroutine
- `TryDispatch(Vector2 pos)`: CombatAlertQueue → Warrior 파견 공개 API
- `IsAvailableForDispatch`: Standby 상태 + HP 80% 이상

**`WarriorHealHelper.cs` 신규 (`Assets/Scripts/Units/`):**
- Warrior.enabled=false 상태에서도 별도 Update()로 HP 회복
- StartHealing() / StopHealing() API (EnterStandby/ExitStandby에서 호출)
- null 경고 로그 (#if UNITY_EDITOR)

**`AIUnit.cs` 수정:**
- `SetState()`: Fighting 조건 추가 (`enabled = Moving || Fleeing || Fighting`)
- `SetFleeing()`: `public virtual` 추가 (Warrior override 허용)
- `OnThreatDetected(Monster monster)`: `public virtual` 추가 (base: SetFleeing)

**`Monster.cs` 수정:**
- `_maxHp = 50f` SerializeField 추가
- `_hp` private 필드 추가 (Start에서 _maxHp로 초기화)
- `public float Hp` 프로퍼티 추가
- `public void TakeDamage(float amount)` 추가 (HP 0 → Destroy)

**`Building.cs` 수정:**
- `private void OnDestroy()` → `protected virtual void OnDestroy()` (Barracks override 허용)

**`PlayerController.cs` 수정:**
- `_barracksPrefab` 필드 추가
- 4번 키 → Barracks 선택 (`_selectedBuildingIndex = 3`)

**PR 리뷰 수정 내역 (unity-code-reviewer):**
- [Warning] Warrior.AttackRoutine 끝 중복 StartReturning() 제거
- [Warning] Warrior._attackCoroutine 필드 추가 + EnterStandby/StartReturning에서 StopCoroutine
- [Warning] CombatAlertQueue: FindObjectsOfType 제거 → _knownBarracks 캐시 + RegisterBarracks/UnregisterBarracks
- [Warning] CombatAlertQueue: _dispatchBuffer 재사용 (GC 최적화)
- [Warning] CombatAlertQueue: Keyboard.current null 체크 추가
- [Suggestion] WarriorHealHelper: null 경고 로그 추가
- [Suggestion] Barracks: TODO 주석 (훈련 중 건물 파괴 자원 환불)

**Unity 에디터 설정 (v0.2-3 완료 후 필수):**
1. Warrior 프리팹 생성 → Warrior 컴포넌트 추가 (WarriorHealHelper 자동 추가)
2. Barracks 프리팹 생성 → Barracks 컴포넌트 추가 (나무15/돌10 자동세팅), _warriorPrefab 할당
3. PlayerController Inspector: _barracksPrefab 할당
4. Monster 프리팹: MaxHp(50f) Inspector에서 확인

---

### ✅ v0.2-4: Gatherer OnThreatDetected + CombatAlertQueue (완료)

**구현 내용:**

**`AIUnit.cs` 수정:**
- `public virtual void OnThreatDetected(Monster monster)` 추가 (base: SetFleeing())

**`Gatherer.cs` 수정:**
- `OnThreatDetected()` override: Monster HP 기반 ThreatLevel 평가 → CombatAlertQueue 등록 → base() 호출

**`Warrior.cs` 수정:**
- `OnThreatDetected()` override → 빈 메서드

**`GameManager.cs` 수정:**
- `CheckThreatForAllUnits()`: `unit.SetFleeing()` → `unit.OnThreatDetected(nearest)` 1줄 교체
- `CacheComponents()`: CombatAlertQueue 자동 AddComponent 추가

**`CombatAlertQueue.cs` 신규 (`Assets/Scripts/Core/`):**
- ThreatLevel enum: Weak/Strong/VeryStrong/AllNeeded
- Queue<CombatAlert> FIFO 큐
- _knownBarracks 캐시 (Barracks OnBuilt/OnDestroy 시 등록/해제)
- _dispatchBuffer 재사용 버퍼 (GC 최적화)
- Q/W/E/Space 키 처리 (Keyboard.current null 체크 포함)
- Monster null 체크로 처치된 경보 자동 스킵

**위협 강도 판단 기준 (Monster HP 기반):**
| HP 범위 | 강도 |
|---------|------|
| >= 40 | AllNeeded |
| >= 25 | VeryStrong |
| >= 15 | Strong |
| < 15 | Weak |

---

## v0.1 완료 현황 (2026-05-30)

| 항목 | 상태 |
|------|------|
| Week 1~10 전체 기능 구현 | ✅ |
| Debug.Log #if UNITY_EDITOR 래핑 (18개 파일) | ✅ |
| PlayerController 다중 건물 지원 (1/2/3 키) | ✅ |
| Quarry 45초 돌 자동생산 | ✅ |
| 첫 플레이어블 빌드 | ⏳ File → Build Settings → Build |

---

## v0.2 설계 확정 내용 (2026-05-30)

### 배경 — v0.2 추가 이유
v0.1은 플레이어가 아무것도 안 해도 게임이 돌아가는 순수 시뮬레이션.
v0.2는 플레이어가 전투 결정권을 갖는 전략 요소 추가.

---

### v0.2-1: 카메라 엣지 스크롤링

**확정 사항:**
- 마우스가 화면 끝(상하좌우) 경계에 닿으면 카메라 이동
- 스크롤 속도 Inspector 조정 가능
- 맵 경계 클램핑 적용

**신규 파일:** `Assets/Scripts/Core/CameraController.cs`

**미확정:** 카메라 이동 가능 범위 (맵 크기에 의존)

---

### v0.2-2: 새 자원 (ResourceType 확장)

**확정 사항:**
- 은(Silver), 구리(Copper) 추가
- 맵에 노드로 배치 (나무/돌과 동일 방식, ResourceNode 재사용)
- ResourceType enum에 SILVER, COPPER 추가

**수정 파일:** `Assets/Scripts/ResourceType.cs`

**미확정:** 채집 시간, 재생 시간, 노드 초기 수량

---

### v0.2-3: 훈련소 (Barracks) + Warrior 유닛

**훈련소 확정 사항:**
- Building 상속, Reset() 패턴 적용
- 최대 3마리 Warrior 슬롯 관리
- Warrior 사망 시 슬롯 해제 → 재훈련 가능
- PlayerController 키: 4번 (건물 선택)

**미확정:** 훈련소 건설 비용 / Warrior 훈련 비용 및 시간

**Warrior 확정 사항:**
- AIUnit 상속 (isEquipped, loyalty 기존 필드 활용)
- 전체 최대 3마리 (훈련소 슬롯 기준)
- 대기 상태: 기지에 숨김
  - `SpriteRenderer.enabled = false` + `this.enabled = false`
  - `GameObject.SetActive(false)` 금지 (코루틴 멈춤)
  - HP 회복 코루틴은 별도 유지
- 전투: 끝까지 싸움, 철수 없음
- 사망: Destroy. 패배 조건 미포함
- 승리 후: 기지 복귀 → HP 회복 → 다시 숨김

**Warrior FSM:**
```
Standby(숨김/기지) → Moving(파견) → Fighting(전투)
                                          ↓ 승리
                                     Moving(귀환) → Standby
                                          ↓ 사망
                                       Destroyed
```

**UnitState enum 추가:** `Standby`, `Fighting`

**신규 파일:** `Assets/Scripts/Units/Warrior.cs`
**수정 파일:** `Assets/Scripts/Data/UnitState.cs`, `Assets/Scripts/Buildings/Barracks.cs`

---

### v0.2-4: Gatherer OnThreatDetected + CombatAlertQueue

**Gatherer 역할 확장 — 확정 사항:**

현재 GameManager.CheckThreatForAllUnits()에서 `unit.SetFleeing()` 직접 호출.
변경: `unit.OnThreatDetected(monster)` 가상 메서드로 교체.

```
AIUnit.OnThreatDetected(Monster monster) — 기본: SetFleeing()
Gatherer.OnThreatDetected(Monster monster) — 오버라이드:
  1. 몬스터 강도 판단 (4단계)
  2. CombatAlertQueue에 경보 등록
  3. base.OnThreatDetected(monster) → SetFleeing()
```

**몬스터 강도 4단계 (보유 Warrior 수 기반):**
| 강도 | 판단 기준 |
|------|---------|
| 약함 | Warrior 1명으로 처리 가능 |
| 강함 | Warrior 2명 필요 |
| 매우강함 | Warrior 3명 필요 |
| 전부필요 | 3명도 위험 |

**수정 파일:**
- `AIUnit.cs`: `protected virtual void OnThreatDetected(Monster monster)` 추가
- `Gatherer.cs`: override 구현
- `GameManager.cs`: `unit.SetFleeing()` → `unit.OnThreatDetected(nearest)` 1줄 교체

**CombatAlertQueue 확정 사항:**
- FIFO 큐 (순서대로 처리)
- 새 경보가 와도 기존 큐 유지 (덮어쓰기 없음)
- 처리 시 Monster null 체크 → 이미 처치됐으면 자동 스킵

**CombatAlert 구조:**
```csharp
struct CombatAlert {
    Monster    target;    // null이면 처치됨 → 자동 스킵
    ThreatLevel level;    // 약함/강함/매우강함/전부필요
    Vector2    position;  // 발생 위치
    float      timestamp; // 발생 시각
}
```

**플레이어 처리 키:**
| 키 | 동작 |
|----|------|
| Q | Warrior 1명 파견 → 큐 front 처리 |
| W | Warrior 2명 파견 |
| E | 가용 Warrior 전원 파견 |
| Space | 스킵 (파견 안 함) |

**콘솔 로그 형식:**
```
[경보 1/3] 강함 | Q:1명 W:2명 E:전원 Space:스킵
[경보] Warrior 없음 — 파견 불가 (강함 위협 감지)
```

**Warrior 0마리 시:** 로그만 출력, 경보 자동 소멸

**신규 파일:** `Assets/Scripts/Core/CombatAlertQueue.cs`

---

### v0.2-5: 무기 시스템

**확정 사항:**
- 기존 유닛 구조에 무기 장착 (AIUnit.isEquipped 필드 활용)
- 추후 확장 가능 구조 (enum 방식)

**WeaponType enum:**
```csharp
None, CopperDagger, SilverSword  // 추후 확장
```

**승률 기준:**
| 무기 | vs 기본 몬스터 승률 |
|------|-----------------|
| 없음 | 20% |
| 구리 단검 (CopperDagger) | 45% |
| 은검 (SilverSword) | 70% |

**새 건물:**
- 제철소 (Forge): 구리/은 → 재료 가공
- 대장간 (Blacksmith): 재료 → 무기 제작
- PlayerController 키: 5=Forge, 6=Blacksmith

**미확정:** 건설 비용 / 제작 비용 / 제작 시간 / 무기 제작 방식(자동 vs 플레이어 명령)

**신규 파일:** `WeaponType.cs`, `Forge.cs`, `Blacksmith.cs`

---

### v0.2-6: AI 정보 패널 (클릭 시)

**확정 사항:**
- 유닛 클릭 → 해당 유닛 정보 콘솔 로그 출력
- 표시 항목: HP, 현재 상태, 장착 무기, 진행 중 작업

**미확정:** 구현 방식 (PlayerController 확장 vs 별도 SelectionManager)

---

### v0.2 개발 순서 (권장)

| 순서 | 작업 | 에이전트 |
|------|------|---------|
| 1 | 카메라 엣지 스크롤링 | senior-programmer |
| 2 | ResourceType 확장 (Silver/Copper) | senior-programmer |
| 3 | 훈련소 (Barracks) | senior-programmer |
| 4 | Warrior 기본 (생성/대기/숨김) | **architect 필수** → senior-programmer |
| 5 | AIUnit.OnThreatDetected + Gatherer override | senior-programmer |
| 6 | CombatAlertQueue + Q/W/E/Space 처리 | senior-programmer |
| 7 | Warrior 전투 FSM | senior-programmer |
| 8 | WeaponType + Forge + Blacksmith | senior-programmer |
| 9 | 승률 계산 통합 | senior-programmer |

---

### v0.2 미확정 항목 (개발 시작 전 결정 필요)

| 항목 | 비고 |
|------|------|
| 훈련소 건설 비용 | |
| Warrior 훈련 비용 + 시간 | 나무5+돌5, 10초 제안 |
| 제철소/대장간 건설 비용 | |
| 구리/은 노드 채집 시간 + 재생 시간 | |
| 무기 제작 방법 (자동 vs 플레이어 명령) | |
| 카메라 이동 가능 범위 | 맵 크기 확정 후 |
| 경보 타임아웃 여부 | |
| Warrior 파견 시 복수 경보 처리 순서 | |

---

*이 문서는 매 개발 세션 종료 시 업데이트됩니다.*

*이 문서는 매 개발 세션 종료 시 업데이트됩니다.*

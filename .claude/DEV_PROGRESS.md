# 📝 AI Village — 개발 진행 로그

> 세션별 개발 내용, 트러블슈팅, 구현 완료 파일 목록을 기록한 문서.
> 최종 수정: 2026-05-28

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
│   └── PopulationManager.cs      Week 7  — 인구 추적, HasRoom 체크
├── Data/
│   ├── UnitState.cs              Week 3  — FSM 상태 열거형 (6개)
│   └── IMessageBus.cs            Week 5  — 이벤트 버스 인터페이스
├── Units/
│   ├── AIUnit.cs                 Week 3  — 공통 기반 클래스, A* 비동기 이동, FSM
│   ├── Gatherer.cs               Week 4  — 자원 수집 FSM (Idle→Moving→Gathering→Returning)
│   └── Builder.cs                Week 6  — 건설 FSM (Idle→Moving→Building→Idle)
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

## 다음 단계 (Week 8~10 예정)

| 주차 | 목표 | 핵심 작업 |
|------|------|---------|
| Week 8 | ThreatManager + Monster + Fleeing | 몬스터 순찰/추적 FSM, AIUnit Fleeing 상태 구현 |
| Week 9 | DangerRegistry + 플레이어 지시 | 위험 좌표 기록, 파견 거부 로직, PlayerController |
| Week 10 | TownHall + 승리/패배 + 폴리싱 | CheckWinLoseCondition, UI, 첫 플레이어블 빌드 |

---

*이 문서는 매 개발 세션 종료 시 업데이트됩니다.*

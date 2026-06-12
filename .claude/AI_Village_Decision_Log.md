# 📖 AI Village — 결정 기록 & 설계 방향 로그

> 이 문서는 기획 과정에서 내려진 모든 결정과 방향을 기록한 참고 문서입니다.
> "왜 이렇게 만들었는가"를 언제든 돌아볼 수 있도록 작성되었습니다.
> 최종 수정: 2026-06-09

---

## 📌 설계 철학 (개발자 원칙)

> **"코드는 기획이 완벽하게 됐을 때 시작한다. 방향을 먼저 확실하게 잡고 가야 한다."**

- AI를 도구로 활용하되, 직접 이해하고 성장하는 방식으로 개발
- 코드보다 설계가 우선 — 블로커 없는 상태를 확인한 뒤 구현 시작
- 추후 확장 기능은 "미확정"으로 명시하되, 데이터 구조에 미리 반영
- **범위를 작게 잡고 완성하는 것이 크게 잡고 미완성인 것보다 낫다**

---

## 1. 게임 컨셉 결정

**날짜:** 2026-05-27
**결정자:** 플레이어

### 핵심 아이디어
> "AI 몹들이 자원(나무, 돌, 철광석, 구리, 은)을 수집하고,
> AI가 스스로 새로운 AI를 생성하며,
> 서로 소통하면서 하나의 작은 마을을 만든다."

### 2D vs 3D
- **결정: 2D 탑다운**
- 이유: AI 행동 로직에 집중하기 위해 3D의 추가 복잡성(카메라, 조명, 모델링) 배제
- 초보 개발자가 핵심 시스템에 집중하기 적합

---

## 2. 경제 시스템 결정 (원본 v1.x)

**날짜:** 2026-05-27

### 구리/은 소비처 (AC-01 해결)
| 결정 | 내용 |
|------|------|
| 구리x10 + 돌x5 | 통신탑 → AI 메시지 범위 2배 |
| 구리x8 + 은x3 + 나무x10 | 연구소 → AI 능력치 업그레이드 해금 |
| 은x15 + 철광석x10 | 자동화 공장 → 자원 가공 효율 2배 |

> ⚠️ 이 결정은 v1.0 이후 적용. v0.1에서는 나무+돌만 구현.

### 자원 수치표 (v1.0 기준, 참고용)
| 자원 | 맵 개수 | 수집 시간 | 재생 시간 |
|------|--------|----------|----------|
| 나무 | 80개 | 2초 | 30초 |
| 돌 | 40개 | 5초 | 60초 |
| 철광석 | 20개 | 10초 | 120초 |
| 구리 | 10개 | 15초 | 180초 |
| 은 | 5개 | 20초 | 300초 |

---

## 3. 플레이어 역할 결정 (1순위 문제 해결)

**날짜:** 2026-05-27
**배경:** 에이전트 분석에서 "플레이어가 아무것도 안 해도 AI가 알아서 마을 완성 → 스크린세이버 문제" 지적됨

### 플레이어가 하는 일
> 1. AI들이 어떤 판단을 하는지 관찰
> 2. 위험 요소를 플레이어가 판단 — AI는 위험을 인식 못하지만 플레이어는 볼 수 있음
> 3. 위험 지역에 AI를 보낼지 말지 **플레이어가 결정**
> 4. 건설 위치를 플레이어가 지정

### 핵심 설계 원칙 (도출된 USP)
> **"AI는 명령을 따르는 도구가 아니라, 생존 판단 능력을 가진 자율 존재다."**
> 플레이어와 AI의 관계: **"제안 → AI가 판단 후 수행 or 거부"**

---

## 4. AI 행동 시스템 결정

**날짜:** 2026-05-27

### 위험 요소를 만난 AI 처리
> **결정: 체력 감소 후 귀환 + 위험 지역 기억 (DangerRegistry)**

- 위험 지역을 경험한 AI는 해당 좌표를 DangerRegistry에 기록
- 체력 80% 미만 시 플레이어 파견 명령도 **AI가 스스로 거부** (생존 우선)
- **이유:** "AI가 생존을 우선시한다"는 컨셉이 게임의 핵심 재미 요소

### 전투 유닛 (v0.1)
> **결정: v0.1에서는 전투 없음 — AI는 도주만 함**

- 전투 시스템은 v0.5에서 추가 (무기 정비 완료 유닛만 참여)
- v0.1에서 몬스터를 만난 AI는 Fleeing 상태로 무조건 도주
- **이유:** 전투 시스템 없이도 "위험 관리" 재미 요소는 살아있음

### 자원 노드 동시 채집
> **결정: 1명만 채집 가능 + 예약 시스템**

- **이유:** MessageBus/ResourceRegistry 설계와 일치, AI들이 자연스럽게 분산 이동

---

## 5. 범위 축소 결정 ← 핵심 전환점

**날짜:** 2026-05-27
**배경:** Claude Code와의 현실적 완성 가능성 분석 대화

### 문제 진단
원본 GDD(v1.6.0)의 완성 가능성 분석 결과:
- 시스템 12개 이상, FSM 상태 14개, 매니저 클래스 12개
- **AI가 코드를 써줘도 Unity 에디터 작업, 시스템 연동 디버깅은 개발자가 직접 해야 함**
- AI가 못 해주는 것: GameObject 씬 배치, Prefab 세팅, Inspector 연결, 스프라이트 제작, 실제 디버깅
- 원본 GDD 기준 프로토타입 완성 가능성: **20%**

### 결정: 3단계 출시 전략

| 버전 | 핵심 내용 | 완성 가능성 |
|------|----------|-----------|
| **v0.1** | AI 마을 빌더 (2자원 + 2유닛 + 3건물 + 1위협) | **80%** |
| v0.5 | Explorer + FogOfWar + 무기/정비 + 방어전투 | 65% |
| v1.0 | 적 팩션 + 침략 + 왕국 정복 | 50% |

### v0.1에서 제거한 기능과 이유

| 제거 기능 | 이유 |
|---------|------|
| Explorer 유닛 | FSM 복잡도 + FogOfWar 연동 필요 → v0.5로 이연 |
| Fog of War | 별도 렌더링 시스템 → Unity 초보자에게 복잡 |
| 철광석/구리/은 | 소비처(대장간/연구소)가 없으면 의미 없음 |
| 대장간 + 무기 + 정비 | 전투 시스템 전제조건 → v0.5로 이연 |
| 전투 모드 | 정비 시스템 전제조건 → v0.5로 이연 |
| FactionManager | 팩션 없는 v0.1에서 불필요 |
| TerritoryManager | 팩션 없는 v0.1에서 불필요 |
| 침략 시스템 | 팩션 전제조건 → v1.0으로 이연 |
| SaveManager | 프로토타입에서 불필요 → v1.0에서 추가 |
| IDestructible | 건물 파괴 없는 v0.1에서 불필요 |
| 통신탑/연구소/자동화공장 | 구리/은 소비처 → v1.0으로 이연 |

### 범위 축소 전후 비교

| 항목 | 원본 GDD | v0.1 |
|------|---------|------|
| 자원 종류 | 5종 | 2종 |
| 유닛 종류 | 3종 | 2종 |
| 건물 종류 | 7종 | 3종 |
| FSM 상태 수 | 14개 | 6개 |
| 매니저 클래스 수 | 12개 | 7개 |
| 플레이어 지시 | 5가지 | 2가지 |
| 완성 가능성 | 20% | **80%** |

---

## 6. 미래 대비 데이터 구조 결정

**날짜:** 2026-05-27
**업데이트:** 2026-05-27 (확장성 전수 분석 후 isEquipped, Building.factionId 추가)

### 확장성 전수 분석 결과 (v0.5~v1.0 기능별 검토)

v0.1 → v0.5 → v1.0 전환 시 코드를 뜯어야 하는 상황이 생기는지 기능별로 분석한 결과:

| 판정 | 기능 | 이유 |
|------|------|------|
| 🟢 안전 | 새 자원 종류 추가 | ResourceType enum 확장만 필요 |
| 🟢 안전 | 새 건물 추가 | Building 상속 클래스 추가 |
| 🟢 안전 | Explorer 유닛 | AIUnit 추상 기본 클래스 상속 |
| 🟢 안전 | FogOfWar | 독립 신규 시스템 |
| 🟢 안전 | FactionManager | AIUnit에 factionId 필드 이미 있음 |
| 🟢 안전 | 유닛 흡수 | originalFactionId 등 필드 이미 있음 |
| 🟡 소수정 | FSM 새 상태 추가 | 기존 switch문에 case 추가 (재작성 아님) |
| 🟡 소수정 | SaveManager | ISaveable 인터페이스 구현 추가 (기계적 작업) |
| 🔴 **위험** | 무기/정비 시스템 | AIUnit에 `isEquipped` 없으면 FSM 전체 수정 필요 |
| 🔴 **위험** | 건물 팩션 귀속 | Building에 `factionId` 없으면 상속 클래스 전체 수정 |

### v0.1부터 반드시 포함해야 할 필드 (최종 확정)

```csharp
// AIUnit — 미래 대비 필드 4개
bool  isEquipped        = false;  // v0.5 무기/정비 시스템 전투 참여 조건
int   originalFactionId = 0;      // v1.0 팩션 흡수 시 원래 출신 기록
int   currentFactionId  = 0;      // v1.0 현재 소속 팩션
float loyalty           = 100f;   // v1.0/v2.0 반란 시스템 충성도

// Building 기본 클래스 — 미래 대비 필드 1개
int   factionId         = 0;      // v1.0 영토 정복 후 건물 귀속 처리
```

**왜 지금 넣어야 하는가:**
- `isEquipped` 누락 시: v0.5에서 GathererFSM, BuilderFSM, ExplorerFSM 전부 수정
- `Building.factionId` 누락 시: v1.0에서 House, Quarry, TownHall 전부 수정
- 지금 기본값만 넣어두면 나중에 로직만 채우면 됨 (추가 작업 0)

### v0.1부터 파일만 만들어둘 클래스 (내용 비워두기)

```csharp
IDestructible.cs    // 건물/유닛 파괴 가능 인터페이스 — v1.0
IUpgradeable.cs     // 건물/무기 강화 가능 인터페이스 — v1.0
EquipmentManager.cs // 무기/정비 관리 — v0.5
FactionManager.cs   // 팩션 관리 — v1.0
SaveManager.cs      // 세이브/로드 — v1.0
```

### FSM 설계 원칙 (확장성 확보)

상태가 6개(v0.1) → 14개(v1.0)로 늘어날 때를 대비해 상태별 메서드 분리 방식 채택:

```csharp
// ❌ 피해야 할 방식: switch 하나에 전부 → 나중에 500줄짜리 메서드
void UpdateFSM() { switch(state) { case Idle: ... case Moving: ... } }

// ✅ 채택 방식: 상태별 독립 메서드 → 새 상태 = 메서드 1개 추가만
void HandleIdle()      { ... }
void HandleMoving()    { ... }
void HandleGathering() { ... }
// v0.5에서 아래만 추가하면 됨 — 기존 코드 건드리지 않음
void HandleEquipping() { ... }
void HandleCombat()    { ... }
```

---

## 7. 팩션 및 정복 시스템 결정 (v1.0 예정)

**날짜:** 2026-05-27
**적용 버전:** v1.0

### 팩션 정복 조건
> **결정: 적 Town Hall 파괴**
> **이유:** 명확한 목표, 전투의 최종 목적지가 건물 파괴로 집중됨

### 적 Town Hall 파괴 후 유닛 처리
> **결정: 플레이어 팩션으로 흡수**
> - `originalFactionId` 보존, `currentFactionId` 변경, `loyalty = 100`

### 정복 후 영토 귀속
> **결정: 자원 노드 + 건물 + 유닛 전부 자동으로 플레이어 팩션 전환**

---

## 8. 맵 및 영토 시스템 결정 (v1.0 예정)

**날짜:** 2026-05-27
**적용 버전:** v1.0

### 맵 방식
> **결정: 팩션별 영토가 구분된 고정 맵 (100x100 타일)**
> v0.1에서는 단일 영역 60x60 타일로 시작

### 영토 소유 규칙 (v1.0)
> - Town Hall 생존 = 해당 팩션 소유
> - Town Hall 파괴 = 무주지(Neutral)
> - 무주지 + 플레이어 유닛 진입 = 점령

---

## 9. 미래 확장 아이디어 (미확정 — 방향성만 기록)

**날짜:** 2026-05-27
**성격:** 확정된 기획이 아닌, 개발 완성 후 검토할 방향

### 반란/독립 시스템 (v2.0 이후)
> "추후에 이 게임이 완성됐다면, 흡수된 적 유닛들이 모여서
> 반기를 들거나 독립하거나 국가 재탈취를 위한 행동을 했으면 좋겠다."

**지금 미리 반영해야 할 데이터 필드:**
```csharp
int   originalFactionId  // 원래 팩션 ID (반란 시 출신 확인)
int   currentFactionId   // 현재 소속 팩션 ID
float loyalty            // 충성도 0~100 (현재는 100 고정)
```

---

## 10. 개발 방향 원칙 요약

| 원칙 | 내용 |
|------|------|
| 기획 우선 | 설계 블로커 없는 상태 확인 후 코드 시작 |
| 범위 최소화 | 완성 가능한 작은 버전부터, 업데이트로 확장 |
| 확장 대비 | 미래 기능을 위한 인터페이스/필드 미리 포함 |
| 단계적 구현 | v0.1 → v0.5 → v1.0 순서로 기능 확장 |
| AI 자율성 | AI는 도구가 아닌 자율 존재, "제안-판단" 관계 유지 |
| 단순화 우선 | 복잡도가 높은 기능은 단순한 버전으로 먼저 구현 |
| 에디터 작업 인식 | AI가 코드를 써줘도 Unity 에디터 작업은 직접 해야 함 |

---

## 11. GDD 설계 공백 전수 보완 결정

**날짜:** 2026-05-27
**배경:** GDD v2.1.0 완성도 점검에서 개발 착수 시 막힐 공백 13개 발견 → 전부 확정

### 확정된 기준값 (모두 Inspector에서 조정 가능)

| 항목 | 확정값 | 변경 가능성 |
|------|--------|-----------|
| 나무 1회 수집량 | 3개 | 높음 (밸런스 조정) |
| 돌 1회 수집량 | 2개 | 높음 |
| Gatherer 인벤토리 | 나무 6개 / 돌 4개 | 높음 |
| 기지 위치 | 시작 House 좌표 고정 | 낮음 (구조 변경) |
| 기지 반경 | 5타일 | 중간 |
| 노드 선택 알고리즘 | 비예약 중 A* 최단거리 | 낮음 (알고리즘 교체) |
| Builder 자동화 | 자동 (House 우선, 플레이어 지시 우선) | 중간 |
| 유닛 자동 생성 | GameManager, Gatherer:Builder=3:1 | 중간 |
| Gathering→Fleeing | 자원 버리고 즉시 도주 | 낮음 |
| Building→Fleeing | 건설 중단, 재지시 필요 | 낮음 |
| 체력 회복 | 기지 내 초당 5 HP | 높음 |
| 몬스터 공격력 | 10 HP / 1초 | 높음 |
| DangerRegistry 회피 반경 | 4타일 | 중간 |
| DangerRegistry 만료 | 120초 | 높음 |
| 건물 예약 방식 | 지시 시 즉시 예약, Fleeing 시 해제 | 낮음 |
| MessageBus 방식 | 문자열 키 기반 이벤트 | 낮음 (구조 변경) |

### 설계 원칙: 수치는 Inspector 노출

모든 수치형 값은 `[SerializeField]`로 Inspector에 노출.
플레이 테스트 후 코드 수정 없이 즉시 조정 가능.

```csharp
[SerializeField] private float hpRecoveryPerSecond = 5f;
[SerializeField] private float dangerRecordExpiry   = 120f;
[SerializeField] private int   woodPerHarvest       = 3;
// 등 모든 밸런스 수치 동일하게 적용
```

---

---

## 12. Week 3 — A* 패키지 불일치 해결 결정

**날짜:** 2026-05-28

### 문제
설치된 패키지가 Aron Granberg의 "A* Pathfinding Project"가 아닌 "AStar 2D Grid Pathfinding"이었음.
`using Pathfinding;`, `Seeker`, `Path` 클래스가 존재하지 않아 CS0246 컴파일 에러 발생.

### 해결 결정
| 항목 | 결정 |
|------|------|
| 패키지 교체 여부 | 교체하지 않고 설치된 패키지 API에 맞게 코드 재작성 |
| 새 패키지 API | `AStarPathfinding.GeneratePath(startX, startY, goalX, goalY, bool[,])` → `(int,int)[]` 반환 |
| 그리드 관리 | 별도 `PathfindingGrid` 싱글톤 신설 — 60x60 맵, WorldToGrid/GridToWorld 변환 담당 |
| 비동기 처리 | `async Task` + `CancellationTokenSource` — 이전 경로 요청 취소 후 새 요청 시작 (Race Condition 방지) |
| 도착 판정 최적화 | `Vector3.Distance` 대신 제곱 거리 비교 (`ARRIVAL_THRESHOLD_SQ = 0.04f`) |
| Update 비용 절감 | `enabled = (newState == UnitState.Moving)` — Moving이 아니면 Update 비활성화 |

### 주요 교훈
- `AIUnit.Awake()`에서 `enabled = false` 설정 시 파생 클래스의 `Start()`가 호출되지 않음
  → Week 4에서 발견, Awake의 enabled 조작 제거로 해결

---

## 13. Week 4 — GathererFSM 설계 결정

**날짜:** 2026-05-28

### FSM 타이머 방식 결정
| 옵션 | 결정 | 이유 |
|------|------|------|
| Update 기반 타이머 | ❌ 미채택 | AIUnit이 Moving이 아닐 때 `enabled=false` → Update 실행 안 됨 |
| **Coroutine 기반 타이머** | ✅ 채택 | `enabled=false`여도 코루틴은 계속 실행, 기반 클래스 수정 불필요 |

### OnIdle 재진입 방지
- 문제: `OnArrival()`이 `OnReachDestination()` + `OnIdle()`을 연속 호출 → 채집 시작 직후 새 노드 탐색 발동
- 해결: `_isInGatherCycle` 플래그 — 사이클 중간이면 `OnIdle()` 무시

### ResourceNode 자동 등록
- 기존: 수동 등록 필요
- 변경: `ResourceNode.Start()`에서 `ResourceManager.RegisterNode(this)` 자동 호출
- 이유: `Start()`는 모든 `Awake()` 후 실행 → GameManager 초기화 완료 보장

---

## 14. Week 5 — MessageBus 설계 결정

**날짜:** 2026-05-28

### IMessageBus 인터페이스 이동
- 기존: `ResourceType.cs` 내 `AIVillage.Resources` 네임스페이스
- 변경: 독립 파일 `IMessageBus.cs`, `AIVillage.Core` 네임스페이스로 이동
- 이유: `MessageBus` 구현체가 Core에 있고, ResourceNode는 이미 `using AIVillage.Core` 보유

### Subscribe 추가
- 기존 IMessageBus: `Publish` 메서드만 정의
- 변경: `Subscribe`, `Unsubscribe`, `Publish` 3개 메서드로 확장
- 이유: 외부 소비자(GameManager 등)가 이벤트 구독에 인터페이스 사용 가능

### Publish 안전성
- `list.ToArray()` 스냅샷 순회 채택
- 이유: 핸들러 내부에서 `Unsubscribe` 호출 시 컬렉션 수정 예외 방지

### 의존성 주입 타이밍
- `ResourceManager.RegisterNode()` 시점에 `node.InjectMessageBus(GameManager.Instance.MessageBus)` 호출
- 이유: GameManager.Awake → CacheComponents에서 MessageBus 이미 생성 → 안전한 주입 타이밍 보장

---

## 15. Week 6 — BuilderFSM + BuildingManager 설계 결정

**날짜:** 2026-05-28

### Building 클래스 계층
- `Building` — 기반 클래스 (Unbuilt/UnderConstruction/Built 상태 + 예약 시스템)
- `House` — Building 상속, 완공 시 `GameManager.BasePosition` 자동 설정
- 이유: Gatherer의 ResourceNode 패턴과 동일하게 유지 → 코드 일관성 확보

### 자원 차감 타이밍
- **결정: Builder가 건설지 도착 시** 두 자원(나무+돌) 동시 확인 후 차감
- 이유: 이동 전 확인 시 이동 중 자원 상태 변화로 인한 오류 가능성 → 도착 시점이 안전
- 안전장치: 두 자원 모두 충분한지 먼저 확인 후 일괄 차감 (부분 차감 방지)

### BuildRoutine 내 ResetCycle 호출 문제
- 문제: 코루틴 내부에서 `ResetCycle()` 호출 시 `StopCoroutine(자기 자신)` 실행
- 해결: 코루틴 종료 시 필드 직접 초기화 후 `SearchAndBuild()` 직접 호출

---

## 16. Week 7 — PopulationManager + 자동 스폰 설계 결정

**날짜:** 2026-05-28

### 인구 등록 방식
- `AIUnit.Start()` — `virtual` 메서드로 추가, PopulationManager 자동 등록
- `AIUnit.OnDestroy()` — 유닛 소멸 시 자동 해제
- 파생 클래스(`Gatherer`, `Builder`)는 `base.Start()` 호출로 등록 연결
- 이유: 모든 AIUnit 파생 클래스가 자동 등록 — 수동 연결 불필요

### 스폰 쿨다운 필요성
- 문제: Tick(0.5s)마다 `CheckAutoSpawn()` 실행 → 자원 충분 시 매 Tick 스폰 폭발
- 해결: `_spawnCooldown = 5f` (Inspector 조정 가능) + `_lastSpawnTime` 타임스탬프 비교
- 이유: `Time.time - _lastSpawnTime < _spawnCooldown` 조건으로 최소 간격 보장

---

---

## 17. Week 8 — ThreatManager + Monster FSM + Fleeing 설계 결정

**날짜:** 2026-05-29

### 위협 감지 방식: ThreatManager 폴링 (Physics2D 아님)
| 항목 | 결정 |
|------|------|
| AIUnit 감지 방식 | GameManager Tick(0.5s)마다 `ThreatManager.GetNearestMonster()` 폴링 |
| Monster 추적 대상 탐지 | Monster 측에서 `Physics2D.OverlapCircle` 사용 |
| 이유 | AIUnit마다 매 Update에서 OverlapCircle 호출 시 유닛 수 × 프레임 횟수의 Physics 연산 발생 → Tick 기반 0.5s 1회로 축소 |

### Monster FSM: 독립 MonoBehaviour
| 항목 | 결정 |
|------|------|
| Monster 이동 방식 | `Vector3.MoveTowards` (A* 미사용) |
| 상속 여부 | AIUnit 상속 안 함, 독립 MonoBehaviour |
| enabled 토글 | 미적용 (Monster Update는 항상 활성) |
| 이유 | 순찰/추적 경로가 단순하므로 A* 비동기 경로 불필요 |

### enabled 토글 확장
- 기존: `enabled = (state == Moving)`
- 변경: `enabled = (state == Moving || state == Fleeing)`
- 이유: Fleeing 중 매 Update에서 기지 반경 체크 + 체력 회복이 필요

### SetState 가시성 변경
- `AIUnit.SetState()`: `private` → `protected`
- `SetFleeing()` public 메서드가 내부에서 `SetState(Fleeing)` + `enabled=true` 호출

### Gatherer/Builder 코루틴 인터럽트 패턴
- `SetFleeing()` 호출 시 파생 클래스 `OnFleeingEnter()` 호출
- Gatherer: `StopCoroutine(_gatherCoroutine)` + `_targetNode.ReleaseReservation()` + 상태 초기화
- Builder: `StopCoroutine(_buildCoroutine)` + `_targetBuilding.ReleaseConstruction()` + 상태 초기화
- `CancelInvoke(nameof(SearchAndGo))` 필수 포함

### Monster 추적 포기 이중 조건
- 조건 A: 거리 > 8타일
- 조건 B: 유닛이 기지 반경(5타일) 내 진입
- 두 조건 중 하나라도 충족 시 Chasing → Patrolling

---

## 18. Week 9 — DangerRegistry + PlayerController 설계 결정

**날짜:** 2026-05-29

### DangerRegistry: Fleeing 진입 시 자동 기록
- Fleeing 상태 진입 시 현재 좌표를 `DangerRegistry`에 자동 기록
- 회피 반경: 4타일 (A* 경로탐색 비용 상승 구역)
- 만료 시간: 120초 (Inspector 조정 가능)

### PlayerController: 좌클릭/우클릭 방식
| 입력 | 동작 |
|------|------|
| 좌클릭 | 가장 가까운 파견 가능 유닛에 이동 명령 |
| 우클릭 | 선택된 건물 프리팹 배치 |
| 1~3번 키 | 건물 선택 (House/Quarry/TownHall) |

### AI 거부 로직 구현
- 플레이어가 위험 지역 파견 명령 시 `AIUnit.hp >= maxHp * 0.8f` 이면 수행
- 80% 미만 시 거부 (Idle 유지)

---

## 19. Week 10 — TownHall + 승패조건 + Debug 정리 결정

**날짜:** 2026-05-30

### 승리/패배 조건 구현
| 조건 | 구현 |
|------|------|
| 승리 | 인구 20명 + TownHall 완공 시 `GameManager.OnVictory()` |
| 패배 | `PopulationManager` 등록 유닛 전멸 시 `GameManager.OnDefeat()` |

### Quarry 자동 생산
- 돌 1개 / 45초
- `Coroutine` 기반 타이머 (`enabled=false`에서도 코루틴 계속 실행됨을 이용)

### Debug.Log 정리
- 모든 Debug.Log: `#if UNITY_EDITOR` + `#endif` 래핑으로 일괄 처리
- 런타임 빌드에서 로그 비용 제거

---

## 20. v0.2-1 — CameraController 설계 결정

**날짜:** 2026-05-30

| 항목 | 결정 |
|------|------|
| 이동 방식 | 마우스 포인터가 화면 가장자리(엣지) 진입 시 이동 방향 결정 |
| 클램핑 범위 | MinX=-30, MaxX=30, MinY=-30, MaxY=30 (맵 월드 좌표 기준) |
| 부착 위치 | MainCamera 오브젝트 |
| 이유 | 60×60 맵이 화면보다 크므로 카메라 이동 필수. 엣지 스크롤이 RTS 장르 표준 |

---

## 21. v0.2-2 — SILVER/COPPER 자원 확장 결정

**날짜:** 2026-05-30

| 항목 | 결정 |
|------|------|
| 추가 자원 | COPPER(구리), SILVER(은) |
| 채집 시간 | 5초 (나무 2초, 돌 5초와 동일 수준) |
| 재생 시간 | 90초 (돌 60초보다 더 희귀하게 설정) |
| 주요 용도 | 무기 제작 (v0.2-5 Forge/Blacksmith) |
| ResourceType enum | `WOOD=0, STONE=1, SILVER=2, COPPER=3` |
| 이유 | v0.2-5 무기 시스템 선행 자원. v0.1 시스템 수정 없이 enum 확장만으로 추가 가능 (기존 설계 원칙 준수) |

---

## 22. v0.2-3 — Warrior FSM + Barracks 설계 결정

**날짜:** 2026-05-31

### Warrior 아키텍처 핵심 결정 (불변)

| 항목 | 결정 | 이유 |
|------|------|------|
| `base.Start()` | **미호출** | PopulationManager 미등록 (Warrior 전멸 = 패배 조건 아님) |
| `base.OnDestroy()` | 호출 유지 | `UnregisterUnit`의 Contains 체크로 무해 |
| `SetFleeing()` | override → 빈 메서드 | Warrior는 도주하지 않음 |
| `OnThreatDetected()` | override → 빈 메서드 | 경보 등록은 Gatherer가 담당 |
| Standby 은신 | `SpriteRenderer.enabled=false` + `Collider2D.enabled=false` | `GameObject.SetActive(false)` 시 코루틴 멈춤 → 금지 |
| 코루틴 참조 | `_attackCoroutine` 필드 보관 | 상태 전환 시 명시적 StopCoroutine 필수 |

### WarriorHealHelper 분리 결정
- **결정:** 별도 MonoBehaviour 컴포넌트로 분리
- **이유:** Warrior의 `SpriteRenderer.enabled=false`는 MonoBehaviour.enabled와 무관. WarriorHealHelper가 독립적으로 `enabled=true` 상태를 유지하며 Standby 중에도 HP 회복 가능

### Barracks 설계
| 항목 | 값 |
|------|---|
| 건설 비용 | 나무x15, 돌x10 |
| 최대 슬롯 | 3 |
| Warrior 훈련 비용 | 나무x5, 돌x5 |
| 훈련 시간 | 10초 |
| Warrior 등록 방식 | `OnBuilt()` → `CombatAlertQueue.RegisterBarracks()` |
| Warrior 해제 방식 | `OnDestroy()` → `CombatAlertQueue.UnregisterBarracks()` |

---

## 23. v0.2-4 — OnThreatDetected + CombatAlertQueue 설계 결정

**날짜:** 2026-05-31

### OnThreatDetected 설계
- **추가 위치:** `AIUnit` 추상 메서드 → `Gatherer`에서 override
- **Gatherer 동작:** Monster HP 평가 → ThreatLevel 결정 → CombatAlertQueue에 경보 등록 → `SetFleeing()` 호출
- **Warrior 동작:** override → 빈 메서드 (경보 등록 없음)

### ThreatLevel 판단 기준 (Monster.Hp 기반)
| ThreatLevel | Monster HP |
|-------------|-----------|
| AllNeeded | ≥ 40 |
| VeryStrong | ≥ 25 |
| Strong | ≥ 15 |
| Weak | < 15 |

### CombatAlertQueue 주요 결정
| 항목 | 결정 | 이유 |
|------|------|------|
| Barracks 목록 관리 | 캐시 등록 패턴 (`RegisterBarracks`/`UnregisterBarracks`) | `FindObjectsOfType<Barracks>()` 런타임 씬 전체 탐색 금지 |
| Dispatch 버퍼 | `_dispatchBuffer` 재사용 (readonly List) | GC 최적화, 파견마다 new List 할당 방지 |
| 키 입력 가드 | `if (Keyboard.current == null) return;` | Play Mode 진입/종료 타이밍 NRE 방지 |
| 단일 책임 | UpdateFighting()이 StartReturning() 단일 호출 | AttackRoutine 코루틴과 Update의 중복 호출 방지 |

---

---

## 24. v0.2-5 — 무기 시스템 수치 확정 (2026-06-09)

**날짜:** 2026-06-09

### 무기 종류: 1종 (합금 무기)
| 항목 | 결정 | 이유 |
|------|------|------|
| 무기 종류 | 1종 (구리+은 혼합) | v0.2-5 범위를 단순하게 유지. 2종으로 만들면 WeaponType 분기, 공격력 단계 등 구현 복잡도 상승 |
| 제작 비용 | 구리x5 + 은x3 | 구리(보통 희귀) + 은(고희귀) 혼합. 전략적 자원 비축 필요 수준 |
| 비무장 공격력 | 15 HP/회 | 기존 값 |
| 무장 공격력 | 25 HP/회 (+10) | Monster(HP 50) 기준 비무장 4회→무장 2회. 명확한 체감 차이 |

### 건물 의존 관계: Forge → Blacksmith
| 항목 | 결정 | 이유 |
|------|------|------|
| 구조 | Forge 먼저 건설해야 Blacksmith 건설 가능 | 진행감(progression) 부여. Forge만 있으면 무기 제작 불가 → 두 건물 투자 필요 |
| Forge 역할 | 선행 건물 (용광로 — 금속 가공 인프라) | 단독으로 기능 없음, Blacksmith의 전제조건 |
| Blacksmith 역할 | 무기 제작 실행 건물 | 플레이어 명령(키 입력) → 구리x5+은x3 소비 → Warrior에게 무기 배급 |
| 건설 비용 | Forge: 나무15+돌10 / Blacksmith: 나무20+돌15 | 기존 확정값 유지 |

### 경보 타임아웃: 없음
| 항목 | 결정 | 이유 |
|------|------|------|
| 타임아웃 | 없음 | 플레이어가 Q/W/E/Space로 명시적으로 처리해야만 경보 소멸. "AI는 명령을 거부하거나 플레이어 판단을 기다린다"는 v0.1 핵심 철학과 일관성 유지 |

### 미확정 (구현 중 결정)
- 무기 제작 트리거 키 (Blacksmith 클릭 후 어떤 키인지)
- 무기 유지 여부 (Warrior가 Standby로 돌아온 후에도 무장 상태 유지인지)

---

---

## 25. GOAP 마이그레이션 전략 결정 (2026-06-10)

**날짜:** 2026-06-10

### 4단계 점진적 교체 전략 채택

| 단계 | 내용 | 이유 |
|------|------|------|
| 1단계 | GOAP 레이어만 추가 (FSM 그대로) | FSM 위에 올라타는 목표 결정 레이어. regression 없음. |
| 2단계 | Gatherer FSM 내부를 GoapAction으로 교체 | 검증 우선. 가장 복잡한 유닛 먼저. |
| 3단계 | Builder / Warrior 동일 적용 | 검증된 구조 복사. |
| 4단계 | GOAP 래퍼 제거 | FSM 완전 소멸. 결과적으로 완전 교체. |

**핵심 원칙:** 각 단계마다 게임 실행 가능 상태 유지. 롤백 가능.

### GOAP 아키텍처 결정

| 항목 | 결정 | 이유 |
|------|------|------|
| WorldState 표현 | `bool[]` + `WorldStateKey enum` 인덱싱 | GC-free. Tick 루프에서 new 없음. |
| 플래닝 알고리즘 | Forward Chaining DFS (max depth 5) | Goal 4개 / Action 5개 규모에서 A* 불필요. |
| GoapPlanner | static class | 20개 유닛이 공유 가능. 상태 없음. |
| GoapAgent → FSM 인터페이스 | pull 방식 (FSM이 CurrentGoalType 읽음) | 발행 시점 제어가 FSM 쪽에 있어 자연스러움. |
| Tick 동기화 | `game.tick` MessageBus 채널 | GameManager 기존 Tick 루프 재활용. |
| Regression 방지 | GoapAgent 미부착 시 기존 FSM 그대로 | 프리팹 단위로 점진 교체 가능. |

### Gatherer 수정 최소화 원칙
- AIUnit.cs: **수정 없음**
- Gatherer.cs: internal getter 4개 + OnIdle 분기 2줄 + 3줄(필드/Start/OnThreatDetected) = **9줄 추가**
- GameManager.cs: OnTick 끝에 `game.tick` 발행 **1줄 추가**

---

## 26. GOAP 2단계 버그 수정 결정 (2026-06-12)

**날짜:** 2026-06-12

### 버그 1: GatherAction 미진입 (_actions 배열 순서)
| 항목 | 결정 |
|------|------|
| 현상 | DFS가 [SearchNode, SearchNode, Gather] 3단 플랜 반환 → NotifyArrival에서 GatherAction 찾지 못함 |
| 원인 | `_actions`에서 SearchNodeAction이 GatherAction 앞에 있어 DFS depth 1에서 SearchNode 재시도 |
| 수정 | `_actions` 순서: `[Flee, GatherAction, SearchNodeAction, Return, Wait]` (GatherAction 앞으로 이동) |

### 버그 2: 활성 플랜 중 불필요한 Replan (근본 원인)
| 항목 | 결정 |
|------|------|
| 현상 | 채집-귀환 반복 사이클이 실행되지 않음 |
| 원인 | `IsAtBase` (기지 이탈 시 false), `HasAvailableNode` (노드 예약 후 Available 목록에서 제외) 변화가 changed detection 발동 → 플랜을 [GatherAction] 단독 또는 WaitForResource로 교체 → GatherAction.Execute()는 no-op이므로 채집 코루틴 미시작 |
| 수정 | `planActive = _currentActionIndex >= 0 && _currentActionIndex < _planBuffer.Count` — 플랜 실행 중엔 changed detection Replan 차단. 초기 상태(index=-1)와 플랜 소진 후에만 Replan 허용 |

### 버그 3: Execute 직후 false changed 감지
| 항목 | 결정 |
|------|------|
| 원인 | `SearchNodeAction.Execute()` → `_targetNode` 즉시 세팅, 다음 Tick에서 `IsNodeReserved` 변화 감지 → Replan |
| 수정 | `TryAdvanceAction` 반환 및 `Replan()` 후 `UpdateWorldState()` 재호출 → prevState에 Execute 직후 상태 반영 |

### PlayerController 파견 대상 제외
- GoapAgent 부착 유닛(Gatherer): 좌클릭 파견 대상에서 제외 — GOAP가 자율 제어
- Builder: 좌클릭 파견 대상에서 제외 — `building.reserved` MessageBus로만 이동

---

## 27. Builder 시스템 개선 결정 (2026-06-12)

**날짜:** 2026-06-12

### Builder 무한 루프 수정
| 항목 | 결정 |
|------|------|
| 현상 | `StartConstruction()` 실패 시 999+ 경고/로그 발생 |
| 원인 | 실패 시 `ResetCycle()` → `_isInBuildCycle=false` → `OnIdle()` → 즉시 `SearchAndBuild()` → 이미 도착한 위치에서 재예약 → 60fps 루프 |
| 수정 | 실패 시 `_isInBuildCycle` 유지 (true), `ReleaseConstruction()` + `_targetBuilding=null` + `Invoke(_retryDelay)` 만 수행. `OnIdle()` 차단됨 |

### OnBuildingReserved 개선
- 기존: `_isInBuildCycle=true`이면 무조건 무시
- 수정: `_isInBuildCycle=true && _targetBuilding!=null`일 때만 무시
- 이유: 자원 부족 대기 중(`_targetBuilding=null`)에도 새 건물 즉시 반응 가능하게

### 자원 우선순위 시스템
| 항목 | 결정 |
|------|------|
| 방식 | 방식 C — GameManager.PreferredGatherType 중앙 관리 |
| 트리거 | Builder.OnReachDestination() 자원 부족 → 부족량 계산 → gm.RequestResource(더 부족한 타입) |
| 적용 | Gatherer.SearchAndGo() 2-패스: 1패스=PreferredType, 2패스=임의 타입 폴백 |
| 이유 | Builder-Gatherer 직접 결합 없음. WorldState 수정 불필요. 최소 코드 변경으로 구현 가능 |

---

## 28. Warehouse 자원 창고 시스템 결정 (2026-06-12)

**날짜:** 2026-06-12

### 문제 배경
- Gatherer가 BasePosition(House)으로 귀환 시 Builder가 근처에서 건설 중이면 시각적으로 "자원을 Builder에게 갖다주는 것처럼" 보임
- House(안전구역)와 자원 납품 지점이 동일 위치로 역할 혼재

### 방식 결정
| 옵션 | 결정 |
|------|------|
| A: WoodDepot + StoneDepot 분리 | 미채택 — 구현 2배 복잡 |
| B: 단일 Warehouse | **채택** — 역할 분리 명확, 구현 최소 |
| C: House 재활용 | 미채택 — 시각적 혼동 미해결 |

### 설계 결정
| 항목 | 결정 | 이유 |
|------|------|------|
| 건물 타입 | Warehouse (Building 상속, static Instance) | 1개 제한 필요 |
| 건설 비용 | 나무 10, 돌 8 | 기본 인프라, 초반 건설 가능 수준 |
| 도착 반경 | 1.5f | A* 격자 오차(~0.7f) + 도착 임계값(0.2f) 고려 |
| 배치 키 | 7번 키 | 6번까지 기존 건물 사용 |
| 폴백 동작 | Warehouse 미완공 시 BasePosition(House) 사용 | 이전 버전 완전 호환 |

### 역할 분리
| 건물 | 역할 |
|------|------|
| House | 안전 구역 (체력 회복, Fleeing 해제 기준) |
| Warehouse | 자원 납품 지점 (GOAP `IsAtBase` 기준) |

---

---

## 29. GOAP 3단계 — Builder 적용 결정 (2026-06-12)

**날짜:** 2026-06-12

### 구조 설계 결정

| 항목 | 결정 | 이유 |
|------|------|------|
| 별도 BuilderGoapAgent | Gatherer GoapAgent와 분리된 독립 클래스 | 각 유닛 타입이 다른 WorldState/Goal/Action을 가지므로 분리가 명확 |
| WorldState 키 공유 | 기존 열거형 확장 (HasPendingBuilding=7, IsBuildingReserved=8 추가) | 새 타입 없이 기존 배열/플래너 인프라 재활용. Count 7→9 |
| GoapAction.InitializeForBuilder() | virtual no-op 추가 (abstract 아님) | Gatherer 기존 Action 수정 없이 Builder Action 초기화 경로 분리 |
| Actions 순서 | `[Flee, ConstructAction, SearchBuildingAction, Wait]` | SearchBuildingAction 앞에 ConstructAction → DFS 2단 플랜 [SearchBuilding, Construct] 보장 |

### Goals / WorldState (Builder 전용)

| 키 | 의미 |
|----|------|
| HasPendingBuilding (7) | BuildingManager에 Unbuilt 건물 존재 여부 |
| IsBuildingReserved (8) | _targetBuilding != null |

| Goal | Priority | 조건 |
|------|---------|------|
| Flee | 0 | HasThreat=true |
| BuildBuilding | 1 | HasPendingBuilding=true, !HasThreat |
| WaitForBuilding | 2 | HasPendingBuilding=false, !HasThreat |

### GOAP 실행 흐름

```
game.tick → BuilderGoapAgent.OnTick → Replan → BuildBuilding
  → [SearchBuildingAction, ConstructAction]
  → SearchBuildingAction.Execute() → Builder.ExecuteSearchBuilding() → SearchAndBuild()
  → Builder 이동 → OnReachDestination() → StartConstruction() → BuildRoutine 시작
  → NotifyArrival() → SearchBuilding → Construct (index 전환)
  → BuildRoutine 완료 → _targetBuilding=null → IsBuildingReserved=false
  → 다음 Tick: ConstructAction.IsComplete=true → Replan → 새 사이클
```

### 자원 부족 처리 (GOAP 모드)
- `StartConstruction()` 실패 → `_targetBuilding=null` + `Invoke(SearchAndBuild, retryDelay)`
- NotifyArrival 미호출 → `_currentActionIndex=0` (SearchBuildingAction 유지)
- planActive=true → changed detection Replan 차단
- retryDelay 후 SearchAndBuild() 재호출 (P-004 가드: CurrentAction=SearchBuildingAction 확인)

### Legacy FSM 호환
- `BuilderGoapAgent` 미부착 시 기존 FSM 완전 동작 (Regression 없음)
- 모든 콜백(OnIdle, OnFleeingExit, OnPathFailed, BuildRoutine)에 GOAP 분기 추가

---

*이 문서는 새로운 결정이 내려질 때마다 업데이트됩니다.*
*버전 이력: 초기 작성 → v0.1 범위 재조정 → 확장성 전수 분석 → 2026-05-27 설계 공백 13개 전수 확정 (GDD v2.2.0) → 2026-05-28 Week 3~7 구현 결정 추가 → 2026-06-09 Week 8~10 + v0.2-1~v0.2-5 결정 추가 (GDD v3.0.0) → 2026-06-10 GOAP 마이그레이션 전략 확정 → 2026-06-12 GOAP 2단계 버그 수정 + Builder 개선 + Warehouse 시스템 추가 (GDD v3.1.0) → 2026-06-12 GOAP 3단계 Builder 완료 (GDD v3.2.0)*

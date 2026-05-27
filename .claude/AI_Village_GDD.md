# 📋 AI Village — 게임 기획 및 개발 명세서

> **문서 버전:** v2.2.0 (설계 공백 전수 보완 — 개발 착수 가능 상태)
> **최종 수정:** 2026-05-27
> **장르:** 2D 탑다운 AI 자율 에이전트 시뮬레이션
> **엔진:** Unity 2D (URP)
> **플랫폼:** PC (Steam)

---

## 🗺️ 출시 로드맵 (전체 그림)

| 버전 | 핵심 내용 | 목표 기간 | 완성 가능성 |
|------|----------|---------|-----------|
| **v0.1** ← 현재 | AI 마을 빌더 (자원 2종 + 유닛 2종 + 건물 3종 + 위협 1종) | 8~10주 | 80% |
| v0.5 | Explorer + FogOfWar + 무기/정비 + 전투 방어 | +6주 | 65% |
| v1.0 | 적 팩션 1개 + 침략 + 왕국 정복 | +8주 | 50% |

> **원칙:** v0.1을 완전히 동작하는 게임으로 만든 뒤 다음 단계로 진행.
> 범위를 줄이는 이유: AI가 코드를 써줘도 Unity 에디터 작업, 시스템 연동, 디버깅은 개발자 직접 수행 → 시스템이 많을수록 완성 가능성이 급감.

---

## 🎯 v0.1 게임 비전

> **"AI들이 스스로 생각하고 움직이는 마을을, 플레이어의 전략적 판단으로 완성한다."**

플레이어는 명령을 내리는 것이 아니라 **제안**한다.
AI는 생존 판단에 따라 제안을 **수행하거나 거부**한다.

### 승리/패배 조건

| 조건 | 내용 |
|------|------|
| **승리** | 인구 20명 + Town Hall 건설 완료 |
| **패배** | 전 유닛 사망 |

### 시작 시드 상태

> Gatherer 1개 + House 1개 배치 완료
> 초기 자원: 나무 x15, 돌 x8

---

## ✅ v0.1 해결된 설계 항목

| # | 항목 | 결정 |
|---|------|------|
| V01-01 | 자원 종류 | 나무 + 돌 2종만 |
| V01-02 | 유닛 종류 | Gatherer + Builder 2종만 |
| V01-03 | 건물 종류 | 집 + 채석장 + Town Hall 3종만 |
| V01-04 | 위협 종류 | 중형 몬스터 1종 (순찰 + 추적) |
| V01-05 | FSM 상태 수 | 6개 (Idle/Moving/Gathering/Returning/Building/Fleeing) |
| V01-06 | 플레이어 지시 | 2가지 (위험 파견 + 건설 위치 지정) |
| V01-07 | 승리 조건 | 인구 20명 + Town Hall 건설 |
| V01-08 | 패배 조건 | 전 유닛 사망 |
| V01-09 | 자원 예약 | 노드당 1명, 이동 시작 시 예약 |
| V01-10 | AI 거부 로직 | 위험 지역 파견 시 체력 80% 미만이면 거부 |
| V01-11 | 나무 1회 수집량 | 3개 (Inspector 조정 가능) |
| V01-12 | 돌 1회 수집량 | 2개 (Inspector 조정 가능) |
| V01-13 | Gatherer 인벤토리 | 나무 6개 or 돌 4개 (2회치, Inspector 조정 가능) |
| V01-14 | 기지 위치 | 시작 House 위치 고정 (월드 좌표 저장, 이동 불가) |
| V01-15 | 노드 선택 알고리즘 | 비예약 노드 중 가장 가까운 노드 (A* 거리 기준) |
| V01-16 | Builder 방식 | 자동: 자원 충족 시 House 우선 건설 / 플레이어 지시 있으면 우선 처리 |
| V01-17 | 유닛 자동 생성 | GameManager가 Gatherer:Builder = 3:1 비율 목표로 자동 생성 |
| V01-18 | Gathering→Fleeing | 자원 버리고 즉시 도주 |
| V01-19 | 체력 회복 | 기지 반경 5타일 내, 초당 5 HP (Inspector 조정 가능) |
| V01-20 | 몬스터 공격력 | 10 HP / 1초 간격 (Inspector 조정 가능) |
| V01-21 | DangerRegistry 회피 반경 | 4타일 우회 |
| V01-22 | DangerRegistry 만료 시간 | 120초 (Inspector 조정 가능) |
| V01-23 | 건물 건설 예약 | 건설 지시 시 해당 타일 즉시 예약 (자원 노드 예약과 동일 방식) |
| V01-24 | MessageBus 메시지 목록 | 아래 섹션 참조 |

## ⏳ v0.1 미해결 (개발 중 정의)

| # | 항목 | 우선순위 |
|---|------|---------|
| D-01 | 몬스터 순찰 경로 생성 방식 | 🟡 중간 |
| D-02 | 최대 유닛 초과 알림 UI | 🟢 낮음 |

---

## 🔮 v0.1에서 제외된 기능 (추후 버전)

> 아래 기능은 v0.1에서 **구현하지 않는다.**
> 단, 데이터 구조에서 미래를 위한 필드는 미리 포함한다.

| 기능 | 예정 버전 |
|------|---------|
| Explorer 유닛 | v0.5 |
| Fog of War | v0.5 |
| 철광석/구리/은 자원 | v0.5~v1.0 |
| 대장간 + 무기 + 정비 시스템 | v0.5 |
| 전투 모드 (마을 방어) | v0.5 |
| 적 팩션 + 침략 시스템 | v1.0 |
| FactionManager / TerritoryManager | v1.0 |
| 통신탑 / 연구소 / 자동화 공장 | v1.0 |
| SaveManager (ISaveable) | v1.0 |
| IDestructible (건물 파괴) | v1.0 |
| 반란/독립 시스템 | v2.0 |

---

## 1. 프로젝트 개요

### 핵심 USP
> **"AI는 명령을 따르는 도구가 아니라, 생존 판단 능력을 가진 자율 존재다."**

v0.1에서는 이 USP의 **기초**를 구현한다:
- AI가 스스로 자원 노드를 선택하고 수집한다
- 위험 지역을 경험한 AI는 DangerRegistry에 기록하고 회피한다
- 체력이 낮은 AI는 플레이어의 위험 지역 파견 명령을 **거부**한다

---

## 2. 자원 시스템

### 자원 종류 (v0.1: 2종)

| 자원 | 맵 개수 | 수집 시간 | 재생 시간 | 주요 용도 |
|------|--------|----------|----------|---------|
| 나무 | 60개 | 2초 | 30초 | 집, 채석장 건설 / 유닛 생성 |
| 돌 | 30개 | 5초 | 60초 | 집, 채석장, Town Hall 건설 |

채석장 자동 생산: 돌 1개 / 45초

### 자원 수치 (Inspector에서 조정 가능 — 기준값)

| 자원 | 1회 수집량 | Gatherer 인벤토리 최대 |
|------|----------|-------------------|
| 나무 | 3개 | 6개 (2회치) |
| 돌 | 2개 | 4개 (2회치) |

> Gatherer는 나무 or 돌 중 하나만 인벤토리에 보유 (혼합 불가).
> 인벤토리 가득 or 노드 고갈 시 → Returning 상태 전환.

### 자원 예약 시스템
- 노드당 Gatherer 1명만 채집 가능
- Gatherer가 이동 시작 시 예약 → 귀환 완료 or 노드 고갈 시 예약 해제
- 예약된 노드는 다른 Gatherer가 선택하지 않음

### Gatherer 노드 선택 알고리즘
1. 비예약 상태인 노드 목록 조회 (ResourceManager에서 가져옴)
2. 그 중 A* 경로 거리가 가장 짧은 노드 선택
3. 동점 시 랜덤 선택
4. 비예약 노드가 없으면 → Idle 유지 (WaitingForNode 서브 상태)

---

## 3. 맵 시스템 (v0.1 단순화)

- 크기: 60 x 60 타일 (v1.0에서 100x100 확장)
- 구성: 단일 플레이어 영역 (팩션 구분 없음)
- 자원 노드는 맵 전역에 분산 배치
- 몬스터는 특정 구역을 순찰 (맵 일부 지역에 위험 구역 존재)
- Fog of War 없음 (v0.5에서 추가)

### 기지(Base) 정의
- **기지 중심**: 시작 시 배치된 House의 월드 좌표 (게임 시작 시 고정)
- **기지 반경**: 5타일 (이 안에서 체력 회복, Fleeing 해제)
- GameManager가 시작 시 `basePosition` 으로 저장하고 모든 시스템이 참조

---

## 4. AI 유닛 시스템

### 유닛 공통 데이터 구조

```csharp
// 모든 AIUnit이 보유해야 하는 필드
float  hp                   // 현재 체력
float  maxHp                // 최대 체력 (기본값: 100)
UnitState currentState      // 현재 FSM 상태

// ⚠️ v0.1에서는 기본값만, v0.5 무기/정비 시스템 대비
bool   isEquipped           // 무기 장착 여부 (기본값: false) — v0.5 전투 참여 조건

// ⚠️ v0.1에서는 기본값만, v1.0 팩션/반란 시스템 대비
int    originalFactionId    // 원래 팩션 ID (기본값: 0 = 플레이어)
int    currentFactionId     // 현재 소속 팩션 ID (기본값: 0 = 플레이어)
float  loyalty              // 충성도 0~100 (기본값: 100 고정)
```

### 유닛 타입 (v0.1: 2종)

| 타입 | 역할 | 생성 비용 | 조건 |
|------|------|---------|------|
| Gatherer | 자원 수집 + 운반 | 나무x5, 돌x3 | House 여유 인구 1 이상 |
| Builder | 건물 건설 | 나무x5, 돌x3 | House 여유 인구 1 이상 |

> 전투 참여 없음 (v0.1). 몬스터를 만나면 도주만 한다.

### FSM 상태 목록 (v0.1: 6개)

```
Idle          → 할 일 없음, 다음 태스크 대기
Moving        → 목적지로 이동 중
Gathering     → 자원 노드에서 채집 중 (Gatherer만)
Returning     → 자원 또는 건설 완료 후 기지 귀환 중
Building      → 건물 건설 중 (Builder만)
Fleeing       → 몬스터 감지, 안전 지역으로 도주 중
```

### FSM 전환 규칙 (완전판)

```
Idle → Moving          : 노드/건설 태스크 할당됨 (GameManager → UnitManager → AIUnit)
Moving → Gathering     : 자원 노드 도착 (Gatherer)
Moving → Building      : 건설 위치 도착 (Builder)
Moving → Fleeing       : 이동 중 몬스터 감지 (감지 범위 내 진입)
Gathering → Returning  : 인벤토리 가득 or 노드 고갈
Gathering → Fleeing    : 채집 중 몬스터 감지 → 자원 버리고 즉시 도주
Building → Returning   : 건설 완료
Building → Fleeing     : 건설 중 몬스터 감지 → 즉시 도주 (건설 중단, 재개 불가 — 재지시 필요)
Returning → Idle       : 기지 반경 도착 + 자원 내려놓기 완료
Fleeing → Idle         : 기지 반경 5타일 내 도달
```

> ⚠️ Fleeing 진입 시 자원은 **무조건 버림** (자원 회수보다 생존 우선).
> ⚠️ Building → Fleeing 시 건설은 중단됨. 재개하려면 플레이어가 다시 건설 위치를 지정해야 함.

### AI 거부 로직 (v0.1)

| 조건 | 결과 |
|------|------|
| 플레이어가 위험 지역 파견 명령 | 체력 80% 이상이면 수행 |
| 플레이어가 위험 지역 파견 명령 | 체력 80% 미만이면 **거부** (Idle 유지) |

---

## 5. AI 기억 시스템 (DangerRegistry)

```csharp
DangerRecord {
    Vector2   location      // 위험 발생 좌표
    int       dangerLevel   // 위험도 1~3 (v0.1에서는 1종 몬스터 = 2)
    float     timestamp     // 발견 시각 (Time.time 기준)
}
```

- Fleeing 상태 진입 시 현재 좌표를 DangerRegistry에 자동 기록
- **회피 반경**: 기록 좌표 기준 4타일 — A* 경로탐색 시 이 범위를 비용이 높은 영역으로 처리
- **만료 시간**: 기록 후 120초 경과 시 자동 삭제 (Inspector 조정 가능)
- 만료된 레코드는 DangerRegistry가 매 Tick마다 정리
- 플레이어가 위험 지역 파견 명령을 내리면 → AI가 조건 체크 후 수행/거부

---

## 6. 위협 시스템 (v0.1: 1종)

| 속성 | 값 | 비고 |
|------|---|------|
| 종류 | 중형 몬스터 | |
| 행동 | 지정 구역 순찰 + 유닛 감지 시 추적 | |
| 공격력 | 10 HP / 1초 간격 | Inspector 조정 가능 |
| 위험도 | 2 | DangerRegistry 기록값 |
| 감지 범위 | 3타일 | |
| 추적 포기 | 거리 8타일 초과 or 기지 반경 도달 시 | |
| 순찰 방식 | 지정 웨이포인트 2~3개를 순환 | 개발 중 결정 (D-01) |

### 위험 상황 흐름

```
몬스터가 AI 유닛 감지 (3타일 이내)
        ↓
AI 유닛: Fleeing 상태 진입 + DangerRegistry 기록
        ↓
기지 방향으로 도주 (A* 경로 사용)
        ↓
기지 반경 진입 → Idle 복귀
        ↓
체력 회복 (시간 경과, 기지 내 자동 회복)
```

---

## 7. 건물 시스템 (v0.1: 3종)

### 건물 공통 데이터 구조

```csharp
// 모든 Building이 보유해야 하는 필드
string buildingName         // 건물 이름
float  hp                   // 현재 체력 (v0.1에서는 파괴 없음, 값만 보유)
float  maxHp                // 최대 체력
bool   isUnderConstruction  // 건설 중 여부 (예약 시스템용)
bool   isReserved           // 다른 Builder에게 이미 배정됐는지 여부

// ⚠️ v0.1에서는 기본값만, v1.0 FactionManager/영토 귀속 대비
int    factionId            // 소유 팩션 ID (기본값: 0 = 플레이어)
```

### 건물 예약 시스템
- 플레이어가 건설 위치를 지정하면 해당 타일 즉시 `isReserved = true`
- `isReserved = true` 인 건물은 다른 Builder에게 배정 불가
- Builder가 건설 완료 or 중단(Fleeing) 시 `isReserved = false` 로 해제

### 건물 목록

| 건물 | 건설 비용 | 효과 | 건설자 |
|------|----------|------|------|
| 집 (House) | 나무x10, 돌x5 | 인구 한도 +2 | Builder |
| 채석장 (Quarry) | 나무x5, 돌x10 | 돌 자동 생산 (1개/45초) | Builder |
| 시청 (Town Hall) ★ | 나무x30, 돌x20 | 승리 조건 완성 | Builder |

> v0.1 건물에는 IDestructible 없음 (파괴 불가).
> v1.0에서 IDestructible 인터페이스 추가 예정 → 지금은 인터페이스만 파일에 정의해두기.

### 유닛 생성 비용

| 유닛 | 비용 | 조건 |
|------|------|------|
| Gatherer | 나무x5, 돌x3 | House 여유 인구 1 이상 |
| Builder | 나무x5, 돌x3 | House 여유 인구 1 이상 |

---

## 7-2. Builder 자동화 로직

Builder는 플레이어 지시가 없을 때 아래 우선순위로 자동 건설:

```
1순위: 플레이어가 지정한 건설 위치 (즉시 이동)
2순위: 인구 한도 포화 상태 → 가장 가까운 빈 타일에 House 자동 건설
3순위: 할 일 없음 → Idle 유지 (WaitingForTask)
```

> 2순위 조건: `현재 인구 >= 인구 한도 - 1` (여유 슬롯 1개 이하일 때 자동 House 건설 발동)

## 7-3. 유닛 자동 생성 시스템 (GameManager 담당)

GameManager가 매 Tick(0.5초)마다 체크:

```
조건 1: 여유 인구 슬롯 >= 1
조건 2: 자원 충족 (나무x5, 돌x3)
조건 3: 현재 Gatherer:Builder 비율 < 3:1

→ 조건 1+2+3 모두 충족 시 Gatherer 자동 생성
→ 조건 1+2 충족, Gatherer가 3배 이상 시 Builder 자동 생성
```

> 플레이어는 자동 생성에 별도로 개입하지 않음 (v0.1).
> 인구 한도 부족 시 생성 중단 → Builder가 House 자동 건설 후 재개.

## 8. 플레이어 지시 시스템 (v0.1: 2가지)

| 지시 | 방법 | AI 반응 |
|------|------|--------|
| 위험 지역 파견 | 위험 지역 클릭 → 유닛 선택 | 체력 80% 이상이면 수행, 미만이면 거부 |
| 건설 위치 지정 | 빈 타일 클릭 → 건물 선택 | Builder에게 건설 지시 전달 |

> v0.5에서 역할 지정, 긴급 귀환 추가.
> v1.0에서 침략 명령 추가.

---

## 8-2. 체력 회복 시스템

| 조건 | 회복량 |
|------|--------|
| 기지 반경(5타일) 내 체류 | 초당 5 HP (Inspector 조정 가능) |
| 기지 반경 밖 | 회복 없음 |
| 최대 체력(100) 초과 불가 | — |

> Fleeing → Idle 전환 후 기지 내에 머무르면 자동 회복.
> 체력이 80% 이상 회복되면 다음 플레이어 위험 파견 명령을 수행할 수 있음.

## 8-3. MessageBus 메시지 목록 (v0.1)

> MessageBus는 문자열 키(string key) 기반 이벤트 시스템으로 구현.
> 새 메시지 추가 시 이 목록에만 추가하면 됨 — 기존 코드 수정 불필요.

| 메시지 키 | 발신자 | 수신자 | 용도 |
|---------|------|------|------|
| `"resource.node.reserved"` | GathererFSM | ResourceManager | 노드 예약 알림 |
| `"resource.node.released"` | GathererFSM | ResourceManager | 노드 예약 해제 |
| `"resource.deposited"` | GathererFSM | GameManager | 자원 기지 반납 완료 |
| `"unit.fleeing"` | AIUnit | DangerRegistry | 위험 감지 + 좌표 기록 요청 |
| `"unit.died"` | AIUnit | UnitManager, GameManager | 유닛 사망 → 패배 조건 체크 |
| `"building.reserved"` | PlayerController | BuildingManager | 건설 위치 예약 |
| `"building.completed"` | BuilderFSM | PopulationManager, GameManager | 건물 완성 → 인구 한도 갱신 |
| `"victory.check"` | TownHall | GameManager | Town Hall 완성 → 승리 조건 체크 |

## 9. 성장 루프 (v0.1)

```
[시작]
Gatherer 1개 + House 1개 + 나무x15, 돌x8
        ↓
Gatherer → 나무/돌 수집 반복
        ↓
자원 충족 → Builder 생성 → House 건설 → 인구 +2
        ↓
Gatherer 추가 생성 → 수집 속도 향상
        ↓
Quarry 건설 → 돌 자동 생산 → 돌 병목 해소
        ↓
인구 20명 도달 + 자원 비축
        ↓
Builder → Town Hall 건설 → 승리
```

**위험 개입 시나리오:**
```
몬스터 출현 → AI 도주 + DangerRegistry 기록
        ↓
플레이어: 위험 지역 파견 (체력 높은 유닛 선택)
        ↓
AI: 조건 충족이면 접근 (위험 탐색)
    조건 미충족이면 거부 (생존 우선)
```

---

## 10. 기술 리스크 (v0.1)

| ID | 내용 | 수준 | 해결 방안 | 상태 |
|----|------|------|----------|------|
| R-001 | AI 메시지 통신 | 🔴 | MessageBus 싱글톤 | ⏳ |
| R-002 | 성능 (유닛 수) | 🟡 | Tick 방식 FSM (0.1초마다 판단), 최대 30 | ⏳ |
| R-003 | 순환 데드락 | 🔴 | Gatherer 할당 시 예약 시스템으로 방지 | ⏳ |
| R-004 | FSM 엣지케이스 | 🟡 | AnyState → Idle 폴백 전환 | 🔄 |
| R-005 | A* 경로탐색 | 🟡 | A* Pathfinding Project (무료 에셋) 사용 | ⏳ |
| R-006 | 몬스터 추적 해제 | 🟢 | 거리 8타일 초과 or 기지 경계 도달 시 추적 포기 | ⏳ |

---

## 11. 핵심 클래스 설계 (v0.1)

```
GameManager (씬 루트, 전체 흐름 관리)
├── ResourceManager
│   └── ResourceNode (나무/돌, 예약 상태 포함)
├── UnitManager
│   └── AIUnit (MonoBehaviour)
│       ├── GathererFSM
│       └── BuilderFSM
├── BuildingManager
│   └── Building
│       ├── House
│       ├── Quarry
│       └── TownHall
├── ThreatManager
│   └── Monster (순찰 + 추적)
├── DangerRegistry (싱글톤)
├── MessageBus (싱글톤)
├── PopulationManager (인구 한도 + 현재 인구 관리)
└── PlayerController (2가지 지시)
```

**v0.5~v1.0을 위해 지금 파일만 만들어둘 클래스 (내용은 비워두기):**
```
IDestructible.cs    (인터페이스만 선언 — v1.0 건물 파괴)
IUpgradeable.cs     (인터페이스만 선언 — v1.0 건물/무기 강화)
EquipmentManager.cs (빈 클래스 — v0.5 무기/정비)
FactionManager.cs   (빈 클래스 — v1.0 팩션 관리)
SaveManager.cs      (빈 클래스 — v1.0 세이브/로드)
```

---

## 12. 개발 착수 순서 (v0.1 기준, 8~10주)

| 주차 | 목표 | 완료 기준 |
|------|------|---------|
| Week 1 | ResourceNode.cs + 기본 씬 세팅 | 나무 클릭 시 콘솔에 "수집됨" 출력 |
| Week 2 | GameManager + ResourceManager | 시작 자원 세팅, 노드 목록 관리 |
| Week 3 | AIUnit 기본 이동 (A* 연동) | Gatherer 1개가 ResourceNode로 이동 |
| Week 4 | GathererFSM (Idle/Moving/Gathering/Returning) | Gatherer가 수집 → 귀환 반복 |
| Week 5 | MessageBus + 다중 Gatherer + 예약 시스템 | Gatherer 3개가 겹치지 않고 분산 채집 |
| Week 6 | BuilderFSM + BuildingManager | Builder가 House 건설 완료 |
| Week 7 | PopulationManager + 유닛 생성 | 자원 충족 시 Gatherer 자동 생성 |
| Week 8 | ThreatManager + Monster + Fleeing 상태 | 몬스터 등장 시 AI 도주 |
| Week 9 | DangerRegistry + 플레이어 지시 2가지 | 위험 파견 거부 로직 동작 확인 |
| Week 10 | Town Hall + 승리/패배 조건 + 폴리싱 | 첫 번째 플레이어블 빌드 완성 |

---

## 13. 에이전트 활용 파이프라인 (v0.1)

```
[새 시스템 구현 시 반드시 이 순서 준수]

1. unity-ai-behavior-architect   → "어떻게 동작할지" 설계
          ↓
2. unity-senior-programmer       → C# 코드 생성
          ↓
3. unity-code-reviewer           → 코드 리뷰 (JSON 출력)
          ↓
4. unity-performance-optimizer   → 퍼포먼스 분석
          ↓
5. unity-pr-revision-coder       → 리뷰 피드백 반영 수정
```

**v0.1에서 주로 사용할 에이전트:**
- `unity-senior-programmer` — Week 1~9 주력 사용
- `unity-code-reviewer` — Week 3 이후 매 시스템 완성마다
- `game-qa-exploiter` — Week 10 QA 단계

---

## 14. v0.5 예정 기능 (참고용)

```
✅ Explorer 유닛 추가 (미탐험 구역 우선 탐색)
✅ Fog of War (시야 5타일)
✅ 철광석 자원 추가
✅ 대장간 건물 추가
✅ 기본 무기 + 정비 시스템
✅ 전투 모드 (마을 방어, 정비 완료 유닛만 참여)
✅ 플레이어 지시 5가지로 확장 (역할 지정, 긴급 귀환 추가)
✅ 승리 조건 변경: 인구 30명 + Town Hall
```

---

*버전 이력: v1.0.0~v1.6.0 (전체 비전 GDD) → v2.0.0 (v0.1 범위 재조정) → v2.1.0 (확장 대비 필드 추가) → v2.2.0 (설계 공백 13개 전수 보완 — 수집량/인벤토리/기지/노드선택/FSM전환/Builder자동화/유닛생성/체력회복/공격수치/DangerRegistry/건물예약/MessageBus 전부 확정, 2026-05-27)*

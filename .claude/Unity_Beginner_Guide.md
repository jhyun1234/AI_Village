# 🎮 Unity 초보자를 위한 AI Village 개발 가이드

> 작성일: 2026-05-27  
> 목적: Unity가 처음인 개발자가 AI Village를 만들면서 성장하기 위한 최적 경로
> 핵심 원칙: **"직접 이해하며 만든다. AI가 짜줘도 내가 읽고 설명할 수 있어야 한다."**

---

## 📌 지금 상황 진단

Program.cs 파일을 열었다는 건 C# 콘솔 앱은 어느 정도 익숙한 것.  
Unity가 낯선 이유는 "C# 문법"이 아니라 **"Unity의 작동 방식"** 이 다르기 때문이다.

| 콘솔 앱 (익숙함) | Unity (낯선 것) |
|-----------------|----------------|
| `Main()` 에서 시작 | `Start()` / `Update()` 생명주기 |
| 객체 직접 생성 (`new`) | `GameObject` + `Component` 패턴 |
| 코드만 있음 | 씬(Scene) + 인스펙터(Inspector) |
| 실행 = 끝 | 실행 = 매 프레임 반복 |

---

## 🏁 추천 학습 경로 (AI Village 목표 기준)

### Phase 0 — Unity 생존 기초 (3~5일)

AI Village를 만들기 전에 반드시 알아야 할 최소한의 것들.

**① GameObject & Component 이해**
- Unity의 모든 것은 GameObject + 붙어있는 Component로 이루어진다
- `AIUnit`도 결국 GameObject에 `AIUnit.cs` 스크립트(Component)를 붙인 것
- 인스펙터에서 값을 수정하면 코드 안 건드려도 동작이 바뀜

**② MonoBehaviour 생명주기 암기**
```
Awake()   → 이 오브젝트가 생성될 때 딱 1번 (다른 오브젝트 참조 전 초기화)
Start()   → 첫 프레임 시작 전 딱 1번 (다른 오브젝트 참조 가능)
Update()  → 매 프레임 (60FPS = 초당 60번 호출)
OnDestroy() → 오브젝트가 삭제될 때
```

**③ Prefab 개념**
- Prefab = 오브젝트의 "틀(템플릿)"
- AIUnit Prefab을 만들면 같은 설정의 유닛을 무한 복제 가능
- AI Village에서 Gatherer, Builder, Explorer 모두 Prefab으로 만들 것

**학습 방법:**
- Unity Learn 공식 사이트: learn.unity.com → "Unity Essentials" 무료 코스 (2시간)
- 직접 빈 프로젝트 만들어서 Cube 하나 움직여보기

---

### Phase 1 — 에이전트 파이프라인 활용법 (AI Village 시작부터 끝까지)

초보자가 복잡한 게임을 혼자 만들 수 없는 이유: 설계 + 구현 + 검증을 동시에 하려다 막힘.  
우리의 9개 에이전트는 이 문제를 해결한다.

**에이전트 사용 순서 (절대 바꾸지 말 것):**

```
[새 시스템을 만들 때마다 이 순서 반복]

1. unity-ai-behavior-architect  → "어떻게 동작할지" 설계
        ↓
2. unity-senior-programmer      → "실제 코드" 생성
        ↓
3. unity-code-reviewer          → "코드가 맞는지" 검토
        ↓
4. unity-performance-optimizer  → "느리지 않은지" 분석
        ↓
5. unity-pr-revision-coder      → "문제 있으면 수정"
```

**초보자가 해야 할 일:**
- 에이전트가 만든 코드를 Unity에 복붙하는 것이 아니라
- 코드를 읽고 "이 줄이 왜 이렇게 생겼지?" 를 스스로 설명할 수 있을 때까지 이해하기
- 모르는 줄이 있으면 그 줄만 Claude에게 설명 요청

---

### Phase 2 — Week 1-2 실제 구현 순서

**절대 원칙: 가장 작은 것부터 작동하게 만든다**

```
❌ 잘못된 순서: 모든 클래스 한번에 만들기 → 아무것도 작동 안 함
✅ 올바른 순서: 1개씩 작동 확인 후 다음으로
```

**Week 1 구현 순서:**
```
Day 1-2: ResourceNode.cs 만들기
  → 나무 오브젝트 하나 클릭하면 콘솔에 "나무 수집됨" 출력되면 성공
  
Day 3-4: GameManager.cs 기본 틀
  → ResourceNode들 목록 관리, 시작 자원 세팅
  
Day 5-7: AIUnit.cs 기본 이동
  → 유닛 하나가 ResourceNode 쪽으로 이동하면 성공
  → A* Pathfinding Project 에셋 설치 필요
```

**각 단계에서 확인할 것:**
1. Unity Console에 에러(빨간 글씨) 없는가?
2. 인스펙터에서 값이 올바르게 보이는가?
3. Play 버튼 눌렀을 때 예상한 동작이 나오는가?

---

## 🛠️ Unity 초보자 필수 세팅 및 도구

### 1. 에셋 스토어에서 반드시 설치할 것
```
무료 에셋:
✅ A* Pathfinding Project (Free) — AI 경로탐색 필수
✅ DOTween (HOTween v2) — UI 애니메이션, 이동 보간
```

### 2. Unity 에디터 기본 세팅
```
Edit → Project Settings → Editor:
  Enter Play Mode Settings → "Reload Domain" 해제
  → Play 모드 진입 속도 10배 빨라짐 (개발 중 필수)

Edit → Preferences → Colors:
  Playmode tint → 색 변경 (Play 모드인지 구분용)
```

### 3. 필수 단축키
```
Ctrl+P    → Play/Stop
Ctrl+D    → 오브젝트 복제
F         → 선택한 오브젝트로 Scene 뷰 포커스
Ctrl+Z    → 실행 취소 (Unity에서 자주 필요)
```

---

## 📚 코드를 이해하는 방법 (에이전트가 코드 줬을 때)

에이전트가 100줄짜리 코드를 주면 어떻게 이해하나?

### 3단계 이해법

**1단계: 클래스 이름만 읽기**
```csharp
public class ResourceNode : MonoBehaviour
// "ResourceNode는 Unity 오브젝트다" — 이것만 이해해도 50%
```

**2단계: 필드(변수) 목록 읽기**
```csharp
[SerializeField] private int woodAmount = 5;
private bool isBeingHarvested = false;
// "이 오브젝트가 기억하는 데이터가 뭔지" — 70% 이해
```

**3단계: 메서드 이름만 읽기 (내부 구현 무시)**
```csharp
public bool TryReserve(AIUnit requester) { ... }
public void StartHarvest() { ... }
public void Respawn() { ... }
// "이 오브젝트가 할 수 있는 일이 뭔지" — 90% 이해
```

**모르는 부분이 있을 때:**
> "ResourceNode.cs에서 TryReserve 메서드가 왜 bool을 반환하는지 설명해줘"
> 이렇게 **한 줄, 한 메서드**씩 질문하기

---

## ⚠️ 초보자가 자주 하는 실수 Top 5

### 실수 1: Update()에서 무거운 작업
```csharp
// ❌ 이렇게 하면 매 프레임(1초에 60번) 실행됨
void Update() {
    FindObjectOfType<GameManager>(); // 매우 느림
}

// ✅ Start()에서 한 번만
void Start() {
    gameManager = FindObjectOfType<GameManager>(); // 캐싱
}
```

### 실수 2: 에러를 무시하고 계속 진행
- Unity Console의 빨간 에러는 반드시 해결 후 다음 단계
- 에러 위에 에러가 쌓이면 원인 찾기 불가능

### 실수 3: 씬 저장을 안 함
- Ctrl+S 습관화 (Unity는 씬 파일을 별도 저장)
- 테스트 후 Play 모드에서 수정한 값은 Stop 시 사라짐

### 실수 4: 모든 것을 한 번에 만들려고 함
- ResourceNode 1개 완성 → Gatherer 1개 완성 → 연동 확인
- 이 순서를 지키면 에러가 나도 원인이 명확함

### 실수 5: Prefab vs 씬 오브젝트 혼동
- Prefab을 수정하면 씬에 있는 모든 복사본에 반영됨
- 씬 오브젝트 하나만 수정하면 그 오브젝트만 바뀜

---

## 🎯 AI Village 개발 중 에이전트 활용 예시

**상황: Gatherer AI FSM을 처음 만들 때**
```
1. unity-ai-behavior-architect에게:
   "Gatherer AI의 FSM 설계해줘. 
    상태: Idle/Moving/Harvesting/Returning/Fleeing
    조건: DangerRegistry, ResourceRegistry 연동"

2. 설계서 읽기 (10분) — 이해 안 되는 부분 바로 질문

3. unity-senior-programmer에게:
   "위 설계서 기반으로 GathererFSM.cs 작성해줘"

4. 코드 받으면 3단계 이해법으로 읽기

5. Unity에 붙여넣고 실행 — 에러 있으면:
   "이 에러 메시지 의미가 뭔지, 어떻게 고치는지 알려줘"
```

---

## 📅 학습 성장 지표

자신이 성장하고 있는지 확인하는 기준:

| 단계 | 기준 |
|------|------|
| Week 1 | 에이전트가 준 코드를 Unity에 넣고 실행할 수 있다 |
| Week 2 | 에러 메시지를 보고 어느 파일 어느 줄 문제인지 찾을 수 있다 |
| Week 3 | 에이전트가 준 코드에서 변수 이름이나 초기값을 직접 바꿀 수 있다 |
| Week 4 | 새 기능을 추가할 때 어떤 에이전트를 먼저 써야 하는지 스스로 결정한다 |
| Month 2 | 에이전트 설계서 없이도 간단한 기능을 혼자 추가할 수 있다 |

---

## 🔗 참고 리소스

| 리소스 | 용도 |
|--------|------|
| learn.unity.com | Unity 공식 무료 학습 (영상 + 실습) |
| docs.unity3d.com | 특정 기능 공식 문서 검색 |
| Unity Forum | 에러 메시지 검색 시 가장 많은 해결책 |

---

*이 문서는 개발 진행에 따라 업데이트됩니다.*
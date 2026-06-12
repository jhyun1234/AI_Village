---
name: feedback-goap-review-patterns
description: GOAP 2단계 PR 리뷰에서 반복된 피드백 패턴 — 인덱스 bounds 가드, 타입 방어, Invoke 가드
metadata:
  type: feedback
---

## GOAP Action 인덱스 관련 가드 패턴

리뷰어가 반복적으로 요구하는 세 가지 방어 패턴:

1. **플랜 소진 조건 누락 (P-001 유형)**: `_currentActionIndex < 0` 가드만으로는 부족함. 반드시 `_currentActionIndex >= _planBuffer.Count` 조건도 함께 추가해야 함. 두 조건이 OR로 묶여야 안전한 재플래닝 진입 보장.

2. **타입 비검증 방어 (P-002 유형)**: 인덱스 증가 후 다음 Action에 접근하기 전 반드시 (a) bounds 초과 체크 → (b) 예상 타입 is 검사 순서로 방어해야 함. 타입 불일치 시 `_currentActionIndex = -1`로 리셋 후 `Replan()` 호출.

3. **Invoke 지연 발화 가드 (P-004 유형)**: Invoke로 예약된 메서드는 Goal 전환 후에도 늦게 발화될 수 있음. GOAP 경로에서 호출되는 메서드 상단에 현재 Action 타입 체크 가드를 배치해야 함.

**Why:** 이 세 가지가 모두 누락되면 Gatherer가 영구 정지(P-001), 잘못된 Action 전환(P-002), 충돌하는 이중 실행(P-004) 문제로 이어짐.

**How to apply:** 향후 GOAP Action 시퀀스를 다루는 모든 메서드(NotifyArrival, TryAdvanceAction, Replan 등)에 세 패턴을 기본 체크리스트로 적용할 것. [[feedback_pr_review_patterns]]

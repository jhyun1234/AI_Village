// =============================================================================
// UnitState.cs
// 역할  : 모든 AIUnit 파생 클래스가 공유하는 상태 열거형 정의
// 사용법: using AIVillage.Units; 후 UnitState.Idle 등으로 참조
// 의존성: 없음 (순수 열거형)
// =============================================================================

namespace AIVillage.Units
{
    /// <summary>
    /// AI 유닛의 행동 상태를 나타내는 열거형.
    /// AIUnit._currentState 필드에서 사용된다.
    /// </summary>
    public enum UnitState
    {
        /// <summary>대기 상태. 기본값.</summary>
        Idle,

        /// <summary>목적지를 향해 이동 중인 상태.</summary>
        Moving,

        /// <summary>자원을 수집 중인 상태.</summary>
        Gathering,

        /// <summary>자원을 기지로 반납하기 위해 이동 중인 상태.</summary>
        Returning,

        /// <summary>건설 작업 중인 상태.</summary>
        Building,

        /// <summary>위협을 감지하여 도주 중인 상태.</summary>
        Fleeing,
    }
}

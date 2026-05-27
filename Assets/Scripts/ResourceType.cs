// =============================================================================
// ResourceType.cs
// 역할  : 자원 종류를 나타내는 enum 및 MessageBus 인터페이스 정의
// 사용법 : ResourceNode.cs 등 자원 관련 스크립트에서 참조
// 의존성 : 없음 (순수 데이터 정의 파일)
// TODO  : IMessageBus는 Week 5 이후 별도 파일(IMessageBus.cs)로 분리
// =============================================================================

namespace AIVillage.Resources
{
    /// <summary>
    /// 게임 내 채집 가능한 자원 종류.
    /// </summary>
    public enum ResourceType
    {
        WOOD,
        STONE
    }

}

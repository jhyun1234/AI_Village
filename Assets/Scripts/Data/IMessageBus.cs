// =============================================================================
// IMessageBus.cs
// 역할  : 게임 이벤트 발행/구독 인터페이스 정의
// 사용법: MessageBus.Instance 또는 GameManager.Instance.MessageBus 로 접근
// 의존성: 없음 (순수 인터페이스)
// GDD   : Week 5 MessageBus
// =============================================================================

using System;

namespace AIVillage.Core
{
    /// <summary>
    /// string 채널 기반 이벤트 발행/구독 인터페이스.
    /// ResourceNode가 Publish 전용으로 주입받고,
    /// GameManager 등 소비자는 MessageBus.Instance를 통해 Subscribe한다.
    /// </summary>
    public interface IMessageBus
    {
        /// <summary>채널을 구독한다. handler는 Publish 시 호출된다.</summary>
        void Subscribe(string channel, Action<object> handler);

        /// <summary>채널 구독을 해제한다.</summary>
        void Unsubscribe(string channel, Action<object> handler);

        /// <summary>채널로 메시지를 발행한다.</summary>
        void Publish(string channel, object payload);
    }
}

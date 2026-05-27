// =============================================================================
// MessageBus.cs
// 역할  : string 채널 기반 이벤트 발행/구독 싱글톤.
//         ResourceNode가 이벤트를 발행하고 GameManager 등이 구독한다.
// 사용법: GameManager와 동일한 GameObject에 추가(자동). 직접 접근은
//         GameManager.Instance.MessageBus 또는 MessageBus.Instance 사용.
// 의존성: IMessageBus (AIVillage.Core)
// GDD   : Week 5 MessageBus
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace AIVillage.Core
{
    /// <summary>
    /// string 채널 기반 이벤트 버스. Subscribe → Publish → handler 호출 흐름.
    /// Publish 중 Unsubscribe가 발생해도 안전하도록 스냅샷 방식으로 순회한다.
    /// </summary>
    public sealed class MessageBus : MonoBehaviour, IMessageBus
    {
        #region Singleton

        public static MessageBus Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        #endregion

        #region Private State

        private readonly Dictionary<string, List<Action<object>>> _handlers
            = new Dictionary<string, List<Action<object>>>();

        #endregion

        #region IMessageBus

        /// <summary>채널을 구독한다. 동일 handler를 중복 등록하면 중복 호출된다.</summary>
        public void Subscribe(string channel, Action<object> handler)
        {
            if (handler == null) return;

            if (!_handlers.TryGetValue(channel, out List<Action<object>> list))
            {
                list = new List<Action<object>>();
                _handlers[channel] = list;
            }

            list.Add(handler);
        }

        /// <summary>채널 구독을 해제한다. 등록되지 않은 handler는 무시한다.</summary>
        public void Unsubscribe(string channel, Action<object> handler)
        {
            if (handler == null) return;

            if (_handlers.TryGetValue(channel, out List<Action<object>> list))
                list.Remove(handler);
        }

        /// <summary>
        /// 채널로 메시지를 발행한다.
        /// 구독자 목록의 스냅샷을 순회하므로 handler 내부에서 Unsubscribe해도 안전하다.
        /// </summary>
        public void Publish(string channel, object payload)
        {
            if (!_handlers.TryGetValue(channel, out List<Action<object>> list) || list.Count == 0)
                return;

            Action<object>[] snapshot = list.ToArray();
            foreach (Action<object> handler in snapshot)
                handler?.Invoke(payload);
        }

        #endregion
    }
}

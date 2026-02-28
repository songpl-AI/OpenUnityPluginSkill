using System;
using Newtonsoft.Json;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 向所有已连接的 WebSocket 客户端广播 Unity 编辑器事件。
    /// 可从任意线程安全调用，内部通过 IWebSocketServer.Broadcast() 发送。
    /// </summary>
    public static class EventBroadcaster
    {
        private static IWebSocketServer _wsServer;

        public static void Initialize(IWebSocketServer wsServer)
        {
            _wsServer = wsServer;
        }

        public static void Shutdown()
        {
            _wsServer = null;
        }

        /// <summary>
        /// 广播事件。eventName 对应技术分析 §2.4 中定义的事件类型。
        /// </summary>
        public static void Broadcast(string eventName, object data = null)
        {
            if (_wsServer == null) return;
            try
            {
                var message = JsonConvert.SerializeObject(new
                {
                    @event    = eventName,
                    timestamp = DateTime.UtcNow.ToString("o"),
                    data      = data ?? new object()
                });
                _wsServer.Broadcast(message);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OpenClaw] EventBroadcaster failed to send '{eventName}': {ex.Message}");
            }
        }
    }
}

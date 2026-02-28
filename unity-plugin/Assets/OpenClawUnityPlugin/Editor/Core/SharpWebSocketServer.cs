#if !UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
// websocket-sharp 通过 UPM 或手动引入：https://github.com/sta/websocket-sharp
// 引入后此文件自动启用（Unity 2021.3 下 UNITY_2022_3_OR_NEWER 未定义）
#if WEBSOCKET_SHARP
using WebSocketSharp.Server;
using WebSocketSharp;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// Unity 2021.3 使用 websocket-sharp 实现 WebSocket 服务端。
    /// 需要在项目中引入 websocket-sharp 包并定义 WEBSOCKET_SHARP 脚本符号。
    /// </summary>
    public class SharpWebSocketServer : IWebSocketServer
    {
        private WebSocketSharp.Server.WebSocketServer _server;
        private readonly List<IWebSocketSession> _sessions = new List<IWebSocketSession>();

        public void Start(int httpPort)
        {
            int wsPort = httpPort + 1;
            _server = new WebSocketSharp.Server.WebSocketServer($"ws://127.0.0.1:{wsPort}");
            _server.AddWebSocketService<OpenClawBehavior>("/ws", behavior =>
            {
                behavior.OnOpenCallback  = s => { lock (_sessions) _sessions.Add(s); };
                behavior.OnCloseCallback = s => { lock (_sessions) _sessions.Remove(s); };
            });
            _server.Start();
            Debug.Log($"[OpenClaw] WebSocket server (websocket-sharp) started on port {wsPort}");
        }

        public void Broadcast(string json)
        {
            _server?.WebSocketServices["/ws"]?.Sessions?.Broadcast(json);
        }

        public void Stop()
        {
            _server?.Stop();
        }
    }

    internal class OpenClawBehavior : WebSocketBehavior
    {
        internal Action<IWebSocketSession> OnOpenCallback;
        internal Action<IWebSocketSession> OnCloseCallback;

        protected override void OnOpen()  => OnOpenCallback?.Invoke(Sessions.ActiveIDs != null ? null : null);
        protected override void OnClose(CloseEventArgs e) => OnCloseCallback?.Invoke(null);
    }
}
#else
namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// websocket-sharp 未引入时的空实现（功能降级：无 WebSocket 推送，仍可 HTTP 轮询）。
    /// 引入 websocket-sharp 并定义 WEBSOCKET_SHARP 脚本符号后此类不再使用。
    /// </summary>
    public class SharpWebSocketServer : IWebSocketServer
    {
        public void Start(int httpPort)
            => UnityEngine.Debug.LogWarning("[OpenClaw] WebSocket unavailable on Unity 2021.3: websocket-sharp not found. " +
                                            "Add websocket-sharp and define WEBSOCKET_SHARP scripting symbol to enable.");
        public void Broadcast(string json) { }
        public void Stop() { }
    }
}
#endif
#endif

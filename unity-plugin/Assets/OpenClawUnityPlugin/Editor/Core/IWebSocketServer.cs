using System.Net;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// WebSocket 服务端统一接口。
    /// Unity 2022.3+ 使用内置实现，2021.3 使用 websocket-sharp。
    /// 两套实现对上层 EventBroadcaster 完全透明。
    /// </summary>
    public interface IWebSocketServer
    {
        /// <summary>在已启动的 HttpListener 上开始接受 WebSocket 升级请求</summary>
        void Start(int port);

        /// <summary>向所有已连接的客户端广播 JSON 字符串</summary>
        void Broadcast(string json);

        /// <summary>断开所有客户端并停止监听</summary>
        void Stop();
    }
}

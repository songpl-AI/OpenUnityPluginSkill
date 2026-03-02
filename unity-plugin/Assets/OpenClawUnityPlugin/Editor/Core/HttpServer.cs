using System;
using System.Net;
using System.Threading;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 封装 HttpListener，在后台线程持续接收 HTTP 请求，
    /// 将 WebSocket 升级请求转交给 IWebSocketServer，其余请求交给 RequestRouter。
    /// </summary>
    public class HttpServer : IDisposable
    {
        private HttpListener      _listener;
        private Thread            _listenerThread;
        private readonly RequestRouter     _router;
        private readonly IWebSocketServer  _wsServer;
        private volatile bool     _running;

        public int Port { get; private set; }

        public HttpServer(RequestRouter router, IWebSocketServer wsServer)
        {
            _router   = router;
            _wsServer = wsServer;
        }

        public bool Start(int preferredPort = 23456)
        {
            Port = FindAvailablePort(preferredPort);
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
            _listener.Prefixes.Add($"http://localhost:{Port}/"); // 同时接受 localhost（Host 头校验）

            try
            {
                _listener.Start();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OpenClaw] HttpListener failed to start on port {Port}: {ex.Message}");
                return false;
            }

            _wsServer.Start(Port);

            _running = true;
            _listenerThread = new Thread(ListenLoop) { IsBackground = true, Name = "OpenClaw-HTTP" };
            _listenerThread.Start();

            Debug.Log($"[OpenClaw] Server started on port {Port}");
            return true;
        }

        public void Stop()
        {
            _running = false;
            try
            {
                _listener?.Stop();
                _listener?.Close();
            }
            catch { /* 忽略关闭异常 */ }
            _wsServer?.Stop();
            Debug.Log("[OpenClaw] Server stopped");
        }

        public void Dispose() => Stop();

        private void ListenLoop()
        {
            while (_running)
            {
                try
                {
                    var ctx = _listener.GetContext();

                    // WebSocket 升级请求交给 WS Server 处理
                    if (ctx.Request.IsWebSocketRequest)
                    {
                        // BuiltinWebSocketServer 通过 AcceptWebSocketAsync 处理
                        // SharpWebSocketServer 通过 websocket-sharp 处理
                        // 两者均在自己的 Start() 中注册监听，此处直接忽略（不重复处理）
                        // HTTP 层面关闭该连接，WS 层面由各自实现接管
                        continue;
                    }

                    // 在新线程处理每个 HTTP 请求，避免阻塞接收循环
                    ThreadPool.QueueUserWorkItem(_ => _router.Route(ctx));
                }
                catch (HttpListenerException) when (!_running)
                {
                    // 正常关闭时的预期异常
                    break;
                }
                catch (Exception ex)
                {
                    if (_running)
                        Debug.LogWarning($"[OpenClaw] HttpListener error: {ex.Message}");
                }
            }
        }

        private static int FindAvailablePort(int start)
        {
            for (int port = start; port < start + 10; port++)
            {
                try
                {
                    var test = new HttpListener();
                    test.Prefixes.Add($"http://127.0.0.1:{port}/");
                    test.Start();
                    test.Stop();
                    test.Close();
                    return port;
                }
                catch { /* 端口被占用，继续尝试下一个 */ }
            }
            throw new InvalidOperationException($"[OpenClaw] No available port found in range {start}~{start + 9}");
        }
    }
}

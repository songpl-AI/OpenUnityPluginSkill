#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// Unity 2022.3+ WebSocket 服务端实现。
    /// 使用 TcpListener + 手动 WebSocket 握手，避免依赖 Mono HttpListener 的
    /// WebSocket 升级支持（Mono 中 AcceptWebSocketAsync 不可靠）。
    /// </summary>
    public class BuiltinWebSocketServer : IWebSocketServer
    {
        private TcpListener  _listener;
        private Thread       _acceptThread;
        private volatile bool _running;

        private readonly ConcurrentDictionary<string, NetworkStream> _clients
            = new ConcurrentDictionary<string, NetworkStream>();

        public void Start(int httpPort)
        {
            int wsPort = httpPort + 1;
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, wsPort);
                _listener.Start();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[OpenClaw] WebSocket listener failed on port {wsPort}: {ex.Message}");
                return;
            }

            _running      = true;
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "OpenClaw-WS-Accept" };
            _acceptThread.Start();
            Debug.Log($"[OpenClaw] WebSocket server started on port {wsPort}");
        }

        public void Broadcast(string json)
        {
            var frame = BuildTextFrame(json);
            foreach (var kv in _clients)
            {
                try   { kv.Value.Write(frame, 0, frame.Length); }
                catch { _clients.TryRemove(kv.Key, out _); }
            }
        }

        public void Stop()
        {
            _running = false;
            foreach (var kv in _clients)
                try { kv.Value.Close(); } catch { /* ignore */ }
            _clients.Clear();
            try { _listener?.Stop(); } catch { /* ignore */ }
        }

        // ── Accept loop ──────────────────────────────────────────────────────────

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    var client = _listener.AcceptTcpClient();
                    Task.Run(() => HandleClient(client));
                }
                catch (SocketException) when (!_running) { break; }
                catch (Exception ex)
                {
                    if (_running)
                        Debug.LogWarning($"[OpenClaw] WS accept error: {ex.Message}");
                }
            }
        }

        private void HandleClient(TcpClient tcp)
        {
            var id = Guid.NewGuid().ToString("N");
            NetworkStream stream = null;
            try
            {
                stream = tcp.GetStream();

                // 1. 读取 HTTP 请求头（字节逐读，避免 StreamReader 预读问题）
                var headers = ReadHttpHeaders(stream);
                if (headers == null) return;

                // 2. 提取 Sec-WebSocket-Key
                string key = null;
                foreach (var h in headers)
                {
                    if (h.StartsWith("Sec-WebSocket-Key:", StringComparison.OrdinalIgnoreCase))
                    {
                        key = h.Substring("Sec-WebSocket-Key:".Length).Trim();
                        break;
                    }
                }
                if (key == null)
                {
                    SendRaw(stream, "HTTP/1.1 400 Bad Request\r\nContent-Length: 0\r\n\r\n");
                    return;
                }

                // 3. 发送 101 Switching Protocols
                var accept = ComputeAcceptKey(key);
                SendRaw(stream,
                    "HTTP/1.1 101 Switching Protocols\r\n" +
                    "Upgrade: websocket\r\n" +
                    "Connection: Upgrade\r\n" +
                    $"Sec-WebSocket-Accept: {accept}\r\n\r\n");

                _clients[id] = stream;
                Debug.Log($"[OpenClaw] WebSocket client connected: {id}");

                // 4. 读帧循环（只处理关闭帧，其余忽略——服务端只需推送）
                ReceiveLoop(stream, id);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[OpenClaw] WS client {id} error: {ex.Message}");
            }
            finally
            {
                _clients.TryRemove(id, out _);
                try { stream?.Close(); tcp.Close(); } catch { /* ignore */ }
                Debug.Log($"[OpenClaw] WebSocket client disconnected: {id}");
            }
        }

        private static void ReceiveLoop(NetworkStream stream, string id)
        {
            var buf = new byte[256];
            while (stream.CanRead)
            {
                // 等待数据（非阻塞轮询，避免 Mono 上 DataAvailable 的已知问题）
                Thread.Sleep(50);
                if (!stream.DataAvailable) continue;

                int b0 = stream.ReadByte();
                int b1 = stream.ReadByte();
                if (b0 == -1 || b1 == -1) break;

                int opcode  = b0 & 0x0F;
                bool masked = (b1 & 0x80) != 0;
                int length  = b1 & 0x7F;

                // 读扩展长度（忽略超大帧）
                if (length == 126) { stream.Read(buf, 0, 2); length = (buf[0] << 8) | buf[1]; }
                else if (length == 127) { stream.Read(buf, 0, 8); length = 0; }

                // 读掩码 + 负载（丢弃，我们不需要客户端数据）
                int maskLen = masked ? 4 : 0;
                int skip = maskLen + length;
                while (skip > 0)
                {
                    int n = stream.Read(buf, 0, Math.Min(skip, buf.Length));
                    if (n <= 0) break;
                    skip -= n;
                }

                if (opcode == 8) break; // Close 帧
            }
        }

        // ── 工具方法 ─────────────────────────────────────────────────────────────

        /// <summary>读取 HTTP 请求头行，返回 header 列表（不含请求行），遇到空行停止。</summary>
        private static List<string> ReadHttpHeaders(NetworkStream stream)
        {
            // 逐字节读取，直到出现 \r\n\r\n（头结束标志）
            var raw = new List<byte>(512);
            int prev3 = 0, prev2 = 0, prev1 = 0;
            while (true)
            {
                int b = stream.ReadByte();
                if (b == -1) return null;
                raw.Add((byte)b);

                if (prev3 == '\r' && prev2 == '\n' && prev1 == '\r' && b == '\n') break;
                prev3 = prev2; prev2 = prev1; prev1 = b;
            }

            var text  = Encoding.UTF8.GetString(raw.ToArray());
            var lines = text.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
            // lines[0] 是请求行（GET /ws HTTP/1.1），从 index 1 开始是 header
            var headers = new List<string>();
            for (int i = 1; i < lines.Length; i++) headers.Add(lines[i]);
            return headers;
        }

        private static string ComputeAcceptKey(string key)
        {
            const string magic = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";
            using var sha1 = SHA1.Create();
            return Convert.ToBase64String(
                sha1.ComputeHash(Encoding.UTF8.GetBytes(key + magic)));
        }

        private static void SendRaw(NetworkStream stream, string text)
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>将文本消息编码为 WebSocket 文本帧（RFC 6455）。</summary>
        private static byte[] BuildTextFrame(string message)
        {
            var payload = Encoding.UTF8.GetBytes(message);
            int len     = payload.Length;
            byte[] frame;

            if (len < 126)
            {
                frame    = new byte[2 + len];
                frame[1] = (byte)len;
                Buffer.BlockCopy(payload, 0, frame, 2, len);
            }
            else if (len < 65536)
            {
                frame    = new byte[4 + len];
                frame[1] = 126;
                frame[2] = (byte)(len >> 8);
                frame[3] = (byte)(len & 0xFF);
                Buffer.BlockCopy(payload, 0, frame, 4, len);
            }
            else
            {
                frame    = new byte[10 + len];
                frame[1] = 127;
                for (int i = 0; i < 8; i++)
                    frame[2 + i] = (byte)((long)len >> (56 - 8 * i));
                Buffer.BlockCopy(payload, 0, frame, 10, len);
            }

            frame[0] = 0x81; // FIN=1, opcode=1 (text)
            return frame;
        }
    }
}
#endif

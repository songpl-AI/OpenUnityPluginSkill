using System;
using System.Collections.Generic;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 捕获 Unity Console 日志，存入定容环形缓冲区，并通过 WebSocket 实时推送。
    /// 使用 logMessageReceivedThreaded（线程安全版本），可从任意线程触发。
    /// </summary>
    public static class ConsoleLogger
    {
        private const int BufferCapacity = 500;

        private static readonly Queue<ConsoleLogDto> _buffer = new Queue<ConsoleLogDto>(BufferCapacity);
        private static readonly object _lock = new object();

        public static void Initialize()
        {
            Application.logMessageReceivedThreaded += OnLogMessage;
        }

        public static void Shutdown()
        {
            Application.logMessageReceivedThreaded -= OnLogMessage;
        }

        /// <summary>返回最近 N 条日志（默认全部），可按类型过滤</summary>
        public static List<ConsoleLogDto> GetLogs(string typeFilter = null, int limit = 0)
        {
            lock (_lock)
            {
                var result = new List<ConsoleLogDto>(_buffer);
                if (!string.IsNullOrEmpty(typeFilter))
                    result = result.FindAll(l => l.Type == typeFilter);
                if (limit > 0 && result.Count > limit)
                    result = result.GetRange(result.Count - limit, limit);
                return result;
            }
        }

        public static void Clear()
        {
            lock (_lock) { _buffer.Clear(); }
        }

        private static void OnLogMessage(string message, string stackTrace, LogType type)
        {
            var dto = new ConsoleLogDto
            {
                Type       = MapLogType(type),
                Message    = message,
                StackTrace = stackTrace,
                Timestamp  = DateTime.UtcNow.ToString("o")
            };

            lock (_lock)
            {
                if (_buffer.Count >= BufferCapacity)
                    _buffer.Dequeue();
                _buffer.Enqueue(dto);
            }

            // 实时推送到 WebSocket（仅 Warning 和 Error，Log 太频繁不推送）
            if (type == LogType.Warning || type == LogType.Error || type == LogType.Exception)
                EventBroadcaster.Broadcast("console_log", dto);
        }

        private static string MapLogType(LogType type) => type switch
        {
            LogType.Warning   => "warning",
            LogType.Error     => "error",
            LogType.Exception => "error",
            _                 => "log"
        };
    }
}

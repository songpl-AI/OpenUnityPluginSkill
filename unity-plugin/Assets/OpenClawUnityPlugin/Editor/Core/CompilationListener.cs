using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Compilation;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 监听 Unity 编译事件，将结果缓存并通过 EventBroadcaster 推送到 WebSocket 客户端。
    /// 使用 assemblyCompilationFinished（携带 CompilerMessage[]）收集错误，
    /// compilationFinished 做汇总广播。
    /// </summary>
    [InitializeOnLoad]
    public static class CompilationListener
    {
        private static readonly List<CompileErrorDto> _currentErrors = new List<CompileErrorDto>();
        private static CompileStatus _status = CompileStatus.Idle;

        public static CompileStatus Status => _status;
        public static IReadOnlyList<CompileErrorDto> LastErrors => _currentErrors;

        static CompilationListener()
        {
            CompilationPipeline.compilationStarted        += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompiled;
            CompilationPipeline.compilationFinished       += OnAllCompilationFinished;
        }

        public static void Shutdown()
        {
            CompilationPipeline.compilationStarted        -= OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompiled;
            CompilationPipeline.compilationFinished       -= OnAllCompilationFinished;
        }

        private static void OnCompilationStarted(object context)
        {
            _currentErrors.Clear();
            _status = CompileStatus.Compiling;
            EventBroadcaster.Broadcast("compile_started");
        }

        private static void OnAssemblyCompiled(string assemblyPath, CompilerMessage[] messages)
        {
            foreach (var msg in messages)
            {
                _currentErrors.Add(new CompileErrorDto
                {
                    File    = msg.file,
                    Line    = msg.line,
                    Column  = msg.column,
                    Message = msg.message,
                    Type    = msg.type == CompilerMessageType.Error ? "error" : "warning"
                });
            }
        }

        private static void OnAllCompilationFinished(object context)
        {
            var errors   = _currentErrors.Where(e => e.Type == "error").ToArray();
            var warnings = _currentErrors.Where(e => e.Type == "warning").ToArray();

            if (errors.Length > 0)
            {
                _status = CompileStatus.Failed;
                EventBroadcaster.Broadcast("compile_failed", new { errors, warnings });
            }
            else
            {
                _status = CompileStatus.Success;
                EventBroadcaster.Broadcast("compile_complete", new { warnings });
            }
        }
    }

    public enum CompileStatus { Idle, Compiling, Success, Failed }
}

using UnityEditor;
using UnityEditor.Compilation;

namespace OpenClaw.UnityPlugin
{
    public class EditorHandler
    {
        public void HandlePlay(HttpContext ctx)
        {
            MainThreadDispatcher.Dispatch(() => { EditorApplication.isPlaying = true; return true; });
            ResponseHelper.WriteSuccess(ctx.Response, new { isPlaying = true });
        }

        public void HandleStop(HttpContext ctx)
        {
            MainThreadDispatcher.Dispatch(() => { EditorApplication.isPlaying = false; return true; });
            ResponseHelper.WriteSuccess(ctx.Response, new { isPlaying = false });
        }

        public void HandlePause(HttpContext ctx)
        {
            MainThreadDispatcher.Dispatch(() =>
            {
                EditorApplication.isPaused = !EditorApplication.isPaused;
                return EditorApplication.isPaused;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { isPaused = EditorApplication.isPaused });
        }

        public void HandleUndo(HttpContext ctx)
        {
            MainThreadDispatcher.Dispatch(() => { Undo.PerformUndo(); return true; });
            ResponseHelper.WriteSuccess(ctx.Response, new { undone = true });
        }

        public void HandleCompile(HttpContext ctx)
        {
            // RequestScriptCompilation 强制重编所有脚本，结果通过 WebSocket 事件推送
            MainThreadDispatcher.Dispatch(() => { CompilationPipeline.RequestScriptCompilation(); return true; });
            ResponseHelper.WriteSuccess(ctx.Response, new
            {
                triggered = true,
                message   = "Compilation triggered. Listen to WebSocket events 'compile_complete' or 'compile_failed' for results."
            });
        }
    }
}

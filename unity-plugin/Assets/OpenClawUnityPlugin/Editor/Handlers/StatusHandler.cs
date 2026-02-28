using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace OpenClaw.UnityPlugin
{
    public class StatusHandler
    {
        public void HandleGet(HttpContext ctx)
        {
            var data = MainThreadDispatcher.Dispatch(() => new
            {
                status       = "ready",
                unityVersion = Application.unityVersion,
                productName  = Application.productName,
                isPlaying    = EditorApplication.isPlaying,
                isCompiling  = EditorApplication.isCompiling,
                compileStatus = CompilationListener.Status.ToString().ToLower(),
                currentScene = EditorSceneManager.GetActiveScene().name,
                httpPort     = UnityEditorServer.HttpPort,
                wsPort       = UnityEditorServer.HttpPort + 1
            });
            ResponseHelper.WriteSuccess(ctx.Response, data);
        }
    }
}

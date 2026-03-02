using UnityEditor;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 处理项目设置相关的 API 请求
    /// </summary>
    public class SettingsHandler
    {
        /// <summary>获取当前激活的输入系统类型</summary>
        public void HandleGetInputSystem(HttpContext ctx)
        {
            string inputSystem;

            #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            inputSystem = "new";
            #elif !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            inputSystem = "legacy";
            #elif ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            inputSystem = "both";
            #else
            inputSystem = "unknown";
            #endif

            ResponseHelper.WriteSuccess(ctx.Response, new { inputSystem });
        }

        /// <summary>获取 Player Settings 关键配置</summary>
        public void HandleGetPlayerSettings(HttpContext ctx)
        {
            var data = MainThreadDispatcher.Dispatch(() => new
            {
                companyName = PlayerSettings.companyName,
                productName = PlayerSettings.productName,
                version = PlayerSettings.bundleVersion,
                inputSystem = GetInputSystemType(),
                scriptingBackend = PlayerSettings.GetScriptingBackend(EditorUserBuildSettings.selectedBuildTargetGroup).ToString(),
                apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(EditorUserBuildSettings.selectedBuildTargetGroup).ToString()
            });

            ResponseHelper.WriteSuccess(ctx.Response, data);
        }

        private string GetInputSystemType()
        {
            #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            return "new";
            #elif !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            return "legacy";
            #elif ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            return "both";
            #else
            return "unknown";
            #endif
        }
    }
}

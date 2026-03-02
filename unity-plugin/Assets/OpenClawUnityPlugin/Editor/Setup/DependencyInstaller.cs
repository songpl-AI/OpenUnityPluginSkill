using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace OpenClaw.UnityPlugin.Setup
{
    /// <summary>
    /// Automatically installs Newtonsoft.Json (com.unity.nuget.newtonsoft-json) on first import.
    ///
    /// This script lives in a separate Assembly Definition (OpenClawUnityPlugin.Setup) with NO
    /// reference to Newtonsoft.Json, so it compiles and runs even when the main plugin assembly
    /// fails to compile because Newtonsoft.Json is missing.
    /// </summary>
    [InitializeOnLoad]
    internal static class DependencyInstaller
    {
        private const string NewtonsoftPackageId = "com.unity.nuget.newtonsoft-json";

        private static AddRequest s_addRequest;

        static DependencyInstaller()
        {
            // delayCall: 等 PackageManager 数据库在本次域重载中完全加载后再检查
            EditorApplication.delayCall += CheckDependencies;
        }

        private static void CheckDependencies()
        {
            // FindForPackageName: 包已安装则返回 PackageInfo，否则返回 null
            var info = UnityEditor.PackageManager.PackageInfo.FindForPackageName(NewtonsoftPackageId);
            if (info != null) return; // 已安装，无需操作

            Debug.Log($"[OpenClaw Unity Plugin] Required package '{NewtonsoftPackageId}' not found. Installing automatically...");
            s_addRequest = Client.Add(NewtonsoftPackageId);
            EditorApplication.update += WaitForInstall;
        }

        private static void WaitForInstall()
        {
            if (!s_addRequest.IsCompleted) return;
            EditorApplication.update -= WaitForInstall;

            if (s_addRequest.Status == StatusCode.Success)
            {
                Debug.Log($"[OpenClaw Unity Plugin] ✅ '{NewtonsoftPackageId}' installed successfully. Unity will recompile now.");
            }
            else
            {
                Debug.LogError(
                    $"[OpenClaw Unity Plugin] ❌ Auto-install failed: {s_addRequest.Error?.message}\n" +
                    $"Please install it manually:\n" +
                    $"  Window → Package Manager → '+' → Add package by name → {NewtonsoftPackageId}"
                );
            }
        }
    }
}

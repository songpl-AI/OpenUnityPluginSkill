using System;
using System.Linq;
using System.Threading;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 处理 Unity Package Manager 相关请求：列出、安装、卸载包。
    /// Client.Add/Remove/List 必须从主线程调用，通过 MainThreadDispatcher 保证。
    /// 请求结果由 Package Manager 服务异步填写，后台 HTTP 线程轮询 IsCompleted。
    /// </summary>
    public class PackageHandler
    {
        /// <summary>列出已安装的包</summary>
        public void HandleList(HttpContext ctx)
        {
            var includeBuiltIn = ctx.Query("includeBuiltIn", "false") == "true";

            var request = MainThreadDispatcher.Dispatch(() => Client.List());

            if (!Poll(request, 30, out var timeoutMsg))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.ExecutionFailed, timeoutMsg);
                return;
            }

            if (request.Status == StatusCode.Failure)
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.ExecutionFailed,
                    request.Error?.message ?? "Failed to list packages");
                return;
            }

            var packages = request.Result
                .Where(p => includeBuiltIn || p.source != PackageSource.BuiltIn)
                .Select(p => new
                {
                    packageId   = p.packageId,
                    displayName = p.displayName,
                    version     = p.version,
                    source      = p.source.ToString()
                })
                .OrderBy(p => p.displayName)
                .ToArray();

            ResponseHelper.WriteSuccess(ctx.Response, new { count = packages.Length, packages });
        }

        /// <summary>安装包。完成后 Unity 会触发一次 Domain Reload</summary>
        public void HandleAdd(HttpContext ctx)
        {
            var req = ctx.ParseBody<PackageIdRequest>();

            if (string.IsNullOrWhiteSpace(req.PackageId))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams,
                    "'packageId' is required. Examples: 'com.unity.textmeshpro' or 'com.unity.textmeshpro@3.0.6'");
                return;
            }

            Debug.Log($"[OpenClaw Package] Installing: {req.PackageId}");

            var request = MainThreadDispatcher.Dispatch(() => Client.Add(req.PackageId));

            if (!Poll(request, 120, out var timeoutMsg))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.ExecutionFailed, timeoutMsg);
                return;
            }

            if (request.Status == StatusCode.Failure)
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.ExecutionFailed,
                    request.Error?.message ?? "Install failed");
                return;
            }

            var pkg = request.Result;
            Debug.Log($"[OpenClaw Package] Installed: {pkg.packageId} v{pkg.version}");

            ResponseHelper.WriteSuccess(ctx.Response, new
            {
                packageId   = pkg.packageId,
                displayName = pkg.displayName,
                version     = pkg.version,
                installed   = true,
                note        = "Package installed. Unity will trigger a Domain Reload to load the new assembly."
            });
        }

        /// <summary>卸载包。完成后 Unity 会触发一次 Domain Reload</summary>
        public void HandleRemove(HttpContext ctx)
        {
            var req = ctx.ParseBody<PackageIdRequest>();

            if (string.IsNullOrWhiteSpace(req.PackageId))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'packageId' is required");
                return;
            }

            Debug.Log($"[OpenClaw Package] Removing: {req.PackageId}");

            var request = MainThreadDispatcher.Dispatch(() => Client.Remove(req.PackageId));

            if (!Poll(request, 60, out var timeoutMsg))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.ExecutionFailed, timeoutMsg);
                return;
            }

            if (request.Status == StatusCode.Failure)
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.ExecutionFailed,
                    request.Error?.message ?? "Remove failed");
                return;
            }

            Debug.Log($"[OpenClaw Package] Removed: {req.PackageId}");

            ResponseHelper.WriteSuccess(ctx.Response, new
            {
                packageId = req.PackageId,
                removed   = true,
                note      = "Package removed. Unity will trigger a Domain Reload."
            });
        }

        // 从后台线程轮询 PackageManager 请求，直到完成或超时
        private static bool Poll(Request request, int timeoutSeconds, out string errorMsg)
        {
            var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
            while (!request.IsCompleted)
            {
                if (DateTime.UtcNow >= deadline)
                {
                    errorMsg = $"Operation timed out after {timeoutSeconds}s. Check Unity Editor console.";
                    return false;
                }
                Thread.Sleep(500);
            }
            errorMsg = null;
            return true;
        }
    }

    public class PackageIdRequest
    {
        public string PackageId { get; set; }
    }
}

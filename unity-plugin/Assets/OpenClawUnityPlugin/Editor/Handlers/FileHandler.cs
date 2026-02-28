using System;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 文件读写 Handler。路径安全限制：只允许访问 Assets/ 目录下的文件。
    /// 写入后自动触发 AssetDatabase.Refresh。
    /// </summary>
    public class FileHandler
    {
        private static readonly string ProjectRoot = Path.GetFullPath(Application.dataPath + "/..");

        public void HandleRead(HttpContext ctx)
        {
            var path = ctx.Query("path");
            if (string.IsNullOrEmpty(path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "Query param 'path' is required");
                return;
            }

            if (!IsPathSafe(path, out var fullPath))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.FileOutsideProject,
                    $"Path '{path}' is outside the project Assets directory", 400);
                return;
            }

            if (!File.Exists(fullPath))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.FileNotFound,
                    $"File not found: {path}", 404);
                return;
            }

            try
            {
                var content = File.ReadAllText(fullPath);
                ResponseHelper.WriteSuccess(ctx.Response, new { path, content });
            }
            catch (Exception ex)
            {
                ResponseHelper.WriteServerError(ctx.Response, ex);
            }
        }

        public void HandleWrite(HttpContext ctx)
        {
            var req = ctx.ParseBody<WriteFileRequest>();
            if (string.IsNullOrEmpty(req.Path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' is required");
                return;
            }
            if (req.Content == null)
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'content' is required");
                return;
            }

            if (!IsPathSafe(req.Path, out var fullPath))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.FileOutsideProject,
                    $"Path '{req.Path}' is outside the project Assets directory", 400);
                return;
            }

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, req.Content);

                // AssetDatabase.Refresh 必须在主线程执行
                MainThreadDispatcher.Dispatch(() =>
                {
                    AssetDatabase.Refresh();
                    return true;
                });

                ResponseHelper.WriteSuccess(ctx.Response, new { path = req.Path, written = req.Content.Length });
            }
            catch (Exception ex)
            {
                ResponseHelper.WriteServerError(ctx.Response, ex);
            }
        }

        private bool IsPathSafe(string relativePath, out string fullPath)
        {
            // 规范化路径，防止 ../ 路径穿越
            var combined  = Path.GetFullPath(Path.Combine(Application.dataPath, "..", relativePath));
            var assetsDir = Path.GetFullPath(Application.dataPath);

            fullPath = combined;
            return combined.StartsWith(assetsDir, StringComparison.OrdinalIgnoreCase);
        }

        private class WriteFileRequest
        {
            [JsonProperty("path")]    public string Path    { get; set; }
            [JsonProperty("content")] public string Content { get; set; }
        }
    }
}

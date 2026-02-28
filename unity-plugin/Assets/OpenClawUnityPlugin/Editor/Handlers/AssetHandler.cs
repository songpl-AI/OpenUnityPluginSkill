using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    public class AssetHandler
    {
        public void HandleFind(HttpContext ctx)
        {
            var filter  = ctx.Query("filter", "");    // e.g. "t:AudioClip BGM"
            var typeStr = ctx.Query("type", "");      // 便捷参数：type=AudioClip → filter="t:AudioClip <filter>"
            if (!string.IsNullOrEmpty(typeStr)) filter = $"t:{typeStr} {filter}".Trim();

            var results = MainThreadDispatcher.Dispatch(() =>
            {
                var guids = AssetDatabase.FindAssets(filter);
                var list  = new List<AssetInfoDto>();
                foreach (var guid in guids)
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var type = AssetDatabase.GetMainAssetTypeAtPath(path);
                    list.Add(new AssetInfoDto
                    {
                        Path = path,
                        Guid = guid,
                        Type = type?.Name ?? "Unknown",
                        Name = Path.GetFileNameWithoutExtension(path)
                    });
                }
                return list;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { count = results.Count, assets = results });
        }

        public void HandleDetails(HttpContext ctx)
        {
            var path = ctx.Query("path");
            if (string.IsNullOrEmpty(path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' query param required");
                return;
            }
            var data = MainThreadDispatcher.Dispatch(() =>
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                if (asset == null) throw new Exception($"Asset not found: {path}");

                var result = new AssetInfoDto
                {
                    Path = path,
                    Guid = AssetDatabase.AssetPathToGUID(path),
                    Type = asset.GetType().Name,
                    Name = asset.name
                };

                // Prefab：返回组件树
                if (asset is GameObject prefabGo)
                {
                    var comps = new List<object>();
                    CollectPrefabComponents(prefabGo, comps);
                    result.Metadata = new { components = comps };
                }
                return result;
            });
            ResponseHelper.WriteSuccess(ctx.Response, data);
        }

        public void HandleCreateScript(HttpContext ctx)
        {
            var req = ctx.ParseBody<CreateScriptRequest>();
            if (string.IsNullOrEmpty(req.Path) || string.IsNullOrEmpty(req.ClassName))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' and 'className' are required");
                return;
            }

            var fullPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", req.Path));
            if (!fullPath.StartsWith(Path.GetFullPath(Application.dataPath), StringComparison.OrdinalIgnoreCase))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.FileOutsideProject, "Path must be inside Assets/");
                return;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            var baseClass = req.BaseClass ?? "MonoBehaviour";
            var template  = GenerateScript(req.ClassName, baseClass, req.Namespace);
            File.WriteAllText(fullPath, template);

            MainThreadDispatcher.Dispatch(() => { AssetDatabase.Refresh(); return true; });
            ResponseHelper.WriteSuccess(ctx.Response, new { path = req.Path, className = req.ClassName });
        }

        public void HandleCreateMaterial(HttpContext ctx)
        {
            var req = ctx.ParseBody<CreateMaterialRequest>();
            if (string.IsNullOrEmpty(req.Path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' is required");
                return;
            }
            MainThreadDispatcher.Dispatch(() =>
            {
                var shader = string.IsNullOrEmpty(req.Shader) ? Shader.Find("Standard") : Shader.Find(req.Shader);
                var mat    = new Material(shader);
                AssetDatabase.CreateAsset(mat, req.Path);
                AssetDatabase.SaveAssets();
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { path = req.Path });
        }

        public void HandleCreatePrefab(HttpContext ctx)
        {
            var req = ctx.ParseBody<CreatePrefabRequest>();
            if (string.IsNullOrEmpty(req.GoPath) || string.IsNullOrEmpty(req.PrefabPath))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'goPath' and 'prefabPath' are required");
                return;
            }
            MainThreadDispatcher.Dispatch(() =>
            {
                var go = GameObject.Find(req.GoPath);
                if (go == null) throw new Exception($"GameObject '{req.GoPath}' not found");
                PrefabUtility.SaveAsPrefabAsset(go, req.PrefabPath);
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { prefabPath = req.PrefabPath });
        }

        public void HandleRefresh(HttpContext ctx)
        {
            MainThreadDispatcher.Dispatch(() => { AssetDatabase.Refresh(); return true; });
            ResponseHelper.WriteSuccess(ctx.Response, new { refreshed = true });
        }

        public void HandleImport(HttpContext ctx)
        {
            var req = ctx.ParseBody<ImportRequest>();
            if (string.IsNullOrEmpty(req.Path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' is required");
                return;
            }
            MainThreadDispatcher.Dispatch(() =>
            {
                AssetDatabase.ImportAsset(req.Path, ImportAssetOptions.ForceUpdate);
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { imported = req.Path });
        }

        // --- Helpers ---

        private static void CollectPrefabComponents(GameObject go, List<object> result, string parentPath = "")
        {
            var path  = string.IsNullOrEmpty(parentPath) ? go.name : $"{parentPath}/{go.name}";
            var comps = new List<string>();
            foreach (var c in go.GetComponents<Component>())
                if (c != null) comps.Add(c.GetType().Name);

            result.Add(new { path, components = comps });
            foreach (Transform child in go.transform)
                CollectPrefabComponents(child.gameObject, result, path);
        }

        private static string GenerateScript(string className, string baseClass, string ns)
        {
            var usingBlock = baseClass == "MonoBehaviour" || baseClass == "ScriptableObject"
                ? "using UnityEngine;\n\n" : "";
            var nsOpen  = string.IsNullOrEmpty(ns) ? "" : $"namespace {ns}\n{{\n";
            var nsClose = string.IsNullOrEmpty(ns) ? "" : "}\n";
            var indent  = string.IsNullOrEmpty(ns) ? "" : "    ";

            return $"{usingBlock}{nsOpen}{indent}public class {className} : {baseClass}\n{indent}{{\n{indent}}}\n{nsClose}";
        }

        // --- Request DTOs ---
        private class CreateScriptRequest
        {
            [JsonProperty("path")]      public string Path      { get; set; }
            [JsonProperty("className")] public string ClassName { get; set; }
            [JsonProperty("baseClass")] public string BaseClass { get; set; }
            [JsonProperty("namespace")] public string Namespace { get; set; }
        }
        private class CreateMaterialRequest { [JsonProperty("path")] public string Path { get; set; } [JsonProperty("shader")] public string Shader { get; set; } }
        private class CreatePrefabRequest   { [JsonProperty("goPath")] public string GoPath { get; set; } [JsonProperty("prefabPath")] public string PrefabPath { get; set; } }
        private class ImportRequest         { [JsonProperty("path")] public string Path { get; set; } }
    }
}

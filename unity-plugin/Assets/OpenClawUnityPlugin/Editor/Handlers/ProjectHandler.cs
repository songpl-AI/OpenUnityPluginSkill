using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    public class ProjectHandler
    {
        public void HandleInfo(HttpContext ctx)
        {
            var data = MainThreadDispatcher.Dispatch(() =>
            {
                // 读取 Packages/manifest.json 获取依赖列表
                var manifestPath = Path.Combine(Application.dataPath, "../Packages/manifest.json");
                object packages  = null;
                if (File.Exists(manifestPath))
                {
                    var json = File.ReadAllText(manifestPath);
                    var obj  = JObject.Parse(json);
                    packages = obj["dependencies"];
                }

                return new
                {
                    productName  = Application.productName,
                    companyName  = Application.companyName,
                    unityVersion = Application.unityVersion,
                    buildTarget  = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    packages
                };
            });
            ResponseHelper.WriteSuccess(ctx.Response, data);
        }

        public void HandleScripts(HttpContext ctx)
        {
            // 编译失败时降级：只返回脚本文件列表
            if (CompilationListener.Status == CompileStatus.Failed)
            {
                var files = MainThreadDispatcher.Dispatch(() =>
                {
                    var guids = AssetDatabase.FindAssets("t:MonoScript");
                    return Array.ConvertAll(guids, g => AssetDatabase.GUIDToAssetPath(g));
                });
                ResponseHelper.WriteSuccess(ctx.Response, new
                {
                    degraded = true,
                    reason   = "Compilation failed. Showing file list only. Fix compile errors first.",
                    files
                });
                return;
            }

            // 编译成功：通过反射分析用户程序集
            var types = MainThreadDispatcher.Dispatch(() =>
            {
                var userAssemblies = AppDomain.CurrentDomain.GetAssemblies()
                    .Where(a => a.GetName().Name.StartsWith("Assembly-CSharp"))
                    .ToArray();

                var result = new List<ScriptTypeDto>();
                foreach (var asm in userAssemblies)
                {
                    foreach (var type in asm.GetTypes().Where(t => t.IsPublic))
                    {
                        var dto = new ScriptTypeDto
                        {
                            FullName        = type.FullName,
                            Name            = type.Name,
                            BaseType        = type.BaseType?.Name,
                            IsMonoBehaviour = typeof(MonoBehaviour).IsAssignableFrom(type),
                            Methods         = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                                                  .Select(m => m.ToString()).ToList(),
                            Fields          = type.GetFields(BindingFlags.Public | BindingFlags.Instance)
                                                  .Select(f => new FieldDto { Name = f.Name, Type = f.FieldType.Name }).ToList()
                        };
                        // 尝试找到对应的源文件路径
                        var monoScript = AssetDatabase.FindAssets($"t:MonoScript {type.Name}");
                        if (monoScript.Length > 0)
                            dto.FilePath = AssetDatabase.GUIDToAssetPath(monoScript[0]);

                        result.Add(dto);
                    }
                }
                return result;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { count = types.Count, types });
        }

        public void HandleSettings(HttpContext ctx)
        {
            var data = MainThreadDispatcher.Dispatch(() => new
            {
                tags    = InternalEditorUtility.tags,
                layers  = InternalEditorUtility.layers,
                physics = new
                {
                    gravity = new { x = Physics.gravity.x, y = Physics.gravity.y, z = Physics.gravity.z }
                }
            });
            ResponseHelper.WriteSuccess(ctx.Response, data);
        }
    }
}

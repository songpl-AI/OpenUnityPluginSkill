using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 处理材质和渲染管线相关的 API 请求。
    /// 涵盖：检测渲染管线、查询/设置材质属性、将材质赋给 Renderer。
    /// </summary>
    public class MaterialHandler
    {
        /// <summary>检测当前渲染管线类型，返回对应的默认 Shader 名</summary>
        public void HandleGetRenderPipeline(HttpContext ctx)
        {
            var data = MainThreadDispatcher.Dispatch(() =>
            {
                var rp = GraphicsSettings.defaultRenderPipeline;
                if (rp == null)
                    return new { pipeline = "builtin", defaultShader = "Standard", pipelineName = "Built-in Render Pipeline" };

                var typeName = rp.GetType().FullName ?? "";
                if (typeName.Contains("Universal"))
                    return new { pipeline = "urp",  defaultShader = "Universal Render Pipeline/Lit", pipelineName = rp.name };
                if (typeName.Contains("HighDefinition") || typeName.Contains("HDRender"))
                    return new { pipeline = "hdrp", defaultShader = "HDRP/Lit",                     pipelineName = rp.name };

                return new { pipeline = "custom", defaultShader = "Standard", pipelineName = rp.name };
            });

            ResponseHelper.WriteSuccess(ctx.Response, data);
        }

        /// <summary>获取材质的所有 Shader 属性及当前值</summary>
        public void HandleGetProperties(HttpContext ctx)
        {
            var path = ctx.Query("path");
            if (string.IsNullOrEmpty(path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' query param is required");
                return;
            }

            var data = MainThreadDispatcher.Dispatch(() =>
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null) throw new Exception($"Material not found: {path}");

                var shader = mat.shader;
                var count  = ShaderUtil.GetPropertyCount(shader);
                var props  = new List<object>();

                for (int i = 0; i < count; i++)
                {
                    if (ShaderUtil.IsShaderPropertyHidden(shader, i)) continue;

                    var propName = ShaderUtil.GetPropertyName(shader, i);
                    var propType = ShaderUtil.GetPropertyType(shader, i);
                    var propDesc = ShaderUtil.GetPropertyDescription(shader, i);

                    object value = null;
                    object range = null;

                    switch (propType)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            var c = mat.GetColor(propName);
                            value = new[] { c.r, c.g, c.b, c.a };
                            break;

                        case ShaderUtil.ShaderPropertyType.Float:
                            value = mat.GetFloat(propName);
                            break;

                        case ShaderUtil.ShaderPropertyType.Range:
                            value = mat.GetFloat(propName);
                            ShaderUtil.GetRangeLimits(shader, i, out float minV, out float maxV);
                            range = new { min = minV, max = maxV };
                            break;

                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            var tex = mat.GetTexture(propName);
                            value = tex != null ? AssetDatabase.GetAssetPath(tex) : null;
                            break;

                        case ShaderUtil.ShaderPropertyType.Vector:
                            var v = mat.GetVector(propName);
                            value = new[] { v.x, v.y, v.z, v.w };
                            break;
                    }

                    props.Add(new { name = propName, type = propType.ToString(), description = propDesc, value, range });
                }

                return new { shader = shader.name, propertyCount = props.Count, properties = props };
            });

            ResponseHelper.WriteSuccess(ctx.Response, data);
        }

        /// <summary>批量设置材质属性（颜色、Float、贴图路径、向量）</summary>
        public void HandleSetProperties(HttpContext ctx)
        {
            var req = ctx.ParseBody<SetMaterialPropertiesRequest>();

            if (string.IsNullOrEmpty(req.Path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' is required");
                return;
            }
            if (req.Properties == null || req.Properties.Count == 0)
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'properties' map is required");
                return;
            }

            var result = MainThreadDispatcher.Dispatch(() =>
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(req.Path);
                if (mat == null) throw new Exception($"Material not found: {req.Path}");

                var shader  = mat.shader;
                var applied = new List<string>();
                var skipped = new List<string>();

                foreach (var kvp in req.Properties)
                {
                    var propType = FindPropertyType(shader, kvp.Key);
                    if (propType == null)
                    {
                        skipped.Add($"{kvp.Key} (not found in shader '{shader.name}')");
                        continue;
                    }

                    switch (propType.Value)
                    {
                        case ShaderUtil.ShaderPropertyType.Color:
                            mat.SetColor(kvp.Key, ParseColor(kvp.Value));
                            break;

                        case ShaderUtil.ShaderPropertyType.Float:
                        case ShaderUtil.ShaderPropertyType.Range:
                            mat.SetFloat(kvp.Key, kvp.Value.Value<float>());
                            break;

                        case ShaderUtil.ShaderPropertyType.TexEnv:
                            var texPath = kvp.Value.Type == JTokenType.Null ? null : kvp.Value.Value<string>();
                            var tex     = string.IsNullOrEmpty(texPath)
                                ? null
                                : AssetDatabase.LoadAssetAtPath<Texture>(texPath);
                            if (!string.IsNullOrEmpty(texPath) && tex == null)
                                throw new Exception($"Texture not found: {texPath}");
                            mat.SetTexture(kvp.Key, tex);
                            break;

                        case ShaderUtil.ShaderPropertyType.Vector:
                            mat.SetVector(kvp.Key, ParseVector(kvp.Value));
                            break;
                    }

                    applied.Add(kvp.Key);
                }

                EditorUtility.SetDirty(mat);
                AssetDatabase.SaveAssets();

                return new { path = req.Path, applied, skipped };
            });

            ResponseHelper.WriteSuccess(ctx.Response, result);
        }

        /// <summary>将材质赋给场景中 GameObject 的 Renderer</summary>
        public void HandleAssign(HttpContext ctx)
        {
            var req = ctx.ParseBody<AssignMaterialRequest>();

            if (string.IsNullOrEmpty(req.GoPath) || string.IsNullOrEmpty(req.MaterialPath))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'goPath' and 'materialPath' are required");
                return;
            }

            MainThreadDispatcher.Dispatch(() =>
            {
                var go = GameObject.Find(req.GoPath);
                if (go == null) throw new Exception($"GameObject '{req.GoPath}' not found");

                var renderer = go.GetComponent<Renderer>();
                if (renderer == null) throw new Exception($"No Renderer on '{req.GoPath}'");

                var mat = AssetDatabase.LoadAssetAtPath<Material>(req.MaterialPath);
                if (mat == null) throw new Exception($"Material not found: {req.MaterialPath}");

                var slot = req.Slot ?? 0;
                if (slot == 0 && renderer.sharedMaterials.Length <= 1)
                {
                    renderer.sharedMaterial = mat;
                }
                else
                {
                    var mats = renderer.sharedMaterials;
                    if (slot >= mats.Length)
                        throw new Exception($"Slot {slot} out of range (renderer has {mats.Length} slots)");
                    mats[slot] = mat;
                    renderer.sharedMaterials = mats;
                }

                EditorUtility.SetDirty(go);
                return true;
            });

            ResponseHelper.WriteSuccess(ctx.Response, new
            {
                goPath       = req.GoPath,
                materialPath = req.MaterialPath,
                slot         = req.Slot ?? 0
            });
        }

        // ── Helpers ────────────────────────────────────────────────────────────

        private static ShaderUtil.ShaderPropertyType? FindPropertyType(Shader shader, string name)
        {
            var count = ShaderUtil.GetPropertyCount(shader);
            for (int i = 0; i < count; i++)
                if (ShaderUtil.GetPropertyName(shader, i) == name)
                    return ShaderUtil.GetPropertyType(shader, i);
            return null;
        }

        private static Color ParseColor(JToken token)
        {
            if (token is JArray arr && arr.Count >= 3)
                return new Color(arr[0].Value<float>(), arr[1].Value<float>(), arr[2].Value<float>(),
                    arr.Count > 3 ? arr[3].Value<float>() : 1f);
            throw new Exception($"Color must be [r,g,b] or [r,g,b,a] (0–1 range), got: {token}");
        }

        private static Vector4 ParseVector(JToken token)
        {
            if (token is JArray arr && arr.Count >= 2)
                return new Vector4(arr[0].Value<float>(), arr[1].Value<float>(),
                    arr.Count > 2 ? arr[2].Value<float>() : 0f,
                    arr.Count > 3 ? arr[3].Value<float>() : 0f);
            throw new Exception($"Vector must be [x,y,z,w] array, got: {token}");
        }
    }

    // ── DTOs ─────────────────────────────────────────────────────────────────

    public class SetMaterialPropertiesRequest
    {
        public string                   Path       { get; set; }
        public Dictionary<string, JToken> Properties { get; set; }
    }

    public class AssignMaterialRequest
    {
        public string Path         { get; set; }  // unused, kept for symmetry
        public string GoPath       { get; set; }
        public string MaterialPath { get; set; }
        public int?   Slot         { get; set; }
    }
}

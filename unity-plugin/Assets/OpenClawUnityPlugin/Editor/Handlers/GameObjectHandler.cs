using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    public class GameObjectHandler
    {
        public void HandleFind(HttpContext ctx)
        {
            var name  = ctx.Query("name");
            var tag   = ctx.Query("tag");

            var results = MainThreadDispatcher.Dispatch(() =>
            {
                var found = new List<object>();
                foreach (var go in UnityEngine.Object.FindObjectsOfType<GameObject>())
                {
                    if (!string.IsNullOrEmpty(name) && !go.name.Contains(name)) continue;
                    if (!string.IsNullOrEmpty(tag)  && go.tag != tag) continue;
                    found.Add(new { name = go.name, path = GetPath(go), active = go.activeInHierarchy, tag = go.tag });
                }
                return found;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { count = results.Count, objects = results });
        }

        public void HandleCreate(HttpContext ctx)
        {
            var req = ctx.ParseBody<CreateGoRequest>();
            if (string.IsNullOrEmpty(req.Name))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'name' is required");
                return;
            }

            var result = MainThreadDispatcher.Dispatch(() =>
            {
                GameObject go;
                go = req.Primitive.HasValue
                    ? GameObject.CreatePrimitive(req.Primitive.Value)
                    : new GameObject();
                go.name = req.Name;

                if (!string.IsNullOrEmpty(req.ParentPath))
                {
                    var parent = GameObject.Find(req.ParentPath);
                    if (parent == null)
                        throw new Exception($"Parent '{req.ParentPath}' not found");
                    go.transform.SetParent(parent.transform, false);
                }

                if (req.Position != null)
                    go.transform.position = new Vector3(req.Position.X, req.Position.Y, req.Position.Z);

                // 设置 Tag（如果提供）
                if (!string.IsNullOrEmpty(req.Tag))
                {
                    go.tag = req.Tag;
                }

                Undo.RegisterCreatedObjectUndo(go, $"Create {req.Name}");
                return new { path = GetPath(go), name = go.name, tag = go.tag };
            });
            ResponseHelper.WriteSuccess(ctx.Response, result);
        }

        public void HandleDelete(HttpContext ctx)
        {
            var req = ctx.ParseBody<PathRequest>();
            if (string.IsNullOrEmpty(req.Path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' is required");
                return;
            }

            MainThreadDispatcher.Dispatch(() =>
            {
                var go = GameObject.Find(req.Path);
                if (go == null) throw new Exception($"GameObject '{req.Path}' not found");
                Undo.DestroyObjectImmediate(go);
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { deleted = req.Path });
        }

        public void HandleTransform(HttpContext ctx)
        {
            var req = ctx.ParseBody<TransformRequest>();
            if (string.IsNullOrEmpty(req.Path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' is required");
                return;
            }

            MainThreadDispatcher.Dispatch(() =>
            {
                var go = GameObject.Find(req.Path);
                if (go == null) throw new Exception($"GameObject '{req.Path}' not found");

                Undo.RecordObject(go.transform, "Set Transform");
                if (req.Position != null) go.transform.position    = new Vector3(req.Position.X, req.Position.Y, req.Position.Z);
                if (req.Rotation != null) go.transform.eulerAngles  = new Vector3(req.Rotation.X, req.Rotation.Y, req.Rotation.Z);
                if (req.Scale    != null) go.transform.localScale   = new Vector3(req.Scale.X,    req.Scale.Y,    req.Scale.Z);
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { updated = req.Path });
        }

        public void HandleParent(HttpContext ctx)
        {
            var req = ctx.ParseBody<ParentRequest>();
            MainThreadDispatcher.Dispatch(() =>
            {
                var go = GameObject.Find(req.Path);
                if (go == null) throw new Exception($"'{req.Path}' not found");
                Undo.SetTransformParent(go.transform,
                    string.IsNullOrEmpty(req.ParentPath) ? null : GameObject.Find(req.ParentPath)?.transform,
                    "Set Parent");
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { updated = req.Path });
        }

        public void HandleGetComponents(HttpContext ctx)
        {
            var path = ctx.PathParams.GetValueOrDefault("path", "");
            var data = MainThreadDispatcher.Dispatch(() =>
            {
                var go = GameObject.Find(path);
                if (go == null) throw new Exception($"'{path}' not found");
                var comps = new List<string>();
                foreach (var c in go.GetComponents<Component>())
                    if (c != null) comps.Add(c.GetType().FullName);
                return comps;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { path, components = data });
        }

        public void HandleGetComponentValues(HttpContext ctx)
        {
            var path = ctx.PathParams.GetValueOrDefault("path", "");
            var type = ctx.PathParams.GetValueOrDefault("type", "");
            var data = MainThreadDispatcher.Dispatch(() =>
            {
                var go   = GameObject.Find(path);
                if (go == null) throw new Exception($"'{path}' not found");
                var comp = go.GetComponent(type);
                if (comp == null) throw new Exception($"Component '{type}' not found on '{path}'");

                var so   = new SerializedObject(comp);
                var iter = so.GetIterator();
                var vals = new Dictionary<string, object>();
                if (iter.NextVisible(true))
                {
                    do { vals[iter.propertyPath] = SerializedPropertyToObject(iter); }
                    while (iter.NextVisible(false));
                }
                return vals;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { path, componentType = type, values = data });
        }

        public void HandleAddComponent(HttpContext ctx)
        {
            var path = ctx.PathParams.GetValueOrDefault("path", "");
            var req  = ctx.ParseBody<ComponentTypeRequest>();
            MainThreadDispatcher.Dispatch(() =>
            {
                var go = GameObject.Find(path);
                if (go == null) throw new Exception($"'{path}' not found");
                var t = Type.GetType(req.ComponentType) ?? FindTypeInAssemblies(req.ComponentType);
                if (t == null) throw new Exception($"Type '{req.ComponentType}' not found");
                Undo.AddComponent(go, t);
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { path, added = req.ComponentType });
        }

        public void HandleRemoveComponent(HttpContext ctx)
        {
            var path = ctx.PathParams.GetValueOrDefault("path", "");
            var req  = ctx.ParseBody<ComponentTypeRequest>();
            MainThreadDispatcher.Dispatch(() =>
            {
                var go   = GameObject.Find(path);
                if (go == null) throw new Exception($"'{path}' not found");
                var comp = go.GetComponent(req.ComponentType);
                if (comp == null) throw new Exception($"Component '{req.ComponentType}' not found");
                Undo.DestroyObjectImmediate(comp);
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { path, removed = req.ComponentType });
        }

        public void HandleSetComponentValues(HttpContext ctx)
        {
            var path = ctx.PathParams.GetValueOrDefault("path", "");
            var type = ctx.PathParams.GetValueOrDefault("type", "");
            var req  = ctx.ParseBody<SetComponentValuesRequest>();

            if (req?.Values == null || req.Values.Count == 0)
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'values' map is required");
                return;
            }

            var updated = MainThreadDispatcher.Dispatch(() =>
            {
                var go = GameObject.Find(path);
                if (go == null) throw new Exception($"GameObject '{path}' not found");

                // 支持短名（如 "Paddle"）和完整名（如 "MyNamespace.Paddle"）
                Component comp = go.GetComponent(type);
                if (comp == null)
                {
                    foreach (var c in go.GetComponents<Component>())
                    {
                        if (c != null && (c.GetType().Name == type || c.GetType().FullName == type))
                        { comp = c; break; }
                    }
                }
                if (comp == null) throw new Exception($"Component '{type}' not found on '{path}'");

                var so          = new SerializedObject(comp);
                var updatedKeys = new List<string>();
                foreach (var kvp in req.Values)
                {
                    var prop = so.FindProperty(kvp.Key);
                    if (prop == null) continue;
                    ApplySerializedValue(prop, kvp.Value);
                    updatedKeys.Add(kvp.Key);
                }
                so.ApplyModifiedProperties();
                return updatedKeys;
            });

            ResponseHelper.WriteSuccess(ctx.Response, new { path, componentType = type, updated });
        }

        // --- Helpers ---

        private static string GetPath(GameObject go)
        {
            var path = go.name;
            var t    = go.transform.parent;
            while (t != null) { path = t.name + "/" + path; t = t.parent; }
            return path;
        }

        private static object SerializedPropertyToObject(SerializedProperty p) => p.propertyType switch
        {
            SerializedPropertyType.Integer   => p.intValue,
            SerializedPropertyType.Float     => p.floatValue,
            SerializedPropertyType.Boolean   => p.boolValue,
            SerializedPropertyType.String    => p.stringValue,
            SerializedPropertyType.Color     => new { r = p.colorValue.r, g = p.colorValue.g, b = p.colorValue.b, a = p.colorValue.a },
            SerializedPropertyType.Vector2   => new { x = p.vector2Value.x, y = p.vector2Value.y },
            SerializedPropertyType.Vector3   => new { x = p.vector3Value.x, y = p.vector3Value.y, z = p.vector3Value.z },
            SerializedPropertyType.Enum      => p.enumDisplayNames.Length > p.enumValueIndex ? p.enumDisplayNames[p.enumValueIndex] : p.enumValueIndex.ToString(),
            SerializedPropertyType.ObjectReference => p.objectReferenceValue != null ? p.objectReferenceValue.name : null,
            _                                => p.propertyType.ToString()
        };

        private static Type FindTypeInAssemblies(string typeName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(typeName);
                if (t != null) return t;
            }
            return null;
        }

        private static void ApplySerializedValue(SerializedProperty prop, JToken value)
        {
            if (value == null || value.Type == JTokenType.Null) return;
            switch (prop.propertyType)
            {
                case SerializedPropertyType.Integer: prop.intValue    = value.Value<int>();    break;
                case SerializedPropertyType.Float:   prop.floatValue  = value.Value<float>();  break;
                case SerializedPropertyType.Boolean: prop.boolValue   = value.Value<bool>();   break;
                case SerializedPropertyType.String:  prop.stringValue = value.Value<string>(); break;
                case SerializedPropertyType.Vector2:
                    prop.vector2Value = new Vector2(
                        value["x"]?.Value<float>() ?? 0f,
                        value["y"]?.Value<float>() ?? 0f); break;
                case SerializedPropertyType.Vector3:
                    prop.vector3Value = new Vector3(
                        value["x"]?.Value<float>() ?? 0f,
                        value["y"]?.Value<float>() ?? 0f,
                        value["z"]?.Value<float>() ?? 0f); break;
                case SerializedPropertyType.Enum:
                    if (value.Type == JTokenType.Integer) prop.enumValueIndex = value.Value<int>(); break;
            }
        }

        // --- Request DTOs ---
        private class CreateGoRequest
        {
            [JsonProperty("name")]       public string          Name       { get; set; }
            [JsonProperty("parentPath")] public string          ParentPath { get; set; }
            [JsonProperty("position")]   public Vector3Dto      Position   { get; set; }
            [JsonProperty("primitive")]  public PrimitiveType?  Primitive  { get; set; }
            [JsonProperty("tag")]        public string          Tag        { get; set; }
        }
        private class PathRequest       { [JsonProperty("path")]       public string Path       { get; set; } }
        private class ParentRequest     { [JsonProperty("path")]       public string Path       { get; set; }
                                          [JsonProperty("parentPath")] public string ParentPath { get; set; } }
        private class ComponentTypeRequest      { [JsonProperty("componentType")] public string ComponentType { get; set; } }
        private class SetComponentValuesRequest { [JsonProperty("values")] public Dictionary<string, JToken> Values { get; set; } }
        private class TransformRequest
        {
            [JsonProperty("path")]     public string     Path     { get; set; }
            [JsonProperty("position")] public Vector3Dto Position { get; set; }
            [JsonProperty("rotation")] public Vector3Dto Rotation { get; set; }
            [JsonProperty("scale")]    public Vector3Dto Scale    { get; set; }
        }
    }
}

using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OpenClaw.UnityPlugin
{
    public class SceneHandler
    {
        public void HandleInfo(HttpContext ctx)
        {
            var data = MainThreadDispatcher.Dispatch(() =>
            {
                var scene = EditorSceneManager.GetActiveScene();
                return new SceneInfoDto
                {
                    Name     = scene.name,
                    Path     = scene.path,
                    IsDirty  = scene.isDirty,
                    IsLoaded = scene.isLoaded
                };
            });
            ResponseHelper.WriteSuccess(ctx.Response, data);
        }

        public void HandleHierarchy(HttpContext ctx)
        {
            int.TryParse(ctx.Query("depth", "0"), out var maxDepth);
            int.TryParse(ctx.Query("maxNodes", "0"), out var maxNodes);

            var counter = new int[1]; // 用数组包装 ref 语义以支持 lambda 捕获
            var roots   = MainThreadDispatcher.Dispatch(() =>
            {
                var scene    = EditorSceneManager.GetActiveScene();
                if (!scene.isLoaded)
                    throw new System.InvalidOperationException("No scene is loaded");

                var result = new List<GameObjectDto>();
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (maxNodes > 0 && counter[0] >= maxNodes) break;
                    result.Add(BuildDto(root, 0, maxDepth, maxNodes, counter));
                }
                return result;
            });

            ResponseHelper.WriteSuccess(ctx.Response, new { roots });
        }

        public void HandleSave(HttpContext ctx)
        {
            MainThreadDispatcher.Dispatch(() =>
            {
                EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
                EventBroadcaster.Broadcast("scene_saved", new { scenePath = EditorSceneManager.GetActiveScene().path });
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { saved = true });
        }

        public void HandleOpen(HttpContext ctx)
        {
            var req = ctx.ParseBody<OpenSceneRequest>();
            if (string.IsNullOrEmpty(req.Path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "'path' is required");
                return;
            }
            MainThreadDispatcher.Dispatch(() =>
            {
                EditorSceneManager.OpenScene(req.Path);
                return true;
            });
            ResponseHelper.WriteSuccess(ctx.Response, new { opened = req.Path });
        }

        private GameObjectDto BuildDto(GameObject go, int depth, int maxDepth, int maxNodes, int[] counter)
        {
            counter[0]++;
            var dto = new GameObjectDto
            {
                Name   = go.name,
                Path   = GetPath(go),
                Active = go.activeInHierarchy,
                Tag    = go.tag,
                Layer  = go.layer,
                Transform = new TransformDto
                {
                    Position = ToDto(go.transform.position),
                    Rotation = ToDto(go.transform.eulerAngles),
                    Scale    = ToDto(go.transform.localScale)
                },
                Components = new List<string>(),
                Children   = new List<GameObjectDto>()
            };

            foreach (var comp in go.GetComponents<Component>())
                if (comp != null) dto.Components.Add(comp.GetType().Name);

            if (maxDepth == 0 || depth < maxDepth - 1)
            {
                foreach (Transform child in go.transform)
                {
                    if (maxNodes > 0 && counter[0] >= maxNodes) break;
                    dto.Children.Add(BuildDto(child.gameObject, depth + 1, maxDepth, maxNodes, counter));
                }
            }
            return dto;
        }

        private static string GetPath(GameObject go)
        {
            var path = go.name;
            var t    = go.transform.parent;
            while (t != null) { path = t.name + "/" + path; t = t.parent; }
            return path;
        }

        private static Vector3Dto ToDto(Vector3 v) => new Vector3Dto { X = v.x, Y = v.y, Z = v.z };

        private class OpenSceneRequest
        {
            [JsonProperty("path")] public string Path { get; set; }
        }
    }
}

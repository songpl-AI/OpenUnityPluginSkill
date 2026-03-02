using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 处理 Tag 相关的 API 请求
    /// </summary>
    public class TagHandler
    {
        /// <summary>获取项目中所有定义的 Tag</summary>
        public void HandleGetTags(HttpContext ctx)
        {
            var tags = InternalEditorUtility.tags;
            ResponseHelper.WriteSuccess(ctx.Response, new { tags });
        }

        /// <summary>创建新 Tag</summary>
        public void HandleCreateTag(HttpContext ctx)
        {
            var req = ctx.ParseBody<CreateTagRequest>();

            if (string.IsNullOrWhiteSpace(req.Name))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "Tag name is required");
                return;
            }

            MainThreadDispatcher.Dispatch(() =>
            {
                // 加载 TagManager
                var tagManager = new SerializedObject(
                    AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var tagsProp = tagManager.FindProperty("tags");

                // 检查是否已存在
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == req.Name)
                        throw new Exception($"Tag '{req.Name}' already exists");
                }

                // 添加新 Tag
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = req.Name;
                tagManager.ApplyModifiedProperties();

                return true;
            });

            ResponseHelper.WriteSuccess(ctx.Response, new { tag = req.Name, created = true });
        }

        /// <summary>设置 GameObject 的 Tag</summary>
        public void HandleSetGameObjectTag(HttpContext ctx)
        {
            var req = ctx.ParseBody<SetTagRequest>();

            if (string.IsNullOrWhiteSpace(req.Path))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "GameObject path is required");
                return;
            }

            if (string.IsNullOrWhiteSpace(req.Tag))
            {
                ResponseHelper.WriteError(ctx.Response, ErrorCode.InvalidParams, "Tag is required");
                return;
            }

            MainThreadDispatcher.Dispatch(() =>
            {
                var go = GameObject.Find(req.Path);
                if (go == null)
                    throw new Exception($"GameObject '{req.Path}' not found");

                // 验证 Tag 是否存在
                var availableTags = InternalEditorUtility.tags;
                bool tagExists = false;
                foreach (var t in availableTags)
                {
                    if (t == req.Tag)
                    {
                        tagExists = true;
                        break;
                    }
                }

                if (!tagExists)
                    throw new Exception($"Tag '{req.Tag}' does not exist. Create it first using unity_create_tag.");

                go.tag = req.Tag;
                EditorUtility.SetDirty(go);

                return true;
            });

            ResponseHelper.WriteSuccess(ctx.Response, new { path = req.Path, tag = req.Tag });
        }
    }

    public class CreateTagRequest
    {
        public string Name { get; set; }
    }

    public class SetTagRequest
    {
        public string Path { get; set; }
        public string Tag { get; set; }
    }
}

---
name: unity-editor
description: >
  Use this skill whenever the user mentions Unity, Unity Editor, scene, GameObject, prefab, component,
  MonoBehaviour, C# script, AssetDatabase, build, Play Mode, compile, Inspector, Hierarchy, or any
  Unity-related task. Also use when the user wants to write or fix game code, create assets, search
  project resources (audio, textures, animations, UI), understand existing project structure, or
  run/stop the game in the editor. Always try to use this skill proactively for any game development
  request — even if the user doesn't say "Unity" explicitly, context like "game", "player controller",
  "enemy AI", "scene setup" should trigger this skill.
metadata: '{"openclaw":{"requires":{"config":["plugins.unity-editor.enabled"]}}}'
version: 1.0.0
---

# Unity Editor Skill

All Unity operations are executed by **Claude Code via MCP**. You have no direct Unity tools — your role is to receive the user's request, delegate it to Claude Code via `exec`, wait for it to finish, and **always reply to the user with the result**.

## ⚠️ Critical Rules

1. **Use the `exec` tool** (tool name is `exec`, NOT `bash`). Do NOT answer from your own knowledge.
2. **Always include `host:gateway`** so the command runs on the host machine (not the sandbox).
3. **Always send a text reply** to the user after the exec completes — never finish silently.

## Executing Claude Code

Call the `exec` tool with these exact parameters:

```
exec host:gateway pty:true workdir:"/Users/tal/Documents/UnityProjects/TestOpenClawUnityPluginSkill" command:"claude -p '<task description>' --mcp-config ~/.claude/unity-mcp-config.json --allowedTools 'mcp__unity-editor__unity_check_status,mcp__unity-editor__unity_get_scene_info,mcp__unity-editor__unity_get_hierarchy,mcp__unity-editor__unity_save_scene,mcp__unity-editor__unity_create_gameobject,mcp__unity-editor__unity_delete_gameobject,mcp__unity-editor__unity_set_transform,mcp__unity-editor__unity_find_gameobjects,mcp__unity-editor__unity_get_components,mcp__unity-editor__unity_add_component,mcp__unity-editor__unity_set_component_property,mcp__unity-editor__unity_read_file,mcp__unity-editor__unity_write_file,mcp__unity-editor__unity_compile,mcp__unity-editor__unity_get_compile_errors,mcp__unity-editor__unity_get_console_logs,mcp__unity-editor__unity_get_project_info,mcp__unity-editor__unity_get_scripts,mcp__unity-editor__unity_find_assets' --append-system-prompt 'All file operations must stay within Assets/.' --max-turns 30"
```

Wait for the exec to complete (1–5 min), then **write a text reply** to the user.

## After exec completes

Read the output and reply to the user. Examples:
- "✅ Cube created with bobbing animation script attached."
- "❌ Unity is not running. Please open the Unity project first."

## Follow-up task (continue session)

```
exec host:gateway pty:true workdir:"/Users/tal/Documents/UnityProjects/TestOpenClawUnityPluginSkill" command:"claude -p '<next task>' --resume <session_id from previous output> --mcp-config ~/.claude/unity-mcp-config.json --allowedTools 'mcp__unity-editor__unity_check_status,mcp__unity-editor__unity_get_scene_info,mcp__unity-editor__unity_get_hierarchy,mcp__unity-editor__unity_save_scene,mcp__unity-editor__unity_create_gameobject,mcp__unity-editor__unity_delete_gameobject,mcp__unity-editor__unity_set_transform,mcp__unity-editor__unity_find_gameobjects,mcp__unity-editor__unity_get_components,mcp__unity-editor__unity_add_component,mcp__unity-editor__unity_set_component_property,mcp__unity-editor__unity_read_file,mcp__unity-editor__unity_write_file,mcp__unity-editor__unity_compile,mcp__unity-editor__unity_get_compile_errors,mcp__unity-editor__unity_get_console_logs,mcp__unity-editor__unity_get_project_info,mcp__unity-editor__unity_get_scripts,mcp__unity-editor__unity_find_assets' --max-turns 30"
```

## Task Decomposition

Break large requests into sub-tasks each completable in ≤ 30 turns:

```
"Player controller: movement + jump + coyote time"
  → Sub-task 1: basic left/right movement
  → Sub-task 2: jump with Rigidbody2D
  → Sub-task 3: coyote time
```

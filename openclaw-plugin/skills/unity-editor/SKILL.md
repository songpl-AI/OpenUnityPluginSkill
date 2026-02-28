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

Enables the AI Agent to control Unity Editor via HTTP + WebSocket — reading/writing scenes, scripts, assets, and forming a code self-correction loop.

## Prerequisites

Before using any tool, always call `unity_check_status` first if you're unsure whether Unity is running. If it returns an error, inform the user to:
1. Open Unity Editor
2. Ensure the OpenClaw Unity Plugin is in `Assets/OpenClawUnityPlugin/Editor/`
3. Wait for Unity to finish compiling

## Available Tools (overview)

| Tool | Purpose |
|------|---------|
| `unity_get_scene_info` | Current scene name, path, dirty state |
| `unity_get_hierarchy` | Full GameObject tree |
| `unity_save_scene` | Save current scene |
| `unity_create_gameobject` | Create object in scene |
| `unity_delete_gameobject` | Delete object from scene |
| `unity_set_transform` | Set position/rotation/scale |
| `unity_find_gameobjects` | Find objects by name/tag |
| `unity_read_file` | Read any file in Assets/ |
| `unity_write_file` | Write/overwrite file in Assets/ |
| `unity_compile` | Trigger compile, wait for result via WebSocket |
| `unity_get_compile_errors` | Get last compile error list |
| `unity_get_console_logs` | Get Unity Console output |
| `unity_get_project_info` | Project name, Unity version, packages |
| `unity_get_scripts` | All script classes with public API |
| `unity_find_assets` | Search assets by type/name |

Full parameter docs: `references/tools-reference.md`

## Code Self-Correction Loop

When writing or fixing C# scripts, always follow this pattern:

```
1. unity_get_scripts          ← understand existing code first
2. unity_write_file           ← write the script
3. unity_compile              ← compile and WAIT for result (WebSocket)
4. if errors → unity_read_file + unity_write_file ← fix and repeat
5. unity_get_console_logs     ← check runtime behavior after Play Mode
```

Never assume a script compiled successfully without calling `unity_compile` and checking the result.

## Accessing Existing Project Resources

Before creating new assets or scripts, always check what already exists:
- Scripts/classes: `unity_get_scripts`
- Audio/textures/prefabs: `unity_find_assets` with type filter
- Scene state: `unity_get_hierarchy`
- Project packages: `unity_get_project_info`

## Error Handling

| Error Code | Meaning | Action |
|-----------|---------|--------|
| `SERVER_NOT_READY` | Unity not running | Tell user to open Unity |
| `SCENE_NOT_LOADED` | No scene open | Ask user to open a scene |
| `OBJECT_NOT_FOUND` | Bad path | Use `unity_get_hierarchy` to find correct path |
| `FILE_OUTSIDE_PROJECT` | Path outside Assets/ | Correct path to start with `Assets/` |
| `COMPILE_ERROR` | Script error | Read compile errors and fix |

When `unity_get_scripts` returns `degraded: true`, fix compile errors first before attempting to understand or extend code.

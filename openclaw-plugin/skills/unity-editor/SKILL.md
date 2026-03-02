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
| `unity_check_status` | Check if Unity Editor is running and plugin is active |
| `unity_get_scene_info` | Current scene name, path, dirty state |
| `unity_get_hierarchy` | Full GameObject tree |
| `unity_save_scene` | Save current scene |
| `unity_create_gameobject` | Create object in scene (use `primitive` param for Cube/Sphere/etc.) |
| `unity_delete_gameobject` | Delete object from scene |
| `unity_set_transform` | Set position/rotation/scale |
| `unity_find_gameobjects` | Find objects by name/tag |
| `unity_get_components` | List all components on a GameObject |
| `unity_add_component` | Add a component by type name (built-in or user script) |
| `unity_set_component_property` | Set serialized field values on a component |
| `unity_read_file` | Read any file in Assets/ |
| `unity_write_file` | Write/overwrite file in Assets/ |
| `unity_compile` | Trigger compile, wait for result via WebSocket |
| `unity_get_compile_errors` | Get last compile error list |
| `unity_get_console_logs` | Get Unity Console output |
| `unity_get_project_info` | Project name, Unity version, packages |
| `unity_get_scripts` | All script classes with public API |
| `unity_find_assets` | Search assets by type/name |

Full parameter docs: `references/tools-reference.md`

## Two-Layer Architecture — Who Does What

**OpenClaw's agent (you) orchestrates; Claude Code executes. Never mix these roles.**

| What the user wants | Your role | How |
|---|---|---|
| "What's in the scene?" / "Is Unity running?" | Call read-only tools directly | `unity_check_status`, `unity_get_hierarchy`, etc. |
| Anything that writes, compiles, modifies, or creates | **Delegate to Claude Code** via `exec` | See "Dispatching" section below |

**⛔ NEVER call these tools directly from this OpenClaw session:**
`unity_write_file`, `unity_create_gameobject`, `unity_delete_gameobject`, `unity_compile`,
`unity_add_component`, `unity_set_component_property`, `unity_set_transform`, `unity_save_scene`

Calling write-tools directly bypasses Claude Code's compile → fix → attach loop and produces broken results (scripts written but not compiled, GameObjects created but scripts not attached).

**✅ Direct calls are only allowed for read-only tools:**
`unity_check_status`, `unity_get_scene_info`, `unity_get_hierarchy`, `unity_get_scripts`,
`unity_get_compile_errors`, `unity_get_console_logs`, `unity_get_project_info`,
`unity_find_assets`, `unity_find_gameobjects`, `unity_get_components`

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

---

## Dispatching Implementation Tasks to Claude Code

For any task that requires writing C# scripts, multi-step scene configuration, or a compile → fix → recompile loop, **delegate to Claude Code** via the `exec` tool instead of calling Unity tools directly.

### Direct tools vs. Delegation — when to choose

| Situation | Approach |
|-----------|----------|
| Read-only query: "what's in the scene?", "is Unity running?" | Call read-only tools directly in-session |
| **Everything else** — any write, compile, create, modify, attach | **ALWAYS delegate to Claude Code** |

### Task Decomposition Principle

Break large user requests into sub-tasks, each completable in ≤ 30 turns.

```
"Player Controller with movement, jump, coyote time, jump buffer"
  → Sub-task 1: Basic left/right movement (speed = 5f)
  → Sub-task 2: Jump with Rigidbody (jumpForce = 10f)
  → Sub-task 3: Coyote time (0.15s grace window)
  → Sub-task 4: Jump buffer (0.1s input buffer)
```

Execute sub-tasks **sequentially** — pass `session_id` from each result to the next call's `--resume` to preserve context.

### First Call (new session)

Use `exec` to run:

```bash
claude -p "<sub-task 1 description>" \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__unity_check_status,mcp__unity-editor__unity_get_scene_info,mcp__unity-editor__unity_get_hierarchy,mcp__unity-editor__unity_save_scene,mcp__unity-editor__unity_create_gameobject,mcp__unity-editor__unity_delete_gameobject,mcp__unity-editor__unity_set_transform,mcp__unity-editor__unity_find_gameobjects,mcp__unity-editor__unity_get_components,mcp__unity-editor__unity_add_component,mcp__unity-editor__unity_set_component_property,mcp__unity-editor__unity_read_file,mcp__unity-editor__unity_write_file,mcp__unity-editor__unity_compile,mcp__unity-editor__unity_get_compile_errors,mcp__unity-editor__unity_get_console_logs,mcp__unity-editor__unity_get_project_info,mcp__unity-editor__unity_get_scripts,mcp__unity-editor__unity_find_assets" \
  --append-system-prompt "All file operations must stay within Assets/. Do not touch ProjectSettings/, Packages/, or Library/." \
  --output-format json \
  --max-turns 30 \
  --max-budget-usd 1.00
```

### Subsequent Sub-tasks (resume context)

Extract `session_id` from the previous result, then:

```bash
claude -p "<sub-task 2 description — reference prior work explicitly>" \
  --resume <session_id from previous result> \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__unity_check_status,mcp__unity-editor__unity_get_scene_info,mcp__unity-editor__unity_get_hierarchy,mcp__unity-editor__unity_save_scene,mcp__unity-editor__unity_create_gameobject,mcp__unity-editor__unity_delete_gameobject,mcp__unity-editor__unity_set_transform,mcp__unity-editor__unity_find_gameobjects,mcp__unity-editor__unity_get_components,mcp__unity-editor__unity_add_component,mcp__unity-editor__unity_set_component_property,mcp__unity-editor__unity_read_file,mcp__unity-editor__unity_write_file,mcp__unity-editor__unity_compile,mcp__unity-editor__unity_get_compile_errors,mcp__unity-editor__unity_get_console_logs,mcp__unity-editor__unity_get_project_info,mcp__unity-editor__unity_get_scripts,mcp__unity-editor__unity_find_assets" \
  --output-format json \
  --max-turns 30 \
  --max-budget-usd 1.00
```

> Always mention what was done in the prior sub-task in the new prompt (e.g., "Based on the PlayerController.cs we just created with movement..."). This ensures Claude Code's context is complete even if the session is resumed after a gap.

### Parsing the Result

The `exec` output is a JSON object. Key fields (confirmed with Claude Code v2.1.61):

| Field | Type | Description |
|-------|------|-------------|
| `result` | string | Claude Code's text summary — report this to the user |
| `session_id` | string | **Save this.** Pass to next sub-task's `--resume` |
| `is_error` | bool | `true` if the call failed |
| `subtype` | string | `"success"` \| `"error_max_turns"` \| `"error_during_execution"` |
| `total_cost_usd` | number | API cost for this call |
| `num_turns` | number | Turns used (max is `--max-turns`) |

### When to Split Further

If `subtype` is `"error_max_turns"` or `result` describes incomplete work:
- **Do NOT increase `--max-turns`**
- Split the sub-task into 2–3 smaller pieces
- Use `--resume <same session_id>` to continue from where it stopped
- Each smaller piece should be independently verifiable

### Project State Snapshot (context fallback)

If a session expires or `--resume` fails, inject context via `--append-system-prompt`:

```bash
--append-system-prompt "Project state:
- PlayerController.cs: basic movement (speed=5f) + jump (jumpForce=10f) ✅
- Next: add coyote time (0.15s grace window after walking off ledge)"
```

Maintain a running summary of completed sub-tasks to use as a fallback whenever `--resume` is unavailable.

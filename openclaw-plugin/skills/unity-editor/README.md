# Unity Editor Skill for OpenClaw

Tell OpenClaw what you want to build in Unity — it writes the C# scripts, compiles them, fixes any errors, and attaches the components to your GameObjects. No manual coding required.

**Example:**
> "给玩家角色加一个跳跃功能，按空格键触发，有土狼时间"

OpenClaw will decompose this into sub-tasks, dispatch each one to Claude Code (which controls Unity via MCP), and report back when done.

---

## Requirements

| Requirement | Version | Notes |
|-------------|---------|-------|
| Unity Editor | 2021.3 LTS+ or 2022.3 LTS+ or Unity 6 | Unity 6 (6000.x) confirmed working |
| Node.js | 18+ | Required by the MCP Server |
| Claude Code CLI | Latest | `claude` command must be in PATH |
| OpenClaw | Latest | This skill runs inside OpenClaw |

---

## Installation

### Step 1 — Import the Unity Plugin

Copy the plugin folder into your Unity project:

```
unity-plugin/Assets/OpenClawUnityPlugin/  →  YourUnityProject/Assets/OpenClawUnityPlugin/
```

Wait for Unity to finish compiling. If **Newtonsoft.Json** is not installed, the plugin will install it automatically — watch the Console for:

```
[OpenClaw Unity Plugin] ✅ 'com.unity.nuget.newtonsoft-json' installed successfully.
```

Verify the plugin is running:

```bash
curl http://127.0.0.1:23456/api/v1/status
# Expected: {"ok":true,"data":{"status":"ready",...}}
```

> Use `127.0.0.1`, not `localhost` — the HTTP server validates the Host header strictly.

### Step 2 — Install the MCP Server

From the project root, run:

```bash
bash openclaw-plugin/skills/unity-editor/install.sh
```

This will:
1. Install Node.js dependencies and build the MCP Server (`mcp-server/`)
2. Write the MCP config to `~/.claude/unity-mcp-config.json`

### Step 3 — Verify the Connection

Run this from a regular terminal (not inside a Claude Code session):

```bash
claude -p "用 unity_check_status 检查 Unity 状态" \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__unity_check_status" \
  --output-format json --max-turns 3
```

Expected: the response describes your Unity version, project name, and open scene.

---

## Usage

Once installed, just talk to OpenClaw naturally. The skill handles everything else.

### Simple queries (OpenClaw handles directly)

- "当前场景里有哪些 GameObject？"
- "Unity 现在在运行吗？"
- "项目里有哪些脚本？"

### Implementation tasks (dispatched to Claude Code)

These trigger the full OpenClaw → Claude Code → Unity pipeline:

- "在场景里创建一个旋转的立方体"
- "给 Player 写一个左右移动脚本，speed=5，编译并挂上去"
- "在 PlayerController.cs 里加跳跃功能，不要改现有的移动代码"
- "找出所有编译错误并修复"

### Multi-step features

For larger features, OpenClaw will automatically decompose and sequence the work:

> "实现一个平台跳跃玩家控制器，有移动、跳跃、土狼时间和跳跃缓冲"

→ OpenClaw breaks this into sub-tasks and runs them one by one, preserving context across each step.

---

## How It Works

```
You (natural language)
    ↓
OpenClaw Agent          — decides what to build, decomposes into sub-tasks
    ↓  exec: claude -p
Claude Code             — writes scripts, triggers compile, reads errors, fixes, repeats
    ↓  MCP (stdio)
MCP Server              — translates MCP calls to Unity HTTP/WebSocket requests
    ↓  HTTP + WebSocket
Unity C# Plugin         — executes operations, pushes compile events back
    ↓
Unity Editor
```

The compile loop runs entirely inside Claude Code — it writes a script, waits for the WebSocket `compile_complete` / `compile_failed` event, reads errors if any, fixes the code, and recompiles. This repeats until success or the turn limit is reached.

---

## Troubleshooting

**`curl http://127.0.0.1:23456/api/v1/status` returns `Connection refused`**
→ Unity Editor is not running, or the plugin hasn't been imported yet.

**`curl http://localhost:23456/...` returns `400 Bad Request (Invalid host)`**
→ Always use `127.0.0.1`, not `localhost`.

**Newtonsoft.Json compile errors after importing the plugin**
→ Wait a few seconds — `DependencyInstaller.cs` will automatically install the package and trigger a recompile.

**`install.sh` fails with `sed: No such file or directory`**
→ Run `install.sh` from the project root, not from inside the skill folder:
```bash
bash openclaw-plugin/skills/unity-editor/install.sh
```

**`unity_compile` times out after 60 seconds**
→ Unity is still doing a Domain Reload. Wait for Unity to finish compiling, then retry. For large projects, the sub-task prompt can include `timeoutSeconds: 120`.

**Sub-task hits `--max-turns` limit without finishing**
→ The task is too large. OpenClaw will split it into smaller sub-tasks and use `--resume` to continue. Do not increase `--max-turns`.

---

## File Structure

```
openclaw-plugin/skills/unity-editor/
├── SKILL.md          ← OpenClaw reads this to understand the skill
├── README.md         ← This file
├── install.sh        ← One-command setup script
└── references/       ← (reserved for tool reference docs)

mcp-server/           ← MCP Server (built by install.sh)
├── src/
│   ├── index.ts
│   └── tools/        ← 16 Unity tools
└── dist/             ← Compiled output (generated)

unity-plugin/
└── Assets/OpenClawUnityPlugin/Editor/
    ├── Core/         ← HTTP + WebSocket server
    ├── Handlers/     ← Route handlers for each API endpoint
    └── Setup/        ← Auto-installs Newtonsoft.Json on first import
```

---

## Supported Unity Versions

| Version | WebSocket Support | Notes |
|---------|-------------------|-------|
| Unity 6 (6000.x) | Built-in | Confirmed working |
| Unity 2022.3 LTS | Built-in | Recommended |
| Unity 2021.3 LTS | websocket-sharp | Requires manual package install |

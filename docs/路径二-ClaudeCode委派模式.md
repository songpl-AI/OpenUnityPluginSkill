# 路径二：OpenClaw → Claude Code → MCP 委派模式

**版本**: 1.0.0
**最后更新**: 2026-03-02

---

## 概述

OpenClaw 的 AI Agent 作为**任务编排者**，将所有写操作委派给 Claude Code 子进程。Claude Code 通过 MCP（Model Context Protocol）工具控制 Unity，具备编译自修正能力。

```
用户（飞书）
  → OpenClaw Agent（MiniMax / Claude）
    → exec("claude -p ..." --mcp-config unity-mcp-config.json)
      → Claude Code 子进程
        → MCP Server（mcp-server/dist/index.js）
          → HTTP → http://127.0.0.1:23456/api/v1/...
          → WebSocket → ws://127.0.0.1:23457/ws（等待编译事件）
            → Unity C# Plugin
              → Unity Editor API
```

---

## 适用场景

| 场景 | 示例需求 |
|------|---------|
| 写 C# 脚本并自动挂载 | "给 Player 添加移动控制脚本" |
| 编译错误自修正 | 写脚本 → 编译报错 → 自动修复 → 重新编译 |
| 多步场景搭建 | "创建一个带 Rigidbody 和旋转脚本的 Cube" |
| 复杂功能实现 | "实现土狼时间跳跃缓冲机制" |
| 跨任务上下文保持 | "在上一个脚本基础上添加跳跃功能" |

> ✅ **任何需要写文件、编译、创建/修改 GameObject、挂载组件的任务都应走路径二。**

---

## 核心优势：编译自修正循环

Claude Code 会自动执行以下循环，不需要人工干预：

```
unity_write_file("RotatingCube.cs", ...)
  → unity_compile()          [等待 WebSocket 事件]
  → compile_failed: CS1002   [自动读取错误详情]
  → unity_read_file(...)     [读取错误文件]
  → unity_write_file(...)    [修复错误]
  → unity_compile()          [再次等待]
  → compile_complete ✅
  → unity_add_component(...)
  → unity_save_scene()
```

---

## 安装方式

### 前置条件

- Node.js 18+（`node --version` 需 ≥ 18）
- Claude Code CLI 已安装（`claude --version`）
- Anthropic API Key 已配置（`echo $ANTHROPIC_API_KEY`）
- Unity Plugin 已导入并运行（参见路径一安装步骤 1）

### 步骤 1：构建 MCP Server 并生成配置

在项目根目录执行：

```bash
bash openclaw-plugin/skills/unity-editor/install.sh
```

脚本自动完成：
1. 检查 Node.js 版本
2. 安装 MCP Server 依赖（`npm install`）
3. 编译 TypeScript（`npm run build`）
4. 生成 `~/.claude/unity-mcp-config.json`（替换路径占位符）

### 步骤 2：验证 MCP Server

```bash
claude -p "检查 Unity 状态" \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__unity_check_status" \
  --output-format json \
  --max-turns 3
```

期望输出（JSON）中 `result` 字段包含 Unity 版本和场景名称。

### 步骤 3：安装并启用 OpenClaw 插件

参见路径一的步骤 2、3、4。

路径二额外要求：SKILL.md 已包含委派指令（当前版本已配置）。

---

## OpenClaw 调用 Claude Code 的模板

OpenClaw 的 Agent 在判断需要委派时，通过 `exec` 工具执行以下命令：

### 首次调用（新 session）

```bash
claude -p "<任务描述>" \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__unity_check_status,mcp__unity-editor__unity_get_scene_info,mcp__unity-editor__unity_get_hierarchy,mcp__unity-editor__unity_save_scene,mcp__unity-editor__unity_create_gameobject,mcp__unity-editor__unity_delete_gameobject,mcp__unity-editor__unity_set_transform,mcp__unity-editor__unity_find_gameobjects,mcp__unity-editor__unity_get_components,mcp__unity-editor__unity_add_component,mcp__unity-editor__unity_set_component_property,mcp__unity-editor__unity_read_file,mcp__unity-editor__unity_write_file,mcp__unity-editor__unity_compile,mcp__unity-editor__unity_get_compile_errors,mcp__unity-editor__unity_get_console_logs,mcp__unity-editor__unity_get_project_info,mcp__unity-editor__unity_get_scripts,mcp__unity-editor__unity_find_assets" \
  --append-system-prompt "所有文件操作必须在 Assets/ 目录内。禁止修改 ProjectSettings/、Packages/、Library/。" \
  --output-format json \
  --max-turns 30 \
  --max-budget-usd 1.00
```

### 后续子任务（续接上下文）

```bash
claude -p "<子任务描述，需引用前序工作>" \
  --resume <上一次返回的 session_id> \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__unity_check_status,...（同上）" \
  --output-format json \
  --max-turns 30 \
  --max-budget-usd 1.00
```

### 解析返回结果

`--output-format json` 输出（Claude Code v2.1.61 确认字段）：

```json
{
  "type": "result",
  "subtype": "success",
  "is_error": false,
  "session_id": "fac8c5bf-7108-...",
  "result": "Claude Code 的文字摘要",
  "num_turns": 8,
  "total_cost_usd": 0.12
}
```

| 字段 | 含义 |
|------|------|
| `result` | 向用户展示的完成摘要 |
| `session_id` | **保存此值**，传给下一个子任务的 `--resume` |
| `is_error` | `true` 表示失败 |
| `subtype` | `"success"` / `"error_max_turns"` / `"error_during_execution"` |
| `total_cost_usd` | 本次 API 费用 |

---

## 任务拆解原则

每个子任务应在 ≤ 30 轮内完成。拆解示例：

```
用户: "玩家控制器：移动 + 跳跃 + 土狼时间 + 跳跃缓冲"

子任务 1: 基础左右移动（speed = 5f）
子任务 2: 跳跃（jumpForce = 10f，Rigidbody2D）
子任务 3: 土狼时间（0.15s 宽限窗口）
子任务 4: 跳跃缓冲（0.1s 输入缓冲）
```

每个子任务通过 `--resume <session_id>` 保持上下文连续。

**如果子任务返回 `subtype: "error_max_turns"`：**
- ❌ 不要增大 `--max-turns`
- ✅ 将该子任务拆成更小的两步，用 `--resume` 续接

---

## Claude Code 执行的完整工作流

Claude Code 在收到任务后自动按以下顺序操作（不需要 OpenClaw 介入）：

```
1. unity_check_status              → 确认 Unity 在运行
2. unity_get_hierarchy             → 了解现有场景结构
3. unity_create_gameobject         → 创建 GameObject（必须带 primitive 参数）
4. unity_write_file                → 写 C# 脚本到 Assets/Scripts/
5. unity_compile                   → 触发编译，等待 WebSocket 事件
6. [有错误] unity_get_compile_errors → 读取错误详情
           unity_write_file        → 修复代码，重回步骤 5
7. unity_add_component             → 挂载脚本到 GameObject
8. unity_set_component_property    → 设置公开字段（speed、jumpForce 等）
9. unity_save_scene                → 保存场景
```

---

## 最佳实践

**1. 任务描述要具体**

```
❌ "实现玩家控制器"
✅ "在 Assets/Scripts/PlayerController.cs 中实现左右移动（speed=5f），
   将脚本挂到场景中名为 Player 的 GameObject 上"
```

**2. 后续子任务显式引用前序工作**

```
✅ "在上一个子任务创建的 PlayerController.cs 基础上（已有 speed=5f 移动），
   添加跳跃功能（jumpForce=10f，使用 Rigidbody2D）"
```

**3. 双重限制防止超支**

同时设置 `--max-turns 30` 和 `--max-budget-usd 1.00`，防止单次任务失控。

**4. session 过期时用快照恢复上下文**

```bash
--append-system-prompt "项目状态：
- PlayerController.cs：移动（speed=5f）+ 跳跃（jumpForce=10f）✅
- 下一步：添加土狼时间（0.15s 宽限窗口）"
```

**5. 验证时监控日志**

```bash
# 监控 Claude Code 工具调用
tail -f /tmp/openclaw/openclaw-$(date +%F).log \
  | grep "embedded run tool\|exec"
```

---

## 与路径一的对比

| | 路径一（直接工具） | 路径二（Claude Code 委派） |
|--|--|--|
| 执行主体 | OpenClaw LLM | Claude Code |
| 工具调用 | OpenClaw 直接调 | Claude Code 通过 MCP 调 |
| 适合操作 | 只读查询 | 所有写操作 |
| 编译自修正 | ❌ 不支持 | ✅ 自动循环修复 |
| 上下文保持 | 依赖 OpenClaw session | `--resume session_id` |
| 典型响应时间 | 5–15 秒 | 1–5 分钟（多轮 API 调用） |
| API 费用 | 仅 OpenClaw LLM | OpenClaw LLM + Claude API |

---

## 常见问题

**Q: `claude -p` 报 `--mcp-config` 找不到文件**
A: 先运行 `bash openclaw-plugin/skills/unity-editor/install.sh` 生成配置文件。

**Q: `mcp-server/dist/index.js` 不存在**
A: 进入 `mcp-server/` 目录运行 `npm run build`。

**Q: `unity_compile` 一直等待，60 秒后超时**
A: 可能原因：
1. WebSocket 未连接（先调 `unity_check_status` 确认）
2. 脚本内容未变化（Unity 不会重新编译未修改的文件）
3. 大型项目编译超时：传入 `timeoutSeconds: 120`

**Q: 子任务完成了但脚本没挂到 GameObject**
A: 检查任务描述是否明确要求"挂载到 [GameObject 名称]"。Claude Code 需要明确的目标 GameObject 路径才会调用 `unity_add_component`。

**Q: `--resume` 报 session 不存在**
A: Session 可能过期。改用 `--append-system-prompt` 注入项目状态快照，不传 `--resume`。

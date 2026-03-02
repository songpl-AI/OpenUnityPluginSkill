# 多 Agent 架构落地方案：OpenClaw + Claude Code + Unity MCP

**版本**: 2.1.0
**日期**: 2026-03-02
**状态**: 调研完成，可进入实施

---

## 1. 调研结论摘要

### 1.1 三个关键发现

**发现一：OpenClaw 内置 `exec` 工具，无需开发自定义插件工具**

OpenClaw Agent 自带 `exec` 工具，可直接运行任意 Shell 命令，支持前台/后台执行和长进程管理。这意味着 OpenClaw 调用 Claude Code **不需要写任何插件代码**，只需在 SKILL.md 中指导 Agent 使用 `exec` 工具执行 `claude -p "..."` 命令。

```json
// OpenClaw Agent 可以直接调用
{ "tool": "exec", "command": "claude -p '实现玩家跳跃功能' --output-format json --mcp-config /path/to/unity-mcp.json" }
```

**发现二：Claude Code CLI 支持完整的非交互编程接口**

`claude -p` 是 Anthropic 官方 Agent SDK 的 CLI 入口，支持：
- `--output-format json` → 结构化 JSON 输出，包含 session_id、result
- `--resume session_id` → 恢复上次会话（跨任务上下文保持）
- `--mcp-config ./mcp.json` → 指定 MCP Server 配置
- `--allowedTools "mcp__unity__*,Read,Edit,Bash"` → 限定可用工具
- `--append-system-prompt` → 注入 Unity 专用系统提示
- `--max-turns 30` → 限制最大循环轮次
- `--max-budget-usd 2.00` → 限制单次任务花费上限
- `--agents` → 在 Claude Code 内部再定义子 Agent

**发现三：MCP Server 构建成本极低，现有代码复用率 > 70%**

官方 TypeScript SDK `@modelcontextprotocol/sdk` 成熟稳定，现有 `openclaw-plugin/src/` 的所有工具定义逻辑可以直接迁移，只需适配工具注册格式差异。

**发现四：单 Claude Code 实例 + 顺序任务拆解是 Unity 场景的最优解**

Unity 开发流程本质是串行的：写代码 → 编译（瓶颈）→ 修复 → 挂载组件。编译每次只能执行一次，并行写代码带来的时间收益在编译环节归零，且多实例会引入跨进程冲突和协调开销。更重要的是，多实例并行要求 MCP Server 使用 HTTP/SSE 长驻进程模式（而非 stdio），架构复杂度倍增。采用单实例 + OpenClaw 顺序拆分任务 + `--resume` 恢复上下文，既能覆盖所有需求，又保持架构简单。

---

### 1.2 架构全图

```
用户（自然语言）
    │ "给我做一个有跳跃和攻击的玩家控制器"
    ▼
┌──────────────────────────────────────────────────────────────────┐
│                     OpenClaw Agent（规划层）                       │
│                                                                   │
│  职责：游戏设计决策 / 任务拆解 / 顺序编排 / 结果验收               │
│                                                                   │
│  工具：exec（内置，顺序调用 Claude Code，--resume 保持上下文）      │
└──────────────────────────────┬───────────────────────────────────┘
                               │ exec: claude -p "..." --mcp-config ...
                               │        [--resume <session_id>]
                               │ （Shell 子进程，顺序执行）
                               ▼
                    ┌──────────────────────┐
                    │    Claude Code       │
                    │    单实例            │
                    │  顺序完成每个子任务  │
                    └──────────┬───────────┘
                               │ MCP 协议（stdio）
                               ▼
┌──────────────────────────────────────────────────────────────────┐
│                   MCP Server（新增，~250行）                        │
│                                                                   │
│  工具：unity_get_hierarchy / unity_create_gameobject / ...        │
│  传输：StdioServerTransport（per-process，无需长驻）               │
└──────────────────────────────┬───────────────────────────────────┘
                               │ HTTP REST + WebSocket
                               ▼
┌──────────────────────────────────────────────────────────────────┐
│              Unity C# Plugin（已有，不改动）                        │
│              HTTP :23456 + WebSocket :23457                      │
└──────────────────────────────┬───────────────────────────────────┘
                               │ Unity Editor API（主线程）
                               ▼
                         Unity Editor
```

---

## 2. 需要构建的三个组件

### 组件一：MCP Server（新增）

**位置**：`mcp-server/`（与 `openclaw-plugin/` 同级）
**作用**：把现有 Unity HTTP API 包装成 MCP 工具，供 Claude Code 调用

**目录结构**：
```
mcp-server/
├── package.json
├── tsconfig.json
├── src/
│   ├── index.ts          # MCP Server 入口，stdio 传输
│   ├── unity-client.ts   # HTTP 客户端（直接复制 openclaw-plugin/src/unity-client.ts）
│   ├── operation-queue.ts # Unity 操作串行队列（防并发冲突）
│   └── tools/
│       ├── scene.ts       # 复用 openclaw-plugin/src/tools/scene.ts 逻辑
│       ├── gameobject.ts
│       ├── file.ts
│       ├── compile.ts     # 含 waitForCompile 阻塞工具
│       └── project.ts
└── README.md             # 配置说明
```

**核心入口示例**：
```typescript
// src/index.ts
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { z } from "zod";
import { UnityClient } from "./unity-client";
import { UnityWsClient } from "./unity-ws-client"; // ⚠️ 必须初始化，compile 工具依赖 WS 事件

const server = new McpServer({ name: "unity-editor", version: "1.0.0" });
const client = new UnityClient({ port: Number(process.env.UNITY_PORT ?? 23456), timeout: 15000 });
const ws     = new UnityWsClient(Number(process.env.UNITY_WS_PORT ?? 23457));

// WS 在进程启动时连接，全程保持（处理 Domain Reload 断连会自动重连）
ws.connect();

// 工具注册（以 compile 为例，其余同理）
server.registerTool("unity_compile", {
  description: "触发 Unity 编译，等待完成，返回编译结果和错误列表",
  inputSchema: {
    timeoutSeconds: z.number().optional().describe("最大等待秒数，默认 60"),
  },
}, async ({ timeoutSeconds }) => {
  await client.post("/editor/compile");
  const timeoutMs = (timeoutSeconds ?? 60) * 1000;
  const result = await Promise.race([
    ws.waitForEvent("compile_complete", timeoutMs),
    ws.waitForEvent("compile_failed",   timeoutMs),
  ]) as { errors?: unknown[] };

  if (result?.errors?.length) {
    const errRes = await client.get<{ errors: unknown[] }>("/compile/errors");
    // ... 格式化错误并返回
    return { content: [{ type: "text", text: "Compilation FAILED:\n..." }] };
  }
  return { content: [{ type: "text", text: "Compilation succeeded." }] };
});

const transport = new StdioServerTransport();
await server.connect(transport);
```

---

### 组件二：Claude Code 配置文件

**位置**：`mcp-server/claude-mcp-config.json`（用户复制到 `~/.claude/` 或通过 `--mcp-config` 参数传入）

```json
{
  "mcpServers": {
    "unity-editor": {
      "command": "node",
      "args": ["/path/to/mcp-server/dist/index.js"],
      "env": {
        "UNITY_PORT": "23456",
        "UNITY_WS_PORT": "23457"
      }
    }
  }
}
```

**OpenClaw 调用 Claude Code 的标准命令模板**（在 SKILL.md 中定义）：

首次调用（新会话）：
```bash
claude -p "<任务描述>" \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__*,Read,Edit,Bash" \
  --append-system-prompt "你正在操作一个 Unity 项目，路径为 /path/to/project。
    完成任务后输出 JSON：{\"status\": \"success|failed\", \"summary\": \"...\", \"files_modified\": [...]}" \
  --output-format json \
  --max-turns 30
```

后续任务（恢复上下文）：
```bash
claude -p "<下一个任务描述>" \
  --resume <上次返回的 session_id> \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__*,Read,Edit,Bash" \
  --output-format json \
  --max-turns 30
```

**关键参数说明**：

| 参数 | 作用 |
|------|------|
| `--allowedTools "mcp__unity-editor__*"` | 只允许 unity MCP 工具，防止 Claude Code 越界操作 |
| `--append-system-prompt` | 注入项目路径、输出格式要求、编码规范等上下文 |
| `--output-format json` | 结构化输出，OpenClaw 可解析 session_id 和 result |
| `--max-turns 30` | 防止无限循环，编译修复循环通常 5-15 轮足够 |
| `--resume <session_id>` | 恢复之前的会话，保留已有上下文（跨任务状态传递） |

> **注**：任务复杂度超过 `--max-turns` 上限时，正确做法是 OpenClaw 把任务拆得更细，而不是增大上限。

---

### 组件三：OpenClaw Skill 包（SKILL.md + install.sh）

**不需要写新的 OpenClaw Plugin 工具**，只需更新 SKILL.md，并补充 `install.sh` 自动化 MCP Server 配置。

参照 ClawHub 上的 Skill 标准结构，完整的 Skill 包应包含：

```
openclaw-plugin/skills/unity-editor/
├── SKILL.md        ← OpenClaw 读取的技能定义（已有）
├── install.sh      ← 自动化安装脚本（待补充）
└── README.md       ← 用户说明文档（待补充）
```

`install.sh` 负责：构建 MCP Server（`npm run build`）、生成 `~/.claude/unity-mcp-config.json`（替换路径占位符）、验证 `claude` CLI 是否已安装。这样用户只需运行一次脚本即可完成全部配置，无需手动编辑 JSON。

> **注**：OpenClaw 还有原生 `sessions_spawn` 工具可管理子 Agent 会话，待查阅官方文档确认其与 `exec + claude -p` 方案的区别，再决定是否采用。

SKILL.md 新增内容：

```markdown
## 派发实现任务给 Claude Code

当用户要求实现具体的游戏功能逻辑时，将大任务拆解为多个子任务，
使用 exec 工具**顺序**调用 Claude Code，通过 --resume 在子任务间传递上下文。

### 适用场景
- 需要编写/修改 C# 脚本
- 需要配置 GameObjects/组件/场景
- 需要调试编译错误
- 任何需要多步骤反复操作 Unity 的实现任务

### 任务拆解原则
将用户需求拆解为粒度适中的子任务，每个子任务应能在 30 轮内完成。
例如"玩家控制器"拆为：① 基础移动 → ② 跳跃逻辑 → ③ 土狼时间 → ④ 跳跃缓冲

### 首次调用
exec 工具 command：
claude -p "<子任务1描述>" \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__*,Read,Edit,Bash" \
  --output-format json --max-turns 30

### 后续子任务（保留上下文）
从上次 JSON 输出中取出 session_id，用 --resume 恢复：
claude -p "<子任务2描述>" \
  --resume <session_id> \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__*,Read,Edit,Bash" \
  --output-format json --max-turns 30

### 解析结果
exec 输出的 JSON 中：
- result 字段：Claude Code 的文字总结
- session_id：必须保存，传给下一个子任务的 --resume

### 任务粒度判断
若子任务在 30 轮内未完成（result 包含"未完成"或工具调用超限），
将该子任务进一步拆小后重新派发，不要简单增大 --max-turns。
```

---

## 3. Claude Code 自主循环详解

一旦 Claude Code 收到任务，它会自主执行以下循环，**完全不需要人工干预**：

```
接收任务："实现玩家跳跃功能"
    ↓
1. mcp__unity-editor__unity_get_hierarchy()
   → 找到场景中的 Player GameObject
    ↓
2. 用 Edit 工具写 Assets/Scripts/PlayerJump.cs
    ↓
3. mcp__unity-editor__unity_compile()
   → 触发编译，阻塞等待结果
    ↓
4. 编译失败？→ 读取错误 → 修改脚本 → 回到步骤 3
   编译成功？↓
    ↓
5. mcp__unity-editor__unity_add_component("Player", "PlayerJump")
    ↓
6. mcp__unity-editor__unity_set_property("Player/PlayerJump", "jumpForce", 5.0)
    ↓
7. 输出完成摘要：
   {"status":"success","summary":"已实现跳跃功能，jumpForce=5.0","files_modified":["Assets/Scripts/PlayerJump.cs"]}
```

这个循环中，**编译 → 报错 → 修复** 可以自动迭代多次，直到成功或达到 `--max-turns` 上限。

---

## 4. 并行任务（高级用法，暂不实施）

> **当前阶段不采用并行多实例方案**。Unity 开发流程以编译为串行瓶颈，并行写代码的收益在编译环节归零，而多实例方案的实现成本显著更高：stdio transport 下每个 `claude -p` 进程会 spawn 自己的 MCP Server 实例，各进程的 OperationQueue 互不可见，无法防止并发编译冲突。修复此问题需将 MCP Server 改为 HTTP/SSE 长驻进程，架构复杂度倍增。
>
> **主路径**：OpenClaw 将大任务拆为多个子任务，顺序调用 Claude Code，用 `--resume` 传递上下文。

### 何时考虑并行（未来场景）

仅当任务满足以下**所有条件**时才值得并行：
- 子任务操作的文件完全独立（不共享任何 .cs 文件或 Scene 资产）
- 子任务之间无编译依赖（A 的代码不引用 B 尚未写好的类）
- 子任务数量多（≥3 个）且顺序执行总时间明显不可接受

满足条件后，并行方案需要将 MCP Server 改为 HTTP 长驻模式，使操作队列跨进程共享：

```json
// 并行模式下的 MCP 配置（与当前 stdio 模式不同）
{
  "mcpServers": {
    "unity-editor": {
      "url": "http://localhost:3456/mcp"
    }
  }
}
```

在达到此需求之前，保持当前单实例 + stdio 架构。

---

## 5. 实施路线图

### 阶段 0（当前）：Unity C# Plugin 集成测试

**前置条件**，本阶段必须完成才能进入后续阶段。

- [ ] 将 `unity-plugin/` 导入真实 Unity 工程
- [ ] 验证 HTTP 通信（GET /status → 200 OK）
- [ ] 验证 WebSocket 事件（修改场景 → 收到事件）
- [ ] 验证编译反馈（写错误代码 → 收到 compile_failed + 错误详情）

**预计时间**：1-2 天

---

### 阶段 1：MCP Server 搭建

**目标**：Claude Code 可以通过 MCP 工具控制 Unity。

**任务清单**：

- [ ] 初始化 `mcp-server/` 项目（`npm create @modelcontextprotocol/server`）
- [ ] 复制 `unity-client.ts`（不修改）
- [ ] 实现 `operation-queue.ts`（串行队列，约 30 行）
- [ ] 迁移 5 个工具文件（格式转换，逻辑复用）
- [ ] 编写 `install.sh`（构建 MCP Server + 生成 `~/.claude/unity-mcp-config.json`）
- [ ] 本地测试：`claude -p "获取场景层级" --mcp-config ./test-config.json`
- [ ] 验证编译循环：让 Claude Code 写一个有错误的脚本并自动修复

**工具迁移对照表**：

| openclaw-plugin 工具格式 | MCP Server 工具格式 |
|------------------------|-------------------|
| `api.registerTool({name, description, parameters, execute})` | `server.registerTool(name, {description, inputSchema}, handler)` |
| `return textResult("...")` | `return {content: [{type:"text", text:"..."}]}` |
| `return unityError(res)` | `throw new Error(res.error.message)` |

**预计时间**：2-3 天

---

### 阶段 2：端到端单链路验证

**目标**：验证"OpenClaw → Claude Code → Unity"完整链路跑通。

**测试场景**：

1. OpenClaw Agent 使用内置 `exec` 工具调用 Claude Code
2. Claude Code 自主创建一个 GameObject、写一个 C# 脚本、编译、挂载组件
3. OpenClaw 解析返回的 JSON 结果，报告给用户

**验证命令**（在 OpenClaw 中说）：
> 在 Unity 场景里创建一个名为 TestCube 的立方体，写一个让它旋转的脚本并挂上去

**预计时间**：1 天

---

### 阶段 3：SKILL.md 精调与上下文保持

**目标**：优化 OpenClaw 的任务拆解质量和跨任务上下文传递。

- [ ] 更新 SKILL.md，加入顺序调用 + `--resume` 模板
- [ ] 验证 session_id 保存和 `--resume` 恢复机制（实测跨任务上下文是否保留）
- [ ] 建立项目状态快照（游戏已实现的功能清单，每次任务完成后更新）
- [ ] 测试：实现"玩家移动"后 `--resume` 继续实现"跳跃"，验证 Claude Code 记得已有代码结构

---

## 6. 技术风险与应对

| 风险 | 概率 | 影响 | 应对方案 |
|------|------|------|---------|
| Claude Code 超过 `--max-turns` 仍未完成 | 中 | 中 | OpenClaw 将子任务拆得更细后重新派发，不增大 max-turns |
| `--resume` 上下文丢失（session 过期） | 低 | 中 | OpenClaw 在 append-system-prompt 中注入已完成功能摘要作为补充上下文 |
| `exec` 子进程 JSON 输出格式变化 | 低 | 中 | 使用 `--output-format json` 保证结构稳定性，实测确认字段名 |
| Claude Code 修改了不该改的文件 | 低 | 高 | `--allowedTools` 限制工具范围，系统提示注明禁区 |
| Unity Plugin 因 Domain Reload 断连 | 中 | 中 | `UnityWsClient` 已有自动重连（2s 间隔），`compile` 工具等待事件前应确认 WS 已连接 |
| Unity C# Plugin 集成测试发现问题 | 中 | 高 | 阶段 0 完成前不推进 MCP Server 开发，避免在不稳定基础上叠加 |

---

## 7. 最终效果预览

配置完成后，使用体验：

**用户对 OpenClaw 说**：
> 我想做一个 2D 平台跳跃游戏，先实现玩家的基础移动（左右移动 + 跳跃），要有土狼时间和跳跃缓冲，手感要流畅

**OpenClaw 自动执行**：
1. 拆解为顺序子任务：① 基础移动 → ② 跳跃逻辑 → ③ 土狼时间 → ④ 跳跃缓冲
2. 首次 exec：`claude -p "实现基础左右移动..." --output-format json --max-turns 30`
3. Claude Code 自主循环：写代码 → 编译 → 修复错误 → 挂载 → 配置参数
4. 返回 JSON，OpenClaw 取出 session_id
5. 后续 exec：`claude -p "在上述基础上增加跳跃..." --resume <session_id>`（Claude Code 记得已有代码）
6. 重复步骤 3-5 直到所有子任务完成，OpenClaw 汇报给用户

**从用户说话到 Unity 里跑起来，全程 0 次人工干预。**

---

*关联文档：`docs/架构设计.md`、`docs/开发进度.md`*
*下一步行动：完成阶段 0（Unity C# Plugin 集成测试），然后按阶段 1 搭 MCP Server（注意 `src/index.ts` 入口需初始化 `UnityWsClient`）。*

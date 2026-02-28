# 多 Agent 架构落地方案：OpenClaw + Claude Code + Unity MCP

**版本**: 2.0.0
**日期**: 2026-02-28
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

---

### 1.2 架构全图

```
用户（自然语言）
    │ "给我做一个有跳跃和攻击的玩家控制器"
    ▼
┌──────────────────────────────────────────────────────────────────┐
│                     OpenClaw Agent（规划层）                       │
│                                                                   │
│  职责：游戏设计决策 / 任务拆解 / 多任务编排 / 结果验收              │
│                                                                   │
│  工具：exec（内置，调用 Claude Code）                              │
│        process（内置，管理长进程 / 并行任务）                       │
└──────────────────────────────┬───────────────────────────────────┘
                               │ exec: claude -p "..." --mcp-config ...
                               │ （Shell 子进程，支持并行）
              ┌────────────────┼────────────────┐
              ▼                ▼                ▼
    ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
    │ Claude Code  │  │ Claude Code  │  │ Claude Code  │
    │ 实例 #1      │  │ 实例 #2      │  │ 实例 #3      │
    │ 玩家移动     │  │ 攻击系统     │  │ 动画控制     │
    └──────┬───────┘  └──────┬───────┘  └──────┬───────┘
           │                 │                 │
           └─────────────────┼─────────────────┘
                             │ MCP 协议（stdio）
                             ▼
┌──────────────────────────────────────────────────────────────────┐
│                   MCP Server（新增，~300行）                        │
│                                                                   │
│  工具：unity_get_hierarchy / unity_create_gameobject / ...        │
│  队列：Unity 操作串行化（防多实例并发冲突）                         │
└──────────────────────────────┬───────────────────────────────────┘
                               │ HTTP REST
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
import { OperationQueue } from "./operation-queue";

const server = new McpServer({ name: "unity-editor", version: "1.0.0" });
const client = new UnityClient({ port: Number(process.env.UNITY_PORT ?? 23456) });
const queue  = new OperationQueue(); // 串行化所有 Unity 写操作

// 工具注册（以 compile 为例，其余同理）
server.registerTool("unity_compile", {
  description: "触发 Unity 编译，等待完成，返回编译结果和错误列表",
  inputSchema: {
    timeoutSeconds: z.number().optional().describe("最大等待秒数，默认 60"),
  },
}, async ({ timeoutSeconds }) => {
  return queue.enqueue(async () => {
    const res = await client.post("/editor/compile");
    // ... 等待 WebSocket 事件，返回结果
    return { content: [{ type: "text", text: "Compilation succeeded." }] };
  });
});

const transport = new StdioServerTransport();
await server.connect(transport);
```

**串行队列实现**（防止多 Claude Code 实例并发写入 Unity）：
```typescript
// src/operation-queue.ts
export class OperationQueue {
  private queue: Promise<unknown> = Promise.resolve();

  // 写操作（创建 GameObject、写文件、编译等）入队串行执行
  enqueue<T>(fn: () => Promise<T>): Promise<T> {
    const next = this.queue.then(fn);
    this.queue = next.catch(() => {}); // 不让单次失败阻塞后续
    return next;
  }

  // 读操作（查询层级、获取日志等）直接并发执行
  async read<T>(fn: () => Promise<T>): Promise<T> {
    return fn();
  }
}
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

```bash
claude -p "<任务描述>" \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__*,Read,Edit,Bash" \
  --append-system-prompt "你正在操作一个 Unity 项目，路径为 /path/to/project。
    完成任务后输出 JSON：{\"status\": \"success|failed\", \"summary\": \"...\", \"files_modified\": [...]}" \
  --output-format json \
  --max-turns 30 \
  --max-budget-usd 2.00
```

**关键参数说明**：

| 参数 | 作用 |
|------|------|
| `--allowedTools "mcp__unity-editor__*"` | 只允许 unity MCP 工具，防止 Claude Code 越界操作 |
| `--append-system-prompt` | 注入项目路径、输出格式要求、编码规范等上下文 |
| `--output-format json` | 结构化输出，OpenClaw 可解析 session_id 和 result |
| `--max-turns 30` | 防止无限循环，编译修复循环通常 5-15 轮足够 |
| `--max-budget-usd 2.00` | 单任务花费保护 |
| `--resume <session_id>` | 恢复之前的会话（用于跨任务上下文保持） |

---

### 组件三：OpenClaw SKILL.md 更新

**不需要写新的 OpenClaw Plugin 工具**，只需更新 SKILL.md，告诉 OpenClaw Agent 何时以及如何使用内置的 `exec` 工具调用 Claude Code。

SKILL.md 新增内容：

```markdown
## 派发实现任务给 Claude Code

当用户要求实现具体的游戏功能逻辑时，使用 exec 工具调用 Claude Code 自主完成：

### 适用场景
- 需要编写/修改 C# 脚本
- 需要配置 GameObjects/组件/场景
- 需要调试编译错误
- 任何需要多步骤反复操作 Unity 的实现任务

### 调用模板
exec 工具 command：
claude -p "<详细任务描述>" --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__*,Read,Edit,Bash" \
  --output-format json --max-turns 30

### 解析结果
exec 输出的 JSON 中：
- result 字段：Claude Code 的文字总结
- session_id：可用于后续 --resume 恢复上下文

### 并行任务
对于无依赖关系的模块，可同时启动多个 exec（background: true），
用 process tool 轮询各实例状态，合并结果。
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

## 4. 并行任务与冲突控制

### 4.1 哪些任务可以并行

| 可并行 ✅ | 不可并行 ❌ |
|----------|-----------|
| 各自编写不同的 C# 脚本文件 | 同时修改同一个 .cs 文件 |
| 并行读取场景信息 | 同时触发 Unity 编译 |
| 各自设计不同 GameObject | 同时修改同一个 Scene |
| 生成不同系统的代码 | 同时调用 AssetDatabase.Refresh |

### 4.2 冲突控制机制（双保险）

**保险一：MCP Server 操作队列**

写操作（createGameObject / writeFile / compile 等）在 MCP Server 层自动排队，即使多个 Claude Code 实例并发调用，Unity 操作也会串行执行。读操作（getHierarchy / getConsoleLogs 等）不入队，可以并发。

**保险二：OpenClaw 任务级锁**

OpenClaw 在派发并行任务时，在 `exec` 命令中通过系统提示注入"当前并行任务清单"，让每个 Claude Code 实例知道其他实例在操作哪些文件，主动避开冲突区域：

```bash
--append-system-prompt "当前并行任务：
  #1（你）：实现玩家移动，修改 Assets/Scripts/PlayerMovement.cs
  #2（其他）：实现攻击系统，修改 Assets/Scripts/AttackSystem.cs
  禁止修改对方负责的文件。"
```

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

### 阶段 3：多任务并行验证

**目标**：验证并行任务执行和冲突控制有效。

**测试场景**：同时实现"玩家移动"和"敌人巡逻"两个独立功能，验证两个 Claude Code 实例互不干扰，MCP 操作队列正常工作。

**预计时间**：1-2 天

---

### 阶段 4：SKILL.md 精调与上下文保持

**目标**：优化 OpenClaw 的任务拆解质量和跨任务上下文保持。

- [ ] 更新 SKILL.md，加入 Claude Code 调用模板
- [ ] 实现 session_id 保存和 `--resume` 恢复机制
- [ ] 建立项目状态快照（游戏已实现的功能清单，每次任务完成后更新）

**预计时间**：2-3 天

---

## 6. 技术风险与应对

| 风险 | 概率 | 影响 | 应对方案 |
|------|------|------|---------|
| Claude Code 超过 `--max-turns` 仍未完成 | 中 | 中 | 拆分为更小的子任务，OpenClaw 重新派发 |
| 并发编译导致 Unity 不稳定 | 中 | 高 | MCP 操作队列强制串行化 compile 操作 |
| `exec` 子进程输出格式变化 | 低 | 中 | 使用 `--output-format json` 保证结构稳定性 |
| Claude Code 修改了不该改的文件 | 低 | 高 | `--allowedTools` 限制工具范围，系统提示注明禁区 |
| Unity Plugin 因 Domain Reload 断连 | 中 | 中 | MCP Server 实现自动重连，HTTP 请求失败时重试 |

---

## 7. 最终效果预览

配置完成后，使用体验：

**用户对 OpenClaw 说**：
> 我想做一个 2D 平台跳跃游戏，先实现玩家的基础移动（左右移动 + 跳跃），要有土狼时间和跳跃缓冲，手感要流畅

**OpenClaw 自动执行**：
1. 拆解任务：基础移动脚本 + 土狼时间逻辑 + 跳跃缓冲逻辑
2. 判断可并行：代码可并行写，但 Unity 操作串行执行
3. 启动 Claude Code 实例（exec，background）
4. Claude Code 自主循环：写代码 → 编译 → 修复错误 → 挂载 → 配置参数
5. 返回结果给 OpenClaw，OpenClaw 验收后汇报给用户

**从用户说话到 Unity 里跑起来，全程 0 次人工干预。**

---

*关联文档：`docs/架构设计.md`、`docs/开发进度.md`*  
*下一步行动：完成阶段 0（Unity C# Plugin 集成测试），然后按阶段 1 搭 MCP Server。*

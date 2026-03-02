# FAQ：OpenClaw + Claude Code + Unity MCP 多 Agent 系统

**版本**: 1.1.0
**日期**: 2026-03-02

---

## 架构决策类

**Q：为什么不直接用 OpenClaw Plugin 工具操作 Unity，而要引入 Claude Code 这一层？**

OpenClaw Plugin 工具是"单次操作"的——每次 Agent 调用一个工具，完成一个动作。而"写脚本 → 编译 → 报错 → 修复 → 再编译"这种自修正循环需要一个能自主规划、多步迭代的 Agent。Claude Code 天然具备这种能力，而 OpenClaw 的角色是更高层的游戏设计决策和任务拆解，两者职责清晰。

---

**Q：为什么不并行运行多个 Claude Code 实例？**

Unity 开发的核心瓶颈是编译，每次只能串行执行一次。并行写代码节省的时间，在编译这一步全部归零。更麻烦的是，`stdio` 传输模式下每个 `claude -p` 进程会 spawn 自己的 MCP Server 实例，各进程的操作队列互不可见，无法防止两个实例同时触发编译。修复此问题需改用 HTTP/SSE 长驻 MCP Server，架构复杂度大幅上升，不值得。详见 `../多Agent架构方案分析.md` 第 4 节。

---

**Q：MCP Server 为什么用 stdio 而不是 HTTP/SSE？**

stdio 的优势是**无需长驻进程**，Claude Code 在调用时按需启动 MCP Server，退出时自动清理。对于单实例顺序任务，这完全够用，且不需要额外管理进程生命周期。HTTP/SSE 只有在需要多实例共享状态（如并行任务的操作队列）时才有必要。

---

**Q：为什么保留 openclaw-plugin，而不是只保留 MCP Server？**

两者服务不同的场景：
- `openclaw-plugin`：OpenClaw 直接操作 Unity（无需 Claude Code），适合简单、即时的单步操作（如"告诉我当前场景有什么"）
- `mcp-server`：Claude Code 通过 MCP 操作 Unity，适合需要多步迭代的实现任务（如"写一个有编译自修正循环的脚本功能"）

两者复用相同的底层 HTTP 客户端代码，维护成本不高。

---

## 技术实现类

**Q：`--output-format json` 的输出结构是什么？**

已确认字段（Claude Code v2.1.61，实测）：

```json
{
  "type": "result",
  "subtype": "success",
  "is_error": false,
  "session_id": "fac8c5bf-7108-49f1-b1dc-a6bf6e5c9336",
  "result": "Claude 的文字回复内容",
  "num_turns": 2,
  "duration_ms": 12732,
  "duration_api_ms": 12313,
  "total_cost_usd": 0.0999,
  "stop_reason": null,
  "usage": { "input_tokens": 16, "cache_creation_input_tokens": 23136, "cache_read_input_tokens": 22902, "output_tokens": 415, "..." },
  "permission_denials": []
}
```

关键字段：
- `result`：Claude 的文字输出（OpenClaw 解析此字段）
- `session_id`：会话 ID，传给下一个子任务的 `--resume`
- `is_error`：`true` 表示工具调用失败或超出轮次
- `subtype`：`"success"` / `"error_max_turns"` / `"error_during_execution"` 等
- `total_cost_usd`：本次调用的 API 费用
- `num_turns`：实际使用的轮次数

> **注1**：`subtype: "error_max_turns"` 表示到达 `--max-turns` 上限但任务未完成，此时 OpenClaw 应将任务拆更小后重新派发。
>
> **注2**：首次调用时 `cache_creation_input_tokens` 较大（~23K tokens），会产生较高费用（约 $0.10）。后续调用会命中 cache（`cache_read_input_tokens`），费用大幅降低。

---

**Q：`--resume <session_id>` 能保留多久？**

session 的有效期取决于 Claude Code 的实现，目前没有官方文档明确说明过期时间。作为备用方案，在每次子任务完成后，OpenClaw 应将 Claude Code 的输出摘要保存到项目状态快照文件，通过 `--append-system-prompt` 注入后续任务，即使 session 过期也能恢复上下文。

---

**Q：`compile_complete` 和 `compile_failed` 事件什么时候推送？**

C# Plugin 的 `CompilationListener` 订阅了两个事件：

```csharp
// 收集每个 assembly 的错误（有错误时才触发）
CompilationPipeline.assemblyCompilationFinished += (path, messages) => { ... };

// 全部编译完成后广播汇总结果
CompilationPipeline.compilationFinished += _ => EventBroadcaster.Broadcast(...);
```

`compile_complete`/`compile_failed` 在 `compilationFinished` 后通过 WebSocket 推送，这是阻塞结束的信号。MCP Server 的 `unity_compile` 工具等待此事件再返回。

---

**Q：Domain Reload 期间调用 `unity_compile` 会怎样？**

Domain Reload 会断开 WebSocket 连接，此时 `ws.waitForEvent` 会超时（默认 60s）。`UnityWsClient` 有 2s 自动重连逻辑，但已注册的 `waitForEvent` Promise 在断连时不会自动迁移到新连接。

当前暂不做额外处理（Domain Reload 本身就是编译触发的，正常情况下 `compile_complete` 会在重连前推送）。如果出现超时问题，在 `unity_compile` 工具中捕获超时错误并返回"编译超时，请重试"，让 Claude Code 再次调用。

---

**Q：`--allowedTools "mcp__unity-editor__*"` 的通配符是否被支持？**

`mcp__<server-name>__<tool-name>` 是 Claude Code 中 MCP 工具的命名格式，`*` 通配符**需要实测确认**是否支持。如果不支持，使用以下完整列表（共 19 个工具）：

```bash
--allowedTools "mcp__unity-editor__unity_check_status,mcp__unity-editor__unity_get_scene_info,mcp__unity-editor__unity_get_hierarchy,mcp__unity-editor__unity_save_scene,mcp__unity-editor__unity_create_gameobject,mcp__unity-editor__unity_delete_gameobject,mcp__unity-editor__unity_set_transform,mcp__unity-editor__unity_find_gameobjects,mcp__unity-editor__unity_get_components,mcp__unity-editor__unity_add_component,mcp__unity-editor__unity_set_component_property,mcp__unity-editor__unity_read_file,mcp__unity-editor__unity_write_file,mcp__unity-editor__unity_compile,mcp__unity-editor__unity_get_compile_errors,mcp__unity-editor__unity_get_console_logs,mcp__unity-editor__unity_get_project_info,mcp__unity-editor__unity_get_scripts,mcp__unity-editor__unity_find_assets"
```

---

**Q：`--max-budget-usd` 参数是否可用？**

**已确认可用**（Claude Code v2.1.61）。`--max-budget-usd <amount>` 限制单次任务的 API 费用上限，与 `--max-turns` 配合使用效果更好：

```bash
claude -p "..." \
  --max-turns 30 \
  --max-budget-usd 0.50   # 单次任务最多消耗 $0.50
```

建议在生产环境中两者都加，双重防护。

---

**Q：OpenClaw 的 `sessions_spawn` 工具和 `exec + claude -p` 有什么区别？**

`sessions_spawn` 是 OpenClaw 原生的 Agent 会话管理工具（在 ClawHub 上的 skill 文档中有提及）。目前尚未调研其具体接口。如果它能直接管理 Claude Code 子 Agent 的生命周期（包括传递 MCP 配置），可能比 `exec + claude -p` 更稳定。待查阅 OpenClaw 官方文档后决定是否切换。

---

## 使用与调试类

**Q：导入插件后 Unity 报 `Newtonsoft.Json` 相关编译错误怎么办？**

插件依赖 `com.unity.nuget.newtonsoft-json` 包。导入后，`DependencyInstaller.cs`（在独立的 `OpenClawUnityPlugin.Setup` Assembly 中）会在下一帧自动调用 `PackageManager.Client.Add()` 安装该包，并在 Console 打印进度。

- **等待 Console 出现** `[OpenClaw Unity Plugin] ✅ '...newtonsoft-json' installed successfully.`，随后 Unity 自动重新编译，错误消失。
- **如果自动安装失败**（网络不通或权限问题）：Console 会打印错误并附带手动安装步骤：`Window → Package Manager → '+' → Add package by name → com.unity.nuget.newtonsoft-json`

> **设计要点**：`DependencyInstaller.cs` 所在的 `OpenClawUnityPlugin.Setup` Assembly 不引用任何 Newtonsoft.Json 类型，因此即使主 Assembly 因缺少该包而编译失败，Setup Assembly 仍然能正常运行并完成安装。

---

**Q：运行 `install.sh` 后，`claude -p ... --mcp-config` 报找不到 MCP Server 怎么办？**

检查以下几点：
1. `~/.claude/unity-mcp-config.json` 中的 `args` 路径是否正确（指向 `dist/index.js`）
2. `mcp-server/dist/index.js` 是否存在（执行 `npm run build`）
3. `node` 是否在 PATH 中（`command -v node`）

---

**Q：`curl http://localhost:23456/api/v1/status` 返回 `400 Bad Request (Invalid host)`，但 `127.0.0.1` 正常？**

这是 .NET `HttpListener` 的 **Host 头校验机制**导致的。`HttpListener` 会校验每个 HTTP 请求的 `Host` 头是否与注册的 Prefix 完全匹配。

原因链路：
1. `HttpServer.cs` 注册的 Prefix 是 `http://127.0.0.1:23456/`
2. 用 `localhost` 访问时，curl 发送的请求头是 `Host: localhost:23456`
3. `localhost ≠ 127.0.0.1` → `HttpListener` 直接返回 `400 Bad Request (Invalid host)`
4. 注意这不是连接失败（`Connection refused`），TCP 连接是成功的，是应用层拒绝

**修复**（已在 `HttpServer.cs` 中更新）：同时注册两个 Prefix：
```csharp
_listener.Prefixes.Add($"http://127.0.0.1:{Port}/");
_listener.Prefixes.Add($"http://localhost:{Port}/");  // 新增
```

**临时绕过**：在修复版本之前，所有验证命令统一用 `127.0.0.1`（MCP Server TypeScript 代码已正确使用 `127.0.0.1`，不受此问题影响）。

---

**Q：`wscat: command not found` 怎么解决？**

`wscat` 是一个 npm 工具，需要全局安装：

```bash
npm install -g wscat
```

安装后使用：
```bash
wscat -c ws://127.0.0.1:23457/ws
```

连接后在 Unity 中修改场景（如移动一个 GameObject），即可在终端看到 WebSocket 事件推送。

---

**Q：`unity_compile` 工具调用后一直等待，没有返回怎么办？**

可能原因：
1. **WebSocket 未连接**：MCP Server 启动时 Unity 未运行，WS 连接失败。此时调用 `unity_check_status` 会先报错，能提前发现问题
2. **编译事件未推送**：Unity 没有触发 Domain Reload（文件没有实际变化时不重新编译）。确认脚本内容确实修改了
3. **超时时间过短**：大型项目编译可能超过 60s，调用时传入 `timeoutSeconds: 120`

---

**Q：Claude Code 修改了 Assets/ 外的文件怎么办？**

使用 `--allowedTools "mcp__unity-editor__*,Read,Edit,Bash"` 限定工具范围，同时在 `--append-system-prompt` 中明确：

```
所有文件操作必须限制在 Assets/ 目录内。禁止修改 ProjectSettings/、Packages/、Library/ 下的任何文件。
```

如果已经发生误修改，通过 `git checkout` 还原（建议在开始前确保工程有 git 记录）。

---

**Q：AI 创建了 GameObject 但场景里只有空节点，脚本也没挂上去，怎么回事？**

两个根本原因：

**原因 1：`unity_create_gameobject` 没有指定 `primitive`**
不带 `primitive` 参数时创建的是纯空 GameObject（没有 Mesh、Collider）。需要显式传入：
```
unity_create_gameobject("Paddle", primitive: "Cube")   ← 会自动附带 BoxCollider + MeshRenderer
unity_create_gameobject("Ball",   primitive: "Sphere")
```

**原因 2：缺少 `unity_add_component` 工具（已修复）**
此前 MCP Server 没有暴露 C# 后端已有的组件操作接口。现已新增三个工具：
- `unity_get_components(path)` — 查看 GameObject 当前的组件列表
- `unity_add_component(path, componentType)` — 挂载脚本或组件，如 `"Paddle"`、`"Rigidbody2D"`
- `unity_set_component_property(path, componentType, properties)` — 设置组件字段值，如 `{ "speed": 8.0 }`

**正确的游戏对象创建流程**：
```
1. unity_create_gameobject("Paddle", primitive: "Cube")  ← 有形状、有 Collider
2. unity_write_file("Assets/Scripts/Paddle.cs", ...)     ← 写脚本
3. unity_compile()                                        ← 等编译成功
4. unity_add_component("Paddle", "Paddle")               ← 挂脚本
5. unity_set_component_property("Paddle", "Paddle", {"speed": 8.0})  ← 设参数
6. unity_save_scene()
```

---

**Q：`unity_add_component` 传组件名时用全名还是短名？**

两者都支持，按优先级：
1. **短名**（推荐）：`"Paddle"`、`"Rigidbody2D"`、`"BoxCollider"` — 搜索所有已加载程序集
2. **全名**：`"UnityEngine.Rigidbody2D"`、`"MyNamespace.Paddle"` — 直接精确匹配

若短名匹配到多个类型（命名冲突），使用带命名空间的全名。

---

**Q：子任务超过 30 轮还没完成，怎么处理？**

不要增大 `--max-turns`。正确做法：

1. 检查任务描述是否过于宽泛（如"实现完整的战斗系统"应拆成多个子任务）
2. 查看当前已完成的进度（通过 `--resume` 继续或从 JSON 输出中读取 summary）
3. 将剩余工作拆成更小的子任务重新派发，用 `--resume` 保持上下文连续

---

**Q：为什么 MCP Server 用 ESM（`"type": "module"`），而 openclaw-plugin 用 CommonJS？**

`@modelcontextprotocol/sdk` 的内部导入路径带 `.js` 扩展名（如 `@modelcontextprotocol/sdk/server/mcp.js`），这是 ESM 的规范，在 CommonJS + `moduleResolution: node` 下无法正确解析。因此 MCP Server 必须使用 ESM：

```json
{
  "type": "module",
  "compilerOptions": {
    "module": "Node16",
    "moduleResolution": "Node16"
  }
}
```

同时，MCP Server 内部的所有相对导入也必须带 `.js` 扩展名（即使源文件是 `.ts`）：

```typescript
import { UnityClient } from "./unity-client.js";  // ✅
import { UnityClient } from "./unity-client";      // ❌ Node16 ESM 下会报错
```

---

**Q：MCP Server `inputSchema` 应该用 zod 还是 JSON Schema？**

推荐用 zod。`@modelcontextprotocol/sdk` 原生支持 zod 的 `ZodRawShape`（即对象各字段的 zod 类型），运行时自动转换为 JSON Schema 传给 Claude Code。

```typescript
// ✅ 用 zod（推荐）
server.registerTool("unity_get_hierarchy", {
  description: "...",
  inputSchema: {
    depth:    z.number().optional().describe("Max depth"),
    maxNodes: z.number().optional().describe("Max nodes"),
  },
}, async ({ depth, maxNodes }) => { ... });

// 无参数的工具传空对象
server.registerTool("unity_save_scene", {
  description: "...",
  inputSchema: {},
}, async () => { ... });
```

---

**Q：MCP Server 工具返回 `content` 数组时，`type` 字段为什么要写 `"text" as const`？**

TypeScript 会将字符串字面量 `"text"` 推断为 `string` 类型，而 MCP SDK 要求 `type` 是精确的字面量联合类型 `"text" | "image" | "resource"`。加 `as const` 避免类型不匹配编译错误：

```typescript
return { content: [{ type: "text" as const, text: "..." }] };
```

---

## 运行环境类

**Q：支持哪些 Unity 版本？**

- Unity 2022.3 LTS+：内置 WebSocket 支持，无需额外依赖
- Unity 2021.3 LTS：需要安装 websocket-sharp，C# Plugin 中有 `#if UNITY_2022_3_OR_NEWER` 条件编译

---

**Q：MCP Server 需要什么 Node.js 版本？**

Node.js 18+ （支持原生 `fetch` API，`unity-client.ts` 中使用了 `fetch`）。

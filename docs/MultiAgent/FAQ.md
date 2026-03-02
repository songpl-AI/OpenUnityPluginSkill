# FAQ：OpenClaw + Claude Code + Unity MCP 多 Agent 系统

**版本**: 1.0.0
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

架构文档中假设包含 `session_id` 和 `result` 字段，但**需要实测确认**。运行以下命令查看：

```bash
claude -p "说一句你好" --output-format json --max-turns 1
```

记录实际字段名，更新 SKILL.md 中的解析说明。

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

`mcp__<server-name>__<tool-name>` 是 Claude Code 中 MCP 工具的命名格式，`*` 通配符**需要实测确认**是否支持。如果不支持，需要逐一列出工具名：

```bash
--allowedTools "mcp__unity-editor__unity_check_status,mcp__unity-editor__unity_get_hierarchy,..."
```

---

**Q：`--max-budget-usd` 参数是否可用？**

架构调研时列入的参数，但**未经实测验证**。如果 Claude Code CLI 不支持此参数会报错。建议先不使用，以 `--max-turns 30` 作为主要保护手段。

---

**Q：OpenClaw 的 `sessions_spawn` 工具和 `exec + claude -p` 有什么区别？**

`sessions_spawn` 是 OpenClaw 原生的 Agent 会话管理工具（在 ClawHub 上的 skill 文档中有提及）。目前尚未调研其具体接口。如果它能直接管理 Claude Code 子 Agent 的生命周期（包括传递 MCP 配置），可能比 `exec + claude -p` 更稳定。待查阅 OpenClaw 官方文档后决定是否切换。

---

## 使用与调试类

**Q：运行 `install.sh` 后，`claude -p ... --mcp-config` 报找不到 MCP Server 怎么办？**

检查以下几点：
1. `~/.claude/unity-mcp-config.json` 中的 `args` 路径是否正确（指向 `dist/index.js`）
2. `mcp-server/dist/index.js` 是否存在（执行 `npm run build`）
3. `node` 是否在 PATH 中（`command -v node`）

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

**Q：子任务超过 30 轮还没完成，怎么处理？**

不要增大 `--max-turns`。正确做法：

1. 检查任务描述是否过于宽泛（如"实现完整的战斗系统"应拆成多个子任务）
2. 查看当前已完成的进度（通过 `--resume` 继续或从 JSON 输出中读取 summary）
3. 将剩余工作拆成更小的子任务重新派发，用 `--resume` 保持上下文连续

---

## 运行环境类

**Q：支持哪些 Unity 版本？**

- Unity 2022.3 LTS+：内置 WebSocket 支持，无需额外依赖
- Unity 2021.3 LTS：需要安装 websocket-sharp，C# Plugin 中有 `#if UNITY_2022_3_OR_NEWER` 条件编译

---

**Q：MCP Server 需要什么 Node.js 版本？**

Node.js 18+ （支持原生 `fetch` API，`unity-client.ts` 中使用了 `fetch`）。

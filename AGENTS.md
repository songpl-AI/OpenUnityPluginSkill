# AGENTS.md — OpenClaw Unity Editor Plugin

本文件供 AI 助手在每次会话时自动读取，包含项目约束、开发规范和关键决策摘要。
详细内容见 `docs/` 目录，本文件只做精简摘要和指针。

---

## 项目概况

为 [OpenClaw](https://docs.openclaw.ai/) 开发一个 Unity Editor 集成插件，使 AI Agent 能通过自然语言驱动 Unity Editor 执行操作。

**两个独立子项目，规范不同，不得混用：**

```
项目根/
├── AGENTS.md                  ← 本文件
├── docs/                      ← 设计文档（非代码）
├── unity-editor-mcp/          ← 独立发布包（MCP Server + Unity Plugin）
│   ├── mcp-server/            ← Node.js MCP Server（30 个工具）
│   └── unity-plugin/          ← Unity C# 插件（UPM 格式）
│       └── Editor/
└── openclaw-plugin/           ← OpenClaw 专属封装层
    └── skills/unity-editor/
        └── SKILL.md
```

---

## Unity C# 插件规范

### 强制规则

**主线程安全** — 所有 Unity Editor API 调用必须通过 `MainThreadDispatcher.Dispatch()` 提交，绝不在 HTTP 后台线程直接调用。

```csharp
// ❌ 禁止
var go = GameObject.Find("Player");

// ✅ 必须
var go = MainThreadDispatcher.Dispatch(() => GameObject.Find("Player"));
```

**Editor-only 目录** — 所有代码必须位于 `Assets/.../Editor/` 目录下，不得放在 Runtime 目录，否则会被打包进 Player Build。

**Domain Reload 清理** — 任何持有资源的静态类必须注册 `AssemblyReloadEvents.beforeAssemblyReload` 进行清理，防止端口占用和内存泄漏。

**Undo 支持** — 所有修改场景的操作必须注册 Undo（`Undo.RegisterCreatedObjectUndo`、`Undo.RecordObject` 等），不得绕过。

**不直接序列化 Unity 类型** — `Vector3`、`Transform`、`GameObject` 等 Unity 类型不得直接传给 `JsonConvert.SerializeObject`，必须转为 DTO。

### 代码风格

- 命名：PascalCase 类名，camelCase 私有字段加 `_` 前缀（`_listener`）
- 错误返回：统一使用 `ApiResponse.Error(code, message)`，不得在 Handler 中直接 throw 到 HTTP 层
- 日志：使用 `Debug.Log("[OpenClaw] ...")` 前缀，便于 Console 过滤

### API 响应格式（不得改变）

```json
{ "ok": true,  "data": { ... }, "error": null }
{ "ok": false, "data": null,    "error": { "code": "ERROR_CODE", "message": "..." } }
```

错误码见 `docs/技术分析.md` § 2.2。

---

## MCP Server (TypeScript) 规范

> 适用于 `unity-editor-mcp/mcp-server/src/`，也适用于 `openclaw-plugin/src/`（工具逻辑相同，注册 API 不同）。

### 强制规则

**工具命名** — 所有工具名以 `unity_` 前缀开头，使用 `snake_case`（如 `unity_get_hierarchy`）。

**错误抛出而非返回** — MCP handler 内部用 `throw new Error(...)` 报错，SDK 自动将其转为 MCP 错误响应。不得 `return` 错误字符串。

**MCP handler 正确返回格式：**

```typescript
// ✅ MCP Server 正确模式（unity-editor-mcp/mcp-server/src/tools/*.ts）
server.tool("unity_get_hierarchy", { description: "...", inputSchema: {} }, async () => {
  const res = await client.get("/api/v1/scene/hierarchy");
  if (!res.ok) throw new Error(res.error?.message ?? "Unknown error");
  return { content: [{ type: "text", text: formatHierarchy(res.data) }] };
});
```

**输出格式化** — 工具返回文本给 Agent 读，必须经过 `utils/format.ts` 格式化，不得直接返回原始 JSON 字符串。

### 代码风格

- 严格 TypeScript，不使用 `any`（临时类型声明除外，需加 `// TODO: replace with SDK types` 注释）
- 每个工具独立文件，放在 `src/tools/` 下，按功能分类（`scene.ts`、`gameobject.ts` 等）
- 异步函数统一使用 `async/await`，不使用 `.then()` 链式调用

---

## OpenClaw Skill 规范

> 仅适用于 `openclaw-plugin/skills/unity-editor/SKILL.md`。

**触发描述要"主动"** — `SKILL.md` 的 `description` 字段需覆盖用户可能说的各种表达，避免 undertrigger。参考 `docs/架构设计.md` § 2.4。

**工具文档分层** — `SKILL.md` 正文只放工具概览（< 500 行），详细参数放 `references/tools-reference.md`，在正文中明确指向。

**Gating 配置必须存在** — `metadata` 字段的 `requires.config` 必须包含 Plugin 启用条件，确保 Plugin 未启用时 Skill 不对 Agent 可见。

---

## 架构决策（不得推翻，如需变更先更新文档）

| 决策 | 结论 | 文档位置 |
|------|------|----------|
| 通信协议 | HTTP REST（主动操作）+ WebSocket（事件推送）双协议，同端口同期实现 | `docs/架构设计.md` § 6.3 |
| WebSocket 兼容 | Unity 2022.3+：.NET 内置；Unity 2021.3：websocket-sharp（MIT）；条件编译隔离 | `docs/技术分析.md` § 2.1 |
| 非文本资产检索 | 关键字 + 目录约定 + 元数据过滤，不引入向量数据库 | `docs/技术分析.md` § 3.8 |
| 服务器绑定 | 仅 `127.0.0.1`，不暴露外网 | `docs/技术分析.md` § 5 |
| GameObject 标识 | 使用场景层级路径，不使用 InstanceID | `docs/技术分析.md` § 3.4 |
| 资产路径限制 | 所有路径必须在 `Assets/` 目录下 | `docs/技术分析.md` § 5 |
| Unity 兼容版本 | 2021.3 LTS 及以上 | `docs/设计文档.md` § 3 |

---

## 当前开发阶段

**功能已稳定，已剥离为独立发布包 `unity-editor-mcp/`，支持 Claude Code / Cursor / Claude Desktop / Continue。**

进度详情：`docs/开发进度.md`
插件路径：`unity-editor-mcp/unity-plugin/Editor/`（复制到 Unity 工程 Assets/ 或通过 UPM 安装）

---

## 遇到问题先查

`docs/FAQ.md` — 已收录 20 个常见问题和解决方案，覆盖主线程、Domain Reload、连接失败、Skill 触发等高频问题。

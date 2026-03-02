# OpenClaw Unity Editor MCP Server

让 Claude Code 通过 MCP 协议直接操作 Unity Editor —— 创建 GameObject、编写脚本、管理场景、触发编译，全部通过自然语言完成。

## 架构概览

```
OpenClaw / 用户
    ↓ 自然语言
SKILL.md (unity-editor skill)
    ↓ exec
Claude Code  ←→  MCP Server (本目录)  ←→  Unity Editor (HTTP :23456 / WS :23457)
```

MCP Server 是中间层，把 Claude Code 的工具调用翻译成对 Unity 内置 HTTP Server 的 REST 请求。

---

## 环境要求

| 依赖 | 最低版本 | 说明 |
|------|----------|------|
| Unity Editor | 2021.3 LTS | 支持 2021.3 / 2022.3 / 6000.x |
| Node.js | 18+ | `node --version` 确认 |
| npm | 8+ | 随 Node.js 附带 |
| Claude Code CLI | 最新 | `claude --version` 确认 |

---

## 安装步骤

### 第一步：安装 Unity C# 插件

将插件目录复制到你的 Unity 项目：

```bash
cp -r unity-plugin/Assets/OpenClawUnityPlugin  /path/to/YourUnityProject/Assets/
```

> 把 `/path/to/YourUnityProject` 替换为你的实际 Unity 项目路径。

然后在 Unity 项目的 `Packages/manifest.json` 中添加 Newtonsoft.Json 依赖（如果还没有）：

```json
{
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    ...
  }
}
```

打开 Unity Editor，等待编译完成。Console 中看到以下日志表示插件启动成功：

```
[OpenClaw] Starting Unity Editor Plugin...
[OpenClaw] Plugin ready. HTTP: http://127.0.0.1:23456/api/v1  WS: ws://127.0.0.1:23457/ws
[OpenClaw] Registered 30 routes.
```

**Unity 2021.3 LTS 额外步骤（WebSocket 支持）：**

Unity 2022.3+ 内置 WebSocket 无需额外操作。2021.3 需手动安装 websocket-sharp：

1. 下载 `websocket-sharp.dll`（NuGet 或 GitHub releases）
2. 放入 `Assets/OpenClawUnityPlugin/Plugins/` 目录

### 第二步：构建 MCP Server

```bash
cd mcp-server
npm install
npm run build
```

构建成功后会生成 `dist/index.js`。

### 第三步：生成 Claude Code 配置

**方式 A（推荐）：使用一键安装脚本**

```bash
bash openclaw-plugin/skills/unity-editor/install.sh
```

脚本会自动构建并生成 `~/.claude/unity-mcp-config.json`。

**方式 B：手动配置**

将 `claude-mcp-config.template.json` 复制并替换路径：

```bash
MCP_SERVER_DIR="$(pwd)/mcp-server"
sed "s|__MCP_SERVER_PATH__|$MCP_SERVER_DIR|g" \
  mcp-server/claude-mcp-config.template.json > ~/.claude/unity-mcp-config.json
```

生成的配置文件内容示例：

```json
{
  "mcpServers": {
    "unity-editor": {
      "command": "node",
      "args": ["/absolute/path/to/mcp-server/dist/index.js"],
      "env": {
        "UNITY_PORT": "23456",
        "UNITY_WS_PORT": "23457"
      }
    }
  }
}
```

---

## 验证安装

### 1. 验证 Unity 插件是否运行

```bash
curl http://127.0.0.1:23456/api/v1/status
```

期望返回：

```json
{
  "ok": true,
  "data": {
    "status": "ready",
    "unityVersion": "6000.3.10f1",
    "isCompiling": false,
    ...
  }
}
```

> 注意：必须使用 `127.0.0.1`，不能用 `localhost`（会返回 400）。

### 2. 验证 MCP Server 可被 Claude Code 调用

```bash
claude -p "用 unity_check_status 检查 Unity 状态" \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__unity_check_status" \
  --max-turns 3
```

期望输出包含 Unity 版本和当前场景名称。

### 3. 端对端功能测试

```bash
# 测试带路径参数的路由（曾经是已知 bug，现已修复）
curl "http://127.0.0.1:23456/api/v1/gameobject/Main Camera/components"
# 期望返回: {"ok":true,"data":{"path":"Main Camera","components":[...]}}

# 测试场景信息
curl "http://127.0.0.1:23456/api/v1/scene/info"

# 测试层级结构
curl "http://127.0.0.1:23456/api/v1/scene/hierarchy"
```

---

## 可用工具列表

| 工具名 | 功能 |
|--------|------|
| `unity_check_status` | 检查 Unity Editor 连接状态 |
| `unity_get_scene_info` | 获取当前场景基本信息 |
| `unity_get_hierarchy` | 获取场景层级结构 |
| `unity_save_scene` | 保存当前场景 |
| `unity_create_gameobject` | 创建 GameObject（可指定父节点、位置）|
| `unity_delete_gameobject` | 删除 GameObject |
| `unity_set_transform` | 设置 GameObject 的位置/旋转/缩放 |
| `unity_find_gameobjects` | 按名称或标签查找 GameObject |
| `unity_get_components` | 获取 GameObject 上挂载的组件列表 |
| `unity_add_component` | 为 GameObject 添加组件 |
| `unity_set_component_property` | 设置组件属性值 |
| `unity_read_file` | 读取 Assets/ 内的文件 |
| `unity_write_file` | 写入 Assets/ 内的文件（自动创建目录）|
| `unity_compile` | 触发编译，等待结果（含 WebSocket 事件）|
| `unity_get_compile_errors` | 获取上次编译的错误列表 |
| `unity_get_console_logs` | 获取 Console 日志（含后台线程日志）|
| `unity_get_project_info` | 获取项目名称、Unity 版本等基本信息 |
| `unity_get_scripts` | 列出项目中的所有 C# 脚本 |
| `unity_find_assets` | 按类型或关键字搜索资产 |

---

## 端口说明

| 服务 | 默认端口 | 环境变量 | 说明 |
|------|----------|----------|------|
| HTTP REST | 23456 | `UNITY_PORT` | 主操作接口 |
| WebSocket | 23457 | `UNITY_WS_PORT` | 事件推送（编译完成、日志等）|

端口冲突时 Unity 会自动递增（23456 → 23457 → 23458 ...）。若端口变化，需同步更新 `~/.claude/unity-mcp-config.json` 中的 `UNITY_PORT`/`UNITY_WS_PORT`，以及 `SKILL.md` 中的 MCP 配置路径。

查看实际端口：
```bash
curl http://127.0.0.1:23456/api/v1/status | grep -E "httpPort|wsPort"
# 如端口变了，改用: curl http://127.0.0.1:23457/api/v1/status
```

---

## 故障排除

### curl 返回空内容或连接被拒

- 确认 Unity Editor 正在运行
- 确认 Console 中有 `[OpenClaw] Plugin ready.` 日志
- 使用 `127.0.0.1` 而非 `localhost`
- 检查 Unity Console 是否有编译错误阻止了插件加载

### Newtonsoft.Json 找不到

打开 Unity，菜单 **Window > Package Manager**，搜索 `Newtonsoft Json`（com.unity.nuget.newtonsoft-json）并安装 3.2.1+。

### 带路径参数的 API 返回 404

例如 `/api/v1/gameobject/Cube/components` 返回 `NOT_FOUND`。

这是已知 bug（见 `BUG_REPORT.md`），已在当前版本修复。如仍复现，请检查 `RequestRouter.cs` 的 `RouteEntry` 构造函数中是否存在 `.Replace("/", "\\/")` 调用（需删除）。

### Domain Reload 后插件未重启

Unity 重新编译（Domain Reload）后，`[InitializeOnLoad]` 会自动重启插件。如果 Console 没有出现 `[OpenClaw] Starting...` 日志，尝试：
1. 手动菜单 **Assets > Reimport All**
2. 或强制删除 `Library/ScriptAssemblies/` 目录后重新打开 Unity

### MCP Server 构建失败

```bash
# 确认 Node.js 版本
node --version  # 需要 18+

# 清除缓存后重新构建
cd mcp-server
rm -rf node_modules dist
npm install
npm run build
```

### unity_compile 超时

编译默认等待 30 秒。大型项目可能需要更长时间。检查 Unity Console 确认编译是否完成。

---

## 目录结构

```
mcp-server/
├── src/
│   ├── index.ts              # 入口：注册所有工具，连接 stdio 传输
│   ├── unity-client.ts       # HTTP 客户端（与 Unity REST API 通信）
│   ├── unity-ws-client.ts    # WebSocket 客户端（接收编译/日志事件）
│   └── tools/
│       ├── scene.ts          # 场景相关工具
│       ├── gameobject.ts     # GameObject 相关工具
│       ├── file.ts           # 文件读写工具
│       ├── compile.ts        # 编译触发和错误查询
│       └── project.ts        # 项目信息和资产查询
├── dist/                     # 编译输出（npm run build 生成）
├── claude-mcp-config.template.json  # MCP 配置模板
├── package.json
└── tsconfig.json
```

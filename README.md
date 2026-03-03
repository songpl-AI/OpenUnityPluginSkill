# OpenClaw Unity Editor Plugin

让 AI Agent 通过自然语言控制 Unity Editor：读写场景、修改脚本、编译反馈、资源查询。

> **本 README 为 OpenClaw 集成安装指南。**
> 如需在 Claude Code / Cursor / Claude Desktop 等 MCP 客户端中独立使用，请参阅 [`unity-editor-mcp/README.md`](./unity-editor-mcp/README.md)。

---

## 环境要求

| 组件 | 最低版本 |
|------|---------|
| Unity Editor | 2020.3 LTS+ |
| OpenClaw | 2026.x |
| Node.js | 18+ |
| npm | 8+ |

---

## 安装

### 第一步：安装 Unity C# 插件

**1.1 复制插件文件**

将 `unity-editor-mcp/unity-mcp-plugin/Editor/` 整个文件夹复制到你的 Unity 工程的 `Assets/` 目录下：

```
Assets/
└── OpenClawUnityPlugin/        ← 复制到这里
    └── Editor/
        ├── Core/
        ├── Handlers/
        ├── Models/
        ├── UnityEditorServer.cs
        └── OpenClawUnityPlugin.Editor.asmdef
```

> 或通过 UPM Git URL 安装，详见 `unity-editor-mcp/README.md`。

**1.2 安装 Newtonsoft.Json**

打开 `你的工程/Packages/manifest.json`，在 `dependencies` 中加入：

```json
{
  "dependencies": {
    "com.unity.nuget.newtonsoft-json": "3.2.1",
    ...
  }
}
```

> Unity 2020.3 / 2021.x 需额外安装 websocket-sharp，见 [Unity 2020.3 / 2021.x 补充说明](#unity-2020-3--2021x-补充说明)。

**1.3 重新打开 Unity**

Unity 会自动：
1. 下载 Newtonsoft.Json
2. 编译 `OpenClawUnityPlugin.Editor`
3. 启动 HTTP 服务（端口 23456）和 WebSocket 服务（端口 23457）

Console 中应出现：

```
[OpenClaw] HTTP server started on port 23456
[OpenClaw] WebSocket server started on port 23457
```

**1.4 验证 HTTP 服务**

```bash
curl -s http://127.0.0.1:23456/api/v1/status | python3 -m json.tool
```

预期响应：

```json
{
  "ok": true,
  "data": {
    "unityVersion": "6000.0.46f1",
    "isPlaying": false,
    "isCompiling": false,
    "httpPort": 23456,
    "wsPort": 23457
  }
}
```

---

### 第二步：安装 OpenClaw TypeScript 插件

**2.1 构建**

```bash
cd openclaw-plugin
npm install
npm run build
```

**2.2 安装到 OpenClaw**

```bash
# 软链接安装（推荐开发阶段使用，修改代码后重新 build 即可生效）
openclaw plugins install --link /path/to/openclaw-plugin

# 或复制安装（生产使用）
openclaw plugins install /path/to/openclaw-plugin
```

> 把 `/path/to/openclaw-plugin` 替换为实际绝对路径，如：
> `/Users/your-name/projects/openclaw-unity/openclaw-plugin`

**2.3 启用插件**

```bash
openclaw plugins enable unity-editor
```

然后在 OpenClaw 配置中确认端口（默认值通常无需修改）：

```bash
openclaw config set plugins.unity-editor.port 23456
openclaw config set plugins.unity-editor.wsPort 23457
```

**2.4 重启 OpenClaw**

```bash
# 停止当前运行实例（如有）再重启，或执行
openclaw restart
```

---

## 验证端到端

打开 Unity（已加载场景），在 OpenClaw 聊天渠道中发送：

```
查看当前 Unity 场景有哪些 GameObject
```

Agent 应调用 `unity_get_hierarchy` 并返回场景树。

---

## 可用工具

| 工具 | 功能 |
|------|------|
| `unity_check_status` | 检查 Unity Editor 是否运行、插件是否活跃 |
| `unity_get_scene_info` | 当前场景名称、路径、是否有未保存改动 |
| `unity_get_hierarchy` | 完整 GameObject 树 |
| `unity_save_scene` | 保存场景 |
| `unity_create_gameobject` | 在场景中创建对象（支持 tag 参数）|
| `unity_delete_gameobject` | 删除对象 |
| `unity_set_transform` | 设置位置/旋转/缩放 |
| `unity_find_gameobjects` | 按名称或 Tag 查找对象 |
| `unity_get_components` | 获取 GameObject 上的组件列表 |
| `unity_add_component` | 添加组件到 GameObject |
| `unity_set_component_property` | 设置组件属性值 |
| `unity_read_file` | 读取 Assets/ 内任意文件 |
| `unity_write_file` | 写入/覆盖 Assets/ 内文件 |
| `unity_compile` | 触发编译，通过 WebSocket 等待结果 |
| `unity_get_compile_errors` | 获取最近一次编译错误列表 |
| `unity_get_console_logs` | 获取 Unity Console 输出 |
| `unity_get_project_info` | 项目名、Unity 版本、已安装包 |
| `unity_get_scripts` | 列出所有脚本类及公开 API |
| `unity_find_assets` | 按类型/名称搜索资产 |
| `unity_get_tags` | 获取项目中所有已定义 Tag |
| `unity_create_tag` | 创建新 Tag |
| `unity_set_gameobject_tag` | 为 GameObject 设置 Tag |
| `unity_get_input_system_type` | 检测输入系统类型（legacy/new/both）|
| `unity_get_player_settings` | 获取 Player Settings 关键配置 |
| `unity_get_render_pipeline` | 检测渲染管线（Built-in/URP/HDRP）|
| `unity_get_material_properties` | 查看材质所有属性及当前值 |
| `unity_set_material_properties` | 设置材质属性（颜色/float/贴图等）|
| `unity_assign_material` | 将材质赋给场景中 Renderer |
| `unity_list_packages` | 列出已安装的 Unity 包 |
| `unity_install_package` | 安装 Unity 包 |
| `unity_remove_package` | 卸载 Unity 包 |

---

## 代码自修正循环

当 AI 写或修复 C# 脚本时，会自动执行以下循环：

```
1. unity_get_scripts        ← 了解现有代码结构
2. unity_write_file         ← 写入脚本
3. unity_compile            ← 触发编译，等待 WebSocket 结果
4. 有错误 → unity_read_file + unity_write_file ← 修正并重复
5. unity_get_console_logs   ← 进入 Play Mode 后检查运行时行为
```

---

## Unity 2020.3 / 2021.x 补充说明

Unity 2020.3 和 2021.x 的内置 HTTP 不支持 WebSocket，需要额外安装 `websocket-sharp`：

1. 下载 [websocket-sharp.dll](https://github.com/sta/websocket-sharp)（MIT License，~200KB）
2. 放入 `Assets/OpenClawUnityPlugin/Plugins/` 目录
3. 在 Player Settings → Scripting Define Symbols 中添加：`WEBSOCKET_SHARP`

Unity 2022.3 及以上无需此步骤，使用 .NET 内置实现。

---

## 端口冲突处理

如果 23456 端口已被占用，插件会自动递增尝试（23456 → 23457 → 23458...），实际端口会打印到 Console。
WebSocket 端口始终 = HTTP 端口 + 1。

如果端口不是默认值，需相应更新 OpenClaw 配置：

```bash
openclaw config set plugins.unity-editor.port <实际HTTP端口>
openclaw config set plugins.unity-editor.wsPort <实际WS端口>
```

---

## 常见问题

**Q: curl 返回空内容 / Connection refused**
A: Unity 没有运行，或插件没有正确安装。检查 Unity Console 中是否有 `[OpenClaw] HTTP server started` 日志。

**Q: Unity Console 报 `Newtonsoft.Json not found`**
A: `manifest.json` 中 `com.unity.nuget.newtonsoft-json` 没有正确添加，或 Unity 尚未完成包下载。

**Q: `unity_compile` 超时**
A: WebSocket 未连接。检查 OpenClaw 配置中 `wsPort` 是否与 Unity Console 中显示的端口一致。

**Q: `unity_get_scripts` 返回 `degraded: true`**
A: 当前有编译错误，先调用 `unity_get_compile_errors` 查看错误，修复后再调用。

更多问题见 `docs/FAQ.md`。

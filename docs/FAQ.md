# Unity Editor OpenClaw Plugin — FAQ

**用途**: 记录开发过程中遇到的问题、排查过程和解决方案，防止重复踩坑。
**维护规则**: 遇到问题先查这里；解决后及时补充到对应分类；已过时的条目标记 `[已过时]`。

---

## 目录

- [Unity C# Plugin](#unity-c-plugin)
- [OpenClaw Plugin (TypeScript)](#openclaw-plugin-typescript)
- [通信 & 连接](#通信--连接)
- [Skill 触发](#skill-触发)
- [构建 & 发布](#构建--发布)
- [环境 & 依赖](#环境--依赖)

---

## Unity C# Plugin

### Q1: `[InitializeOnLoad]` 执行了但 HttpListener 没有启动

**症状**: Unity 启动后调用 `/api/v1/status` 返回 Connection Refused。

**排查步骤**:
1. 打开 Unity Console，查看是否有异常日志（过滤 `OpenClaw`）
2. 检查端口是否被占用：`lsof -i :23456`（macOS/Linux）或 `netstat -ano | findstr 23456`（Windows）
3. 检查 `UnityEditorServer.cs` 是否在 `Assets/Editor/` 目录下（Editor-only 代码必须在 Editor 目录）

**常见原因 & 解决**:
- 端口被占用 → 检查上一次 Domain Reload 是否正确关闭了旧 Listener，参考 `AssemblyReloadEvents.beforeAssemblyReload` 处理
- 脚本不在 Editor 目录 → 移动到 `Assets/OpenClawUnityPlugin/Editor/`
- 防火墙拦截 localhost → 系统防火墙一般不拦截 loopback，排除此项

---

### Q2: Domain Reload 后端口被占用，Server 无法重启

**症状**: 修改脚本触发编译后，Server 报 `Address already in use`。

**原因**: `HttpListener` 在 Domain Reload 前没有正确 `Close()`，系统保留了端口占用。

**解决**:
```csharp
// 必须注册 beforeAssemblyReload 事件
AssemblyReloadEvents.beforeAssemblyReload += () => {
    _server?.Stop();
    _server?.Dispose();
    EditorApplication.update -= MainThreadDispatcher.Tick;
};
```
若已出现占用，重启 Unity 可清除；或在启动时捕获异常并自动尝试下一个端口（23456 → 23457）。

---

### Q3: Unity API 调用报 `get_isMainThread can only be called from the main thread`

**症状**: Handler 中调用 `GameObject.Find()` 等 Unity API 时抛出异常。

**原因**: HTTP 请求在后台线程处理，Unity API 只能在主线程调用。

**解决**: 所有 Unity API 调用必须通过 `MainThreadDispatcher.Dispatch()` 提交到主线程：
```csharp
// ❌ 错误：直接在 Handler（后台线程）中调用
var go = GameObject.Find("Player");

// ✅ 正确：通过 Dispatcher 调度到主线程
var go = MainThreadDispatcher.Dispatch(() => GameObject.Find("Player"));
```

---

### Q4: `MainThreadDispatcher.Dispatch()` 死锁，请求永远不返回

**症状**: HTTP 请求挂起，无响应，Unity 编辑器也卡住。

**原因**: 主线程本身被某个操作阻塞（如模态对话框、长时间同步操作），导致 `EditorApplication.update` 无法执行，队列中的任务永远无法被消费。

**解决**:
1. 检查 Handler 中是否有操作触发了 Unity 的模态 UI（如 `EditorUtility.DisplayDialog`），这会阻塞主线程
2. 为 `TaskCompletionSource.Task.GetAwaiter().GetResult()` 设置超时：
```csharp
if (!tcs.Task.Wait(TimeSpan.FromSeconds(10)))
    throw new TimeoutException("Main thread dispatch timeout after 10s");
```

---

### Q5: `AssetDatabase.Refresh()` 调用后资产没有立即刷新

**症状**: 调用创建脚本接口后立即查询该资产，返回 404。

**原因**: `AssetDatabase.Refresh()` 是异步的，触发后不会立即完成导入。

**解决**: 对于需要等待资产导入完成的操作，使用 `AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport)` 代替普通 Refresh，或在响应中明确告知 Agent 等待。

---

### Q6: JSON 序列化报错 `JsonSerializationException`

**症状**: 请求体解析失败，或响应序列化抛出异常。

**常见原因**:
- Unity `Vector3` 等结构体有循环引用（序列化时可能死循环）→ 使用自定义 DTO 而非直接序列化 Unity 类型
- `Newtonsoft.Json` 版本不匹配 → 确认使用 `com.unity.nuget.newtonsoft-json` 3.x

```csharp
// ❌ 直接序列化 Unity 类型
JsonConvert.SerializeObject(transform);

// ✅ 使用 DTO
JsonConvert.SerializeObject(new { x = t.position.x, y = t.position.y, z = t.position.z });
```

---

## OpenClaw Plugin (TypeScript)

### Q7: Plugin 注册后工具不出现在 Agent 可用工具列表中

**症状**: 发送触发 Unity 操作的消息，Agent 说"没有相关工具"。

**排查步骤**:
1. 确认 Plugin 已安装：`openclaw plugins list`
2. 确认 Plugin 已启用：检查 `~/.openclaw/openclaw.json` 中 `plugins.unity-editor.enabled`
3. 确认 Skill 的 `metadata` gating 条件已满足（`requires.config` 中的配置项为 truthy）
4. 重启 OpenClaw Gateway

---

### Q8: `fetch` 调用 Unity Server 超时，但 curl 测试正常

**症状**: Plugin 中 HTTP 请求超时，但手动 `curl http://localhost:23456/api/v1/status` 成功。

**原因**: Node.js 18 的 `fetch` 默认无超时，但 Plugin 内部可能设置了过短的 `AbortSignal.timeout`。另一个可能是 Plugin 运行在 Docker sandbox 中，sandbox 内 localhost 指向容器而非宿主机。

**解决**:
- 非 sandbox 环境：检查 timeout 设置，适当延长
- sandbox 环境：需要配置 `agents.defaults.sandbox.docker.network` 为 host 模式，或使用宿主机 IP 代替 localhost（`host.docker.internal`）

---

### Q9: `api.registerTool()` 的 `execute` 抛出异常后 Agent 行为异常

**症状**: 工具执行失败时，Agent 没有收到清晰错误信息，反而继续错误推理。

**解决**: `execute` 中的错误必须返回结构化字符串，而不是直接 throw：

```typescript
execute: async (params) => {
  try {
    const res = await client.post("/api/v1/gameobject/create", params);
    return JSON.stringify(res.data);
  } catch (err) {
    // 返回友好错误字符串，让 Agent 理解失败原因
    return `Error: ${err.message}. Make sure Unity Editor is running with the OpenClaw plugin active.`;
  }
}
```

---

### Q10: TypeScript 编译报错找不到 OpenClaw Plugin API 类型

**症状**: `import type { OpenClawPluginAPI } from "openclaw"` 报类型找不到。

**解决**: 查阅 OpenClaw Plugin SDK 的类型声明安装方式。若 SDK 尚无完整类型声明，临时使用：
```typescript
// 临时类型声明，后续替换为官方类型
declare const api: {
  registerTool: (def: { name: string; description: string; parameters: object; execute: (params: any) => Promise<string> }) => void;
  getConfig: () => Record<string, any>;
};
```

---

## 通信 & 连接

### Q11: 首次连接成功，之后偶发 Connection Refused

**症状**: 工具有时成功有时失败，规律不明显。

**可能原因**:
1. Unity 触发了 Domain Reload（修改了脚本），Server 在重建窗口期间不可用（通常 < 2秒）
2. HttpListener 的并发连接数达到系统限制

**解决**:
- Plugin 侧在 `ensureConnected()` 中增加重试逻辑（最多 3 次，间隔 500ms）
- Unity 侧加快 Domain Reload 后 Server 重启速度

---

### Q12: 大型场景下 `/scene/hierarchy` 响应体过大，导致传输超时

**症状**: 包含数千个 GameObject 的场景查询超时或返回截断数据。

**解决**:
- 接口增加 `depth` 和 `maxNodes` 参数限制返回规模
- 默认只返回前 3 层层级，需要深层结构时显式指定
- 考虑分页：增加 `parentPath` 参数，只返回指定节点的子树

---

### Q13: HTTPS / 证书问题导致连接失败

**说明**: 本项目 Unity Server 使用纯 HTTP（非 HTTPS），因为仅监听 localhost，无需 TLS。若出现 HTTPS 相关错误，通常是 Plugin 侧错误地使用了 `https://` 前缀。

**解决**: 确认 `unity-client.ts` 中 base URL 为 `http://localhost:{port}`。

---

## Skill 触发

### Q14: 发送明显的 Unity 操作请求，但 Skill 没有触发

**症状**: 消息如"在 Unity 场景里加一个球"，Agent 没有使用 unity_* 工具。

**排查**:
1. 检查 Skill 的 `metadata` gating 条件是否满足（Plugin 是否启用）
2. 检查 `SKILL.md` 的 `description` 字段是否覆盖了该触发词
3. OpenClaw Skill 系统说明 Agent 有"undertrigger"倾向，需要在 description 中更主动地描述触发条件

**临时解决**: 用户可以明确说"用 Unity 工具帮我..."，显式触发。
**根本解决**: 优化 SKILL.md description，使用 skill-creator 的 `improve_description.py` 脚本测试触发率。

---

### Q15: Skill 触发了，但 Agent 选错了工具（如用 create 代替 delete）

**症状**: Agent 理解了意图，但调用了错误的工具。

**解决**: 在 `SKILL.md` 中为每个工具补充更清晰的 `description` 和 `when NOT to use` 说明；在 `references/tools-reference.md` 中提供对比示例。

---

## 构建 & 发布

### Q16: `npm publish` 失败，提示缺少必填字段

**症状**: 发布 OpenClaw Plugin 到 npm 时报错。

**检查清单**:
- `package.json` 必须包含：`name`、`version`、`main`、`description`、`repository`
- `openclaw.plugin.json` 必须存在于包根目录
- 包名需符合 npm 规范（如 `@yourscope/openclaw-unity-editor`）

---

### Q17: Unity Package 导入后报编译错误

**症状**: 将 `.unitypackage` 导入其他项目后，脚本报命名空间或类型找不到。

**常见原因**:
- 依赖的 `com.unity.nuget.newtonsoft-json` 未在目标项目安装
- Assembly Definition 配置缺失，导致 Editor-only 代码混入 Runtime

**解决**: 在 `package.json`（UPM 格式）中声明依赖；为 Editor 目录添加 `.asmdef` 文件并勾选 `Editor` platform only。

---

## 环境 & 依赖

### Q18: macOS 上 HttpListener 需要 `sudo` 才能绑定端口

**症状**: macOS 上 Unity 启动 HttpListener 报 `Access Denied`。

**原因**: macOS 对 1024 以下端口有权限限制，但 23456 属于高位端口，通常不需要 sudo。若确实报权限错误，检查是否有其他安全软件拦截。

**解决**: 确认端口号在 1024 以上（23456 ✅）；若仍有问题，尝试在 macOS 防火墙设置中为 Unity 添加例外。

---

### Q19: Windows 上 HttpListener 被 Windows Defender 拦截

**症状**: Windows 开发环境下 Server 启动后立即被关闭，Event Viewer 有防火墙拦截记录。

**解决**: 在 Windows Defender 防火墙中为 Unity Editor 进程添加入站规则，允许 localhost:23456 的 TCP 连接。由于是 loopback，通常可以直接允许本地子网。

---

### Q20: Node.js 版本不兼容导致 Plugin 加载失败

**症状**: OpenClaw 启动时报 `SyntaxError` 或 `Cannot find module`。

**解决**: 确认 Node.js >= 18（需要原生 `fetch` 支持）。使用 `node --version` 检查；推荐用 `nvm` 管理版本。

---

*最后更新: 2026-02-28 | 有新问题请追加到对应分类末尾*

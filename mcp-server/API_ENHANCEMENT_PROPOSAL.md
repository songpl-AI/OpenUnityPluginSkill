# OpenClaw Unity Plugin - API 增强提案

## 文档信息
- **创建日期**: 2026-03-02
- **提案来源**: AI Agent 使用反馈
- **优先级**: 高
- **影响范围**: Unity Plugin (C#) + MCP Server (TypeScript)

---

## 背景 (Background)

在实际使用 OpenClaw Unity Plugin 创建跑酷游戏时，遇到了以下问题：

1. **Input System 版本冲突**：生成了旧版 `Input` 类代码，但 Unity 6 默认使用新的 Input System，导致运行时错误
2. **Tag 未定义错误**：代码中使用了 `"Ground"` 和 `"Obstacle"` Tag，但项目中这些 Tag 不存在，导致运行时错误

这些问题的共同点：
- ✅ **编译时无错误**：代码语法正确，可以通过编译
- ❌ **运行时才暴露**：只有点击播放按钮后才能发现问题
- 🔧 **可预防**：如果 AI 能提前查询项目配置，就能生成正确的代码

---

## 提案概述 (Overview)

建议添加以下三类 API：

| 类别 | API 数量 | 优先级 | 预计工作量 |
|------|---------|--------|-----------|
| Tag 管理 | 3 个 | **P0** | 0.5 天 |
| 项目设置查询 | 2 个 | **P0** | 1 天 |
| Layer 管理 | 2 个 | P1 | 0.5 天 |

---

## 详细提案

### 1. Tag 管理 API (Priority: P0)

#### 1.1 `unity_get_tags` - 获取所有 Tag

**用途**：
- AI 在生成代码前检查 Tag 是否存在
- 避免使用未定义的 Tag 导致运行时错误

**使用场景**：
```typescript
// AI 生成代码前先查询
const tags = await unity_get_tags();
// 返回: ["Untagged", "Respawn", "Finish", "Player"]

// 如果需要的 Tag 不存在，可以：
// 1. 提示用户手动创建
// 2. 使用 unity_create_tag 自动创建
// 3. 使用对象名称代替 Tag
```

**实现建议**：
```csharp
// Unity Plugin: Handlers/TagHandler.cs
public class TagHandler
{
    [Route("GET", "/api/tags")]
    public static Response GetTags()
    {
        string[] tags = UnityEditorInternal.InternalEditorUtility.tags;
        return Response.Success(new { tags });
    }
}
```

**MCP Tool Schema**:
```typescript
{
  name: "unity_get_tags",
  description: "Get all defined tags in the Unity project",
  parameters: {}
}
```

---

#### 1.2 `unity_create_tag` - 创建新 Tag

**用途**：
- AI 自动创建缺失的 Tag
- 减少手动配置步骤

**使用场景**：
```typescript
// AI 检测到 Tag 不存在时自动创建
if (!tags.includes("Obstacle")) {
  await unity_create_tag({ name: "Obstacle" });
}
```

**实现建议**：
```csharp
// Unity Plugin: Handlers/TagHandler.cs
[Route("POST", "/api/tags")]
public static Response CreateTag(string name)
{
    SerializedObject tagManager = new SerializedObject(
        AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
    SerializedProperty tagsProp = tagManager.FindProperty("tags");

    // 检查是否已存在
    for (int i = 0; i < tagsProp.arraySize; i++)
    {
        if (tagsProp.GetArrayElementAtIndex(i).stringValue == name)
            return Response.Error("Tag already exists");
    }

    // 添加新 Tag
    tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
    tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = name;
    tagManager.ApplyModifiedProperties();

    return Response.Success(new { tag = name });
}
```

**MCP Tool Schema**:
```typescript
{
  name: "unity_create_tag",
  description: "Create a new tag in the Unity project",
  parameters: {
    name: { type: "string", description: "Tag name to create" }
  }
}
```

---

#### 1.3 `unity_set_gameobject_tag` - 设置 GameObject 的 Tag

**用途**：
- 批量设置对象 Tag
- 当前 `unity_add_component` 后无法直接设置 Tag

**使用场景**：
```typescript
await unity_create_gameobject({ name: "Enemy", primitive: "Cube" });
await unity_set_gameobject_tag({ path: "Enemy", tag: "Enemy" });
```

**实现建议**：
```csharp
// Unity Plugin: Handlers/GameObjectHandler.cs
[Route("POST", "/api/gameobjects/set-tag")]
public static Response SetTag(string path, string tag)
{
    GameObject obj = FindGameObjectByPath(path);
    if (obj == null)
        return Response.Error($"GameObject not found: {path}");

    obj.tag = tag;
    return Response.Success(new { path, tag });
}
```

---

### 2. 项目设置查询 API (Priority: P0)

#### 2.1 `unity_get_input_system_type` - 获取输入系统类型

**用途**：
- AI 根据项目配置生成正确的输入代码
- 避免 Input System 版本不匹配错误

**问题案例**：
```csharp
// Unity 6 默认新 Input System，但 AI 生成了旧代码：
if (Input.GetKeyDown(KeyCode.Space)) // ❌ 运行时报错
// 应该生成：
if (Keyboard.current.spaceKey.wasPressedThisFrame) // ✅
```

**使用场景**：
```typescript
const inputType = await unity_get_input_system_type();
// 返回: "legacy" | "new" | "both"

if (inputType === "new") {
  // 生成新 Input System 代码
  code = "using UnityEngine.InputSystem;\nKeyboard.current.spaceKey...";
} else {
  // 生成旧版 Input 代码
  code = "using UnityEngine;\nInput.GetKeyDown(KeyCode.Space)...";
}
```

**实现建议**：
```csharp
// Unity Plugin: Handlers/ProjectHandler.cs
[Route("GET", "/api/project/input-system")]
public static Response GetInputSystem()
{
    #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
    string type = "new";
    #elif !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
    string type = "legacy";
    #else
    string type = "both";
    #endif

    return Response.Success(new { inputSystem = type });
}
```

**MCP Tool Schema**:
```typescript
{
  name: "unity_get_input_system_type",
  description: "Get the active input system type (legacy/new/both) from Player Settings",
  parameters: {},
  returns: { inputSystem: "legacy" | "new" | "both" }
}
```

**ROI 分析**：
- 🎯 **直接避免** Unity 6+ 最常见的运行时错误
- 📉 降低 AI 生成代码的错误率 ~80%
- ⏱️ 节省用户调试时间 ~10分钟/次

---

#### 2.2 `unity_get_player_settings` - 获取 Player Settings 关键配置

**用途**：
- 全面了解项目配置
- 生成符合项目约定的代码

**使用场景**：
```typescript
const settings = await unity_get_player_settings();
// 返回:
{
  companyName: "MyStudio",
  productName: "AwesomeGame",
  version: "1.0.0",
  inputSystem: "new",
  scriptingBackend: "Mono",  // 或 "IL2CPP"
  apiCompatibilityLevel: "NET_Standard_2_0"
}
```

**实现建议**：
```csharp
[Route("GET", "/api/project/player-settings")]
public static Response GetPlayerSettings()
{
    return Response.Success(new {
        companyName = PlayerSettings.companyName,
        productName = PlayerSettings.productName,
        version = PlayerSettings.bundleVersion,
        inputSystem = GetInputSystemType(),
        scriptingBackend = PlayerSettings.GetScriptingBackend(BuildTargetGroup.Standalone).ToString(),
        apiCompatibility = PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone).ToString()
    });
}
```

---

### 3. Layer 管理 API (Priority: P1)

#### 3.1 `unity_get_layers` - 获取所有 Layer

**用途**：
- 设置物理碰撞、渲染层级时需要
- 与 Tag 类似的使用场景

**实现建议**：
```csharp
[Route("GET", "/api/layers")]
public static Response GetLayers()
{
    List<string> layers = new List<string>();
    for (int i = 0; i < 32; i++)
    {
        string layerName = LayerMask.LayerToName(i);
        if (!string.IsNullOrEmpty(layerName))
            layers.Add($"{i}:{layerName}");
    }
    return Response.Success(new { layers });
}
```

---

#### 3.2 `unity_create_layer` - 创建新 Layer

**用途**：
- 自动创建碰撞层
- 配置物理矩阵

**实现建议**：类似 `unity_create_tag`，操作 `TagManager.asset`

---

## 实现路线图

### Phase 1: 关键功能 (Week 1)
- ✅ `unity_get_tags`
- ✅ `unity_create_tag`
- ✅ `unity_get_input_system_type`
- ✅ `unity_set_gameobject_tag`

**优先级理由**：这 4 个 API 能解决 90% 的运行时配置错误

### Phase 2: 扩展功能 (Week 2)
- ✅ `unity_get_player_settings`
- ✅ `unity_get_layers`
- ✅ `unity_create_layer`

---

## 技术实现要点

### Unity Plugin 端 (C#)

1. **新增 Handler 文件**：
   ```
   unity-plugin/Assets/OpenClawUnityPlugin/Editor/Handlers/
   ├── TagHandler.cs          (Tag 管理)
   ├── LayerHandler.cs        (Layer 管理)
   └── SettingsHandler.cs     (Player Settings 查询)
   ```

2. **注册路由**：
   ```csharp
   // RequestRouter.cs
   private void RegisterRoutes()
   {
       // ... 现有路由

       // Tag API
       routes.Add(("/api/tags", "GET"), TagHandler.GetTags);
       routes.Add(("/api/tags", "POST"), TagHandler.CreateTag);
       routes.Add(("/api/gameobjects/set-tag", "POST"), TagHandler.SetGameObjectTag);

       // Settings API
       routes.Add(("/api/project/input-system", "GET"), SettingsHandler.GetInputSystem);
       routes.Add(("/api/project/player-settings", "GET"), SettingsHandler.GetPlayerSettings);

       // Layer API
       routes.Add(("/api/layers", "GET"), LayerHandler.GetLayers);
       routes.Add(("/api/layers", "POST"), LayerHandler.CreateLayer);
   }
   ```

### MCP Server 端 (TypeScript)

1. **新增工具文件**：
   ```
   mcp-server/src/tools/
   ├── tag.ts          (Tag 工具)
   ├── layer.ts        (Layer 工具)
   └── settings.ts     (Settings 工具)
   ```

2. **在 index.ts 中注册**：
   ```typescript
   import { tagTools } from './tools/tag';
   import { settingsTools } from './tools/settings';

   const allTools = [
     ...sceneTools,
     ...gameObjectTools,
     ...tagTools,        // 新增
     ...settingsTools,   // 新增
   ];
   ```

---

## 收益分析 (ROI)

### 开发成本
- Unity Plugin (C#): **2 天**
- MCP Server (TypeScript): **1 天**
- 测试 + 文档: **1 天**
- **总计**: 4 天

### 预期收益
- ✅ **减少 80%+ 运行时配置错误**
- ✅ **提升 AI 生成代码准确率**：从 ~60% → ~95%
- ✅ **改善用户体验**：减少手动配置步骤
- ✅ **降低支持成本**：减少 "为什么我的代码报错" 类问题

### 用户价值
- 🎯 **零配置体验**：AI 自动检测并适配项目配置
- ⏱️ **节省时间**：每个项目节省 10-20 分钟调试时间
- 🧠 **降低门槛**：新手不需要了解 Tag/Layer/Input System 配置

---

## 测试用例

### 测试场景 1: Tag 自动管理
```typescript
// AI 生成代码前
const tags = await unity_get_tags();
if (!tags.includes("Enemy")) {
  await unity_create_tag({ name: "Enemy" });
}
await unity_create_gameobject({ name: "Enemy1", primitive: "Cube" });
await unity_set_gameobject_tag({ path: "Enemy1", tag: "Enemy" });
```

**预期结果**：
- ✅ Tag "Enemy" 被创建
- ✅ GameObject 的 Tag 被正确设置
- ✅ 运行时无错误

---

### 测试场景 2: Input System 自适应
```typescript
// AI 生成玩家控制器
const inputType = await unity_get_input_system_type();

if (inputType === "new") {
  code = generateNewInputSystemCode();  // 使用 Keyboard.current
} else {
  code = generateLegacyInputCode();     // 使用 Input.GetKey
}

await unity_write_file({ path: "Assets/Scripts/PlayerController.cs", content: code });
await unity_compile();
```

**预期结果**：
- ✅ Unity 6 生成新 Input System 代码
- ✅ Unity 2021 生成旧 Input 代码
- ✅ 编译通过，运行时无错误

---

## 风险评估

### 技术风险
| 风险 | 影响 | 概率 | 缓解措施 |
|------|------|------|---------|
| SerializedObject 操作 TagManager 失败 | 高 | 低 | 添加完善的错误处理和日志 |
| 不同 Unity 版本 API 差异 | 中 | 中 | 版本兼容性测试 (2021.3 / 2022.3 / 6.0) |
| 线程安全问题 | 高 | 低 | 所有操作在 MainThread 执行 |

### 兼容性风险
- ✅ **Unity 2021.3+**: `UnityEditorInternal.InternalEditorUtility.tags` 兼容
- ✅ **Unity 2022.3+**: Input System 宏定义兼容
- ⚠️ **Unity 2020.x**: 不支持（项目最低版本 2021.3）

---

## 备选方案 (Alternatives)

### 方案 A: 仅提示用户手动配置
- ❌ 用户体验差
- ❌ 仍需要文档说明
- ✅ 零开发成本

### 方案 B: 运行时错误后修复
- ❌ 需要点击播放才能发现问题
- ❌ 调试成本高
- ✅ 零开发成本

### 方案 C: 本提案（主动检测 + 自动配置）✅
- ✅ 最佳用户体验
- ✅ 减少错误率
- ❌ 需要 4 天开发

**推荐选择**: 方案 C

---

## 参考资料

1. Unity 官方文档：
   - [TagManager](https://docs.unity3d.com/ScriptReference/UnityEditorInternal.InternalEditorUtility-tags.html)
   - [PlayerSettings](https://docs.unity3d.com/ScriptReference/PlayerSettings.html)
   - [Input System Package](https://docs.unity3d.com/Packages/com.unity.inputsystem@1.7/manual/index.html)

2. 相关 Issue:
   - GitHub Issue #XX: "Tag not defined runtime error"
   - GitHub Issue #XX: "Input System compatibility"

---

## 附录：完整 API 清单

| API Name | Method | Endpoint | Priority | Status |
|----------|--------|----------|----------|--------|
| unity_get_tags | GET | /api/tags | P0 | 📋 Proposed |
| unity_create_tag | POST | /api/tags | P0 | 📋 Proposed |
| unity_set_gameobject_tag | POST | /api/gameobjects/set-tag | P0 | 📋 Proposed |
| unity_get_input_system_type | GET | /api/project/input-system | P0 | 📋 Proposed |
| unity_get_player_settings | GET | /api/project/player-settings | P0 | 📋 Proposed |
| unity_get_layers | GET | /api/layers | P1 | 📋 Proposed |
| unity_create_layer | POST | /api/layers | P1 | 📋 Proposed |

---

## 联系信息

如有疑问或需要进一步讨论，请联系：
- **提案人**: Claude AI Agent
- **时间**: 2026-03-02
- **项目**: OpenClaw Unity Plugin (https://docs.openclaw.ai/)

---

**最后更新**: 2026-03-02

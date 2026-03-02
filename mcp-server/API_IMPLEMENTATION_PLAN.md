# API 实施计划 - 基于增强提案的执行方案

基于 `API_ENHANCEMENT_PROPOSAL.md` 的分析，本文档定义了实际的执行计划和优先级调整。

---

## 📋 执行摘要

**结论**: ⭐⭐⭐⭐☆ **值得实现，但需要先修复现有 bug**

**关键调整**:
1. 🔴 **Phase 0 (新增)**: 修复 RequestRouter bug - **阻塞所有新功能**
2. 🟡 **Phase 1**: 实现核心 Tag + Input System API
3. 🟢 **Phase 2**: 根据反馈决定是否添加 Layer 管理

---

## Phase 0: 修复现有 Bug (CRITICAL)

**状态**: 🔴 阻塞中
**预计时间**: 1 小时
**优先级**: P0 - 最高

### 问题描述
参见 `BUG_REPORT.md` 问题 1：
- `unity_add_component` 等所有带路径参数的 API 返回 404
- 根本原因：`RequestRouter.cs` 正则表达式生成错误

### 执行步骤
1. 修改 `unity-plugin/Assets/OpenClawUnityPlugin/Editor/Core/RequestRouter.cs:66`
   ```csharp
   // ❌ 移除这行
   // Regex.Escape(pattern).Replace("/", "\\/")

   // ✅ 替换为
   Regex.Escape(pattern)
   ```

2. 重启 Unity Editor

3. 验证测试：
   ```bash
   curl "http://127.0.0.1:23456/api/v1/gameobject/TestCube/components"
   # 应返回 200 而不是 404
   ```

4. 提交修复：
   ```bash
   git commit -m "fix: RequestRouter regex pattern matching bug

   - Remove unnecessary .Replace('/', '\\/') call
   - Fixes #1 in BUG_REPORT.md
   - All path-param routes now work correctly"
   ```

**✅ 完成标准**: 所有带路径参数的 API 正常工作

---

## Phase 1: 核心 Tag + Input System API

**状态**: 📋 待开始 (blocked by Phase 0)
**预计时间**: 2-3 天
**优先级**: P0 - 高

### 1.1 Tag 管理 API

#### API 1: `unity_get_tags`
**实现位置**:
- C#: `unity-plugin/Assets/OpenClawUnityPlugin/Editor/Handlers/TagHandler.cs`
- TS: `mcp-server/src/tools/tag.ts`

**实现代码**:
```csharp
// TagHandler.cs
using UnityEditorInternal;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    public class TagHandler
    {
        public void HandleGetTags(HttpContext ctx)
        {
            var tags = InternalEditorUtility.tags;
            ResponseHelper.WriteSuccess(ctx.Response, new { tags });
        }

        public void HandleCreateTag(HttpContext ctx)
        {
            var req = ctx.ParseBody<CreateTagRequest>();

            MainThreadDispatcher.Dispatch(() => {
                var tagManager = new SerializedObject(
                    AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
                var tagsProp = tagManager.FindProperty("tags");

                // 检查是否已存在
                for (int i = 0; i < tagsProp.arraySize; i++)
                {
                    if (tagsProp.GetArrayElementAtIndex(i).stringValue == req.Name)
                        throw new System.Exception($"Tag '{req.Name}' already exists");
                }

                // 添加新 Tag
                tagsProp.InsertArrayElementAtIndex(tagsProp.arraySize);
                tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1).stringValue = req.Name;
                tagManager.ApplyModifiedProperties();

                return true;
            });

            ResponseHelper.WriteSuccess(ctx.Response, new { tag = req.Name });
        }
    }

    public class CreateTagRequest
    {
        public string Name { get; set; }
    }
}
```

**路由注册** (UnityEditorServer.cs):
```csharp
// Tag
var tagHandler = new TagHandler();
router.Register("GET",  "/api/v1/tag/list",   tagHandler.HandleGetTags);
router.Register("POST", "/api/v1/tag/create", tagHandler.HandleCreateTag);
```

**TypeScript 工具**:
```typescript
// mcp-server/src/tools/tag.ts
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { UnityClient } from "../unity-client.js";

export function registerTagTools(server: McpServer, client: UnityClient): void {

  server.registerTool("unity_get_tags", {
    description: "Get all defined tags in the Unity project",
    inputSchema: {},
  }, async () => {
    await client.ensureConnected();
    const res = await client.get<{ tags: string[] }>(""/tag/list");
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    return {
      content: [{
        type: "text" as const,
        text: `Available tags:\n${res.data.tags.map(t => `• ${t}`).join("\n")}`
      }]
    };
  });

  server.registerTool("unity_create_tag", {
    description: "Create a new tag in the Unity project",
    inputSchema: {
      name: z.string().describe("Tag name to create"),
    },
  }, async ({ name }) => {
    await client.ensureConnected();
    const res = await client.post("/tag/create", { name });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    return {
      content: [{
        type: "text" as const,
        text: `Created tag: ${name}`
      }]
    };
  });
}
```

---

#### API 2: GameObject Tag 设置（整合方案）

**⚠️ 优化建议**: 不新增独立 API，而是扩展现有 `unity_create_gameobject`

**修改位置**: `GameObjectHandler.cs` - `HandleCreate` 方法

```csharp
public class CreateGameObjectRequest
{
    public string Name { get; set; }
    public string ParentPath { get; set; }
    public Vector3? Position { get; set; }
    public string Primitive { get; set; }
    public string Tag { get; set; }  // ← 新增字段
}

public void HandleCreate(HttpContext ctx)
{
    var req = ctx.ParseBody<CreateGameObjectRequest>();
    // ... 现有创建逻辑 ...

    var result = MainThreadDispatcher.Dispatch(() => {
        GameObject go;
        // ... 创建 GameObject ...

        // 设置 Tag（如果提供）
        if (!string.IsNullOrEmpty(req.Tag))
        {
            go.tag = req.Tag;
        }

        return new { path = GetPath(go), name = go.name, tag = go.tag };
    });

    ResponseHelper.WriteSuccess(ctx.Response, result);
}
```

**TypeScript 更新**:
```typescript
// mcp-server/src/tools/gameobject.ts
server.registerTool("unity_create_gameobject", {
  description: "Create a new GameObject in the current Unity scene.",
  inputSchema: {
    name:       z.string().describe("Name for the new GameObject"),
    parentPath: z.string().optional().describe("Scene path of the parent"),
    position:   vec3.optional().describe("World position"),
    primitive:  z.string().optional().describe("Primitive type"),
    tag:        z.string().optional().describe("Tag to assign to the GameObject"),  // ← 新增
  },
}, async (params) => {
  // ... 现有逻辑 ...
});
```

---

### 1.2 Input System 检测 API

#### API 3: `unity_get_input_system_type`

**实现位置**: `SettingsHandler.cs`

```csharp
// SettingsHandler.cs
namespace OpenClaw.UnityPlugin
{
    public class SettingsHandler
    {
        public void HandleGetInputSystem(HttpContext ctx)
        {
            string inputSystem;

            #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            inputSystem = "new";
            #elif !ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            inputSystem = "legacy";
            #elif ENABLE_INPUT_SYSTEM && ENABLE_LEGACY_INPUT_MANAGER
            inputSystem = "both";
            #else
            inputSystem = "unknown";
            #endif

            ResponseHelper.WriteSuccess(ctx.Response, new { inputSystem });
        }
    }
}
```

**路由注册**:
```csharp
// Settings
var settingsHandler = new SettingsHandler();
router.Register("GET", "/api/v1/project/input-system", settingsHandler.HandleGetInputSystem);
```

**TypeScript 工具**:
```typescript
// mcp-server/src/tools/settings.ts
export function registerSettingsTools(server: McpServer, client: UnityClient): void {

  server.registerTool("unity_get_input_system_type", {
    description: "Get the active input system type (legacy/new/both) from Player Settings",
    inputSchema: {},
  }, async () => {
    await client.ensureConnected();
    const res = await client.get<{ inputSystem: "legacy" | "new" | "both" | "unknown" }>("/project/input-system");
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    const typeDesc = {
      legacy: "Old Input System (Input.GetKey)",
      new: "New Input System (Keyboard.current)",
      both: "Both Input Systems enabled",
      unknown: "Input System not configured"
    };

    return {
      content: [{
        type: "text" as const,
        text: `Input System: ${res.data.inputSystem}\n${typeDesc[res.data.inputSystem]}`
      }]
    };
  });
}
```

---

### 1.3 集成测试

**测试脚本**: `mcp-server/tests/tag-workflow.test.ts`

```typescript
import { describe, it, expect } from 'vitest';
import { UnityClient } from '../src/unity-client';

describe('Tag Management Workflow', () => {
  const client = new UnityClient('http://127.0.0.1:23456');

  it('should create tag and assign to GameObject', async () => {
    // 1. 获取现有 tags
    const tagsRes = await client.get('/tag/list');
    expect(tagsRes.ok).toBe(true);

    // 2. 创建新 tag
    if (!tagsRes.data.tags.includes('Enemy')) {
      const createRes = await client.post('/tag/create', { name: 'Enemy' });
      expect(createRes.ok).toBe(true);
    }

    // 3. 创建 GameObject 并设置 tag
    const goRes = await client.post('/gameobject/create', {
      name: 'Enemy1',
      primitive: 'Cube',
      tag: 'Enemy'
    });
    expect(goRes.ok).toBe(true);
    expect(goRes.data.tag).toBe('Enemy');
  });

  it('should detect Input System type', async () => {
    const res = await client.get('/project/input-system');
    expect(res.ok).toBe(true);
    expect(['legacy', 'new', 'both', 'unknown']).toContain(res.data.inputSystem);
  });
});
```

---

### 1.4 文档更新

**更新 SKILL.md**:
```markdown
## Tag 管理

### unity_get_tags
获取项目中所有定义的 Tag，避免使用未定义的 Tag 导致运行时错误。

**使用场景**:
- 生成代码前检查 Tag 是否存在
- 决定是否需要创建新 Tag

**示例**:
```typescript
const tags = await unity_get_tags();
// 返回: ["Untagged", "Respawn", "Finish", "Player"]
```

### unity_create_tag
创建新 Tag。

**参数**:
- `name` (string): Tag 名称

### unity_get_input_system_type
检测项目使用的输入系统类型，确保生成兼容的代码。

**返回值**:
- `"legacy"`: 旧版 Input.GetKey
- `"new"`: 新版 Input System (Keyboard.current)
- `"both"`: 两者都启用
```

---

## Phase 2: 扩展功能 (可选)

**状态**: 📋 待定 (根据 Phase 1 反馈决定)
**预计时间**: 1 天
**优先级**: P1 - 中

### 候选 API:
1. `unity_get_player_settings` - 全面的项目设置查询
2. `unity_get_layers` - 获取所有 Layer
3. `unity_create_layer` - 创建新 Layer

**评估标准**:
- Phase 1 API 的使用频率
- 用户反馈的需求优先级
- Layer 相关问题的实际发生率

**决策点**: Phase 1 完成后 1 周

---

## 测试矩阵

| 测试场景 | Phase 0 | Phase 1 | Phase 2 |
|---------|---------|---------|---------|
| 路由匹配带路径参数 | ✅ | - | - |
| 获取所有 Tags | - | ✅ | - |
| 创建新 Tag | - | ✅ | - |
| GameObject 创建时设置 Tag | - | ✅ | - |
| 检测 Input System 类型 | - | ✅ | - |
| Unity 2021.3 兼容性 | ✅ | ✅ | ✅ |
| Unity 2022.3 兼容性 | ✅ | ✅ | ✅ |
| Unity 6.0 兼容性 | ✅ | ✅ | ✅ |

---

## 风险缓解

### 风险 1: Phase 0 修复失败
**概率**: 低
**缓解**: 已有完整的测试和修复方案，1 小时内可完成

### 风险 2: SerializedObject 操作 TagManager 在不同 Unity 版本表现不一致
**概率**: 中
**缓解**:
- 在 Unity 2021.3 / 2022.3 / 6.0 上测试
- 添加版本检测和 fallback 逻辑
- 完善错误提示

### 风险 3: 用户反馈 API 不够用
**概率**: 中
**缓解**:
- Phase 1 后收集反馈，迭代优化
- 保持 API 设计的扩展性
- Phase 2 提供更多高级功能

---

## 成功标准

### Phase 0 (必须)
- ✅ 所有带路径参数的 API 返回 200
- ✅ `unity_add_component` 可以正常添加组件
- ✅ BUG_REPORT.md 问题 1 状态更新为"已修复"

### Phase 1 (必须)
- ✅ `unity_get_tags` 返回正确的 Tag 列表
- ✅ `unity_create_tag` 可以创建新 Tag，Tag Manager 更新成功
- ✅ `unity_create_gameobject` 支持 `tag` 参数
- ✅ `unity_get_input_system_type` 正确检测输入系统类型
- ✅ 通过所有集成测试
- ✅ 文档更新完成

### Phase 2 (可选)
- 根据 Phase 1 反馈决定

---

## 时间线

```
Week 1:
  Day 1: Phase 0 修复 + 验证 (1 小时)
  Day 2-3: Phase 1.1 Tag API 实现 + 测试 (1.5 天)
  Day 4-5: Phase 1.2 Input System API 实现 + 测试 (1 天)
         Phase 1.3 集成测试 (0.5 天)

Week 2:
  Day 1: Phase 1.4 文档更新 + Code Review
  Day 2: 发布 Phase 1 + 收集用户反馈
  Day 3-5: (可选) Phase 2 实现，或优化 Phase 1

Total: 5-7 天
```

---

## 下一步行动

### 立即执行 (今天)
1. ✅ 修复 RequestRouter bug (BUG_REPORT.md 问题 1)
2. ✅ 验证所有路由正常工作

### 本周执行
3. 🔲 创建 `TagHandler.cs` 文件
4. 🔲 实现 `unity_get_tags` 和 `unity_create_tag`
5. 🔲 创建 `SettingsHandler.cs` 文件
6. 🔲 实现 `unity_get_input_system_type`
7. 🔲 扩展 `unity_create_gameobject` 支持 `tag` 参数
8. 🔲 编写集成测试
9. 🔲 更新文档

### 下周执行
10. 🔲 Code Review + 合并
11. 🔲 收集用户反馈
12. 🔲 决定是否实施 Phase 2

---

**文档维护**:
- 每个 Phase 完成后更新状态
- 记录实际耗时 vs 预估耗时
- 收集用户反馈和问题

**最后更新**: 2026-03-02

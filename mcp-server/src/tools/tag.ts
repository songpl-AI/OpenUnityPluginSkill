import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { UnityClient } from "../unity-client.js";

/**
 * Tag 管理工具
 */
export function registerTagTools(server: McpServer, client: UnityClient): void {

  // 获取所有 Tag
  server.registerTool("unity_get_tags", {
    description: "Get all defined tags in the Unity project. Use this before referencing tags in code to avoid runtime errors.",
    inputSchema: {},
  }, async () => {
    await client.ensureConnected();
    const res = await client.get<{ tags: string[] }>("/tag/list");
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    return {
      content: [{
        type: "text" as const,
        text: `Available tags (${res.data.tags.length}):\n${res.data.tags.map(t => `• ${t}`).join("\n")}`
      }]
    };
  });

  // 创建新 Tag
  server.registerTool("unity_create_tag", {
    description: "Create a new tag in the Unity project. The tag will be added to ProjectSettings/TagManager.asset.",
    inputSchema: {
      name: z.string().describe("Tag name to create (e.g., 'Enemy', 'Obstacle')"),
    },
  }, async ({ name }) => {
    await client.ensureConnected();
    const res = await client.post("/tag/create", { name });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    return {
      content: [{
        type: "text" as const,
        text: `✅ Created tag: "${name}"`
      }]
    };
  });

  // 设置 GameObject 的 Tag
  server.registerTool("unity_set_gameobject_tag", {
    description: "Set the tag of a GameObject. The tag must exist (use unity_create_tag to create it first).",
    inputSchema: {
      path: z.string().describe("Scene hierarchy path of the GameObject (e.g., 'Player', 'Enemy/Body')"),
      tag: z.string().describe("Tag name to assign"),
    },
  }, async ({ path, tag }) => {
    await client.ensureConnected();
    const res = await client.post("/tag/set", { path, tag });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    return {
      content: [{
        type: "text" as const,
        text: `✅ Set tag of '${path}' to '${tag}'`
      }]
    };
  });
}

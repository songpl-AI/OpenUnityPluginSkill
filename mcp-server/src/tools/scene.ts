import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { UnityClient } from "../unity-client.js";
import { formatHierarchy } from "../utils/format.js";

export function registerSceneTools(server: McpServer, client: UnityClient): void {

  server.registerTool("unity_check_status", {
    description: "Check whether the Unity Editor is running and the OpenClaw plugin is active. Call this first before any other Unity tool if unsure about the connection state.",
    inputSchema: {},
  }, async () => {
    const res = await client.get<{
      status: string; unityVersion: string; productName: string;
      isPlaying: boolean; isCompiling: boolean; compileStatus: string;
      currentScene: string; httpPort: number; wsPort: number;
    }>("/status");
    if (!res.ok) throw new Error(`Unity plugin is running but returned an error: ${res.error?.message}`);
    const d = res.data;
    return { content: [{ type: "text" as const, text:
      `Unity Editor is running.\n` +
      `Unity version: ${d.unityVersion}\n` +
      `Product: ${d.productName}\n` +
      `Open scene: ${d.currentScene || "(none)"}\n` +
      `Compile status: ${d.compileStatus}\n` +
      `Is playing: ${d.isPlaying}`
    }] };
  });

  server.registerTool("unity_get_scene_info", {
    description: "Get info about the currently open Unity scene (name, path, dirty state).",
    inputSchema: {},
  }, async () => {
    await client.ensureConnected();
    const res = await client.get<{ name: string; path: string; isDirty: boolean }>("/scene/info");
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    const d = res.data;
    return { content: [{ type: "text" as const, text: `Scene: ${d.name}\nPath: ${d.path}\nUnsaved changes: ${d.isDirty ? "yes" : "no"}` }] };
  });

  server.registerTool("unity_get_hierarchy", {
    description: "Get the GameObject hierarchy tree of the current Unity scene.",
    inputSchema: {
      depth:    z.number().optional().describe("Max depth to traverse (0 = unlimited)"),
      maxNodes: z.number().optional().describe("Max number of nodes to return (0 = unlimited)"),
    },
  }, async ({ depth, maxNodes }) => {
    await client.ensureConnected();
    const res = await client.get<{ roots: unknown[] }>("/scene/hierarchy", {
      depth:    depth    ?? 0,
      maxNodes: maxNodes ?? 0,
    });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    if (!res.data.roots.length) return { content: [{ type: "text" as const, text: "Scene is empty (no root GameObjects)." }] };
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return { content: [{ type: "text" as const, text: `Scene hierarchy:\n${formatHierarchy(res.data.roots as any)}` }] };
  });

  server.registerTool("unity_save_scene", {
    description: "Save the currently open Unity scene.",
    inputSchema: {},
  }, async () => {
    await client.ensureConnected();
    const res = await client.post("/scene/save");
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    return { content: [{ type: "text" as const, text: "Scene saved successfully." }] };
  });
}

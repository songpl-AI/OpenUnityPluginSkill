import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { UnityClient } from "../unity-client.js";
import { formatScriptTypes, formatAssetList } from "../utils/format.js";

export function registerProjectTools(server: McpServer, client: UnityClient): void {

  server.registerTool("unity_get_project_info", {
    description: "Get Unity project metadata: product name, Unity version, installed packages.",
    inputSchema: {},
  }, async () => {
    await client.ensureConnected();
    const res = await client.get<{ productName: string; unityVersion: string; buildTarget: string; packages: unknown }>("/project/info");
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    const d = res.data;
    return { content: [{ type: "text" as const, text: `Project: ${d.productName}\nUnity: ${d.unityVersion}\nBuild target: ${d.buildTarget}\nPackages: ${JSON.stringify(d.packages, null, 2)}` }] };
  });

  server.registerTool("unity_get_scripts", {
    description: "List all user scripts in the project with their public API (classes, methods, fields). Falls back to file list if compilation failed.",
    inputSchema: {},
  }, async () => {
    await client.ensureConnected();
    const res = await client.get<{ degraded?: boolean; reason?: string; types?: unknown[]; files?: string[] }>("/project/scripts");
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    if (res.data.degraded) {
      return { content: [{ type: "text" as const, text: `⚠ ${res.data.reason}\nScript files:\n${(res.data.files ?? []).map(f => `• ${f}`).join("\n")}` }] };
    }
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return { content: [{ type: "text" as const, text: `Found ${res.data.types?.length ?? 0} types:\n${formatScriptTypes(res.data.types as any)}` }] };
  });

  server.registerTool("unity_find_assets", {
    description: "Search project assets by type and/or name keyword.",
    inputSchema: {
      type:   z.string().optional().describe("Asset type: AudioClip, Texture2D, Material, Prefab, AnimationClip, etc."),
      filter: z.string().optional().describe("Name keyword filter"),
    },
  }, async ({ type, filter }) => {
    await client.ensureConnected();
    const res = await client.get<{ count: number; assets: unknown[] }>("/asset/find", {
      type:   type   ?? "",
      filter: filter ?? "",
    });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    return { content: [{ type: "text" as const, text: `Found ${res.data.count} assets:\n${formatAssetList(res.data.assets as any)}` }] };
  });
}

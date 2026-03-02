import { UnityClient } from "../unity-client";
import { textResult, ToolResult } from "../utils/format";
import { handleError, unityError } from "../utils/error";

// TODO: replace with SDK types
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type API = any;

/** Encode a GameObject hierarchy path for use as a URL path segment. */
function encodeGoPath(path: string): string {
  return encodeURIComponent(path);
}

// Read-only tools only. Write operations (create, delete, transform, add_component, etc.)
// are handled exclusively by Claude Code via MCP Server.
export function registerGameObjectTools(api: API, client: UnityClient): void {

  api.registerTool({
    name: "unity_find_gameobjects",
    description: "Find GameObjects in the current scene by name or tag.",
    parameters: {
      type: "object",
      properties: {
        name: { type: "string", description: "Partial name to search for" },
        tag:  { type: "string", description: "Exact tag to filter by" }
      },
      required: []
    },
    execute: async (_toolCallId: string, params: { name?: string; tag?: string }): Promise<ToolResult> => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ count: number; objects: Array<{ name: string; path: string; active: boolean; tag: string }> }>(
          "/gameobject", { name: params?.name ?? "", tag: params?.tag ?? "" }
        );
        if (!res.ok) return unityError(res);
        if (!res.data.count) return textResult("No GameObjects found matching criteria.");
        return textResult(res.data.objects.map(o => `• ${o.path} (tag: ${o.tag}, active: ${o.active})`).join("\n"));
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_get_components",
    description: "List all components attached to a GameObject.",
    parameters: {
      type: "object",
      properties: { path: { type: "string", description: "Scene hierarchy path, e.g. 'Paddle'" } },
      required: ["path"]
    },
    execute: async (_toolCallId: string, params: { path: string }): Promise<ToolResult> => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ path: string; components: string[] }>(
          `/gameobject/${encodeGoPath(params.path)}/components`
        );
        if (!res.ok) return unityError(res);
        return textResult(`Components on '${params.path}':\n${res.data.components.map(c => `• ${c}`).join("\n")}`);
      } catch (err) { return handleError(err); }
    }
  });
}

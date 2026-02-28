import { UnityClient } from "../unity-client";
import { formatScriptTypes, formatAssetList } from "../utils/format";
import { handleError, unityError } from "../utils/error";

// TODO: replace with SDK types
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type API = any;

export function registerProjectTools(api: API, client: UnityClient): void {

  api.registerTool({
    name: "unity_get_project_info",
    description: "Get Unity project metadata: product name, Unity version, installed packages.",
    parameters: { type: "object", properties: {} },
    execute: async () => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ productName: string; unityVersion: string; buildTarget: string; packages: unknown }>("/project/info");
        if (!res.ok) return unityError(res);
        const d = res.data;
        return `Project: ${d.productName}\nUnity: ${d.unityVersion}\nBuild target: ${d.buildTarget}\nPackages: ${JSON.stringify(d.packages, null, 2)}`;
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_get_scripts",
    description: "List all user scripts in the project with their public API (classes, methods, fields). Falls back to file list if compilation failed.",
    parameters: { type: "object", properties: {} },
    execute: async () => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ degraded?: boolean; reason?: string; types?: unknown[]; files?: string[] }>("/project/scripts");
        if (!res.ok) return unityError(res);
        if (res.data.degraded) {
          return `⚠ ${res.data.reason}\nScript files:\n${(res.data.files ?? []).map(f => `• ${f}`).join("\n")}`;
        }
        // TODO: replace with SDK types
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return `Found ${res.data.types?.length ?? 0} types:\n${formatScriptTypes(res.data.types as any)}`;
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_find_assets",
    description: "Search project assets by type and/or name keyword.",
    parameters: {
      type: "object",
      properties: {
        type:   { type: "string", description: "Asset type: AudioClip, Texture2D, Material, Prefab, AnimationClip, etc." },
        filter: { type: "string", description: "Name keyword filter" }
      }
    },
    execute: async (params: { type?: string; filter?: string }) => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ count: number; assets: unknown[] }>("/asset/find", {
          type:   params.type   ?? "",
          filter: params.filter ?? ""
        });
        if (!res.ok) return unityError(res);
        // TODO: replace with SDK types
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return `Found ${res.data.count} assets:\n${formatAssetList(res.data.assets as any)}`;
      } catch (err) { return handleError(err); }
    }
  });
}

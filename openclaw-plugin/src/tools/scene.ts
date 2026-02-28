import { UnityClient } from "../unity-client";
import { formatHierarchy } from "../utils/format";
import { handleError, unityError } from "../utils/error";

// TODO: replace with SDK types
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type API = any;

export function registerSceneTools(api: API, client: UnityClient): void {

  api.registerTool({
    name: "unity_get_scene_info",
    description: "Get info about the currently open Unity scene (name, path, dirty state).",
    parameters: { type: "object", properties: {} },
    execute: async () => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ name: string; path: string; isDirty: boolean }>("/scene/info");
        if (!res.ok) return unityError(res);
        const d = res.data;
        return `Scene: ${d.name}\nPath: ${d.path}\nUnsaved changes: ${d.isDirty ? "yes" : "no"}`;
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_get_hierarchy",
    description: "Get the GameObject hierarchy tree of the current Unity scene.",
    parameters: {
      type: "object",
      properties: {
        depth:    { type: "number", description: "Max depth to traverse (0 = unlimited)" },
        maxNodes: { type: "number", description: "Max number of nodes to return (0 = unlimited)" }
      }
    },
    execute: async (params: { depth?: number; maxNodes?: number }) => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ roots: unknown[] }>("/scene/hierarchy", {
          depth:    params.depth    ?? 0,
          maxNodes: params.maxNodes ?? 0
        });
        if (!res.ok) return unityError(res);
        if (!res.data.roots.length) return "Scene is empty (no root GameObjects).";
        // TODO: replace with SDK types
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        return `Scene hierarchy:\n${formatHierarchy(res.data.roots as any)}`;
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_save_scene",
    description: "Save the currently open Unity scene.",
    parameters: { type: "object", properties: {} },
    execute: async () => {
      try {
        await client.ensureConnected();
        const res = await client.post("/scene/save");
        if (!res.ok) return unityError(res);
        return "Scene saved successfully.";
      } catch (err) { return handleError(err); }
    }
  });
}

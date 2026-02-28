import { UnityClient } from "../unity-client";
import { textResult, ToolResult } from "../utils/format";
import { handleError, unityError } from "../utils/error";

// TODO: replace with SDK types
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type API = any;

export function registerGameObjectTools(api: API, client: UnityClient): void {

  api.registerTool({
    name: "unity_create_gameobject",
    description: "Create a new GameObject in the current Unity scene.",
    parameters: {
      type: "object",
      properties: {
        name:       { type: "string", description: "Name for the new GameObject" },
        parentPath: { type: "string", description: "Scene path of the parent (e.g. 'Player/Body')" },
        position:   { type: "object", description: "World position", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } },
        primitive:  { type: "string", description: "Optional primitive type: Cube, Sphere, Capsule, Cylinder, Plane, Quad" }
      },
      required: ["name"]
    },
    execute: async (_toolCallId: string, params: { name: string; parentPath?: string; position?: { x: number; y: number; z: number }; primitive?: string }): Promise<ToolResult> => {
      try {
        await client.ensureConnected();
        const res = await client.post<{ path: string; name: string }>("/gameobject/create", params);
        if (!res.ok) return unityError(res);
        return textResult(`Created GameObject '${res.data.name}' at path: ${res.data.path}`);
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_delete_gameobject",
    description: "Delete a GameObject from the current scene by its scene path.",
    parameters: {
      type: "object",
      properties: {
        path: { type: "string", description: "Scene hierarchy path, e.g. 'Player/Weapon'" }
      },
      required: ["path"]
    },
    execute: async (_toolCallId: string, params: { path: string }): Promise<ToolResult> => {
      try {
        await client.ensureConnected();
        const res = await client.post("/gameobject/delete", params);
        if (!res.ok) return unityError(res);
        return textResult(`Deleted GameObject: ${params.path}`);
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_set_transform",
    description: "Set position, rotation (Euler angles), or scale of a GameObject.",
    parameters: {
      type: "object",
      properties: {
        path:     { type: "string", description: "Scene path of the target GameObject" },
        position: { type: "object", description: "World position", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } },
        rotation: { type: "object", description: "Euler angles in degrees", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } },
        scale:    { type: "object", description: "Local scale", properties: { x: { type: "number" }, y: { type: "number" }, z: { type: "number" } } }
      },
      required: ["path"]
    },
    execute: async (_toolCallId: string, params: { path: string; position?: object; rotation?: object; scale?: object }): Promise<ToolResult> => {
      try {
        await client.ensureConnected();
        const res = await client.post("/gameobject/transform", params);
        if (!res.ok) return unityError(res);
        return textResult(`Transform updated for: ${params.path}`);
      } catch (err) { return handleError(err); }
    }
  });

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
}

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { UnityClient } from "../unity-client.js";

const vec3 = z.object({ x: z.number(), y: z.number(), z: z.number() });

/** Encode a GameObject hierarchy path for use as a URL path segment.
 *  "Player/Weapon" → "Player%2FWeapon" so the router's [^/]+ regex matches it.
 *  The C# router calls Uri.UnescapeDataString, restoring the original path. */
function encodeGoPath(path: string): string {
  return encodeURIComponent(path);
}

export function registerGameObjectTools(server: McpServer, client: UnityClient): void {

  server.registerTool("unity_create_gameobject", {
    description: "Create a new GameObject in the current Unity scene.",
    inputSchema: {
      name:       z.string().describe("Name for the new GameObject"),
      parentPath: z.string().optional().describe("Scene path of the parent (e.g. 'Player/Body')"),
      position:   vec3.optional().describe("World position"),
      primitive:  z.string().optional().describe("Optional primitive type: Cube, Sphere, Capsule, Cylinder, Plane, Quad"),
      tag:        z.string().optional().describe("Tag to assign to the GameObject (e.g., 'Player', 'Enemy')"),
    },
  }, async (params) => {
    await client.ensureConnected();
    const res = await client.post<{ path: string; name: string; tag: string }>("/gameobject/create", params);
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    const tagInfo = res.data.tag ? ` (tag: ${res.data.tag})` : "";
    return { content: [{ type: "text" as const, text: `Created GameObject '${res.data.name}' at path: ${res.data.path}${tagInfo}` }] };
  });

  server.registerTool("unity_delete_gameobject", {
    description: "Delete a GameObject from the current scene by its scene path.",
    inputSchema: {
      path: z.string().describe("Scene hierarchy path, e.g. 'Player/Weapon'"),
    },
  }, async ({ path }) => {
    await client.ensureConnected();
    const res = await client.post("/gameobject/delete", { path });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    return { content: [{ type: "text" as const, text: `Deleted GameObject: ${path}` }] };
  });

  server.registerTool("unity_set_transform", {
    description: "Set position, rotation (Euler angles), or scale of a GameObject.",
    inputSchema: {
      path:     z.string().describe("Scene path of the target GameObject"),
      position: vec3.optional().describe("World position"),
      rotation: vec3.optional().describe("Euler angles in degrees"),
      scale:    vec3.optional().describe("Local scale"),
    },
  }, async (params) => {
    await client.ensureConnected();
    const res = await client.post("/gameobject/transform", params);
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    return { content: [{ type: "text" as const, text: `Transform updated for: ${params.path}` }] };
  });

  server.registerTool("unity_find_gameobjects", {
    description: "Find GameObjects in the current scene by name or tag.",
    inputSchema: {
      name: z.string().optional().describe("Partial name to search for"),
      tag:  z.string().optional().describe("Exact tag to filter by"),
    },
  }, async ({ name, tag }) => {
    await client.ensureConnected();
    const res = await client.get<{ count: number; objects: Array<{ name: string; path: string; active: boolean; tag: string }> }>(
      "/gameobject", { name: name ?? "", tag: tag ?? "" }
    );
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    if (!res.data.count) return { content: [{ type: "text" as const, text: "No GameObjects found matching criteria." }] };
    return { content: [{ type: "text" as const, text: res.data.objects.map(o => `• ${o.path} (tag: ${o.tag}, active: ${o.active})`).join("\n") }] };
  });

  server.registerTool("unity_get_components", {
    description: "List all components attached to a GameObject.",
    inputSchema: {
      path: z.string().describe("Scene hierarchy path, e.g. 'Paddle' or 'Player/Weapon'"),
    },
  }, async ({ path }) => {
    await client.ensureConnected();
    const res = await client.get<{ path: string; components: string[] }>(
      `/gameobject/${encodeGoPath(path)}/components`
    );
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    return { content: [{ type: "text" as const, text: `Components on '${path}':\n${res.data.components.map(c => `• ${c}`).join("\n")}` }] };
  });

  server.registerTool("unity_add_component", {
    description: "Add a component to a GameObject by type name. Works for both Unity built-in types (e.g. 'Rigidbody', 'BoxCollider2D') and user scripts (e.g. 'Paddle', 'BallController').",
    inputSchema: {
      path:          z.string().describe("Scene hierarchy path, e.g. 'Paddle' or 'Player/Weapon'"),
      componentType: z.string().describe("Component type name, e.g. 'Rigidbody2D', 'BoxCollider', 'Paddle'"),
    },
  }, async ({ path, componentType }) => {
    await client.ensureConnected();
    const res = await client.post(`/gameobject/${encodeGoPath(path)}/component/add`, { componentType });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    return { content: [{ type: "text" as const, text: `Added '${componentType}' to '${path}'.` }] };
  });

  server.registerTool("unity_set_component_property", {
    description: "Set serialized field values on a component. Use after unity_add_component to configure script variables (speed, damage, etc.) or built-in component properties.",
    inputSchema: {
      path:          z.string().describe("Scene hierarchy path, e.g. 'Paddle'"),
      componentType: z.string().describe("Component type name, e.g. 'Paddle', 'Rigidbody2D'"),
      properties:    z.record(z.union([z.string(), z.number(), z.boolean(),
                       z.object({ x: z.number(), y: z.number(), z: z.number().optional() })
                     ])).describe("Field name → value map, e.g. { \"speed\": 5.0, \"jumpForce\": 10.0 }"),
    },
  }, async ({ path, componentType, properties }) => {
    await client.ensureConnected();
    const res = await client.post(
      `/gameobject/${encodeGoPath(path)}/component/${encodeURIComponent(componentType)}/values`,
      { values: properties }
    );
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    return { content: [{ type: "text" as const, text: `Updated properties on '${componentType}' of '${path}': ${Object.keys(properties).join(", ")}` }] };
  });
}

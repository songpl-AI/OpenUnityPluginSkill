import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { UnityClient } from "../unity-client.js";

export function registerMaterialTools(server: McpServer, client: UnityClient): void {

  // ── Render Pipeline ─────────────────────────────────────────────────────────

  server.registerTool("unity_get_render_pipeline", {
    description: [
      "Detect the current render pipeline (builtin / urp / hdrp) and get the correct default shader name.",
      "Always call this BEFORE creating materials to avoid the pink-material issue.",
      "Returns the exact shader name to pass to unity_create_material.",
    ].join(" "),
    inputSchema: {},
  }, async () => {
    await client.ensureConnected();
    const res = await client.get<{
      pipeline: string;
      defaultShader: string;
      pipelineName: string;
    }>("/material/render-pipeline");
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    return {
      content: [{
        type: "text" as const,
        text: [
          `Render Pipeline : ${res.data.pipeline.toUpperCase()}`,
          `Pipeline asset  : ${res.data.pipelineName}`,
          `Default shader  : ${res.data.defaultShader}`,
          ``,
          `→ Pass "${res.data.defaultShader}" as the shader param when calling unity_create_material.`,
        ].join("\n"),
      }],
    };
  });

  // ── Material Properties ─────────────────────────────────────────────────────

  server.registerTool("unity_get_material_properties", {
    description: [
      "Get all shader properties and current values for a material asset.",
      "Use this to discover exact property names before calling unity_set_material_properties.",
      "Returns property name, type (Color/Float/Range/TexEnv/Vector), description, current value, and range limits.",
    ].join(" "),
    inputSchema: {
      path: z.string().describe("Asset path to the .mat file (e.g. 'Assets/Materials/Rock.mat')"),
    },
  }, async ({ path }) => {
    await client.ensureConnected();
    const res = await client.get<{
      shader: string;
      propertyCount: number;
      properties: Array<{
        name: string;
        type: string;
        description: string;
        value: unknown;
        range?: { min: number; max: number };
      }>;
    }>("/material/properties", { path });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    const lines = res.data.properties.map(p => {
      const rangeStr = p.range ? ` [${p.range.min}–${p.range.max}]` : "";
      const valStr   = Array.isArray(p.value)
        ? `[${(p.value as number[]).map(v => v.toFixed(3)).join(", ")}]`
        : String(p.value ?? "null");
      return `  ${p.name.padEnd(28)} ${("(" + p.type + rangeStr + ")").padEnd(20)}  "${p.description}"  =  ${valStr}`;
    });

    return {
      content: [{
        type: "text" as const,
        text: [
          `Shader: ${res.data.shader}  (${res.data.propertyCount} properties)`,
          ...lines,
        ].join("\n"),
      }],
    };
  });

  server.registerTool("unity_set_material_properties", {
    description: [
      "Set one or more properties on a material asset and save it.",
      "Color: [r, g, b, a] in 0–1 range.",
      "Float / Range: number.",
      "Texture: asset path string (e.g. 'Assets/Textures/Wood.png'), or null to clear.",
      "Vector: [x, y, z, w].",
      "Call unity_get_material_properties first to get correct property names.",
    ].join(" "),
    inputSchema: {
      path: z.string().describe("Asset path to the .mat file"),
      properties: z.record(z.unknown()).describe(
        "Map of shader property name → value. " +
        "Examples: { \"_BaseColor\": [1, 0.2, 0, 1], \"_Metallic\": 0.8, \"_BaseMap\": \"Assets/Textures/Rock.png\" }"
      ),
    },
  }, async ({ path, properties }) => {
    await client.ensureConnected();
    const res = await client.post<{
      path: string;
      applied: string[];
      skipped: string[];
    }>("/material/properties", { path, properties });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    const lines: string[] = [
      `✅ Updated material: ${res.data.path}`,
      `Applied (${res.data.applied.length}): ${res.data.applied.join(", ") || "none"}`,
    ];
    if (res.data.skipped.length > 0)
      lines.push(`⚠️  Skipped (not in shader): ${res.data.skipped.join(", ")}`);

    return { content: [{ type: "text" as const, text: lines.join("\n") }] };
  });

  // ── Assign ─────────────────────────────────────────────────────────────────

  server.registerTool("unity_assign_material", {
    description: [
      "Assign a material asset to a GameObject's Renderer in the current scene.",
      "Supports multi-material renderers via the slot parameter.",
      "Uses sharedMaterial to avoid creating runtime instances.",
    ].join(" "),
    inputSchema: {
      goPath:       z.string().describe("Scene hierarchy path of the GameObject (e.g. 'Cube', 'Level/Floor')"),
      materialPath: z.string().describe("Asset path to the .mat file (e.g. 'Assets/Materials/Rock.mat')"),
      slot:         z.number().optional().describe("Material slot index for multi-material meshes (default: 0)"),
    },
  }, async ({ goPath, materialPath, slot }) => {
    await client.ensureConnected();
    const res = await client.post<{
      goPath: string;
      materialPath: string;
      slot: number;
    }>("/material/assign", { goPath, materialPath, slot: slot ?? 0 });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    return {
      content: [{
        type: "text" as const,
        text: `✅ Assigned '${res.data.materialPath}' → '${res.data.goPath}' (slot ${res.data.slot})`,
      }],
    };
  });
}

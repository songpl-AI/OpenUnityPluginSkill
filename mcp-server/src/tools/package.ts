import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { UnityClient } from "../unity-client.js";

export function registerPackageTools(server: McpServer, client: UnityClient): void {

  server.registerTool("unity_list_packages", {
    description: "List all installed Unity packages (excluding built-in engine packages by default).",
    inputSchema: {
      includeBuiltIn: z.boolean().optional().describe("Include Unity built-in packages in the list (default: false)"),
    },
  }, async ({ includeBuiltIn }) => {
    await client.ensureConnected();
    const res = await client.get<{
      count: number;
      packages: Array<{ packageId: string; displayName: string; version: string; source: string }>;
    }>("/package/list", { includeBuiltIn: includeBuiltIn ? "true" : "false" });

    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    if (res.data.count === 0) return { content: [{ type: "text" as const, text: "No packages installed." }] };

    const lines = res.data.packages.map(p => `• ${p.displayName} (${p.packageId}) v${p.version}`);
    return {
      content: [{ type: "text" as const, text: `Installed packages (${res.data.count}):\n${lines.join("\n")}` }]
    };
  });

  server.registerTool("unity_install_package", {
    description: [
      "Install a Unity package via Package Manager. Use the package ID from the Unity registry.",
      "Examples: 'com.unity.textmeshpro', 'com.unity.cinemachine', 'com.unity.textmeshpro@3.0.6'.",
      "WARNING: Installation triggers a Domain Reload — the Unity plugin will restart automatically.",
      "After calling this tool, wait a few seconds then use unity_check_status to confirm Unity is ready.",
    ].join(" "),
    inputSchema: {
      packageId: z.string().describe(
        "Package identifier, optionally with version (e.g. 'com.unity.textmeshpro' or 'com.unity.textmeshpro@3.0.6')"
      ),
    },
  }, async ({ packageId }) => {
    await client.ensureConnected();

    // Package install blocks C# side for up to 120s — use extended timeout
    const res = await client.post<{
      packageId: string;
      displayName: string;
      version: string;
      installed: boolean;
      note: string;
    }>("/package/add", { packageId }, { timeoutMs: 130_000 });

    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    return {
      content: [{
        type: "text" as const,
        text: [
          `✅ Installed: ${res.data.displayName} v${res.data.version}`,
          `   Package ID: ${res.data.packageId}`,
          `   ${res.data.note}`,
        ].join("\n")
      }]
    };
  });

  server.registerTool("unity_remove_package", {
    description: [
      "Remove an installed Unity package via Package Manager.",
      "WARNING: Removal triggers a Domain Reload — the Unity plugin will restart automatically.",
      "Do not remove packages that the project depends on.",
    ].join(" "),
    inputSchema: {
      packageId: z.string().describe("Package identifier to remove (e.g. 'com.unity.textmeshpro')"),
    },
  }, async ({ packageId }) => {
    await client.ensureConnected();

    const res = await client.post<{ packageId: string; removed: boolean; note: string }>(
      "/package/remove",
      { packageId },
      { timeoutMs: 70_000 }
    );

    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);

    return {
      content: [{
        type: "text" as const,
        text: [`✅ Removed: ${res.data.packageId}`, `   ${res.data.note}`].join("\n")
      }]
    };
  });
}

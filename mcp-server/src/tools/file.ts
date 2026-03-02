import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { UnityClient } from "../unity-client.js";

export function registerFileTools(server: McpServer, client: UnityClient): void {

  server.registerTool("unity_read_file", {
    description: "Read the contents of a file inside the Unity project (Assets/ directory).",
    inputSchema: {
      path: z.string().describe("Relative path from project root, e.g. Assets/Scripts/Player.cs"),
    },
  }, async ({ path }) => {
    await client.ensureConnected();
    const res = await client.get<{ path: string; content: string }>("/file/read", { path });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    return { content: [{ type: "text" as const, text: `File: ${res.data.path}\n\`\`\`csharp\n${res.data.content}\n\`\`\`` }] };
  });

  server.registerTool("unity_write_file", {
    description: "Write or overwrite a file in the Unity project Assets directory. Triggers AssetDatabase refresh automatically.",
    inputSchema: {
      path:    z.string().describe("Relative path, e.g. Assets/Scripts/Enemy.cs"),
      content: z.string().describe("Full file content to write"),
    },
  }, async ({ path, content }) => {
    await client.ensureConnected();
    const res = await client.post<{ path: string; written: number }>("/file/write", { path, content });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    return { content: [{ type: "text" as const, text: `Written ${res.data.written} chars to ${res.data.path}. AssetDatabase refresh triggered.` }] };
  });
}

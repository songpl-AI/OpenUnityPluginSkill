import { z } from "zod";
export function registerFileTools(server, client) {
    server.registerTool("unity_read_file", {
        description: "Read the contents of a file inside the Unity project (Assets/ directory).",
        inputSchema: {
            path: z.string().describe("Relative path from project root, e.g. Assets/Scripts/Player.cs"),
        },
    }, async ({ path }) => {
        await client.ensureConnected();
        const res = await client.get("/file/read", { path });
        if (!res.ok)
            throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
        return { content: [{ type: "text", text: `File: ${res.data.path}\n\`\`\`csharp\n${res.data.content}\n\`\`\`` }] };
    });
    server.registerTool("unity_write_file", {
        description: "Write or overwrite a file in the Unity project Assets directory. Triggers AssetDatabase refresh automatically.",
        inputSchema: {
            path: z.string().describe("Relative path, e.g. Assets/Scripts/Enemy.cs"),
            content: z.string().describe("Full file content to write"),
        },
    }, async ({ path, content }) => {
        await client.ensureConnected();
        const res = await client.post("/file/write", { path, content });
        if (!res.ok)
            throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
        return { content: [{ type: "text", text: `Written ${res.data.written} chars to ${res.data.path}. AssetDatabase refresh triggered.` }] };
    });
}
//# sourceMappingURL=file.js.map
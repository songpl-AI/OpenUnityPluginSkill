import { UnityClient } from "../unity-client";
import { textResult, ToolResult } from "../utils/format";
import { handleError, unityError } from "../utils/error";

// TODO: replace with SDK types
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type API = any;

export function registerFileTools(api: API, client: UnityClient): void {

  api.registerTool({
    name: "unity_read_file",
    description: "Read the contents of a file inside the Unity project (Assets/ directory).",
    parameters: {
      type: "object",
      properties: {
        path: { type: "string", description: "Relative path from project root, e.g. Assets/Scripts/Player.cs" }
      },
      required: ["path"]
    },
    execute: async (_toolCallId: string, params: { path: string }): Promise<ToolResult> => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ path: string; content: string }>("/file/read", { path: params.path });
        if (!res.ok) return unityError(res);
        return textResult(`File: ${res.data.path}\n\`\`\`csharp\n${res.data.content}\n\`\`\``);
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_write_file",
    description: "Write or overwrite a file in the Unity project Assets directory. Triggers AssetDatabase refresh automatically.",
    parameters: {
      type: "object",
      properties: {
        path:    { type: "string", description: "Relative path, e.g. Assets/Scripts/Enemy.cs" },
        content: { type: "string", description: "Full file content to write" }
      },
      required: ["path", "content"]
    },
    execute: async (_toolCallId: string, params: { path: string; content: string }): Promise<ToolResult> => {
      try {
        await client.ensureConnected();
        const res = await client.post<{ path: string; written: number }>("/file/write", {
          path:    params.path,
          content: params.content
        });
        if (!res.ok) return unityError(res);
        return textResult(`Written ${res.data.written} chars to ${res.data.path}. AssetDatabase refresh triggered.`);
      } catch (err) { return handleError(err); }
    }
  });
}

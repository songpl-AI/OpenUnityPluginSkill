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

}

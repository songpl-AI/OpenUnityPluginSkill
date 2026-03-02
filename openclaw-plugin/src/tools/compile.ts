import { UnityClient } from "../unity-client";
import { formatCompileErrors, textResult, ToolResult } from "../utils/format";
import { handleError, unityError } from "../utils/error";

// TODO: replace with SDK types
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type API = any;

export function registerCompileTools(api: API, client: UnityClient): void {

  api.registerTool({
    name: "unity_get_compile_errors",
    description: "Get the list of compile errors from the last compilation attempt.",
    parameters: {
      type: "object",
      properties: {
        type: { type: "string", description: "Filter by type: 'error' or 'warning' (default: all)" }
      },
      required: []
    },
    execute: async (_toolCallId: string, params: { type?: string }): Promise<ToolResult> => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ errors: unknown[]; status: string }>("/compile/errors", { type: params?.type ?? "" });
        if (!res.ok) return unityError(res);
        // TODO: replace with SDK types
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const formatted = formatCompileErrors(res.data.errors as any);
        return textResult(`Compile status: ${res.data.status}\n${formatted}`);
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_get_console_logs",
    description: "Get Unity Console logs (errors, warnings, log messages).",
    parameters: {
      type: "object",
      properties: {
        type:  { type: "string", description: "'log' | 'warning' | 'error' (default: all)" },
        limit: { type: "number", description: "Max entries to return (default: 50)" }
      },
      required: []
    },
    execute: async (_toolCallId: string, params: { type?: string; limit?: number }): Promise<ToolResult> => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ logs: Array<{ type: string; message: string }> }>(
          "/console/logs",
          { type: params?.type ?? "", limit: params?.limit ?? 50 }
        );
        if (!res.ok) return unityError(res);
        if (!res.data.logs.length) return textResult("Console is empty.");
        return textResult(res.data.logs.map(l => `[${l.type.toUpperCase()}] ${l.message}`).join("\n"));
      } catch (err) { return handleError(err); }
    }
  });
}

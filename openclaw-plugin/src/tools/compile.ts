import { UnityClient } from "../unity-client";
import { UnityWsClient } from "../unity-ws-client";
import { formatCompileErrors } from "../utils/format";
import { handleError, unityError } from "../utils/error";

// TODO: replace with SDK types
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type API = any;

export function registerCompileTools(api: API, client: UnityClient, ws: UnityWsClient): void {

  api.registerTool({
    name: "unity_compile",
    description: "Trigger Unity script compilation and wait for the result. Returns compile errors if any.",
    parameters: {
      type: "object",
      properties: {
        timeoutSeconds: {
          type: "number",
          description: "Max seconds to wait for compilation result (default: 60)"
        }
      }
    },
    execute: async (params: { timeoutSeconds?: number }) => {
      try {
        await client.ensureConnected();
        // 触发编译
        await client.post("/editor/compile");
        // 等待 WebSocket 编译结果事件
        const timeoutMs = (params.timeoutSeconds ?? 60) * 1000;
        const result    = await Promise.race([
          ws.waitForEvent("compile_complete", timeoutMs),
          ws.waitForEvent("compile_failed",   timeoutMs),
        ]) as { errors?: unknown[] };

        if (result?.errors?.length) {
          // compile_failed
          const errRes = await client.get<{ errors: unknown[] }>("/compile/errors");
          if (!errRes.ok) return unityError(errRes);
          // TODO: replace with SDK types
          // eslint-disable-next-line @typescript-eslint/no-explicit-any
          return `Compilation FAILED:\n${formatCompileErrors(errRes.data.errors as any)}`;
        }
        return "Compilation succeeded.";
      } catch (err) { return handleError(err); }
    }
  });

  api.registerTool({
    name: "unity_get_compile_errors",
    description: "Get the list of compile errors from the last compilation attempt.",
    parameters: {
      type: "object",
      properties: {
        type: { type: "string", description: "Filter by type: 'error' or 'warning' (default: all)" }
      }
    },
    execute: async (params: { type?: string }) => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ errors: unknown[]; status: string }>("/compile/errors", { type: params.type ?? "" });
        if (!res.ok) return unityError(res);
        // TODO: replace with SDK types
        // eslint-disable-next-line @typescript-eslint/no-explicit-any
        const formatted = formatCompileErrors(res.data.errors as any);
        return `Compile status: ${res.data.status}\n${formatted}`;
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
      }
    },
    execute: async (params: { type?: string; limit?: number }) => {
      try {
        await client.ensureConnected();
        const res = await client.get<{ logs: Array<{ type: string; message: string }> }>(
          "/console/logs",
          { type: params.type ?? "", limit: params.limit ?? 50 }
        );
        if (!res.ok) return unityError(res);
        if (!res.data.logs.length) return "Console is empty.";
        return res.data.logs.map(l => `[${l.type.toUpperCase()}] ${l.message}`).join("\n");
      } catch (err) { return handleError(err); }
    }
  });
}

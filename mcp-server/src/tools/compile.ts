import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { z } from "zod";
import { UnityClient } from "../unity-client.js";
import { UnityWsClient } from "../unity-ws-client.js";
import { formatCompileErrors } from "../utils/format.js";

export function registerCompileTools(server: McpServer, client: UnityClient, ws: UnityWsClient): void {

  server.registerTool("unity_compile", {
    description: "Trigger Unity script compilation and wait for the result. Returns compile errors if any. Use this after writing/modifying C# scripts.",
    inputSchema: {
      timeoutSeconds: z.number().optional().describe("Max seconds to wait for compilation result (default: 60)"),
    },
  }, async ({ timeoutSeconds }) => {
    await client.ensureConnected();
    // 触发编译
    await client.post("/editor/compile");
    // 等待 WebSocket 编译结果事件（compile_complete 或 compile_failed）
    const timeoutMs = (timeoutSeconds ?? 60) * 1000;
    const result = await Promise.race([
      ws.waitForEvent("compile_complete", timeoutMs),
      ws.waitForEvent("compile_failed",   timeoutMs),
    ]) as { errors?: unknown[] };

    if (result?.errors?.length) {
      // compile_failed：获取详细错误并格式化
      const errRes = await client.get<{ errors: unknown[] }>("/compile/errors");
      if (!errRes.ok) throw new Error(`Failed to get compile errors: ${errRes.error?.message}`);
      // eslint-disable-next-line @typescript-eslint/no-explicit-any
      return { content: [{ type: "text" as const, text: `Compilation FAILED:\n${formatCompileErrors(errRes.data.errors as any)}` }] };
    }
    return { content: [{ type: "text" as const, text: "Compilation succeeded." }] };
  });

  server.registerTool("unity_get_compile_errors", {
    description: "Get the list of compile errors from the last compilation attempt.",
    inputSchema: {
      type: z.string().optional().describe("Filter by type: 'error' or 'warning' (default: all)"),
    },
  }, async ({ type }) => {
    await client.ensureConnected();
    const res = await client.get<{ errors: unknown[]; status: string }>("/compile/errors", { type: type ?? "" });
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    // eslint-disable-next-line @typescript-eslint/no-explicit-any
    const formatted = formatCompileErrors(res.data.errors as any);
    return { content: [{ type: "text" as const, text: `Compile status: ${res.data.status}\n${formatted}` }] };
  });

  server.registerTool("unity_get_console_logs", {
    description: "Get Unity Console logs (errors, warnings, log messages).",
    inputSchema: {
      type:  z.string().optional().describe("'log' | 'warning' | 'error' (default: all)"),
      limit: z.number().optional().describe("Max entries to return (default: 50)"),
    },
  }, async ({ type, limit }) => {
    await client.ensureConnected();
    const res = await client.get<{ logs: Array<{ type: string; message: string }> }>(
      "/console/logs",
      { type: type ?? "", limit: limit ?? 50 }
    );
    if (!res.ok) throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
    if (!res.data.logs.length) return { content: [{ type: "text" as const, text: "Console is empty." }] };
    return { content: [{ type: "text" as const, text: res.data.logs.map(l => `[${l.type.toUpperCase()}] ${l.message}`).join("\n") }] };
  });
}

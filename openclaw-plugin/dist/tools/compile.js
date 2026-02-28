"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.registerCompileTools = registerCompileTools;
const format_1 = require("../utils/format");
const error_1 = require("../utils/error");
function registerCompileTools(api, client, ws) {
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
        execute: async (params) => {
            try {
                await client.ensureConnected();
                // 触发编译
                await client.post("/editor/compile");
                // 等待 WebSocket 编译结果事件
                const timeoutMs = (params.timeoutSeconds ?? 60) * 1000;
                const result = await Promise.race([
                    ws.waitForEvent("compile_complete", timeoutMs),
                    ws.waitForEvent("compile_failed", timeoutMs),
                ]);
                if (result?.errors?.length) {
                    // compile_failed
                    const errRes = await client.get("/compile/errors");
                    if (!errRes.ok)
                        return (0, error_1.unityError)(errRes);
                    // TODO: replace with SDK types
                    // eslint-disable-next-line @typescript-eslint/no-explicit-any
                    return `Compilation FAILED:\n${(0, format_1.formatCompileErrors)(errRes.data.errors)}`;
                }
                return "Compilation succeeded.";
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
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
        execute: async (params) => {
            try {
                await client.ensureConnected();
                const res = await client.get("/compile/errors", { type: params.type ?? "" });
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                // TODO: replace with SDK types
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                const formatted = (0, format_1.formatCompileErrors)(res.data.errors);
                return `Compile status: ${res.data.status}\n${formatted}`;
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
    api.registerTool({
        name: "unity_get_console_logs",
        description: "Get Unity Console logs (errors, warnings, log messages).",
        parameters: {
            type: "object",
            properties: {
                type: { type: "string", description: "'log' | 'warning' | 'error' (default: all)" },
                limit: { type: "number", description: "Max entries to return (default: 50)" }
            }
        },
        execute: async (params) => {
            try {
                await client.ensureConnected();
                const res = await client.get("/console/logs", { type: params.type ?? "", limit: params.limit ?? 50 });
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                if (!res.data.logs.length)
                    return "Console is empty.";
                return res.data.logs.map(l => `[${l.type.toUpperCase()}] ${l.message}`).join("\n");
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
}
//# sourceMappingURL=compile.js.map
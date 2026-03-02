"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.registerCompileTools = registerCompileTools;
const format_1 = require("../utils/format");
const error_1 = require("../utils/error");
function registerCompileTools(api, client) {
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
        execute: async (_toolCallId, params) => {
            try {
                await client.ensureConnected();
                const res = await client.get("/compile/errors", { type: params?.type ?? "" });
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                // TODO: replace with SDK types
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                const formatted = (0, format_1.formatCompileErrors)(res.data.errors);
                return (0, format_1.textResult)(`Compile status: ${res.data.status}\n${formatted}`);
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
            },
            required: []
        },
        execute: async (_toolCallId, params) => {
            try {
                await client.ensureConnected();
                const res = await client.get("/console/logs", { type: params?.type ?? "", limit: params?.limit ?? 50 });
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                if (!res.data.logs.length)
                    return (0, format_1.textResult)("Console is empty.");
                return (0, format_1.textResult)(res.data.logs.map(l => `[${l.type.toUpperCase()}] ${l.message}`).join("\n"));
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
}
//# sourceMappingURL=compile.js.map
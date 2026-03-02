"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.registerGameObjectTools = registerGameObjectTools;
const format_1 = require("../utils/format");
const error_1 = require("../utils/error");
/** Encode a GameObject hierarchy path for use as a URL path segment. */
function encodeGoPath(path) {
    return encodeURIComponent(path);
}
// Read-only tools only. Write operations (create, delete, transform, add_component, etc.)
// are handled exclusively by Claude Code via MCP Server.
function registerGameObjectTools(api, client) {
    api.registerTool({
        name: "unity_find_gameobjects",
        description: "Find GameObjects in the current scene by name or tag.",
        parameters: {
            type: "object",
            properties: {
                name: { type: "string", description: "Partial name to search for" },
                tag: { type: "string", description: "Exact tag to filter by" }
            },
            required: []
        },
        execute: async (_toolCallId, params) => {
            try {
                await client.ensureConnected();
                const res = await client.get("/gameobject", { name: params?.name ?? "", tag: params?.tag ?? "" });
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                if (!res.data.count)
                    return (0, format_1.textResult)("No GameObjects found matching criteria.");
                return (0, format_1.textResult)(res.data.objects.map(o => `• ${o.path} (tag: ${o.tag}, active: ${o.active})`).join("\n"));
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
    api.registerTool({
        name: "unity_get_components",
        description: "List all components attached to a GameObject.",
        parameters: {
            type: "object",
            properties: { path: { type: "string", description: "Scene hierarchy path, e.g. 'Paddle'" } },
            required: ["path"]
        },
        execute: async (_toolCallId, params) => {
            try {
                await client.ensureConnected();
                const res = await client.get(`/gameobject/${encodeGoPath(params.path)}/components`);
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                return (0, format_1.textResult)(`Components on '${params.path}':\n${res.data.components.map(c => `• ${c}`).join("\n")}`);
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
}
//# sourceMappingURL=gameobject.js.map
"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.registerSceneTools = registerSceneTools;
const format_1 = require("../utils/format");
const error_1 = require("../utils/error");
function registerSceneTools(api, client) {
    api.registerTool({
        name: "unity_check_status",
        description: "Check whether the Unity Editor is running and the OpenClaw plugin is active. Call this first before any other Unity tool if unsure about the connection state.",
        parameters: { type: "object", properties: {}, required: [] },
        // _toolCallId 是框架注入的调用 ID，忽略即可
        execute: async (_toolCallId) => {
            try {
                const res = await client.get("/status");
                if (!res.ok)
                    return (0, format_1.textResult)(`Unity plugin is running but returned an error: ${res.error?.message}`);
                const d = res.data;
                return (0, format_1.textResult)(`Unity Editor is running.\n` +
                    `Unity version: ${d.unityVersion}\n` +
                    `Product: ${d.productName}\n` +
                    `Open scene: ${d.currentScene || "(none)"}\n` +
                    `Compile status: ${d.compileStatus}\n` +
                    `Is playing: ${d.isPlaying}`);
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
    api.registerTool({
        name: "unity_get_scene_info",
        description: "Get info about the currently open Unity scene (name, path, dirty state).",
        parameters: { type: "object", properties: {}, required: [] },
        execute: async (_toolCallId) => {
            try {
                await client.ensureConnected();
                const res = await client.get("/scene/info");
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                const d = res.data;
                return (0, format_1.textResult)(`Scene: ${d.name}\nPath: ${d.path}\nUnsaved changes: ${d.isDirty ? "yes" : "no"}`);
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
    api.registerTool({
        name: "unity_get_hierarchy",
        description: "Get the GameObject hierarchy tree of the current Unity scene.",
        parameters: {
            type: "object",
            properties: {
                depth: { type: "number", description: "Max depth to traverse (0 = unlimited)" },
                maxNodes: { type: "number", description: "Max number of nodes to return (0 = unlimited)" }
            },
            required: []
        },
        execute: async (_toolCallId, params) => {
            try {
                await client.ensureConnected();
                const res = await client.get("/scene/hierarchy", {
                    depth: params?.depth ?? 0,
                    maxNodes: params?.maxNodes ?? 0
                });
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                if (!res.data.roots.length)
                    return (0, format_1.textResult)("Scene is empty (no root GameObjects).");
                // TODO: replace with SDK types
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                return (0, format_1.textResult)(`Scene hierarchy:\n${(0, format_1.formatHierarchy)(res.data.roots)}`);
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
}
//# sourceMappingURL=scene.js.map
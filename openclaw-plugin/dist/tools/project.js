"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.registerProjectTools = registerProjectTools;
const format_1 = require("../utils/format");
const error_1 = require("../utils/error");
function registerProjectTools(api, client) {
    api.registerTool({
        name: "unity_get_project_info",
        description: "Get Unity project metadata: product name, Unity version, installed packages.",
        parameters: { type: "object", properties: {} },
        execute: async () => {
            try {
                await client.ensureConnected();
                const res = await client.get("/project/info");
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                const d = res.data;
                return `Project: ${d.productName}\nUnity: ${d.unityVersion}\nBuild target: ${d.buildTarget}\nPackages: ${JSON.stringify(d.packages, null, 2)}`;
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
    api.registerTool({
        name: "unity_get_scripts",
        description: "List all user scripts in the project with their public API (classes, methods, fields). Falls back to file list if compilation failed.",
        parameters: { type: "object", properties: {} },
        execute: async () => {
            try {
                await client.ensureConnected();
                const res = await client.get("/project/scripts");
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                if (res.data.degraded) {
                    return `⚠ ${res.data.reason}\nScript files:\n${(res.data.files ?? []).map(f => `• ${f}`).join("\n")}`;
                }
                // TODO: replace with SDK types
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                return `Found ${res.data.types?.length ?? 0} types:\n${(0, format_1.formatScriptTypes)(res.data.types)}`;
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
    api.registerTool({
        name: "unity_find_assets",
        description: "Search project assets by type and/or name keyword.",
        parameters: {
            type: "object",
            properties: {
                type: { type: "string", description: "Asset type: AudioClip, Texture2D, Material, Prefab, AnimationClip, etc." },
                filter: { type: "string", description: "Name keyword filter" }
            }
        },
        execute: async (params) => {
            try {
                await client.ensureConnected();
                const res = await client.get("/asset/find", {
                    type: params.type ?? "",
                    filter: params.filter ?? ""
                });
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                // TODO: replace with SDK types
                // eslint-disable-next-line @typescript-eslint/no-explicit-any
                return `Found ${res.data.count} assets:\n${(0, format_1.formatAssetList)(res.data.assets)}`;
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
}
//# sourceMappingURL=project.js.map
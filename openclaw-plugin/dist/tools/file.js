"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.registerFileTools = registerFileTools;
const format_1 = require("../utils/format");
const error_1 = require("../utils/error");
function registerFileTools(api, client) {
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
        execute: async (_toolCallId, params) => {
            try {
                await client.ensureConnected();
                const res = await client.get("/file/read", { path: params.path });
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                return (0, format_1.textResult)(`File: ${res.data.path}\n\`\`\`csharp\n${res.data.content}\n\`\`\``);
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
    api.registerTool({
        name: "unity_write_file",
        description: "Write or overwrite a file in the Unity project Assets directory. Triggers AssetDatabase refresh automatically.",
        parameters: {
            type: "object",
            properties: {
                path: { type: "string", description: "Relative path, e.g. Assets/Scripts/Enemy.cs" },
                content: { type: "string", description: "Full file content to write" }
            },
            required: ["path", "content"]
        },
        execute: async (_toolCallId, params) => {
            try {
                await client.ensureConnected();
                const res = await client.post("/file/write", {
                    path: params.path,
                    content: params.content
                });
                if (!res.ok)
                    return (0, error_1.unityError)(res);
                return (0, format_1.textResult)(`Written ${res.data.written} chars to ${res.data.path}. AssetDatabase refresh triggered.`);
            }
            catch (err) {
                return (0, error_1.handleError)(err);
            }
        }
    });
}
//# sourceMappingURL=file.js.map
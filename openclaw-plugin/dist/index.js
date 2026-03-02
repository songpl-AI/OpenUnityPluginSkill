"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.default = register;
const unity_client_1 = require("./unity-client");
const scene_1 = require("./tools/scene");
const gameobject_1 = require("./tools/gameobject");
const file_1 = require("./tools/file");
const compile_1 = require("./tools/compile");
const project_1 = require("./tools/project");
// Only read-only tools are registered here.
// All write operations (create GO, write file, compile, add component, etc.)
// are performed exclusively by Claude Code via MCP Server (mcp-server/).
function register(api) {
    const config = (api.pluginConfig ?? {});
    const port = config.port ?? 23456;
    const timeout = config.timeout ?? 15000;
    const client = new unity_client_1.UnityClient({ port, timeout });
    (0, scene_1.registerSceneTools)(api, client);
    (0, gameobject_1.registerGameObjectTools)(api, client);
    (0, file_1.registerFileTools)(api, client);
    (0, compile_1.registerCompileTools)(api, client);
    (0, project_1.registerProjectTools)(api, client);
}
//# sourceMappingURL=index.js.map
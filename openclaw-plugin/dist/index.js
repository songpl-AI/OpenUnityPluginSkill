"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.default = register;
const unity_client_1 = require("./unity-client");
const unity_ws_client_1 = require("./unity-ws-client");
const scene_1 = require("./tools/scene");
const gameobject_1 = require("./tools/gameobject");
const file_1 = require("./tools/file");
const compile_1 = require("./tools/compile");
const project_1 = require("./tools/project");
function register(api) {
    const config = (api.pluginConfig ?? {});
    const port = config.port ?? 23456;
    const wsPort = config.wsPort ?? (port + 1);
    const timeout = config.timeout ?? 15000;
    const client = new unity_client_1.UnityClient({ port, timeout });
    const ws = new unity_ws_client_1.UnityWsClient(wsPort);
    // WebSocket 连接（后台持续，不阻塞 Plugin 加载）
    ws.connect();
    ws.on("connected", () => console.log("[OpenClaw Unity] WebSocket connected"));
    ws.on("disconnected", () => console.log("[OpenClaw Unity] WebSocket disconnected, reconnecting..."));
    // 注册所有工具
    (0, scene_1.registerSceneTools)(api, client);
    (0, gameobject_1.registerGameObjectTools)(api, client);
    (0, file_1.registerFileTools)(api, client);
    (0, compile_1.registerCompileTools)(api, client, ws);
    (0, project_1.registerProjectTools)(api, client);
    // Plugin 卸载时关闭 WS（若 OpenClaw 支持 dispose 钩子）
    if (typeof api.onDispose === "function") {
        api.onDispose(() => ws.disconnect());
    }
}
//# sourceMappingURL=index.js.map
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { UnityClient } from "./unity-client.js";
import { UnityWsClient } from "./unity-ws-client.js";
import { registerSceneTools } from "./tools/scene.js";
import { registerGameObjectTools } from "./tools/gameobject.js";
import { registerFileTools } from "./tools/file.js";
import { registerCompileTools } from "./tools/compile.js";
import { registerProjectTools } from "./tools/project.js";
import { registerTagTools } from "./tools/tag.js";
import { registerSettingsTools } from "./tools/settings.js";
import { registerPackageTools } from "./tools/package.js";
import { registerMaterialTools } from "./tools/material.js";

const server = new McpServer({
  name: "unity-editor",
  version: "1.0.0",
});

const client = new UnityClient({
  port: Number(process.env.UNITY_PORT ?? 23456),
  timeout: 15000,
});

// ⚠️ 必须在工具注册前连接，unity_compile 工具依赖 WS 事件
const ws = new UnityWsClient(Number(process.env.UNITY_WS_PORT ?? 23457));
ws.connect();

// 注册所有 Unity 工具
registerSceneTools(server, client);
registerGameObjectTools(server, client);
registerFileTools(server, client);
registerCompileTools(server, client, ws); // ws 传入 compile 工具
registerProjectTools(server, client);
registerTagTools(server, client);
registerSettingsTools(server, client);
registerPackageTools(server, client);
registerMaterialTools(server, client);

// stdio 传输：按需启动，无需长驻进程
const transport = new StdioServerTransport();
await server.connect(transport);

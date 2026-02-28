import { UnityClient } from "./unity-client";
import { UnityWsClient } from "./unity-ws-client";
import { registerSceneTools }      from "./tools/scene";
import { registerGameObjectTools } from "./tools/gameobject";
import { registerFileTools }       from "./tools/file";
import { registerCompileTools }    from "./tools/compile";
import { registerProjectTools }    from "./tools/project";

// TODO: replace with SDK types
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type OpenClawPluginAPI = any;

export default function register(api: OpenClawPluginAPI): void {
  const config = (api.pluginConfig ?? {}) as { port?: number; wsPort?: number; timeout?: number };

  const port    = config.port    ?? 23456;
  const wsPort  = config.wsPort  ?? (port + 1);
  const timeout = config.timeout ?? 15000;

  const client = new UnityClient({ port, timeout });
  const ws     = new UnityWsClient(wsPort);

  // WebSocket 连接（后台持续，不阻塞 Plugin 加载）
  ws.connect();
  ws.on("connected",    () => console.log("[OpenClaw Unity] WebSocket connected"));
  ws.on("disconnected", () => console.log("[OpenClaw Unity] WebSocket disconnected, reconnecting..."));

  // 注册所有工具
  registerSceneTools(api, client);
  registerGameObjectTools(api, client);
  registerFileTools(api, client);
  registerCompileTools(api, client, ws);
  registerProjectTools(api, client);

  // Plugin 卸载时关闭 WS（若 OpenClaw 支持 dispose 钩子）
  if (typeof api.onDispose === "function") {
    api.onDispose(() => ws.disconnect());
  }
}

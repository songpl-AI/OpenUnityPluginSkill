import { UnityClient } from "./unity-client";
import { registerSceneTools }      from "./tools/scene";
import { registerGameObjectTools } from "./tools/gameobject";
import { registerFileTools }       from "./tools/file";
import { registerCompileTools }    from "./tools/compile";
import { registerProjectTools }    from "./tools/project";

// TODO: replace with SDK types
// eslint-disable-next-line @typescript-eslint/no-explicit-any
type OpenClawPluginAPI = any;

// Only read-only tools are registered here.
// All write operations (create GO, write file, compile, add component, etc.)
// are performed exclusively by Claude Code via MCP Server (mcp-server/).
export default function register(api: OpenClawPluginAPI): void {
  const config = (api.pluginConfig ?? {}) as { port?: number; timeout?: number };

  const port    = config.port    ?? 23456;
  const timeout = config.timeout ?? 15000;

  const client = new UnityClient({ port, timeout });

  registerSceneTools(api, client);
  registerGameObjectTools(api, client);
  registerFileTools(api, client);
  registerCompileTools(api, client);
  registerProjectTools(api, client);
}

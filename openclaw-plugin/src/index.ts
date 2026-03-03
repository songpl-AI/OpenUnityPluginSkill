// OpenClaw Unity Editor Plugin
//
// This plugin registers the unity-editor skill with OpenClaw.
// It does NOT register any direct Unity tools — all Unity operations are
// performed exclusively by Claude Code via MCP Server (see mcp-server/).
//
// For installation and usage, see:
//   skills/unity-editor/SKILL.md
//   docs/路径二-ClaudeCode委派模式.md

// eslint-disable-next-line @typescript-eslint/no-explicit-any
export default function register(_api: any): void {
  // No tools registered here.
  // The skill instructions in skills/unity-editor/SKILL.md tell OpenClaw
  // to delegate all Unity tasks to Claude Code via exec.
}

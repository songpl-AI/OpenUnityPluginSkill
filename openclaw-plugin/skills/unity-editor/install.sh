#!/bin/bash
# Unity Editor MCP Skill 一键安装脚本
# 用法：bash install.sh
# 效果：构建 MCP Server，生成 ~/.claude/unity-mcp-config.json

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
MCP_SERVER_DIR="$(cd "$SCRIPT_DIR/../../../unity-editor-mcp/mcp-server" && pwd)"
CONFIG_DEST="$HOME/.claude/unity-mcp-config.json"

echo "=== Unity Editor MCP Skill 安装 ==="
echo "MCP Server 路径: $MCP_SERVER_DIR"

# 检查依赖
command -v node  >/dev/null 2>&1 || { echo "❌ 需要 Node.js 18+（当前未找到）"; exit 1; }
command -v npm   >/dev/null 2>&1 || { echo "❌ 需要 npm"; exit 1; }
command -v claude >/dev/null 2>&1 || { echo "❌ 需要 Claude Code CLI（brew install claude 或参考 https://docs.anthropic.com/claude-code）"; exit 1; }

NODE_VERSION=$(node -e "console.log(process.versions.node.split('.')[0])")
if [ "$NODE_VERSION" -lt 18 ]; then
  echo "❌ Node.js 版本需要 18+（当前: $NODE_VERSION）"
  exit 1
fi

# 构建 MCP Server
echo ""
echo "📦 安装依赖并构建 MCP Server..."
cd "$MCP_SERVER_DIR"
npm install
npm run build

# 验证构建产物
if [ ! -f "$MCP_SERVER_DIR/dist/index.js" ]; then
  echo "❌ 构建失败：$MCP_SERVER_DIR/dist/index.js 不存在"
  exit 1
fi

# 生成 MCP 配置文件
echo ""
echo "⚙️  生成配置文件: $CONFIG_DEST"
mkdir -p "$(dirname "$CONFIG_DEST")"
sed "s|__MCP_SERVER_PATH__|$MCP_SERVER_DIR|g" \
  "$MCP_SERVER_DIR/claude-mcp-config.template.json" > "$CONFIG_DEST"

echo "✅ 配置已写入 $CONFIG_DEST"

# 打印配置内容供确认
echo ""
echo "--- 配置内容 ---"
cat "$CONFIG_DEST"
echo "----------------"

echo ""
echo "✅ 安装完成！"
echo ""
echo "下一步："
echo "  1. 确保 Unity Editor 正在运行，且已导入 OpenClaw Unity Plugin"
echo "  2. 在 OpenClaw 中说："
echo "     '在 Unity 场景里创建一个旋转的立方体'"
echo ""
echo "手动测试命令："
echo "  claude -p \"用 unity_check_status 检查 Unity 状态\" \\"
echo "    --mcp-config \"$CONFIG_DEST\" \\"
echo "    --allowedTools \"mcp__unity-editor__unity_check_status\" \\"
echo "    --output-format json --max-turns 3"

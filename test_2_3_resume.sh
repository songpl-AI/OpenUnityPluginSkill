#!/bin/bash
# 任务 2.3：验证 --resume 跨任务上下文保持
# 用法：bash test_2_3_resume.sh
# 原理：先创建 PlayerController.cs（移动），取出 session_id，再用 --resume 添加跳跃，
#        验证 Claude 记得已有的代码结构而不会重写移动逻辑。

MCP_CONFIG="$HOME/.claude/unity-mcp-config.json"
TOOLS="mcp__unity-editor__unity_check_status,mcp__unity-editor__unity_write_file,mcp__unity-editor__unity_compile,mcp__unity-editor__unity_get_compile_errors,mcp__unity-editor__unity_read_file"

echo "=== Phase 2.3：--resume 跨任务上下文验证 ==="
echo ""

# --- 第一次调用：创建移动脚本 ---
echo ">>> 任务 1/2：创建基础移动脚本..."
RESULT1=$(claude -p "在 Assets/Scripts/ 创建 PlayerController.cs，实现基础左右移动（speed = 5f），编译成功后停止。代码要完整可编译。" \
  --mcp-config "$MCP_CONFIG" \
  --allowedTools "$TOOLS" \
  --output-format json \
  --max-turns 15)

echo "$RESULT1" | python3 -c "
import sys, json
data = json.load(sys.stdin)
print('结果:', data.get('result', '')[:200])
print('session_id:', data.get('session_id'))
print('is_error:', data.get('is_error'))
" 2>/dev/null || echo "$RESULT1"

SESSION_ID=$(echo "$RESULT1" | python3 -c "import sys,json; print(json.load(sys.stdin).get('session_id',''))" 2>/dev/null)

if [ -z "$SESSION_ID" ]; then
  echo "❌ 未能获取 session_id，终止测试"
  exit 1
fi

echo ""
echo "✅ session_id: $SESSION_ID"
echo ""

# --- 第二次调用：用 --resume 添加跳跃 ---
echo ">>> 任务 2/2：用 --resume 添加跳跃功能..."
RESULT2=$(claude -p "在上述 PlayerController.cs 的基础上增加跳跃功能（jumpForce = 10f，用 Rigidbody），不要删改已有的移动代码，编译成功后停止。" \
  --resume "$SESSION_ID" \
  --mcp-config "$MCP_CONFIG" \
  --allowedTools "$TOOLS" \
  --output-format json \
  --max-turns 15)

echo "$RESULT2" | python3 -c "
import sys, json
data = json.load(sys.stdin)
print('结果:', data.get('result', '')[:300])
print('is_error:', data.get('is_error'))
" 2>/dev/null || echo "$RESULT2"

echo ""
echo "=== 验收标准 ==="
echo "打开 Unity 中的 Assets/Scripts/PlayerController.cs"
echo "验证文件同时包含：移动逻辑（speed）+ 跳跃逻辑（jumpForce）"

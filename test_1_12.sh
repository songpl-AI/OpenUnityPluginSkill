#!/bin/bash
# 任务 1.12：编译自修正循环验证
# 用法：bash test_1_12.sh

PROMPT="请完成以下步骤：
1. 用 unity_write_file 在 Assets/Scripts/TestBrokenScript.cs 写入下面的内容（故意缺少分号）：
using UnityEngine;
public class TestBrokenScript : MonoBehaviour {
    void Start() {
        Debug.Log(\"Hello\")
    }
}
2. 用 unity_compile 触发编译并等待结果
3. 如果编译失败，读取错误信息，修复文件（补上分号），再次编译
4. 编译成功后，用 unity_write_file 把 Assets/Scripts/TestBrokenScript.cs 内容替换为空的合法 C# 文件
5. 输出整个过程的摘要"

claude -p "$PROMPT" \
  --mcp-config ~/.claude/unity-mcp-config.json \
  --allowedTools "mcp__unity-editor__unity_check_status,mcp__unity-editor__unity_write_file,mcp__unity-editor__unity_compile,mcp__unity-editor__unity_get_compile_errors,mcp__unity-editor__unity_read_file" \
  --output-format json \
  --max-turns 15

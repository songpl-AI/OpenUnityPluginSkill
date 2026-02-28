import { UnityResponse } from "../unity-client";
import { ToolResult, textResult } from "./format";

/**
 * 将 Unity API 响应或异常转为 AgentToolResult 格式。
 * execute() 函数捕获所有异常，返回此处生成的结果，不抛出。
 */
export function handleError(err: unknown): ToolResult {
  const msg = err instanceof Error ? `Error: ${err.message}` : `Error: ${String(err)}`;
  return textResult(msg);
}

export function unityError(res: UnityResponse): ToolResult {
  const msg = `Unity API Error [${res.error?.code ?? "UNKNOWN"}]: ${res.error?.message ?? "Unknown error"}`;
  return textResult(msg);
}

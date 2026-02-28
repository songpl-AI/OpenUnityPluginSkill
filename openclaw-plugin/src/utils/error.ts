import { UnityResponse } from "../unity-client";

/**
 * 将 Unity API 响应或异常转为 Agent 可读的错误字符串。
 * execute() 函数捕获所有异常，返回此处生成的字符串，不抛出。
 */
export function handleError(err: unknown): string {
  if (err instanceof Error) return `Error: ${err.message}`;
  return `Error: ${String(err)}`;
}

export function unityError(res: UnityResponse): string {
  return `Unity API Error [${res.error?.code ?? "UNKNOWN"}]: ${res.error?.message ?? "Unknown error"}`;
}

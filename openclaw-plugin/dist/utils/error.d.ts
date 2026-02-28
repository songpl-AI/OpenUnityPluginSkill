import { UnityResponse } from "../unity-client";
import { ToolResult } from "./format";
/**
 * 将 Unity API 响应或异常转为 AgentToolResult 格式。
 * execute() 函数捕获所有异常，返回此处生成的结果，不抛出。
 */
export declare function handleError(err: unknown): ToolResult;
export declare function unityError(res: UnityResponse): ToolResult;

"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.handleError = handleError;
exports.unityError = unityError;
const format_1 = require("./format");
/**
 * 将 Unity API 响应或异常转为 AgentToolResult 格式。
 * execute() 函数捕获所有异常，返回此处生成的结果，不抛出。
 */
function handleError(err) {
    const msg = err instanceof Error ? `Error: ${err.message}` : `Error: ${String(err)}`;
    return (0, format_1.textResult)(msg);
}
function unityError(res) {
    const msg = `Unity API Error [${res.error?.code ?? "UNKNOWN"}]: ${res.error?.message ?? "Unknown error"}`;
    return (0, format_1.textResult)(msg);
}
//# sourceMappingURL=error.js.map
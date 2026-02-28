"use strict";
Object.defineProperty(exports, "__esModule", { value: true });
exports.handleError = handleError;
exports.unityError = unityError;
/**
 * 将 Unity API 响应或异常转为 Agent 可读的错误字符串。
 * execute() 函数捕获所有异常，返回此处生成的字符串，不抛出。
 */
function handleError(err) {
    if (err instanceof Error)
        return `Error: ${err.message}`;
    return `Error: ${String(err)}`;
}
function unityError(res) {
    return `Unity API Error [${res.error?.code ?? "UNKNOWN"}]: ${res.error?.message ?? "Unknown error"}`;
}
//# sourceMappingURL=error.js.map
"use strict";
var __importDefault = (this && this.__importDefault) || function (mod) {
    return (mod && mod.__esModule) ? mod : { "default": mod };
};
Object.defineProperty(exports, "__esModule", { value: true });
exports.UnityWsClient = void 0;
const ws_1 = __importDefault(require("ws"));
const events_1 = require("events");
/**
 * WebSocket 客户端，订阅 Unity Editor 推送的事件。
 * 自动重连（处理 Domain Reload 期间的断连窗口）。
 */
class UnityWsClient extends events_1.EventEmitter {
    constructor(port) {
        super();
        this.ws = null;
        this.reconnectTimer = null;
        this._connected = false;
        this.url = `ws://127.0.0.1:${port}/ws`;
    }
    connect() {
        if (this.ws?.readyState === ws_1.default.OPEN)
            return;
        this.ws = new ws_1.default(this.url);
        this.ws.on("open", () => {
            this._connected = true;
            this.emit("connected");
        });
        this.ws.on("message", (raw) => {
            try {
                const evt = JSON.parse(raw.toString());
                this.emit(evt.event, evt.data);
                this.emit("*", evt); // 通配符监听
            }
            catch { /* 忽略解析失败 */ }
        });
        this.ws.on("close", () => {
            this._connected = false;
            this.emit("disconnected");
            // 2s 后重连（覆盖 Domain Reload ~0.5~2s 的断连窗口）
            this.reconnectTimer = setTimeout(() => this.connect(), 2000);
        });
        this.ws.on("error", () => {
            // error 后 close 也会触发，重连由 close 处理
        });
    }
    disconnect() {
        if (this.reconnectTimer)
            clearTimeout(this.reconnectTimer);
        this.reconnectTimer = null;
        this.ws?.close();
        this.ws = null;
    }
    get connected() { return this._connected; }
    /**
     * 等待特定事件，含超时。用于代码自修正循环：
     * 写脚本 → waitForEvent("compile_complete" | "compile_failed") → 处理结果
     */
    waitForEvent(eventName, timeoutMs = 60000) {
        return new Promise((resolve, reject) => {
            const timer = setTimeout(() => reject(new Error(`Timeout waiting for '${eventName}' after ${timeoutMs}ms`)), timeoutMs);
            this.once(eventName, (data) => {
                clearTimeout(timer);
                resolve(data);
            });
        });
    }
}
exports.UnityWsClient = UnityWsClient;
//# sourceMappingURL=unity-ws-client.js.map
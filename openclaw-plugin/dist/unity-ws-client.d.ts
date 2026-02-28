import { EventEmitter } from "events";
export interface WsEvent {
    event: string;
    timestamp: string;
    data: unknown;
}
/**
 * WebSocket 客户端，订阅 Unity Editor 推送的事件。
 * 自动重连（处理 Domain Reload 期间的断连窗口）。
 */
export declare class UnityWsClient extends EventEmitter {
    private ws;
    private url;
    private reconnectTimer;
    private _connected;
    constructor(port: number);
    connect(): void;
    disconnect(): void;
    get connected(): boolean;
    /**
     * 等待特定事件，含超时。用于代码自修正循环：
     * 写脚本 → waitForEvent("compile_complete" | "compile_failed") → 处理结果
     */
    waitForEvent(eventName: string, timeoutMs?: number): Promise<unknown>;
}

import WebSocket from "ws";
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
export class UnityWsClient extends EventEmitter {
  private ws: WebSocket | null = null;
  private url: string;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private _connected = false;

  constructor(port: number) {
    super();
    this.url = `ws://127.0.0.1:${port}/ws`;
  }

  connect(): void {
    if (this.ws?.readyState === WebSocket.OPEN) return;

    this.ws = new WebSocket(this.url);

    this.ws.on("open", () => {
      this._connected = true;
      this.emit("connected");
    });

    this.ws.on("message", (raw: WebSocket.RawData) => {
      try {
        const evt = JSON.parse(raw.toString()) as WsEvent;
        this.emit(evt.event, evt.data);
        this.emit("*", evt); // 通配符监听
      } catch { /* 忽略解析失败 */ }
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

  disconnect(): void {
    if (this.reconnectTimer) clearTimeout(this.reconnectTimer);
    this.reconnectTimer = null;
    this.ws?.close();
    this.ws = null;
  }

  get connected(): boolean { return this._connected; }

  /**
   * 等待特定事件，含超时。用于代码自修正循环：
   * 写脚本 → waitForEvent("compile_complete" | "compile_failed") → 处理结果
   */
  waitForEvent(eventName: string, timeoutMs = 60_000): Promise<unknown> {
    return new Promise((resolve, reject) => {
      const timer = setTimeout(
        () => reject(new Error(`Timeout waiting for '${eventName}' after ${timeoutMs}ms`)),
        timeoutMs
      );
      this.once(eventName, (data: unknown) => {
        clearTimeout(timer);
        resolve(data);
      });
    });
  }
}

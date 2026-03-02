export interface UnityClientConfig {
  port: number;
  timeout: number;
}

export interface UnityResponse<T = unknown> {
  ok: boolean;
  data: T;
  error: { code: string; message: string } | null;
}

export class UnityClient {
  private baseUrl: string;
  private timeout: number;

  constructor(config: UnityClientConfig) {
    this.baseUrl = `http://127.0.0.1:${config.port}/api/v1`;
    this.timeout = config.timeout;
  }

  async get<T = unknown>(path: string, params?: Record<string, string | number>): Promise<UnityResponse<T>> {
    const url = new URL(`${this.baseUrl}${path}`);
    if (params) {
      for (const [k, v] of Object.entries(params)) {
        if (v !== undefined && v !== null) url.searchParams.set(k, String(v));
      }
    }
    return this.request<T>(url.toString(), { method: "GET" });
  }

  async post<T = unknown>(path: string, body?: unknown, options?: { timeoutMs?: number }): Promise<UnityResponse<T>> {
    return this.request<T>(`${this.baseUrl}${path}`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: body ? JSON.stringify(body) : undefined,
    }, options?.timeoutMs);
  }

  async ensureConnected(): Promise<void> {
    try {
      const res = await this.get("/status");
      if (!res.ok) throw new Error(`Unity server error: ${res.error?.message}`);
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : String(err);
      throw new Error(
        `Cannot connect to Unity Editor (port ${new URL(this.baseUrl).port}).\n` +
        `Make sure:\n` +
        `  1. Unity Editor is running\n` +
        `  2. OpenClaw Unity Plugin is imported and active\n` +
        `  3. No firewall is blocking localhost\n` +
        `Error: ${msg}`
      );
    }
  }

  private async request<T>(url: string, init: RequestInit, timeoutMs?: number): Promise<UnityResponse<T>> {
    const controller = new AbortController();
    const timer = setTimeout(() => controller.abort(), timeoutMs ?? this.timeout);
    try {
      const res  = await fetch(url, { ...init, signal: controller.signal });
      const json = await res.json() as UnityResponse<T>;
      return json;
    } finally {
      clearTimeout(timer);
    }
  }
}

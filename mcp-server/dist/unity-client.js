export class UnityClient {
    baseUrl;
    timeout;
    constructor(config) {
        this.baseUrl = `http://127.0.0.1:${config.port}/api/v1`;
        this.timeout = config.timeout;
    }
    async get(path, params) {
        const url = new URL(`${this.baseUrl}${path}`);
        if (params) {
            for (const [k, v] of Object.entries(params)) {
                if (v !== undefined && v !== null)
                    url.searchParams.set(k, String(v));
            }
        }
        return this.request(url.toString(), { method: "GET" });
    }
    async post(path, body) {
        return this.request(`${this.baseUrl}${path}`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: body ? JSON.stringify(body) : undefined,
        });
    }
    async ensureConnected() {
        try {
            const res = await this.get("/status");
            if (!res.ok)
                throw new Error(`Unity server error: ${res.error?.message}`);
        }
        catch (err) {
            const msg = err instanceof Error ? err.message : String(err);
            throw new Error(`Cannot connect to Unity Editor (port ${new URL(this.baseUrl).port}).\n` +
                `Make sure:\n` +
                `  1. Unity Editor is running\n` +
                `  2. OpenClaw Unity Plugin is imported and active\n` +
                `  3. No firewall is blocking localhost\n` +
                `Error: ${msg}`);
        }
    }
    async request(url, init) {
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), this.timeout);
        try {
            const res = await fetch(url, { ...init, signal: controller.signal });
            const json = await res.json();
            return json;
        }
        finally {
            clearTimeout(timer);
        }
    }
}
//# sourceMappingURL=unity-client.js.map
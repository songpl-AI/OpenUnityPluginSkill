export interface UnityClientConfig {
    port: number;
    timeout: number;
}
export interface UnityResponse<T = unknown> {
    ok: boolean;
    data: T;
    error: {
        code: string;
        message: string;
    } | null;
}
export declare class UnityClient {
    private baseUrl;
    private timeout;
    constructor(config: UnityClientConfig);
    get<T = unknown>(path: string, params?: Record<string, string | number>): Promise<UnityResponse<T>>;
    post<T = unknown>(path: string, body?: unknown, options?: {
        timeoutMs?: number;
    }): Promise<UnityResponse<T>>;
    ensureConnected(): Promise<void>;
    private request;
}

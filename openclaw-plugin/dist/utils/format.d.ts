/**
 * OpenClaw 框架要求 execute 返回 AgentToolResult 格式：
 *   { content: [{ type: "text", text: "..." }], details: unknown }
 * 使用 textResult 将任意字符串包装成正确格式。
 */
export type ToolResult = {
    content: Array<{
        type: "text";
        text: string;
    }>;
    details: unknown;
};
export declare function textResult(text: string, details?: unknown): ToolResult;
export declare function formatHierarchy(roots: GameObjectNode[], indent?: number): string;
export declare function formatAssetList(assets: AssetInfo[]): string;
export declare function formatCompileErrors(errors: CompileError[]): string;
export declare function formatScriptTypes(types: ScriptType[]): string;
interface GameObjectNode {
    name: string;
    active: boolean;
    components?: string[];
    children?: GameObjectNode[];
}
interface AssetInfo {
    path: string;
    type: string;
}
interface CompileError {
    file: string;
    line: number;
    column: number;
    message: string;
    type: string;
}
interface ScriptType {
    fullName: string;
    baseType?: string;
    isMonoBehaviour: boolean;
    methods?: string[];
}
export {};

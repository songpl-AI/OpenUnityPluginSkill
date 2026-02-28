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

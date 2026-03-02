// 将 Unity API 返回的结构化数据格式化为 Agent 可读的文本

export function formatHierarchy(roots: GameObjectNode[], indent = 0): string {
  return roots.map(go => {
    const prefix = "  ".repeat(indent) + (indent === 0 ? "" : "└─ ");
    const line   = `${prefix}${go.name} [${go.components?.join(", ") ?? ""}]${go.active ? "" : " (inactive)"}`;
    const children = go.children?.length ? "\n" + formatHierarchy(go.children, indent + 1) : "";
    return line + children;
  }).join("\n");
}

export function formatAssetList(assets: AssetInfo[]): string {
  if (assets.length === 0) return "No assets found.";
  return assets.map(a => `• ${a.path} (${a.type})`).join("\n");
}

export function formatCompileErrors(errors: CompileError[]): string {
  if (errors.length === 0) return "No compile errors.";
  return errors.map(e =>
    `[${e.type.toUpperCase()}] ${e.file}:${e.line}:${e.column}\n  ${e.message}`
  ).join("\n");
}

export function formatScriptTypes(types: ScriptType[]): string {
  if (types.length === 0) return "No user scripts found.";
  return types.map(t => {
    const base    = t.baseType ? ` : ${t.baseType}` : "";
    const methods = t.methods?.length
      ? `\n  Methods: ${t.methods.slice(0, 5).join(", ")}${t.methods.length > 5 ? ` +${t.methods.length - 5} more` : ""}`
      : "";
    return `• ${t.fullName}${base}${t.isMonoBehaviour ? " [MonoBehaviour]" : ""}${methods}`;
  }).join("\n");
}

// --- Type stubs (mirrors Unity DTO naming) ---
interface GameObjectNode { name: string; active: boolean; components?: string[]; children?: GameObjectNode[] }
interface AssetInfo      { path: string; type: string }
interface CompileError   { file: string; line: number; column: number; message: string; type: string }
interface ScriptType     { fullName: string; baseType?: string; isMonoBehaviour: boolean; methods?: string[] }

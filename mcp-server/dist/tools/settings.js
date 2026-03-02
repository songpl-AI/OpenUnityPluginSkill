/**
 * 项目设置工具
 */
export function registerSettingsTools(server, client) {
    // 获取输入系统类型
    server.registerTool("unity_get_input_system_type", {
        description: "Get the active input system type (legacy/new/both) from Player Settings. Use this to generate compatible input code.",
        inputSchema: {},
    }, async () => {
        await client.ensureConnected();
        const res = await client.get("/project/input-system");
        if (!res.ok)
            throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
        const typeDesc = {
            legacy: "Old Input System (Input.GetKey, Input.GetButton)",
            new: "New Input System (Keyboard.current, Gamepad.current)",
            both: "Both Input Systems enabled",
            unknown: "Input System not configured"
        };
        const recommendations = {
            legacy: "Use: Input.GetKeyDown(KeyCode.Space), Input.GetAxis(\"Horizontal\")",
            new: "Use: using UnityEngine.InputSystem; Keyboard.current.spaceKey.wasPressedThisFrame",
            both: "Prefer New Input System for better features",
            unknown: "Configure Input System in Project Settings > Player"
        };
        return {
            content: [{
                    type: "text",
                    text: [
                        `Input System: ${res.data.inputSystem}`,
                        `Description: ${typeDesc[res.data.inputSystem]}`,
                        `Recommendation: ${recommendations[res.data.inputSystem]}`
                    ].join("\n")
                }]
        };
    });
    // 获取 Player Settings
    server.registerTool("unity_get_player_settings", {
        description: "Get key Player Settings including company name, product name, version, input system, and scripting backend.",
        inputSchema: {},
    }, async () => {
        await client.ensureConnected();
        const res = await client.get("/project/player-settings");
        if (!res.ok)
            throw new Error(`Unity API Error [${res.error?.code}]: ${res.error?.message}`);
        return {
            content: [{
                    type: "text",
                    text: [
                        `Player Settings:`,
                        `• Company: ${res.data.companyName}`,
                        `• Product: ${res.data.productName}`,
                        `• Version: ${res.data.version}`,
                        `• Input System: ${res.data.inputSystem}`,
                        `• Scripting Backend: ${res.data.scriptingBackend}`,
                        `• API Compatibility: ${res.data.apiCompatibility}`
                    ].join("\n")
                }]
        };
    });
}
//# sourceMappingURL=settings.js.map
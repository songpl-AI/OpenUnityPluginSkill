using UnityEditor;
using UnityEditor.Compilation;
using UnityEngine;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 插件入口。[InitializeOnLoad] 确保 Unity Editor 启动及 Domain Reload 后自动运行。
    /// 负责按依赖顺序初始化和关闭所有核心组件。
    /// Version: 1.1.0 - Added Tag and Settings APIs
    /// </summary>
    [InitializeOnLoad]
    public static class UnityEditorServer
    {
        private static HttpServer _httpServer;

        public static int HttpPort => _httpServer?.Port ?? 23456;

        static UnityEditorServer()
        {
            // Domain Reload 前必须注册清理，防止端口占用和内存泄漏
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.quitting               += Shutdown;

            Startup();
        }

        private static void Startup()
        {
            Debug.Log("[OpenClaw] Starting Unity Editor Plugin...");

            // 1. 主线程调度器（最先初始化，其他组件依赖它）
            MainThreadDispatcher.Initialize();

            // 2. WebSocket 服务端（条件编译选择实现）
#if UNITY_2022_3_OR_NEWER
            var wsServer = new BuiltinWebSocketServer();
#else
            var wsServer = new SharpWebSocketServer();
#endif

            // 3. 事件广播器（依赖 WebSocket Server）
            EventBroadcaster.Initialize(wsServer);

            // 4. Console 日志捕获（依赖 EventBroadcaster）
            ConsoleLogger.Initialize();

            // 注意：CompilationListener 通过自己的 [InitializeOnLoad] 静态构造函数初始化，
            // 无需在此显式调用，已依赖 EventBroadcaster 在前面初始化完毕

            // 5. 路由器 + Handler 注册
            var router = new RequestRouter();
            RegisterRoutes(router);

            // 6. HTTP Server（最后启动，端口就绪后开始接受连接）
            _httpServer = new HttpServer(router, wsServer);
            if (!_httpServer.Start())
            {
                Debug.LogError("[OpenClaw] Failed to start HTTP server. Plugin is inactive.");
                return;
            }

            Debug.Log($"[OpenClaw] Plugin ready. HTTP: http://127.0.0.1:{_httpServer.Port}/api/v1  WS: ws://127.0.0.1:{_httpServer.Port + 1}/ws");
        }

        private static void Shutdown()
        {
            Debug.Log("[OpenClaw] Shutting down...");
            ConsoleLogger.Shutdown();
            CompilationListener.Shutdown();
            EventBroadcaster.Shutdown();
            _httpServer?.Stop();
            _httpServer = null;
            MainThreadDispatcher.Shutdown();
        }

        private static void RegisterRoutes(RequestRouter router)
        {
            // Status
            var statusHandler = new StatusHandler();
            router.Register("GET", "/api/v1/status", statusHandler.HandleGet);

            // Scene
            var sceneHandler = new SceneHandler();
            router.Register("GET",  "/api/v1/scene/info",      sceneHandler.HandleInfo);
            router.Register("GET",  "/api/v1/scene/hierarchy", sceneHandler.HandleHierarchy);
            router.Register("POST", "/api/v1/scene/save",      sceneHandler.HandleSave);
            router.Register("POST", "/api/v1/scene/open",      sceneHandler.HandleOpen);

            // GameObject
            var goHandler = new GameObjectHandler();
            router.Register("GET",  "/api/v1/gameobject",                              goHandler.HandleFind);
            router.Register("POST", "/api/v1/gameobject/create",                       goHandler.HandleCreate);
            router.Register("POST", "/api/v1/gameobject/delete",                       goHandler.HandleDelete);
            router.Register("POST", "/api/v1/gameobject/transform",                    goHandler.HandleTransform);
            router.Register("POST", "/api/v1/gameobject/parent",                       goHandler.HandleParent);
            router.Register("GET",  "/api/v1/gameobject/:path/components",             goHandler.HandleGetComponents);
            router.Register("GET",  "/api/v1/gameobject/:path/component/:type/values", goHandler.HandleGetComponentValues);
            router.Register("POST", "/api/v1/gameobject/:path/component/:type/values", goHandler.HandleSetComponentValues);
            router.Register("POST", "/api/v1/gameobject/:path/component/add",          goHandler.HandleAddComponent);
            router.Register("POST", "/api/v1/gameobject/:path/component/remove",       goHandler.HandleRemoveComponent);

            // File
            var fileHandler = new FileHandler();
            router.Register("GET",  "/api/v1/file/read",  fileHandler.HandleRead);
            router.Register("POST", "/api/v1/file/write", fileHandler.HandleWrite);

            // Compile & Console
            var compileHandler  = new CompileHandler();
            var consoleHandler  = new ConsoleHandler();
            router.Register("GET",  "/api/v1/compile/errors", compileHandler.HandleErrors);
            router.Register("GET",  "/api/v1/compile/status", compileHandler.HandleStatus);
            router.Register("GET",  "/api/v1/console/logs",   consoleHandler.HandleLogs);
            router.Register("POST", "/api/v1/console/clear",  consoleHandler.HandleClear);

            // Asset
            var assetHandler = new AssetHandler();
            router.Register("GET",  "/api/v1/asset/find",              assetHandler.HandleFind);
            router.Register("GET",  "/api/v1/asset/details",           assetHandler.HandleDetails);
            router.Register("POST", "/api/v1/asset/create/script",     assetHandler.HandleCreateScript);
            router.Register("POST", "/api/v1/asset/create/material",   assetHandler.HandleCreateMaterial);
            router.Register("POST", "/api/v1/asset/create/prefab",     assetHandler.HandleCreatePrefab);
            router.Register("POST", "/api/v1/asset/refresh",           assetHandler.HandleRefresh);
            router.Register("POST", "/api/v1/asset/import",            assetHandler.HandleImport);

            // Project
            var projectHandler = new ProjectHandler();
            router.Register("GET", "/api/v1/project/info",     projectHandler.HandleInfo);
            router.Register("GET", "/api/v1/project/scripts",  projectHandler.HandleScripts);
            router.Register("GET", "/api/v1/project/settings", projectHandler.HandleSettings);

            // Editor
            var editorHandler = new EditorHandler();
            router.Register("POST", "/api/v1/editor/play",    editorHandler.HandlePlay);
            router.Register("POST", "/api/v1/editor/stop",    editorHandler.HandleStop);
            router.Register("POST", "/api/v1/editor/pause",   editorHandler.HandlePause);
            router.Register("POST", "/api/v1/editor/undo",    editorHandler.HandleUndo);
            router.Register("POST", "/api/v1/editor/compile", editorHandler.HandleCompile);

            // Build
            var buildHandler = new BuildHandler();
            router.Register("GET",  "/api/v1/build/settings", buildHandler.HandleSettings);
            router.Register("POST", "/api/v1/build/run",      buildHandler.HandleRun);
            router.Register("GET",  "/api/v1/build/status",   buildHandler.HandleStatus);

            // Tag
            var tagHandler = new TagHandler();
            router.Register("GET",  "/api/v1/tag/list",   tagHandler.HandleGetTags);
            router.Register("POST", "/api/v1/tag/create", tagHandler.HandleCreateTag);
            router.Register("POST", "/api/v1/tag/set",    tagHandler.HandleSetGameObjectTag);

            // Settings
            var settingsHandler = new SettingsHandler();
            router.Register("GET", "/api/v1/project/input-system",     settingsHandler.HandleGetInputSystem);
            router.Register("GET", "/api/v1/project/player-settings",  settingsHandler.HandleGetPlayerSettings);

            // Material
            var materialHandler = new MaterialHandler();
            router.Register("GET",  "/api/v1/material/render-pipeline", materialHandler.HandleGetRenderPipeline);
            router.Register("GET",  "/api/v1/material/properties",      materialHandler.HandleGetProperties);
            router.Register("POST", "/api/v1/material/properties",      materialHandler.HandleSetProperties);
            router.Register("POST", "/api/v1/material/assign",          materialHandler.HandleAssign);

            // Package Manager
            var packageHandler = new PackageHandler();
            router.Register("GET",  "/api/v1/package/list",   packageHandler.HandleList);
            router.Register("POST", "/api/v1/package/add",    packageHandler.HandleAdd);
            router.Register("POST", "/api/v1/package/remove", packageHandler.HandleRemove);

            Debug.Log($"[OpenClaw] Registered {router.RouteCount} routes.");
        }
    }
}

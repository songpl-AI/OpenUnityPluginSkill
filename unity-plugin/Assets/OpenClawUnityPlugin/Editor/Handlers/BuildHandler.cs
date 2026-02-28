using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace OpenClaw.UnityPlugin
{
    public class BuildHandler
    {
        private static BuildStatus _lastStatus = BuildStatus.Unknown;
        private static string      _lastOutput  = null;

        public void HandleSettings(HttpContext ctx)
        {
            var data = MainThreadDispatcher.Dispatch(() =>
            {
                var scenes = new System.Collections.Generic.List<string>();
                foreach (var scene in EditorBuildSettings.scenes)
                    if (scene.enabled) scenes.Add(scene.path);

                return new
                {
                    platform   = EditorUserBuildSettings.activeBuildTarget.ToString(),
                    scenes,
                    development = EditorUserBuildSettings.development
                };
            });
            ResponseHelper.WriteSuccess(ctx.Response, data);
        }

        public void HandleRun(HttpContext ctx)
        {
            var req = ctx.ParseBody<BuildRequest>();

            // 构建是长耗时操作，通过 WebSocket 推送结果，HTTP 立即返回 202
            MainThreadDispatcher.Dispatch(() =>
            {
                _lastStatus = BuildStatus.Running;
                var options = new BuildPlayerOptions
                {
                    scenes      = req.Scenes ?? GetEnabledScenes(),
                    locationPathName = req.OutputPath ?? "Build/Output",
                    target      = req.Target.HasValue ? req.Target.Value : EditorUserBuildSettings.activeBuildTarget,
                    options     = BuildOptions.None
                };

                var report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result == BuildResult.Succeeded)
                {
                    _lastStatus = BuildStatus.Succeeded;
                    _lastOutput = options.locationPathName;
                    EventBroadcaster.Broadcast("build_complete", new
                    {
                        platform    = options.target.ToString(),
                        outputPath  = options.locationPathName,
                        duration_ms = (long)report.summary.totalTime.TotalMilliseconds
                    });
                }
                else
                {
                    _lastStatus = BuildStatus.Failed;
                    EventBroadcaster.Broadcast("build_failed", new
                    {
                        platform = options.target.ToString(),
                        errors   = report.summary.totalErrors
                    });
                }
                return true;
            });

            ResponseHelper.WriteSuccess(ctx.Response, new
            {
                triggered = true,
                message   = "Build started. Listen to WebSocket 'build_complete' or 'build_failed' for results."
            });
        }

        public void HandleStatus(HttpContext ctx)
        {
            ResponseHelper.WriteSuccess(ctx.Response, new
            {
                status     = _lastStatus.ToString().ToLower(),
                outputPath = _lastOutput
            });
        }

        private static string[] GetEnabledScenes()
        {
            var list = new System.Collections.Generic.List<string>();
            foreach (var s in EditorBuildSettings.scenes)
                if (s.enabled) list.Add(s.path);
            return list.ToArray();
        }

        private enum BuildStatus { Unknown, Running, Succeeded, Failed }

        private class BuildRequest
        {
            [JsonProperty("outputPath")] public string      OutputPath { get; set; }
            [JsonProperty("target")]     public BuildTarget? Target    { get; set; }
            [JsonProperty("scenes")]     public string[]    Scenes     { get; set; }
        }
    }
}

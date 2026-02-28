namespace OpenClaw.UnityPlugin
{
    public class ConsoleHandler
    {
        public void HandleLogs(HttpContext ctx)
        {
            var typeFilter = ctx.Query("type");                             // "log" | "warning" | "error"
            int.TryParse(ctx.Query("limit", "0"), out var limit);

            var logs = ConsoleLogger.GetLogs(typeFilter, limit);
            ResponseHelper.WriteSuccess(ctx.Response, new { count = logs.Count, logs });
        }

        public void HandleClear(HttpContext ctx)
        {
            ConsoleLogger.Clear();
            ResponseHelper.WriteSuccess(ctx.Response, new { cleared = true });
        }
    }
}

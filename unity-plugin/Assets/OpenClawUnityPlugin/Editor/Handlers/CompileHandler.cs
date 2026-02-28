using System.Linq;

namespace OpenClaw.UnityPlugin
{
    public class CompileHandler
    {
        public void HandleErrors(HttpContext ctx)
        {
            var typeFilter = ctx.Query("type"); // "error" | "warning" | null=全部

            var errors = CompilationListener.LastErrors
                .Where(e => typeFilter == null || e.Type == typeFilter)
                .ToArray();

            ResponseHelper.WriteSuccess(ctx.Response, new
            {
                count  = errors.Length,
                status = CompilationListener.Status.ToString().ToLower(),
                errors
            });
        }

        public void HandleStatus(HttpContext ctx)
        {
            ResponseHelper.WriteSuccess(ctx.Response, new
            {
                status      = CompilationListener.Status.ToString().ToLower(),
                errorCount  = CompilationListener.LastErrors.Count(e => e.Type == "error"),
                warningCount = CompilationListener.LastErrors.Count(e => e.Type == "warning")
            });
        }
    }
}

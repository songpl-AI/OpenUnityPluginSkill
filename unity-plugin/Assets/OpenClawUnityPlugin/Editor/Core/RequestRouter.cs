using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// URL 路由器，支持静态路径（/api/v1/status）和参数路径（/api/v1/gameobject/:path/components）。
    /// </summary>
    public class RequestRouter
    {
        private readonly List<RouteEntry> _routes = new List<RouteEntry>();

        /// <summary>已注册的路由数量</summary>
        public int RouteCount => _routes.Count;

        /// <summary>注册一条路由规则</summary>
        public void Register(string method, string pattern, Action<HttpContext> handler)
        {
            _routes.Add(new RouteEntry(method.ToUpperInvariant(), pattern, handler));
        }

        /// <summary>匹配并执行路由；无匹配时写入 404</summary>
        public void Route(HttpListenerContext ctx)
        {
            var method = ctx.Request.HttpMethod.ToUpperInvariant();
            var rawPath = ctx.Request.Url.AbsolutePath.TrimEnd('/');

            foreach (var entry in _routes)
            {
                if (entry.Method != method) continue;
                if (entry.TryMatch(rawPath, out var pathParams))
                {
                    var httpCtx = new HttpContext(ctx, pathParams);
                    try
                    {
                        entry.Handler(httpCtx);
                    }
                    catch (Exception ex)
                    {
                        UnityEngine.Debug.LogError($"[OpenClaw] Handler error for {method} {rawPath}: {ex}");
                        ResponseHelper.WriteServerError(ctx.Response, ex);
                    }
                    return;
                }
            }

            ResponseHelper.WriteError(ctx.Response, "NOT_FOUND", $"No route for {method} {rawPath}", 404);
        }

        private class RouteEntry
        {
            public string                   Method  { get; }
            public string                   Pattern { get; }
            public Action<HttpContext>       Handler { get; }

            private readonly Regex   _regex;
            private readonly string[] _paramNames;

            public RouteEntry(string method, string pattern, Action<HttpContext> handler)
            {
                Method  = method;
                Pattern = pattern;
                Handler = handler;

                // 将 :param 转为命名捕获组，其余字符转义
                var paramNames = new List<string>();

                // 先转义特殊字符，Regex.Escape 会把 : 转义成 \:
                var escaped = Regex.Escape(pattern);

                // 然后将 \:param 替换为命名捕获组
                var regexStr = "^" + Regex.Replace(
                    escaped,
                    @"\\:(\w+)",  // 匹配被转义的 \:param
                    m => { paramNames.Add(m.Groups[1].Value); return $"(?<{m.Groups[1].Value}>[^/]+)"; }
                ) + "$";

                _regex      = new Regex(regexStr, RegexOptions.Compiled);
                _paramNames = paramNames.ToArray();
            }

            public bool TryMatch(string path, out Dictionary<string, string> pathParams)
            {
                pathParams = null;
                var match  = _regex.Match(path);
                if (!match.Success) return false;

                pathParams = new Dictionary<string, string>();
                foreach (var name in _paramNames)
                    pathParams[name] = Uri.UnescapeDataString(match.Groups[name].Value);
                return true;
            }
        }
    }

    /// <summary>封装 HttpListenerContext + 路径参数，简化 Handler 的参数读取</summary>
    public class HttpContext
    {
        public HttpListenerRequest  Request    { get; }
        public HttpListenerResponse Response   { get; }
        public Dictionary<string, string> PathParams { get; }

        public HttpContext(HttpListenerContext ctx, Dictionary<string, string> pathParams)
        {
            Request    = ctx.Request;
            Response   = ctx.Response;
            PathParams = pathParams ?? new Dictionary<string, string>();
        }

        /// <summary>将请求体 JSON 反序列化为 T，失败返回 default</summary>
        public T ParseBody<T>() where T : class, new()
        {
            try
            {
                using var reader = new System.IO.StreamReader(Request.InputStream, System.Text.Encoding.UTF8);
                var json = reader.ReadToEnd();
                return string.IsNullOrWhiteSpace(json) ? new T() : JsonConvert.DeserializeObject<T>(json) ?? new T();
            }
            catch
            {
                return new T();
            }
        }

        /// <summary>读取 Query String 参数</summary>
        public string Query(string key, string defaultValue = null)
            => Request.QueryString[key] ?? defaultValue;
    }
}

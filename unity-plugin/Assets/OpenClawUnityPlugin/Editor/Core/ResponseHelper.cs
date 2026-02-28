using System;
using System.Net;
using Newtonsoft.Json;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// HTTP 响应写入工具。所有 Handler 通过此类返回响应，确保格式统一。
    /// </summary>
    public static class ResponseHelper
    {
        public static void WriteSuccess(HttpListenerResponse response, object data = null, int statusCode = 200)
        {
            WriteJson(response, ApiResponse.Success(data), statusCode);
        }

        public static void WriteError(HttpListenerResponse response, string code, string message, int statusCode = 400)
        {
            WriteJson(response, ApiResponse.Fail(code, message), statusCode);
        }

        public static void WriteNotFound(HttpListenerResponse response, string message)
        {
            WriteError(response, ErrorCode.ObjectNotFound, message, 404);
        }

        public static void WriteServerError(HttpListenerResponse response, Exception ex)
        {
            WriteError(response, ErrorCode.ExecutionFailed, ex.Message, 500);
        }

        private static void WriteJson(HttpListenerResponse response, ApiResponse payload, int statusCode)
        {
            try
            {
                var json  = JsonConvert.SerializeObject(payload);
                var bytes = System.Text.Encoding.UTF8.GetBytes(json);

                response.StatusCode  = statusCode;
                response.ContentType = "application/json; charset=utf-8";
                response.ContentLength64 = bytes.Length;
                response.OutputStream.Write(bytes, 0, bytes.Length);
            }
            finally
            {
                response.OutputStream.Close();
            }
        }
    }
}

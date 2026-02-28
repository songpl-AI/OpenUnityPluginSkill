using System;
using Newtonsoft.Json;

namespace OpenClaw.UnityPlugin
{
    /// <summary>
    /// 所有 HTTP 响应的统一包装格式
    /// {"ok":true,"data":{...},"error":null}
    /// {"ok":false,"data":null,"error":{"code":"...","message":"..."}}
    /// </summary>
    public class ApiResponse
    {
        [JsonProperty("ok")]    public bool   Ok    { get; private set; }
        [JsonProperty("data")]  public object Data  { get; private set; }
        [JsonProperty("error")] public ApiError Error { get; private set; }

        private ApiResponse() { }

        public static ApiResponse Success(object data = null) => new ApiResponse
        {
            Ok    = true,
            Data  = data ?? new object(),
            Error = null
        };

        public static ApiResponse Fail(string code, string message, object details = null) => new ApiResponse
        {
            Ok    = false,
            Data  = null,
            Error = new ApiError { Code = code, Message = message, Details = details }
        };

        public string ToJson() => JsonConvert.SerializeObject(this);
    }

    public class ApiError
    {
        [JsonProperty("code")]    public string Code    { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("details")] public object Details { get; set; }
    }

    /// <summary>已知错误码常量</summary>
    public static class ErrorCode
    {
        public const string ServerNotReady      = "SERVER_NOT_READY";
        public const string SceneNotLoaded      = "SCENE_NOT_LOADED";
        public const string ObjectNotFound      = "OBJECT_NOT_FOUND";
        public const string AssetNotFound       = "ASSET_NOT_FOUND";
        public const string FileNotFound        = "FILE_NOT_FOUND";
        public const string FileOutsideProject  = "FILE_OUTSIDE_PROJECT";
        public const string InvalidParams       = "INVALID_PARAMS";
        public const string ExecutionFailed     = "EXECUTION_FAILED";
        public const string CompileError        = "COMPILE_ERROR";
    }
}

using System.Collections.Generic;
using Newtonsoft.Json;

namespace OpenClaw.UnityPlugin
{
    // DTO 规则：禁止直接序列化 Unity 类型（Vector3、Transform 等），全部转为此处的 DTO

    public class Vector3Dto
    {
        [JsonProperty("x")] public float X { get; set; }
        [JsonProperty("y")] public float Y { get; set; }
        [JsonProperty("z")] public float Z { get; set; }
    }

    public class TransformDto
    {
        [JsonProperty("position")] public Vector3Dto Position { get; set; }
        [JsonProperty("rotation")] public Vector3Dto Rotation { get; set; } // Euler angles
        [JsonProperty("scale")]    public Vector3Dto Scale    { get; set; }
    }

    public class GameObjectDto
    {
        [JsonProperty("name")]       public string         Name       { get; set; }
        [JsonProperty("path")]       public string         Path       { get; set; } // 场景层级路径
        [JsonProperty("active")]     public bool           Active     { get; set; }
        [JsonProperty("tag")]        public string         Tag        { get; set; }
        [JsonProperty("layer")]      public int            Layer      { get; set; }
        [JsonProperty("transform")]  public TransformDto   Transform  { get; set; }
        [JsonProperty("children")]   public List<GameObjectDto> Children { get; set; }
        [JsonProperty("components")] public List<string>   Components { get; set; }
    }

    public class SceneInfoDto
    {
        [JsonProperty("name")]      public string Name     { get; set; }
        [JsonProperty("path")]      public string Path     { get; set; }
        [JsonProperty("isDirty")]   public bool   IsDirty  { get; set; }
        [JsonProperty("isLoaded")]  public bool   IsLoaded { get; set; }
    }

    public class CompileErrorDto
    {
        [JsonProperty("file")]    public string File    { get; set; }
        [JsonProperty("line")]    public int    Line    { get; set; }
        [JsonProperty("column")]  public int    Column  { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("type")]    public string Type    { get; set; } // "error" | "warning"
    }

    public class ConsoleLogDto
    {
        [JsonProperty("type")]       public string Type       { get; set; } // "log" | "warning" | "error"
        [JsonProperty("message")]    public string Message    { get; set; }
        [JsonProperty("stackTrace")] public string StackTrace { get; set; }
        [JsonProperty("timestamp")]  public string Timestamp  { get; set; }
    }

    public class AssetInfoDto
    {
        [JsonProperty("path")]     public string Path     { get; set; }
        [JsonProperty("guid")]     public string Guid     { get; set; }
        [JsonProperty("type")]     public string Type     { get; set; }
        [JsonProperty("name")]     public string Name     { get; set; }
        [JsonProperty("metadata")] public object Metadata { get; set; } // 类型特定的元数据
    }

    public class ScriptTypeDto
    {
        [JsonProperty("fullName")]        public string       FullName        { get; set; }
        [JsonProperty("name")]            public string       Name            { get; set; }
        [JsonProperty("baseType")]        public string       BaseType        { get; set; }
        [JsonProperty("isMonoBehaviour")] public bool         IsMonoBehaviour { get; set; }
        [JsonProperty("filePath")]        public string       FilePath        { get; set; }
        [JsonProperty("methods")]         public List<string> Methods         { get; set; }
        [JsonProperty("fields")]          public List<FieldDto> Fields        { get; set; }
    }

    public class FieldDto
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
    }
}

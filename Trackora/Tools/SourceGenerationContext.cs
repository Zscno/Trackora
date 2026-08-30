using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Zscno.Trackora.Services;

namespace Zscno.Trackora.Tools
{
    /// <summary>
    /// 提供有关与 Json 序列化相关的类型元数据。
    /// </summary>
    /// <remarks>使用源生成将元数据收集过程从运行时移动到编译时间以提高序列化和反序列化的性能。</remarks>
#if DEBUG
    [JsonSourceGenerationOptions(WriteIndented = true)]
#endif
    [JsonSerializable(typeof(ConcurrentDictionary<string, ProcessInfo>))]
    [JsonSerializable(typeof(HashSet<string>))]
    [JsonSerializable(typeof(UsageRecord))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
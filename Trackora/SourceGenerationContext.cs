using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Zscno.Trackora
{
    /// <summary>
    /// 提供有关与 Json 序列化相关的类型元数据。
    /// </summary>
    /// <remarks>使用源生成将元数据收集过程从运行时移动到编译时间以提高序列化和反序列化的性能。</remarks>
#if DEBUG
    [JsonSourceGenerationOptions(WriteIndented = true)]
#endif
    [JsonSerializable(typeof(List<ProcessInfo>))] // TODO: 将移除。
    [JsonSerializable(typeof(ConcurrentDictionary<string, ProcessInfo>))]
    [JsonSerializable(typeof(UsageRecord))]
    internal partial class SourceGenerationContext : JsonSerializerContext
    {
    }
}
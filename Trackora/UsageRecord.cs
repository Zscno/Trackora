using System.Collections.Concurrent;
using System.Text.Json.Serialization;

namespace Zscno.Trackora
{
    /// <summary>
    /// 一天的使用记录。
    /// </summary>
    internal class UsageRecord
    {
        /// <summary>
        /// 所有进程的使用记录，包含进程名称和其使用时间（以毫秒为单位）。
        /// </summary>
        public ConcurrentDictionary<string, uint> ProcessUsageRecords { get; set; } = [];

        /// <summary>
        /// 总使用时间，以毫秒为单位。
        /// </summary>
        public uint DailyDuration { get; set; }

        /// <summary>
        /// 连续使用时间，以毫秒为单位。
        /// </summary>
        [JsonIgnore]
        public uint SessionDuration { get; set; }
    }
}
using System.Collections.Generic;

namespace Zscno.Trackora
{
    /// <summary>
    /// 一天的使用记录。
    /// </summary>
    internal class UsageRecord
    {
        /// <summary>
        /// 所有进程的使用记录，包含进程名称和其使用时间。
        /// </summary>
        internal List<ProcessUsageRecord> ProcessUsageRecords { get; set; } = [];

        /// <summary>
        /// 总使用时间，以毫秒为单位。
        /// </summary>
        internal uint TotalUsageTime { get; set; }
    }
}
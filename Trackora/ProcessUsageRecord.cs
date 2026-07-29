namespace Zscno.Trackora
{
    /// <summary>
    /// 进程使用记录。
    /// </summary>
    internal class ProcessUsageRecord
    {
        /// <summary>
        /// 进程名称。
        /// </summary>
        internal string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// 使用时间，以毫秒为单位。
        /// </summary>
        internal uint UsageTime { get; set; }
    }
}

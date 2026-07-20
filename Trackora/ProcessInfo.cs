using System.Text.Json.Serialization;

namespace Zscno.Trackora
{
    /// <summary>
    /// 进程信息。
    /// </summary>
    internal class ProcessInfo
    {
        /// <summary>
        /// 显示给用户的名称。
        /// </summary>
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// 图标的Uri。
        /// </summary>
        public string IconUri { get; set; } = string.Empty;

        /// <summary>
        /// 进程名称。
        /// </summary>
        public string ProcessName { get; set; } = string.Empty;

        /// <summary>
        /// 使用时长。
        /// </summary>
        [JsonIgnore]
        public string UsageTime { get; set; } = string.Empty;
    }
}
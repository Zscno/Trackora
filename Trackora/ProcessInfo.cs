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
        [JsonInclude]
        internal string DisplayName { get; set; }

        internal ProcessInfo(string displayName)
        {
            DisplayName = displayName;
        }
    }
}
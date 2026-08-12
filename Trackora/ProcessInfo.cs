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

        /// <summary>
        /// 初始化一个新的 <see cref="ProcessInfo"/> 实例。
        /// </summary>
        /// <param name="displayName">显示给用户的名称。</param>
        internal ProcessInfo(string displayName)
        {
            DisplayName = displayName;
        }
    }
}
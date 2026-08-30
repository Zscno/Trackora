using Zscno.Trackora.Tools;

namespace Zscno.Trackora.UI
{
    /// <summary>
    /// 表示一个用于显示进程信息的项。
    /// </summary>
    internal class ProcessDisplayItem
    {
        /// <summary>
        /// 进程图标文件的 Uri。
        /// </summary>
        internal string IconFileUri { get; set; }

        /// <summary>
        /// 进程的友好名称。
        /// </summary>
        internal string Name { get; set; }

        /// <summary>
        /// 进程的使用时间（已本地化的字符串）。
        /// </summary>
        internal string UsageTime { get; set; }

        /// <summary>
        /// 初始化一个新的 <see cref="ProcessDisplayItem"/> 实例。
        /// </summary>
        /// <param name="iconFileUri">进程图标的 Uri。</param>
        /// <param name="name">       进程的友好名称。</param>
        /// <param name="usageTime">  进程的使用时间（以毫秒为单位）。</param>
        internal ProcessDisplayItem(string iconFileUri, string name, uint usageTime)
        {
            IconFileUri = iconFileUri;
            Name = name;
            UsageTime = Localization.ToLocalizedTimeString(usageTime);
        }
    }
}
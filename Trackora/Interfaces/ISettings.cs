using Microsoft.UI.Xaml;

namespace Zscno.Trackora.Interfaces
{
    /// <summary>
    /// 提供获取和修改应用程序设置的功能。
    /// </summary>
    internal interface ISettings
    {
        /// <summary>
        /// 获取或设置一个值，当总使用时间达到此值时将发送通知。
        /// </summary>
        uint DailyThreshold { get; set; }

        /// <summary>
        /// 获取或设置一个值，当空闲时间达到此值将重置连续使用时间。
        /// </summary>
        uint IdleThreshold { get; set; }

        /// <summary>
        /// 获取或设置一个值，当连续使用时间达到此值时将发送通知。
        /// </summary>
        uint SessionThreshold { get; set; }

        /// <summary>
        /// 获取或设置一个值，指定应用程序的主题。TODO: 修改导航视图的主题。
        /// </summary>
        ElementTheme Theme { get; set; }

        /// <summary>
        /// 重置应用程序的设置。
        /// </summary>
        void Reset();
    }
}
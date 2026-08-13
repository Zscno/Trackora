using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage;
using System;

namespace Zscno.Trackora
{
    /// <summary>
    /// 用于操作应用程序的本地设置。
    /// </summary>
    internal static class Settings
    {
        /// <summary>
        /// 当前应用程序的本地设置。
        /// </summary>
        private static readonly ApplicationDataContainer _localSettings = ApplicationData.GetDefault().LocalSettings;

        /// <summary>
        /// 获取或设置一个值，当连续使用时间达到此值时将发送通知。
        /// </summary>
        /// <remarks>该属性的默认值为 30 分钟，以毫秒为单位。</remarks>
        internal static uint SessionThreshold
        {
            get
            {
                return _localSettings.Values.TryGetValue("SessionThreshold", out object? value) ?
                    (uint)value : (uint)TimeSpan.FromMinutes(30d).TotalMilliseconds;
            }
            set
            {
                _localSettings.Values["SessionThreshold"] = value;
            }
        }

        /// <summary>
        /// 获取或设置一个值，指定应用程序的主题。TODO: 修改导航视图的主题。
        /// </summary>
        internal static ElementTheme Theme
        {
            get
            {
                if (_localSettings.Values.TryGetValue("Theme", out object? value))
                {
                    return (int)value switch
                    {
                        1 => ElementTheme.Light,
                        2 => ElementTheme.Dark,
                        _ => ElementTheme.Default,
                    };
                }
                else
                {
                    return ElementTheme.Default;
                }
            }
            set
            {
                _localSettings.Values["Theme"] = (int)value;
            }
        }

        /// <summary>
        /// 获取或设置一个值，当总使用时间达到此值时将发送通知。
        /// </summary>
        /// <remarks>该属性的默认值为 2 小时，以毫秒为单位。</remarks>
        internal static uint DailyThreshold
        {
            get
            {
                return _localSettings.Values.TryGetValue("DailyThreshold", out object? value) ?
                    (uint)value : (uint)TimeSpan.FromHours(2d).TotalMilliseconds;
            }
            set
            {
                _localSettings.Values["DailyThreshold"] = value;
            }
        }

        /// <summary>
        /// 重置应用程序的本地设置。
        /// </summary>
        internal static void Reset()
        {
            _localSettings.Values.Clear();
        }

        /// <summary>
        /// 获取或设置一个值，当空闲时间达到此值将重置连续使用时间。
        /// </summary>
        /// <remarks>该属性的默认值为 10 分钟，以毫秒为单位。</remarks>
        internal static uint IdleThreshold
        {
            get
            {
                return _localSettings.Values.TryGetValue("IdleThreshold", out object? value) ?
                    (uint)value : (uint)TimeSpan.FromMinutes(10d).TotalMilliseconds;
            }
            set
            {
                _localSettings.Values["IdleThreshold"] = value;
            }
        }
    }
}
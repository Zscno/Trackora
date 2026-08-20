using Microsoft.UI.Xaml;
using Microsoft.Windows.Storage;
using System;

namespace Zscno.Trackora
{
    /// <summary>
    /// 提供获取和修改应用程序本地设置的功能。
    /// </summary>
    internal class LocalSettings : ISettings
    {
        private readonly ApplicationDataContainer _localSettings = ApplicationData.GetDefault().LocalSettings;

        /// <inheritdoc cref="ISettings.SessionThreshold"/>
        /// <remarks>该属性的默认值为 30 分钟，以毫秒为单位。</remarks>
        public uint SessionThreshold
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

        public ElementTheme Theme
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

        /// <inheritdoc cref="ISettings.DailyThreshold"/>
        /// <remarks>该属性的默认值为 2 小时，以毫秒为单位。</remarks>
        public uint DailyThreshold
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

        public void Reset()
        {
            _localSettings.Values.Clear();
        }

        /// <inheritdoc cref="ISettings.IdleThreshold"/>
        /// <remarks>该属性的默认值为 10 分钟，以毫秒为单位。</remarks>
        public uint IdleThreshold
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
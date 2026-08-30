using System;
using Zscno.Trackora.Strings;

namespace Zscno.Trackora.Tools
{
    internal class Localization
    {
        /// <summary>
        /// 将 <paramref name="time"/> 转换成本地化时间字符串。
        /// </summary>
        /// <param name="time">要转换的时间（以毫秒为单位）。</param>
        /// <returns>本地化时间字符串。</returns>
        public static string ToLocalizedTimeString(uint time)
        {
            TimeSpan timeSpan = TimeSpan.FromMilliseconds(time);
            return timeSpan switch
            {
                { Days: 0, Hours: 0, Minutes: 0 } => "<1" + Resources.Minute,
                { Days: 0, Hours: 0 } => timeSpan.Minutes + Resources.Minute,
                _ => timeSpan.Hours + Resources.Hour + timeSpan.Minutes + Resources.Minute,
            };
        }
    }
}
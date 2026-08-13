using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;

namespace Zscno.Trackora
{
    /// <summary>
    /// 为提醒用户提供相关操作。
    /// </summary>
    internal static class ReminderManager
    {
        /// <summary>
        /// 发送指定的通知。
        /// </summary>
        /// <remarks>拥有管理员权限的应用程序无法发送通知。</remarks>
        /// <param name="title">          通知的标题。</param>
        /// <param name="content">        通知的文本内容。</param>
        /// <param name="expirationTime"> 通知保留在通知中心的时间（以毫秒为单位）。</param>
        /// <param name="expiresOnReboot">指示通知是否会在重新启动后保留在通知中心。</param>
        internal static void SendNotification(string title,
                                              string content,
                                              uint expirationTime,
                                              bool expiresOnReboot)
        {
            title ??= string.Empty;
            content ??= string.Empty;

            try
            {
                AppNotification notification = new AppNotificationBuilder()
                    .AddText(title)
                    .AddText(content)
                    .BuildNotification();
                notification.Expiration = DateTimeOffset.Now.AddMilliseconds(expirationTime);
                notification.ExpiresOnReboot = expiresOnReboot;
                AppNotificationManager.Default.Show(notification);
            }
            catch (Exception ex)
            {
                LogSystem.WriteLog(LogLevel.Warning,
                    $"通知发送失败。\n\tTitle={title},\n\tContent={content}\n\n{ex}");
            }
        }
    }
}
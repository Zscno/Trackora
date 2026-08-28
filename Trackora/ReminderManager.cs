using Microsoft.Windows.AppNotifications;
using Microsoft.Windows.AppNotifications.Builder;
using System;
using System.Threading;

namespace Zscno.Trackora
{
    /// <inheritdoc cref="IReminderManager"/>
    internal partial class ReminderManager : IReminderManager
    {
        /// <summary>
        /// 用于发送总使用时间提醒的计时器。
        /// </summary>
        private readonly Timer _dailyTimer;

        /// <summary>
        /// 用于发送连续使用时间提醒的计时器。
        /// </summary>
        private readonly Timer _sessionTimer;

        /// <inheritdoc cref="ISettings"/>
        private readonly ISettings _settings;

        /// <inheritdoc cref="IUsageRecordManager"/>
        private readonly IUsageRecordManager _usageRecordManager;

        /// <summary>
        /// 指示是否发送了总使用时间提醒。
        /// </summary>
        private bool _isDailySent;

        /// <summary>
        /// 指示当前 <see cref="ReminderManager"/> 实例使用的所有资源是否释放。若已释放，则为 1，否则为 0。
        /// </summary>
        private int _isDisposed;

        public ReminderManager(ISettings settings, IUsageRecordManager usageRecordManager/*TODO: 接收日志实例。*/)
        {
            _isDailySent = false;
            _dailyTimer = new Timer(SendDueDailyReminder, null, Timeout.Infinite, Timeout.Infinite);
            _sessionTimer = new Timer(SendDueSessionReminder, null, Timeout.Infinite, Timeout.Infinite);
            _settings = settings;
            _usageRecordManager = usageRecordManager;
        }

        /// <summary>
        /// 释放当前 <see cref="ReminderManager"/> 实例使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc cref="IReminderManager.SendDailyIfNeeded"/>
        /// <remarks>该方法只应该在应用程序启动时调用。</remarks>
        public void SendDailyIfNeeded()
        {
            if (_isDailySent)
            {
                return;
            }

            if (_usageRecordManager.Record.DailyDuration < _settings.DailyThreshold)
            {
                return;
            }

            _ = _dailyTimer.Change(0, Timeout.Infinite);
            _isDailySent = true;
        }

        public void StopAll()
        {
            _ = _dailyTimer.Change(Timeout.Infinite, Timeout.Infinite);
            _ = _sessionTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void UpdateDailyDueTime()
        {
            if (_isDailySent)
            {
                return;
            }

            uint dueTime = _usageRecordManager.Record.DailyDuration < _settings.DailyThreshold ?
                _settings.DailyThreshold - _usageRecordManager.Record.DailyDuration : 0;

            _ = _dailyTimer.Change(dueTime, Timeout.Infinite);
        }

        public void UpdateSessionDueTime()
        {
            uint dueTime = _usageRecordManager.Record.SessionDuration < _settings.SessionThreshold ?
                _settings.SessionThreshold - _usageRecordManager.Record.SessionDuration : 0;

            _ = _sessionTimer.Change(dueTime, _settings.SessionThreshold);
        }

        /// <inheritdoc cref="Dispose()"/>
        /// <param name="disposing">指示方法调用来自 <see cref="Dispose()"/>（其值是 <see langword="true"/>），还是来自析构函数（其值是 <see langword="false"/>）。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 1)
            {
                return;
            }

            if (disposing)
            {
                _dailyTimer.Dispose();
                _sessionTimer.Dispose();
            }
        }

        /// <summary>
        /// 发送指定的通知。
        /// </summary>
        /// <remarks>拥有管理员权限的应用程序无法发送通知。</remarks>
        /// <param name="title">          通知的标题。</param>
        /// <param name="content">        通知的文本内容。</param>
        /// <param name="expirationTime"> 通知保留在通知中心的时间（以毫秒为单位）。</param>
        /// <param name="expiresOnReboot">指示通知是否会在重新启动后保留在通知中心。</param>
        private static void SendNotification(string title,
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

        /// <summary>
        /// 发送到期的总使用时间提醒。
        /// </summary>
        /// <remarks>该函数是 <see cref="_dailyTimer"/> 的回调函数。</remarks>
        private void SendDueDailyReminder(object? state)
        {
            if (_isDailySent)
            {
                return;
            }

            SendNotification(
                App.Loader.GetString("UsageTimeReminderTitle"),
                App.Loader.GetString("TotalReminderText1") +
                Localization.ToLocalizedTimeString(_usageRecordManager.Record.DailyDuration) +
                App.Loader.GetString("TotalReminderText2"),
                _settings.IdleThreshold,
                false);

            _isDailySent = true;
        }

        /// <summary>
        /// 发送到期的连续使用时间提醒。
        /// </summary>
        /// <remarks>该函数是 <see cref="_sessionTimer"/> 的回调函数。</remarks>
        private void SendDueSessionReminder(object? state)
        {
            SendNotification(
                App.Loader.GetString("UsageTimeReminderTitle"),
                App.Loader.GetString("ContinuousReminderText1") +
                Localization.ToLocalizedTimeString(_settings.SessionThreshold) +
                App.Loader.GetString("ContinuousReminderText2"),
                _settings.IdleThreshold,
                false);

            _usageRecordManager.Record.SessionDuration = 0;
        }
    }
}
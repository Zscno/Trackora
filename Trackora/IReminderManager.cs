using System;

namespace Zscno.Trackora
{
    /// <summary>
    /// 提供提醒用户的功能。
    /// </summary>
    internal interface IReminderManager : IDisposable
    {
        /// <summary>
        /// 若总使用时间未达到阈值且从未发送过，则发送总使用时间提醒。
        /// </summary>
        void SendDailyIfNeeded();

        /// <summary>
        /// 停止所有提醒（包括总使用时间提醒和连续使用时间提醒）。
        /// </summary>
        void StopAll();

        /// <summary>
        /// 更新总使用时间提醒的到期时间。
        /// </summary>
        void UpdateDailyDueTime();

        /// <summary>
        /// 更新连续使用时间提醒的到期时间。
        /// </summary>
        void UpdateSessionDueTime();
    }
}
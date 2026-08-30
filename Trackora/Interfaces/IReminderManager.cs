using System;

namespace Zscno.Trackora.Interfaces
{
    /// <summary>
    /// 提供提醒用户的功能。
    /// </summary>
    internal interface IReminderManager : IDisposable
    {
        /// <summary>
        /// 重置总使用时间提醒的到期时间。
        /// </summary>
        /// <remarks>不论先前是否发送过，到期后都会发送总使用时间提醒。</remarks>
        void ResetDailyDueTime();

        /// <summary>
        /// 发送已过期的总使用时间提醒。
        /// </summary>
        /// <remarks>若总使用时间已经超过阈值，则不论先前是否发送过，都会发送总使用时间提醒。</remarks>
        void SendOverdueDaily();

        /// <summary>
        /// 停止所有提醒（包括总使用时间提醒和连续使用时间提醒）。
        /// </summary>
        void StopAll();

        /// <summary>
        /// 更新总使用时间提醒的到期时间。
        /// </summary>
        /// <remarks>若先前发送过，则不会发送总使用时间提醒。</remarks>
        void UpdateDailyDueTime();

        /// <summary>
        /// 更新连续使用时间提醒的到期时间。
        /// </summary>
        void UpdateSessionDueTime();
    }
}
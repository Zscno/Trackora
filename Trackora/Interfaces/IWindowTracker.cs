using System;
using System.Threading.Tasks;

namespace Zscno.Trackora.Interfaces
{
    /// <summary>
    /// 提供跟踪前台窗口的功能。
    /// </summary>
    internal interface IWindowTracker : IDisposable
    {
        /// <summary>
        /// 异步地启动对前台窗口的跟踪。
        /// </summary>
        Task StartAsync();
    }
}
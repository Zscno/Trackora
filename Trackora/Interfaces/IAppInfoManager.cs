using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using Zscno.Trackora.Services;

namespace Zscno.Trackora.Interfaces
{
    /// <summary>
    /// 提供获取和保存应用程序信息的功能。
    /// </summary>
    internal interface IAppInfoManager : IDataLoadable, IDataStorable, IDebounceStorable, IDisposable
    {
        /// <summary>
        /// 应用程序的信息映射表，键为进程的名称。
        /// </summary>
        ConcurrentDictionary<string, ProcessInfo> AppInfoMap { get; } // TODO: 使用 AppInfo。

        /// <summary>
        /// 缓存指定应用程序的信息，包括应用程序的显示名称和图标。
        /// </summary>
        /// <param name="windowHandle">进程关联的窗口句柄。</param>
        /// <param name="process">     进程的 <see cref="Process"/> 组件。</param>
        void CacheAppInfo(nint windowHandle, Process process);

        /// <summary>
        /// 获取指定应用程序的图标 URI。
        /// </summary>
        /// <remarks>若在图标文件夹未找到指定应用程序的图标，将使用系统的默认应用程序图标。</remarks>
        /// <param name="processName">进程的名称。</param>
        /// <returns>获取到的图标 URI。</returns>
        string GetAppIconUri(string processName);
    }
}
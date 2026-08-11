using Microsoft.Windows.Storage;
using System.Collections.Generic;
using System.IO;

namespace Zscno.Trackora
{
    /// <summary>
    /// 为筛选应忽略的进程和仅记录时间的进程提供相关操作。
    /// </summary>
    internal static class ProcessFilter
    {
        /// <summary>
        /// 忽略进程名单的文件路径。
        /// </summary>
        private static readonly string _ignoredProcessListFilePath;

        /// <summary>
        /// 写入 <see cref="_ignoredProcesses"/> 时的锁。
        /// </summary>
        private static readonly object _ignoredWriteLock = new();

        /// <summary>
        /// 仅记录时间进程名单的文件路径。
        /// </summary>
        private static readonly string _timeOnlyProcessListFilePath;

        /// <summary>
        /// 写入 <see cref="_timeOnlyProcesses"/> 时的锁。
        /// </summary>
        private static readonly object _timeOnlyWriteLock = new();

        /// <summary>
        /// 所有忽略进程的进程名称。
        /// </summary>
        private static HashSet<string> _ignoredProcesses;

        /// <summary>
        /// 所有仅记录时间进程的进程名称。
        /// </summary>
        private static HashSet<string> _timeOnlyProcesses;

        static ProcessFilter()
        {
            _ignoredProcesses = ["dwm", "LockApp", "ServiceHub.ThreadedWaitDialog"];
            _timeOnlyProcesses = ["StartMenuExperienceHost", "SearchHost", "PickerHost",
                "consent", "OpenWith", "Widgets", "ShellExperienceHost"];
            string localPath = ApplicationData.GetDefault().LocalPath;
            _ignoredProcessListFilePath = Path.Combine(localPath, "IgnoredProcesses.json");
            _timeOnlyProcessListFilePath = Path.Combine(localPath, "TimeOnlyProcesses.json");
        }

        /// <summary>
        /// 添加忽略进程。
        /// </summary>
        /// <param name="processName">要添加进程的名称。</param>
        /// <returns>若进程已添加，则为 <see langword="true"/>；若进程已存在，则为 <see langword="false"/>。</returns>
        internal static bool AddIgnoredProcess(string processName)
        {
            lock (_ignoredWriteLock)
            {
                return _ignoredProcesses.Add(processName);
            }
        }

        /// <summary>
        /// 添加仅记录时间的进程。
        /// </summary>
        /// <param name="processName">要添加进程的名称。</param>
        /// <returns>若进程已添加，则为 <see langword="true"/>；若进程已存在，则为 <see langword="false"/>。</returns>
        internal static bool AddOnlyTimeProcess(string processName)
        {
            lock (_timeOnlyWriteLock)
            {
                return _timeOnlyProcesses.Add(processName);
            }
        }

        /// <summary>
        /// 从文件中读取忽略进程名单和仅记录时间的进程名单。
        /// </summary>
        internal static void Initialize()
        {
            HashSet<string>? ignoredProcesses = Json.ReadJsonFile(_ignoredProcessListFilePath, SourceGenerationContext.Default.HashSetString);
            if (ignoredProcesses is not null)
            {
                _ignoredProcesses = ignoredProcesses;
            }

            HashSet<string>? timeOnlyProcesses = Json.ReadJsonFile(_timeOnlyProcessListFilePath, SourceGenerationContext.Default.HashSetString);
            if (timeOnlyProcesses is not null)
            {
                _timeOnlyProcesses = timeOnlyProcesses;
            }
        }

        /// <summary>
        /// 确定指定的进程是否忽略。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若忽略，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        internal static bool IsIgnoredProcess(string processName)
        {
            return _ignoredProcesses.Contains(processName);
        }

        /// <summary>
        /// 确定指定的进程是否仅记录时间。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若仅记录时间，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        internal static bool IsTimeOnlyProcess(string processName)
        {
            return _timeOnlyProcesses.Contains(processName);
        }

        /// <summary>
        /// 不忽略指定的进程。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若成功找到并删除了进程，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        internal static bool RemoveIgnoredProcess(string processName)
        {
            lock (_ignoredWriteLock)
            {
                return _ignoredProcesses.Remove(processName);
            }
        }

        /// <summary>
        /// 不再仅记录指定的进程的使用时间，而是记录其完整信息。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若成功找到并删除了进程，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        internal static bool RemoveTimeOnlyProcess(string processName)
        {
            lock (_timeOnlyWriteLock)
            {
                return _timeOnlyProcesses.Remove(processName);
            }
        }

        /// <summary>
        /// 保存忽略进程名单。
        /// </summary>
        internal static string SaveIgnoredProcesses()
        {
            return Json.WriteJsonFile(
                _ignoredProcessListFilePath,
                _ignoredProcesses,
                SourceGenerationContext.Default.HashSetString);
        }

        /// <summary>
        /// 保存仅记录时间的进程名单。
        /// </summary>
        internal static string SaveTimeOnlyProcesses()
        {
            return Json.WriteJsonFile(
                _timeOnlyProcessListFilePath,
                _timeOnlyProcesses,
                SourceGenerationContext.Default.HashSetString);
        }
    }
}
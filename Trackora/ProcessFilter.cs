using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zscno.Trackora
{
    /// <inheritdoc cref="IProcessFilter"/>
    internal partial class ProcessFilter : IProcessFilter
    {
        private readonly DebounceSaver _debounceSaverIgnored;

        private readonly DebounceSaver _debounceSaverTimeOnly;

        private readonly string _ignoredProcessListFilePath;

        private readonly string _timeOnlyProcessListFilePath;

        private readonly object _writeLockIgnored;

        private readonly object _writeLockTimeOnly;

        private HashSet<string> _ignoredProcessList;

        /// <summary>
        /// 指示当前 <see cref="WindowTracker"/> 实例使用的所有资源是否释放。若已释放，则为 1，否则为 0。
        /// </summary>
        private int _isDisposed;

        private HashSet<string> _timeOnlyProcessList;

        public ProcessFilter(IAppDataPathProvider pathProvider/*TODO: 接收日志实例。*/)
        {
            _ignoredProcessList = ["dwm", "LockApp", "ServiceHub.ThreadedWaitDialog"];
            _timeOnlyProcessList = ["StartMenuExperienceHost", "SearchHost", "PickerHost",
                "consent", "OpenWith", "Widgets", "ShellExperienceHost"];
            _ignoredProcessListFilePath = Path.Combine(pathProvider.LocalPath, "IgnoredProcesses.json");
            _timeOnlyProcessListFilePath = Path.Combine(pathProvider.LocalPath, "TimeOnlyProcesses.json");
            _writeLockIgnored = new object();
            _writeLockTimeOnly = new object();
            _debounceSaverIgnored = new DebounceSaver(SaveIgnoredProcessListAsync, exceptionHandler: OnSaveIgnoredFailed);
            _debounceSaverTimeOnly = new DebounceSaver(SaveTimeOnlyProcessListAsync, exceptionHandler: OnSaveTimeOnlyFailed);
        }

        public bool AddIgnoredProcess(string processName)
        {
            lock (_writeLockIgnored)
            {
                return _ignoredProcessList.Add(processName);
            }
        }

        public bool AddTimeOnlyProcess(string processName)
        {
            lock (_writeLockTimeOnly)
            {
                return _timeOnlyProcessList.Add(processName);
            }
        }

        /// <summary>
        /// 释放当前 <see cref="ProcessFilter"/> 实例使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public bool IsIgnoredProcess(string processName)
        {
            return _ignoredProcessList.Contains(processName);
        }

        public bool IsTimeOnlyProcess(string processName)
        {
            return _timeOnlyProcessList.Contains(processName);
        }

        /// <summary>
        /// 加载文件中的忽略进程名单和仅记录时间的进程名单。
        /// </summary>
        public Task LoadAsync()
        {
            HashSet<string>? ignoredProcesses = Json.ReadJsonFile(_ignoredProcessListFilePath, SourceGenerationContext.Default.HashSetString);
            if (ignoredProcesses is not null)
            {
                _ignoredProcessList = ignoredProcesses;
            }

            HashSet<string>? timeOnlyProcesses = Json.ReadJsonFile(_timeOnlyProcessListFilePath, SourceGenerationContext.Default.HashSetString);
            if (timeOnlyProcesses is not null)
            {
                _timeOnlyProcessList = timeOnlyProcesses;
            }

            return Task.CompletedTask;
        }

        public bool RemoveIgnoredProcess(string processName)
        {
            lock (_writeLockIgnored)
            {
                return _ignoredProcessList.Remove(processName);
            }
        }

        public bool RemoveTimeOnlyProcess(string processName)
        {
            lock (_writeLockTimeOnly)
            {
                return _timeOnlyProcessList.Remove(processName);
            }
        }

        public void RequestSaveIgnoredProcessList()
        {
            _debounceSaverIgnored.RequestStore();
        }

        public void RequestSaveTimeOnlyProcessList()
        {
            _debounceSaverTimeOnly.RequestStore();
        }

        public Task SaveIgnoredProcessListAsync()
        {
            _ = Json.WriteJsonFile(
                _ignoredProcessListFilePath,
                _ignoredProcessList,
                SourceGenerationContext.Default.HashSetString);
            return Task.CompletedTask;
        }

        public Task SaveTimeOnlyProcessListAsync()
        {
            _ = Json.WriteJsonFile(
                _timeOnlyProcessListFilePath,
                _timeOnlyProcessList,
                SourceGenerationContext.Default.HashSetString);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 将忽略进程名单和仅记录时间的进程名单保存到文件。
        /// </summary>
        public async Task StoreAsync()
        {
            var saveIgnored = SaveIgnoredProcessListAsync();
            var saveTimeOnly = SaveTimeOnlyProcessListAsync();

            await Task.WhenAll(saveIgnored, saveTimeOnly);
        }

        /// <inheritdoc cref="Dispose()"/>
        /// <param name="disposing">指示方法调用来自 <see cref="Dispose()"/>（其值是 <see langword="true"/>），还是来自析构函数（其值是 <see langword="false"/>）。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 1)
            {
                if (disposing)
                {
                    _debounceSaverIgnored.Dispose();
                    _debounceSaverTimeOnly.Dispose();
                }
            }
        }

        private static Task OnSaveIgnoredFailed(Exception ex)
        {
            LogSystem.WriteLog(LogLevel.Error, $"延迟保存忽略进程名单失败。{ex}");
            // TODO: 在主页提示用户。
            return Task.CompletedTask;
        }

        private static Task OnSaveTimeOnlyFailed(Exception ex)
        {
            LogSystem.WriteLog(LogLevel.Error, $"延迟保存仅记录时间进程名单失败。{ex}");
            // TODO: 在主页提示用户。
            return Task.CompletedTask;
        }
    }
}
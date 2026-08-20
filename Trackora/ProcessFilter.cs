using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Zscno.Trackora
{
    /// <inheritdoc cref="IProcessFilter"/>
    internal class ProcessFilter : IProcessFilter
    {
        private readonly string _ignoredProcessListFilePath;

        private readonly string _timeOnlyProcessListFilePath;

        private readonly object _writeLockIgnored;

        private readonly object _writeLockTimeOnly;

        private HashSet<string> _ignoredProcessList;

        private HashSet<string> _timeOnlyProcessList;

        public ProcessFilter(IAppDataPathProvider pathProvider/*TODO: 接收日志实例。*/)
        {
            _ignoredProcessList = ["dwm", "LockApp", "ServiceHub.ThreadedWaitDialog"];
            _timeOnlyProcessList = ["StartMenuExperienceHost", "SearchHost", "PickerHost",
                "consent", "OpenWith", "Widgets", "ShellExperienceHost"];
            _ignoredProcessListFilePath = Path.Combine(pathProvider.LocalPath, "IgnoredProcesses.json");
            _timeOnlyProcessListFilePath = Path.Combine(pathProvider.LocalPath, "TimeOnlyProcesses.json");
            _writeLockIgnored = new();
            _writeLockTimeOnly = new();
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
    }
}
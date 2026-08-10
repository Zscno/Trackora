using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Zscno.Trackora.App;
using static Zscno.Trackora.LogSystem;

namespace Zscno.Trackora
{
    internal class WindowTracker
    {
        /// <summary>
        /// 用于过滤只记录时间的进程名称的字符串数组
        /// </summary>
        private static string[] _lastNotInfoNamesArr = [];

        /// <summary>
        /// 用于过滤只记录时间的进程名称的字符串（以英文逗号分隔）。
        /// </summary>
        private static string _lastOnlyTimeProcessesStr = string.Empty;

        /// <summary>
        /// 用于发送连续使用时间的计时器。
        /// </summary>
        private readonly Timer _continuousReminderTimer;

        /// <summary>
        /// 保存委托实例，防止被GC回收。
        /// </summary>
        private readonly WinEventDelegate _delegate;

        /// <summary>
        /// 事件挂钩实例标识。
        /// </summary>
        private readonly nint _hookHandle;

        /// <summary>
        /// 用于记录正在保存进程信息的进程名称的线程安全字典。
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> _recordingProcessName = new();

        /// <summary>
        /// 用于保存使用记录的计时器。
        /// </summary>
        private readonly Timer _savingRecordTimer;

        /// <summary>
        /// 用于发送总使用时间的计时器。
        /// </summary>
        private readonly Timer _totalReminderTimer;

        /// <summary>
        /// 连续使用时长。
        /// </summary>
        private uint _continuousUsageTime;

        /// <summary>
        /// 上次前台窗口改变的时间（单位为毫秒）。
        /// </summary>
        private uint _lastChangedTime;

        /// <summary>
        /// 用于过滤进程名称的字符串数组。
        /// </summary>
        private string[] _lastIgnoredProcessesArr = [];

        /// <summary>
        /// 用于过滤进程名称的字符串（以英文逗号分隔）。
        /// </summary>
        private string _lastIgnoredProcessesStr = string.Empty;

        /// <summary>
        /// 上一个前台进程名称。
        /// </summary>
        private string? _lastProcessName;

        /// <summary>
        /// 结束使用的时间。
        /// </summary>
        public static TimeSpan EndUsingTime
        {
            get => (TimeSpan)LocalSettings["EndUsingTime"];

            set => LocalSettings["EndUsingTime"] = value;
        }

        /// <summary>
        /// 指示总使用时长提醒是否已经显示。
        /// </summary>
        public static bool IsTotalUsageReminderShown { get; set; }

        public WindowTracker()
        {
            _delegate = new WinEventDelegate(OnForegroundWindowChanged);
            _hookHandle = NativeApi.SetWinEventHook(
                NativeApi.EVENT_SYSTEM_FOREGROUND,
                NativeApi.EVENT_SYSTEM_FOREGROUND,
                nint.Zero, _delegate, 0, 0,
                NativeApi.WINEVENT_OUTOFCONTEXT);
            _totalReminderTimer = new Timer(SendDueTotalReminder, null, Timeout.Infinite, Timeout.Infinite);
            _continuousReminderTimer = new Timer(SendDueContinuousReminder, null, Timeout.Infinite, Timeout.Infinite);
            _savingRecordTimer = new Timer(SaveLatestUsageRecord, null, Timeout.Infinite, Timeout.Infinite);

            if (UsageRecordManager.Record.TotalUsageTime >= Settings.TotalThreshold &&
                !IsTotalUsageReminderShown)
            {
                SendDueTotalReminder(null);
            }
        }

        /// <summary>
        /// 卸载事件挂钩函数并释放计时器资源。
        /// </summary>
        public void Dispose()
        {
            bool isSuccessful = NativeApi.UnhookWinEvent(_hookHandle);
            if (isSuccessful)
            {
                WriteLog(LogLevel.Info, $"事件挂钩函数卸载成功。");
            }
            else
            {
                WriteLog(LogLevel.Error, $"事件挂钩函数卸载失败，错误代码：{Marshal.GetLastWin32Error()}。");
            }
            _totalReminderTimer.Dispose();
            _continuousReminderTimer.Dispose();
            _savingRecordTimer.Dispose();
        }

        /// <summary>
        /// 排除任务栏和桌面。
        /// </summary>
        /// <param name="handle">进程句柄。</param>
        /// <returns>指示是否继续记录进程。</returns>
        private static bool CheckExplorerProcess(nint handle)
        {
            if (!TryGetChildWindowHandle(handle, out nint childHandle,
                    "获取 explorer 进程的子窗口句柄失败。") ||
                !TryGetWindowClassName(childHandle, out string className,
                    $"获取 explorer 子进程 [Handle={childHandle}] 的类名失败。"))
            {
                return false;
            }
            if (className is "Windows.UI.Core.CoreWindow" or "SHELLDLL_DefView")
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// 获取用于过滤只记录时间的进程名称的 <see cref="HashSet{T}"/> 。
        /// </summary>
        /// <returns>用于过滤只记录时间的进程名称的 <see cref="HashSet{T}"/> 。</returns>
        private static HashSet<string> GetNoInfoArr()
        {
            string OnlyTimeProcessesStr = (string)LocalSettings["OnlyTimeProcesses"];
            if (_lastOnlyTimeProcessesStr != OnlyTimeProcessesStr)
            {
                _lastNotInfoNamesArr = OnlyTimeProcessesStr.Split(',');
                _lastOnlyTimeProcessesStr = OnlyTimeProcessesStr;
            }

            return new HashSet<string>(_lastNotInfoNamesArr);
        }

        /// <summary>
        /// 排除任务栏和桌面，并获取真正的 UWP 进程。
        /// </summary>
        /// <remarks>
        /// 当返回的 <see langword="bool"/> 值为 <see langword="false"/> 时会调用 <see cref="NoProcessNow"/> 方法。
        /// </remarks>
        /// <param name="name">进程名称。</param>
        /// <param name="handle">进程句柄。</param>
        /// <param name="uwpProcess">真正的 UWP 进程。</param>
        /// <returns>指示是否继续记录进程。</returns>
        private static bool GetRealProcess(string name, nint handle, out Process? uwpProcess)
        {
            switch (name)
            {
                case "explorer":
                    uwpProcess = null;
                    return CheckExplorerProcess(handle);

                case "ApplicationFrameHost":
                    return GetRealUwpProcess(handle, out uwpProcess);

                default:
                    uwpProcess = null;
                    return true;
            }
        }

        /// <summary>
        /// 获取真正的 UWP 进程。
        /// </summary>
        /// <param name="handle">进程句柄。</param>
        /// <param name="uwpProcess">真正的 UWP 进程。</param>
        /// <returns>指示是否继续记录进程。</returns>
        private static bool GetRealUwpProcess(nint handle, out Process? uwpProcess)
        {
            if (TryGetChildWindowHandle(handle, out nint childHandle,
                    "获取 UWP 进程子窗口的句柄失败。", "Windows.UI.Core.CoreWindow") &&
                NativeApi.TryGetProcessByWindowHandle(childHandle, out uwpProcess))
            {
                return true;
            }
            else
            {
                uwpProcess = null;
                return false;
            }
        }

        /// <summary>
        /// 如果达到了结束使用时间则发送结束使用时间提醒。
        /// </summary>
        private static void SendEndUsingReminderIfNeeded()
        {
            TimeSpan currentTimeWithoutSeconds = new(DateTime.Now.Hour, DateTime.Now.Minute, 0);
            if (EndUsingTime == currentTimeWithoutSeconds && EndUsingTime != TimeSpan.Zero)
            {
                CanShowReminder = ReminderHelper.SendReminder(ReminderKind.EndUsingTimeReminder);
                EndUsingTime = TimeSpan.Zero;
            }
        }

        /// <summary>
        /// 尝试获取句柄为 <paramref name="parentHandle"/> 的进程的子窗口句柄。
        /// </summary>
        /// <param name="parentHandle">进程句柄。</param>
        /// <param name="childHandle">子窗口句柄。</param>
        /// <param name="className">指定子窗口的类名。</param>
        /// <param name="logMessage">如果返回值为 <see langword="false"/> ，则将此和错误写入日志。</param>
        /// <returns>指示 <paramref name="childHandle"/> 是否为 <see cref="nint.Zero"/> 。</returns>
        private static bool TryGetChildWindowHandle(nint parentHandle, out nint childHandle,
            string logMessage, string? className = null)
        {
            childHandle = NativeApi.FindWindowEx(parentHandle, nint.Zero, className, null);
            if (childHandle == nint.Zero)
            {
                WriteLog(LogLevel.Error, $"{logMessage}错误代码：{Marshal.GetLastWin32Error()}。");
                return false;
            }

            return true;
        }

        /// <summary>
        /// 尝试获取窗口的类名。
        /// </summary>
        /// <param name="handle">窗口的句柄。</param>
        /// <param name="className">窗口的类名。</param>
        /// <param name="logMessage">如果返回值为 <see langword="false"/> ，则将此和错误写入日志。</param>
        /// <returns>指示是否成功获取窗口类名。</returns>
        private static bool TryGetWindowClassName(nint handle, out string className, string logMessage)
        {
            StringBuilder classNameBuilder = new(256);
            int classNameLength = NativeApi.GetClassName(handle, classNameBuilder, classNameBuilder.Capacity);
            if (classNameLength == 0)
            {
                WriteLog(LogLevel.Error, $"{logMessage}错误代码：{Marshal.GetLastWin32Error()}。");
                className = string.Empty;
                return false;
            }

            className = classNameBuilder.ToString();
            return true;
        }

        /// <summary>
        /// 获取用于过滤不记录任何信息的进程名称的 <see cref="HashSet{T}"/> 。
        /// </summary>
        /// <returns>用于过滤不记录任何信息的进程名称的 <see cref="HashSet{T}"/> 。</returns>
        private HashSet<string> GetNoTimeArr()
        {
            string ignoredProcessesStr = (string)LocalSettings["IgnoredProcesses"];
            if (_lastIgnoredProcessesStr != ignoredProcessesStr)
            {
                _lastIgnoredProcessesArr = ignoredProcessesStr.Split(',');
                _lastIgnoredProcessesStr = ignoredProcessesStr;
            }
            return new HashSet<string>(_lastIgnoredProcessesArr);
        }

        /// <summary>
        /// 应用程序定义的挂钩函数，系统调用该函数以响应辅助对象生成的事件。挂钩函数根据需要处理事件通知。
        /// </summary>
        /// <param name="hWinEventHook">
        /// 事件挂钩函数的句柄。 此值在安装挂钩函数时由 <see cref="NativeApi.SetWinEventHook(uint, uint, nint,
        /// WinEventDelegate, uint, uint, uint)"/> 返回，并且特定于挂钩函数的每个实例。
        /// </param>
        /// <param name="eventType">指定发生的事件，在此处为 <see cref="NativeApi.EVENT_SYSTEM_FOREGROUND"/>。</param>
        /// <param name="hwnd">生成事件的窗口的句柄，在此处为前台窗口的句柄。</param>
        /// <param name="idObject">标识与事件关联的对象，在此处为 <c>OBJID_WINDOW</c>，指窗口本身，而不是子对象。</param>
        /// <param name="idChild">标识事件是由对象还是对象的子元素触发的事件，在此处为 <c>CHILDID_SELF</c>，指由对象触发。</param>
        /// <param name="dwEventThread"></param>
        /// <param name="dwmsEventTime">指定生成事件的时间（以毫秒为单位）。</param>
        private void OnForegroundWindowChanged(
            nint hWinEventHook,
            uint eventType,
            nint hwnd,
            int idObject,
            int idChild,
            uint dwEventThread,
            uint dwmsEventTime)
        {
            //Stopwatch stopwatch = new();
            //stopwatch.Start();

            bool isSuccessful = false;
            if (_lastProcessName != null)
            {
                isSuccessful = new SafeCaller()
                {
                    LogMessage = $"在内存中记录进程 {_lastProcessName} 的使用时长失败。",
                    RemindingMsgResKey = "ECanNotRecordTime",
                }.CallMethodR(() => RecordUsageTimeIntoMemory(_lastProcessName, dwmsEventTime));
                if (!_savingRecordTimer.Change(2000, Timeout.Infinite))
                {
                    WriteLog(LogLevel.Error, "启动计时器失败，可能无法及时保存使用记录。");
                    // TODO: 使用事件处理失败情况。
                }
                //WriteLog(LogLevel.Debug, $"当前总使用时长：{TotalUsageTime}，连续使用时长：{_continuousUsageTime}。");
            }

            if (!NativeApi.TryGetProcessByWindowHandle(hwnd, out Process? process) ||
                GetNoTimeArr().Contains(process!.ProcessName) ||
                !GetRealProcess(process.ProcessName, hwnd, out Process? uwpProcess))
            {
                if (!_totalReminderTimer.Change(Timeout.Infinite, Timeout.Infinite) ||
                    !_continuousReminderTimer.Change(Timeout.Infinite, Timeout.Infinite))
                {
                    WriteLog(LogLevel.Error, "停止计时器失败，可能无法发送提醒。");
                    // TODO: 使用事件处理失败情况。
                }
                if (_lastProcessName == null &&
                    dwmsEventTime - _lastChangedTime >= Settings.IdleThreshold)
                {
                    _continuousUsageTime = 0;
                }
                else
                {
                    _lastProcessName = null;
                    _lastChangedTime = dwmsEventTime;
                }

                //stopwatch.Stop();
                WriteLog(LogLevel.Debug, $"上次记录：{_lastProcessName ?? "null"}，本次记录：null。");
                //，用时：{stopwatch.Elapsed}
            }
            else
            {
                if (isSuccessful)
                {
                    StartReminderTimers();
                }

                Process recordedProcess = uwpProcess ?? process;
                string processName = recordedProcess.ProcessName;

                if (uwpProcess is not null)
                {
                    process.Close();
                }

                _ = Task.Run(() => _ = new SafeCaller()
                {
                    LogMessage = $"保存 {processName} 的进程信息失败。",
                    ShouldRemind = false,
                }.CallMethodR(() => SaveProcessInfo(hwnd, recordedProcess)));

                //stopwatch.Stop();
                WriteLog(LogLevel.Debug, $"上次记录：{_lastProcessName ?? "null"}，" +
                    $"本次记录：{processName}。");/*，用时：{stopwatch.Elapsed}*/

                _lastProcessName = processName;
                _lastChangedTime = dwmsEventTime;
            }
        }

        /// <summary>
        /// 在内存中记录进程 <paramref name="name"/> 的单次使用时长、总使用时长和连续使用时长。
        /// </summary>
        /// <param name="name">要记录的进程名称。</param>
        /// <param name="currentChangedTime">本次前台窗口改变的时间（单位为毫秒）。</param>
        private void RecordUsageTimeIntoMemory(string name, uint currentChangedTime)
        {
            uint currentUsageTime = currentChangedTime - _lastChangedTime;
            UsageRecordManager.Record.TotalUsageTime += currentUsageTime;
            _continuousUsageTime += currentUsageTime;
            if (!GetNoInfoArr().Contains(name))
            {
                uint appUsageTime =
                    UsageRecordManager.Record.ProcessUsageRecords.TryGetValue(name, out uint pastUsageTime)
                    ? pastUsageTime + currentUsageTime
                    : currentUsageTime;
                UsageRecordManager.Record.ProcessUsageRecords[name] = appUsageTime;
            }
        }

        /// <summary>
        /// 保存最新的使用记录。
        /// </summary>
        /// <remarks>该函数是 <see cref="_savingRecordTimer"/> 的回调函数。</remarks>
        /// <param name="state"></param>
        private void SaveLatestUsageRecord(object? state)
        {
            _ = new SafeCaller()
            {
                LogMessage = "保存使用记录失败。",
                RemindingMsgResKey = "ECanNotSetRecord",
            }.CallMethodR(() => UsageRecordManager.SaveRecord());
        }

        /// <summary>
        /// 保存指定进程的信息。
        /// </summary>
        /// <param name="windowHandle">窗口句柄。</param>
        /// <param name="process">进程的 <see cref="Process"/> 组件。</param>
        private void SaveProcessInfo(nint windowHandle, Process process)
        {
            string processName = process.ProcessName;
            if (GetNoInfoArr().Contains(processName) ||
                ProcessInfoManager.ProcessInfoMap.ContainsKey(processName) ||
                !_recordingProcessName.TryAdd(processName, 0))
            {
                return;
            }

            try
            {
                ProcessInfo processInfo = ProcessInfoManager.GetProcessInfo(windowHandle, process);
                ProcessInfoManager.ProcessInfoMap[processName] = processInfo;
                _ = ProcessInfoManager.SaveProcessInfoMap();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _ = _recordingProcessName.TryRemove(processName, out _);
                process.Close();
            }
        }

        /// <summary>
        /// 发送到期的连续使用时间提醒。
        /// </summary>
        /// <remarks>该函数是 <see cref="_continuousReminderTimer"/> 的回调函数。</remarks>
        /// <param name="state"></param>
        private void SendDueContinuousReminder(object? state)
        {
            CanShowReminder = ReminderHelper.SendReminder(ReminderKind.ContinuousUsageTimeReminder);
            _continuousUsageTime = 0;
        }

        /// <summary>
        /// 发送到期的总使用时间提醒。
        /// </summary>
        /// <remarks>该函数是 <see cref="_totalReminderTimer"/> 的回调函数。</remarks>
        /// <param name="state"></param>
        private void SendDueTotalReminder(object? state)
        {
            if (!IsTotalUsageReminderShown)
            {
                CanShowReminder = ReminderHelper.SendReminder(ReminderKind.TotalUsageTimeReminder);
                IsTotalUsageReminderShown = true;
            }
        }

        /// <summary>
        /// 启动发送提醒的计时器。
        /// </summary>
        private void StartReminderTimers()
        {
            uint continuousRemainingTime = _continuousUsageTime < Settings.SessionThreshold ?
                Settings.SessionThreshold - _continuousUsageTime : 0;
            if (_continuousReminderTimer.Change(continuousRemainingTime, Settings.SessionThreshold))
            {
                WriteLog(LogLevel.Debug, $"连续使用时长提醒将在 {continuousRemainingTime / 1000d:f2} 秒后发送。");
            }
            else
            {
                WriteLog(LogLevel.Error, "启动连续使用时长提醒计时器失败，可能无法发送提醒。");
                // TODO: 使用事件处理失败情况。
            }

            if (IsTotalUsageReminderShown)
            {
                return;
            }
            uint totalRemainingTime = UsageRecordManager.Record.TotalUsageTime < Settings.TotalThreshold ?
                Settings.TotalThreshold - UsageRecordManager.Record.TotalUsageTime : 0;
            if (_totalReminderTimer.Change(totalRemainingTime, Timeout.Infinite))
            {
                WriteLog(LogLevel.Debug, $"总使用时长提醒将在 {totalRemainingTime / 1000d:f2} 秒后发送。");
            }
            else
            {
                WriteLog(LogLevel.Error, "启动总使用时长提醒计时器失败，可能无法发送提醒。");
                // TODO: 使用事件处理失败情况。
            }
        }
    }
}
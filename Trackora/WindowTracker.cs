using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using static Zscno.Trackora.App;
using static Zscno.Trackora.LogSystem;

namespace Zscno.Trackora
{
    internal class WindowTracker
    {
        /// <summary>
        /// 用于发送总使用时间的计时器。
        /// </summary>
        private readonly Timer _dailyTimer;

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
        private readonly Timer _saveRecordTimer;

        /// <summary>
        /// 用于发送连续使用时间的计时器。
        /// </summary>
        private readonly Timer _sessionTimer;

        /// <summary>
        /// 上次发生前台窗口改变事件的时间（单位为毫秒）。
        /// </summary>
        private uint _lastEventTime;

        /// <summary>
        /// 上一个前台进程名称。
        /// </summary>
        private string? _lastProcessName;

        /// <summary>
        /// 连续使用时长。
        /// </summary>
        private uint _sessionDuration;

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
        public static bool IsDailyReminderShown { get; set; }

        public WindowTracker()
        {
            _delegate = new WinEventDelegate(OnForegroundWindowChanged);
            _hookHandle = NativeApi.SetWinEventHook(
                NativeApi.EVENT_SYSTEM_FOREGROUND,
                NativeApi.EVENT_SYSTEM_FOREGROUND,
                nint.Zero, _delegate, 0, 0,
                NativeApi.WINEVENT_OUTOFCONTEXT);
            _sessionTimer = new Timer(SendDueSessionReminder, null, Timeout.Infinite, Timeout.Infinite);
            _saveRecordTimer = new Timer(SaveLatestUsageRecord, null, Timeout.Infinite, Timeout.Infinite);

            if (UsageRecordManager.Record.DailyDuration >= Settings.DailyThreshold && !IsDailyReminderShown)
            {
                _dailyTimer = new Timer(SendDueDailyReminder, null, 0, Timeout.Infinite);
            }
            else
            {
                _dailyTimer = new Timer(SendDueDailyReminder, null, Timeout.Infinite, Timeout.Infinite);
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
            _dailyTimer.Dispose();
            _sessionTimer.Dispose();
            _saveRecordTimer.Dispose();
        }

        /// <summary>
        /// 获取 <c>ApplicationFrameHost</c> 下的 UWP 进程。
        /// </summary>
        /// <remarks></remarks>
        /// <param name="windowHandle"><c>ApplicationFrameHost</c> 窗口的句柄。</param>
        /// <returns>获取到的 UWP 进程，若获取失败则返回 <see langword="null"/>。</returns>
        private static Process? GetUwpProcessOrNull(nint windowHandle)
        {
            if (!NativeApi.TryGetChildWindowHandle(
                windowHandle, out nint childHandle, "Windows.UI.Core.CoreWindow"))
            {
                WriteLog(LogLevel.Warning,
                    $"获取 ApplicationFrameHost 窗口 [Handle={windowHandle}] 的子窗口句柄失败" +
                    $"（{Marshal.GetLastPInvokeError()}）：{Marshal.GetLastPInvokeErrorMessage()}");
                return null;
            }

            if (!TryGetProcessByWindowHandle(childHandle, out Process? uwpProcess))
            {
                return null;
            }

            return uwpProcess;
        }

        /// <summary>
        /// 确定指定的 <c>explorer</c> 窗口是否为任务栏和桌面。
        /// </summary>
        /// <param name="windowHandle"><c>explorer</c> 窗口的句柄。</param>
        /// <returns>指示窗口是否是任务栏或桌面。</returns>
        private static bool IsDesktopOrTaskbar(nint windowHandle)
        {
            if (!NativeApi.TryGetWindowClassName(windowHandle, out string? className))
            {
                WriteLog(LogLevel.Warning,
                    $"获取 explorer 窗口 [Handle={windowHandle}] 的类名失败" +
                    $"（{Marshal.GetLastPInvokeError()}）：{Marshal.GetLastPInvokeErrorMessage()}");
                return false;
            }

            WriteLog(LogLevel.Info, $"explorer 窗口的类名为{className}。");
            if (className is "Windows.UI.Core.CoreWindow" or "SHELLDLL_DefView")
            {
                return true;
            }
            else
            {
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
        /// 尝试通过窗口句柄获取对应的 <see cref="Process"/> 组件。
        /// </summary>
        /// <param name="windowHandle">窗口的句柄。</param>
        /// <param name="process">     获取到的 <see cref="Process"/> 组件。</param>
        /// <returns>指示是否获取成功。</returns>
        private static bool TryGetProcessByWindowHandle(nint windowHandle, [MaybeNullWhen(false)] out Process process)
        {
            _ = NativeApi.GetWindowThreadProcessId(windowHandle, out uint processId);
            if (processId == 0u)
            {
                WriteLog(LogLevel.Error,
                    $"获取窗口 [Handle={windowHandle}] 的进程 Id 失败" +
                    $"（{Marshal.GetLastPInvokeError()}）：{Marshal.GetLastPInvokeErrorMessage()}。");
                process = null;
                return false;
            }

            (bool isSuccessful, process) = new SafeCaller()
            {
                LogMessage = $"获取进程 [Id={processId}] 的 Process 组件失败。",
                ShouldRemind = false,
            }.CallMethodWithReturnR(() => Process.GetProcessById((int)processId));
            return process is not null;
        }

        /// <summary>
        /// 尝试获取需要记录的进程。
        /// </summary>
        /// <param name="windowHandle">进程相关的窗口句柄。</param>
        /// <param name="process">     获取到的进程。</param>
        /// <returns>指示是否获取成功。</returns>
        private static bool TryGetValidProcess(nint windowHandle, [MaybeNullWhen(false)] out Process process)
        {
            process = null;

            if (!TryGetProcessByWindowHandle(windowHandle, out Process? uncertainProcess))
            {
                return false;
            }

            string uncertainName = uncertainProcess.ProcessName;

            if (uncertainName == "explorer" && IsDesktopOrTaskbar(windowHandle))
            {
                return false;
            }

            if (uncertainName == "ApplicationFrameHost")
            {
                Process? uwpProcess = GetUwpProcessOrNull(windowHandle);
                if (uwpProcess is null)
                {
                    process = uncertainProcess;
                }
                else
                {
                    process = uwpProcess;
                    uncertainProcess.Close();
                }
            }
            else
            {
                process = uncertainProcess;
            }

            if (ProcessFilter.IsIgnoredProcess(process.ProcessName))
            {
                process.Close();
                process = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 应用程序定义的挂钩函数，系统调用该函数以响应辅助对象生成的事件。挂钩函数根据需要处理事件通知。
        /// </summary>
        /// <param name="hWinEventHook">事件挂钩函数的句柄。 此值在安装挂钩函数时由 <see cref="NativeApi.SetWinEventHook(uint, uint, nint, WinEventDelegate, uint, uint, uint)"/> 返回，并且特定于挂钩函数的每个实例。</param>
        /// <param name="eventType">    指定发生的事件，在此处为 <see cref="NativeApi.EVENT_SYSTEM_FOREGROUND"/>。</param>
        /// <param name="hwnd">         生成事件的窗口的句柄，在此处为前台窗口的句柄。</param>
        /// <param name="idObject">     标识与事件关联的对象，在此处为 <c>OBJID_WINDOW</c>，指窗口本身，而不是子对象。</param>
        /// <param name="idChild">      标识事件是由对象还是对象的子元素触发的事件，在此处为 <c>CHILDID_SELF</c>，指由对象触发。</param>
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

            uint eventIntervalTime = dwmsEventTime - _lastEventTime;

            bool isSuccessful = false;
            if (_lastProcessName != null)
            {
                isSuccessful = new SafeCaller()
                {
                    LogMessage = $"更新使用时间失败，传入进程为 {_lastProcessName}。",
                    RemindingMsgResKey = "ECanNotRecordTime",
                }.CallMethodR(() => UpdateUsageTime(_lastProcessName, eventIntervalTime));
                if (!_saveRecordTimer.Change(2000, Timeout.Infinite))
                {
                    WriteLog(LogLevel.Error, "启动计时器失败，可能无法及时保存使用记录。");
                    // TODO: 使用事件处理失败情况。
                }
                //WriteLog(LogLevel.Debug, $"当前总使用时长：{TotalUsageTime}，连续使用时长：{_continuousUsageTime}。");
            }

            if (TryGetValidProcess(hwnd, out Process? process))
            {
                if (isSuccessful)
                {
                    StartSessionTimer();
                    StartDailyTimer();
                }

                string processName = process.ProcessName;

                _ = Task.Run(() => _ = new SafeCaller()
                {
                    LogMessage = $"保存 {processName} 的进程信息失败。",
                    ShouldRemind = false,
                }.CallMethodR(() => SaveProcessInfo(hwnd, process)));

                //stopwatch.Stop();
                WriteLog(LogLevel.Debug, $"上次记录：{_lastProcessName ?? "null"}，" +
                    $"本次记录：{processName}。");/*，用时：{stopwatch.Elapsed}*/

                _lastProcessName = processName;
                _lastEventTime = dwmsEventTime;
            }
            else
            {
                if (!_dailyTimer.Change(Timeout.Infinite, Timeout.Infinite) ||
                    !_sessionTimer.Change(Timeout.Infinite, Timeout.Infinite))
                {
                    WriteLog(LogLevel.Error, "停止计时器失败，可能无法发送提醒。");
                    // TODO: 使用事件处理失败情况。
                }

                if (_lastProcessName == null &&
                    eventIntervalTime >= Settings.IdleThreshold)
                {
                    _sessionDuration = 0;
                }
                else
                {
                    _lastProcessName = null;
                    _lastEventTime = dwmsEventTime;
                }

                //stopwatch.Stop();
                WriteLog(LogLevel.Debug, $"上次记录：{_lastProcessName ?? "null"}，本次记录：null。");
                //，用时：{stopwatch.Elapsed}
            }
        }

        /// <summary>
        /// 保存最新的使用记录。
        /// </summary>
        /// <remarks>该函数是 <see cref="_saveRecordTimer"/> 的回调函数。</remarks>
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
        /// <param name="process">     进程的 <see cref="Process"/> 组件。</param>
        private void SaveProcessInfo(nint windowHandle, Process process)
        {
            string processName = process.ProcessName;
            if (ProcessFilter.IsTimeOnlyProcess(processName) ||
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
        /// <remarks>该函数是 <see cref="_sessionTimer"/> 的回调函数。</remarks>
        /// <param name="state"></param>
        private void SendDueSessionReminder(object? state)
        {
            ReminderManager.SendNotification(
                Loader.GetString("UsageTimeReminderTitle"),
                Loader.GetString("ContinuousReminderText1") +
                Localization.ToLocalizedTimeString(Settings.SessionThreshold) +
                Loader.GetString("ContinuousReminderText2"),
                Settings.IdleThreshold,
                false);
            _sessionDuration = 0;
        }

        /// <summary>
        /// 发送到期的总使用时间提醒。
        /// </summary>
        /// <remarks>该函数是 <see cref="_dailyTimer"/> 的回调函数。</remarks>
        /// <param name="state"></param>
        private void SendDueDailyReminder(object? state)
        {
            if (!IsDailyReminderShown)
            {
                ReminderManager.SendNotification(
                    Loader.GetString("UsageTimeReminderTitle"),
                    Loader.GetString("TotalReminderText1") +
                    Localization.ToLocalizedTimeString(UsageRecordManager.Record.DailyDuration) +
                    Loader.GetString("TotalReminderText2"),
                    Settings.IdleThreshold,
                    false);
                IsDailyReminderShown = true;
            }
        }

        /// <summary>
        /// 启动总使用时间提醒计时器。
        /// </summary>
        private void StartDailyTimer()
        {
            if (IsDailyReminderShown)
            {
                return;
            }

            uint dailyDueTime = UsageRecordManager.Record.DailyDuration < Settings.DailyThreshold ?
                Settings.DailyThreshold - UsageRecordManager.Record.DailyDuration : 0;
            if (!_dailyTimer.Change(dailyDueTime, Timeout.Infinite))
            {
                WriteLog(LogLevel.Error, "启动总使用时长提醒计时器失败，可能无法发送提醒。");
                // TODO: 使用事件处理失败情况。
            }
            else
            {
                WriteLog(LogLevel.Debug, $"总使用时长提醒将在 {dailyDueTime / 1000d:f2} 秒后发送。");
            }
        }

        /// <summary>
        /// 启动连续使用时间提醒的计时器。
        /// </summary>
        private void StartSessionTimer()
        {
            uint sessionDueTime = _sessionDuration < Settings.SessionThreshold ?
                Settings.SessionThreshold - _sessionDuration : 0;
            if (!_sessionTimer.Change(sessionDueTime, Settings.SessionThreshold))
            {
                WriteLog(LogLevel.Error, "启动连续使用时长提醒计时器失败，可能无法发送提醒。");
                // TODO: 使用事件处理失败情况。
            }
            else
            {
                WriteLog(LogLevel.Debug, $"连续使用时长提醒将在 {sessionDueTime / 1000d:f2} 秒后发送。");
            }
        }

        /// <summary>
        /// 更新总使用时间、连续使用时间和进程的总使用记录。
        /// </summary>
        /// <param name="processName">     要记录的进程名称。</param>
        /// <param name="processUsageTime">进程的使用时间。</param>
        private void UpdateUsageTime(string processName, uint processUsageTime)
        {
            UsageRecordManager.Record.DailyDuration += processUsageTime;
            _sessionDuration += processUsageTime;
            if (!ProcessFilter.IsTimeOnlyProcess(processName))
            {
                uint appUsageTime =
                    UsageRecordManager.Record.ProcessUsageRecords.TryGetValue(processName, out uint pastUsageTime)
                    ? pastUsageTime + processUsageTime
                    : processUsageTime;
                UsageRecordManager.Record.ProcessUsageRecords[processName] = appUsageTime;
            }
        }
    }
}
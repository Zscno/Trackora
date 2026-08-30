using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Zscno.Trackora.Interfaces;
using Zscno.Trackora.Tools;

namespace Zscno.Trackora.Services
{
    /// <inheritdoc cref="IWindowTracker"/>
    internal partial class WindowTracker : IWindowTracker
    {
        /// <inheritdoc cref="IAppInfoManager"/>
        private readonly IAppInfoManager _appInfoManager;

        /// <summary>
        /// 保存委托实例，防止被GC回收。
        /// </summary>
        private readonly WinEventDelegate _delegate;

        /// <inheritdoc cref="IProcessFilter"/>
        private readonly IProcessFilter _processFilter;

        /// <summary>
        /// 用于确保缓存应用程序信息时的线程安全。
        /// </summary>
        private readonly ConcurrentDictionary<string, byte> _recordingProcessName = new();

        /// <inheritdoc cref="IReminderManager"/>
        private readonly IReminderManager _reminderManager;

        /// <inheritdoc cref="ISettings"/>
        private readonly ISettings _settings;

        /// <inheritdoc cref="IUsageRecordManager"/>
        private readonly IUsageRecordManager _usageRecordManager;

        /// <summary>
        /// 事件挂钩实例标识。
        /// </summary>
        private SafeEventHookHandle? _hookHandle;

        /// <summary>
        /// 指示当前 <see cref="WindowTracker"/> 实例使用的所有资源是否释放。若已释放，则为 1，否则为 0。
        /// </summary>
        private int _isDisposed;

        /// <summary>
        /// 上次发生前台窗口改变事件的时间（单位为毫秒）。
        /// </summary>
        private uint _lastEventTime;

        /// <summary>
        /// 上一个前台窗口关联的进程名称。
        /// </summary>
        private string? _lastProcessName;

        public WindowTracker(
            IAppInfoManager appInfoManager,
            IProcessFilter processFilter,
            IUsageRecordManager usageRecordManager,
            IReminderManager reminderManager,
            ISettings settings
            /*TODO: 接收日志实例。*/)
        {
            _isDisposed = 0;
            _delegate = new WinEventDelegate(OnForegroundWindowChanged);
            _appInfoManager = appInfoManager;
            _processFilter = processFilter;
            _usageRecordManager = usageRecordManager;
            _reminderManager = reminderManager;
            _settings = settings;
        }

        /// <summary>
        /// 释放当前 <see cref="WindowTracker"/> 实例使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public async Task StartAsync()
        {
            await _appInfoManager.LoadAsync();
            await _processFilter.LoadAsync();
            await _usageRecordManager.LoadAsync();

            _reminderManager.SendOverdueDaily();

            _hookHandle = SafeEventHookHandle.SetEventHook(NativeApi.EVENT_SYSTEM_FOREGROUND, _delegate);
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
                _hookHandle?.Dispose();
            }
        }

        /// <summary>
        /// 获取 <c>ApplicationFrameHost</c> 下的 UWP 进程。
        /// </summary>
        /// <param name="windowHandle"><c>ApplicationFrameHost</c> 窗口的句柄。</param>
        /// <returns>获取到的 UWP 进程，若获取失败则返回 <see langword="null"/>。</returns>
        private static Process? GetUwpProcessOrNull(nint windowHandle)
        {
            if (!NativeApi.TryGetChildWindowHandle(
                windowHandle, out nint childHandle, "Windows.UI.Core.CoreWindow"))
            {
                LogSystem.WriteLog(LogLevel.Warning,
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
                LogSystem.WriteLog(LogLevel.Warning,
                    $"获取 explorer 窗口 [Handle={windowHandle}] 的类名失败" +
                    $"（{Marshal.GetLastPInvokeError()}）：{Marshal.GetLastPInvokeErrorMessage()}");
                return false;
            }

            LogSystem.WriteLog(LogLevel.Info, $"explorer 窗口的类名为{className}。");
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
                LogSystem.WriteLog(LogLevel.Error,
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

            bool saveSuccessfully = false;
            if (_lastProcessName != null)
            {
                saveSuccessfully = new SafeCaller()
                {
                    LogMessage = $"更新使用时间失败，传入进程为 {_lastProcessName}。",
                    RemindingMsgResKey = "ECanNotRecordTime",
                }.CallMethodR(() => UpdateUsageTime(_lastProcessName, eventIntervalTime));
                _usageRecordManager.RequestStore();
                //WriteLog(LogLevel.Debug, $"当前总使用时长：{TotalUsageTime}，连续使用时长：{_continuousUsageTime}。");
            }

            if (TryGetValidProcess(hwnd, out Process? process))
            {
                string processName = process.ProcessName;

                _reminderManager.UpdateDailyDueTime();
                _reminderManager.UpdateSessionDueTime();

                _ = Task.Run(() => _ = new SafeCaller()
                {
                    LogMessage = $"保存 {processName} 的进程信息失败。",
                    ShouldRemind = false,
                }.CallMethodR(() => SaveProcessInfo(hwnd, process)));

                //stopwatch.Stop();
                LogSystem.WriteLog(LogLevel.Debug, $"上次记录：{_lastProcessName ?? "null"}，" +
                    $"本次记录：{processName}。");/*，用时：{stopwatch.Elapsed}*/

                _lastProcessName = processName;
                _lastEventTime = dwmsEventTime;
            }
            else
            {
                _reminderManager.StopAll();

                if (_lastProcessName == null && eventIntervalTime >= _settings.IdleThreshold)
                {
                    _usageRecordManager.Record.SessionDuration = 0;
                }
                else
                {
                    _lastProcessName = null;
                    _lastEventTime = dwmsEventTime;
                }

                //stopwatch.Stop();
                LogSystem.WriteLog(LogLevel.Debug, $"上次记录：{_lastProcessName ?? "null"}，本次记录：null。");
                //，用时：{stopwatch.Elapsed}
            }
        }

        /// <summary>
        /// 保存指定进程的信息。
        /// </summary>
        /// <param name="windowHandle">窗口句柄。</param>
        /// <param name="process">     进程的 <see cref="Process"/> 组件。</param>
        private void SaveProcessInfo(nint windowHandle, Process process)
        {
            string processName = process.ProcessName;
            if (_processFilter.IsTimeOnlyProcess(processName) ||
                _appInfoManager.AppInfoMap.ContainsKey(processName) ||
                !_recordingProcessName.TryAdd(processName, 0))
            {
                return;
            }

            try
            {
                _appInfoManager.CacheAppInfo(windowHandle, process);
                _appInfoManager.RequestStore();
            }
            catch (Exception)
            {
                throw;
            }
            finally
            {
                _ = _recordingProcessName.TryRemove(processName, out _);
                process.Dispose();
            }
        }

        /// <summary>
        /// 尝试获取需要记录的进程。
        /// </summary>
        /// <param name="windowHandle">进程相关的窗口句柄。</param>
        /// <param name="process">     获取到的进程。</param>
        /// <returns>指示是否获取成功。</returns>
        private bool TryGetValidProcess(nint windowHandle, [MaybeNullWhen(false)] out Process process)
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
                    uncertainProcess.Dispose();
                }
            }
            else
            {
                process = uncertainProcess;
            }

            if (_processFilter.IsIgnoredProcess(process.ProcessName))
            {
                process.Dispose();
                process = null;
                return false;
            }

            return true;
        }

        /// <summary>
        /// 更新总使用时间、连续使用时间和进程的总使用记录。
        /// </summary>
        /// <param name="processName">     要记录的进程名称。</param>
        /// <param name="processUsageTime">进程的使用时间。</param>
        private void UpdateUsageTime(string processName, uint processUsageTime)
        {
            _usageRecordManager.Record.DailyDuration += processUsageTime;
            _usageRecordManager.Record.SessionDuration += processUsageTime;
            if (!_processFilter.IsTimeOnlyProcess(processName))
            {
                uint appUsageTime =
                    _usageRecordManager.Record.ProcessUsageRecords.TryGetValue(processName, out uint pastUsageTime)
                    ? pastUsageTime + processUsageTime
                    : processUsageTime;
                _usageRecordManager.Record.ProcessUsageRecords[processName] = appUsageTime;
            }
        }
    }
}
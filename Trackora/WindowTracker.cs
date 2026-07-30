using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.Streams;
using static Zscno.Trackora.App;
using static Zscno.Trackora.LogSystem;

namespace Zscno.Trackora
{
    internal class WindowTracker
    {
        /// <summary>
        /// 当无法获取到进程图标时使用的默认图标。
        /// </summary>
        private static readonly string _defaultIconUri = "ms-appx:///Icons/Default.png";

        /// <summary>
        /// Json 序列化时使用的配置。
        /// </summary>
        private static JsonWriterOptions _jsonOptions;

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
        /// 用于发送总使用时间的计时器。
        /// </summary>
        private readonly Timer _totalReminderTimer;

        /// <summary>
        /// 用于保存使用记录的计时器。
        /// </summary>
        private readonly Timer _savingRecordTimer;

        /// <summary>
        /// 连续使用时长。
        /// </summary>
        private uint _continuousUsageTime;

        /// <summary>
        /// 当前正在记录信息的进程的名称。
        /// </summary>
        private string _currentRecordProcessName = string.Empty;

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
#if DEBUG
            _jsonOptions.Indented = true;
#endif

            if (TimeSpan.FromMilliseconds(UsageRecordManager.Record.TotalUsageTime)
                >= (TimeSpan)LocalSettings["TotalUsedRemindTime"] && !IsTotalUsageReminderShown)
            {
                SendDueTotalReminder(null);
            }
            }

        /// <summary>
        /// 获取本地化时间 / 时长。
        /// </summary>
        /// <param name="time">一个时间 / 时长。</param>
        /// <param name="isUsedByReminder">指示是否使用用于触发提醒的总时长。</param>
        /// <returns>本地化时间 / 时长字符串。</returns>
        public static string GetLocalTime(TimeSpan time, bool isUsedByReminder = false)
        {
            TimeSpan realTime = // TODO: 接收 uint  参数。
                isUsedByReminder ? TimeSpan.FromMilliseconds(UsageRecordManager.Record.TotalUsageTime) : time;

            string result;
            switch (realTime)
            {
                case { Days: 0, Hours: 0, Minutes: 0 }:
                    result = "< 1" + Loader.GetString("Minute");
                    break;

                case { Days: 0, Hours: 0 }:
                    result = realTime.Minutes + Loader.GetString("Minute");
                    break;

                default:
                    if (realTime.Days == 0)
                    {
                        result = realTime.Hours + Loader.GetString("Hour")
                                                + realTime.Minutes + Loader.GetString("Minute");
                    }
                    else
                    {
                        result = realTime.Days + Loader.GetString("Day")
                                               + realTime.Hours + Loader.GetString("Hour")
                                               + realTime.Minutes + Loader.GetString("Minute");
                    }

                    break;
            }
            return result;
        }

        /// <summary>
        /// 获取进程名称、图标及使用的时长。
        /// </summary>
        /// <param name="count">需要获取的数量（以使用时长正序排列）。</param>
        /// <returns>使用时长最长的 <paramref name="count"/> 个进程名称、图标和时长。</returns>
        public static List<ProcessInfo> GetProcessesInfo(int count)
        {
            string[] processNames = UsageRecordManager.Record.ProcessUsageRecords
                .OrderByDescending(x => x.Value)
                .Take(count)
                .Select(x => x.Key)
                .ToArray();

            string processesListText;
            try
            {
                processesListText = File.Exists(InfoFilePath) ? File.ReadAllText(InfoFilePath) : string.Empty;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取记录文件[{InfoFilePath}]的文本失败。", ex);
            }

            List<ProcessInfo> processesInfo = [];
            bool isEmpty = string.IsNullOrWhiteSpace(processesListText);
            if (isEmpty)
            {
                WriteLog(LogLevel.Warning, $"程序尚未开始监测和记录（json 文件中无内容） [Path={InfoFilePath}] 。");
            }

            List<ProcessInfo> list = GetProcessesInfoFromJson();
            Dictionary<string, ProcessInfo> processesDict =
                list.Count == 0 ? [] : list.ToDictionary(value => value.ProcessName);

            foreach (string name in processNames)
            {
                ProcessInfo? info;

                if (isEmpty)
                {
                    info = GetDefaultInfo(name);
                }
                else if (!processesDict.TryGetValue(name, out info))
                {
                    WriteLog(LogLevel.Warning, $"在记录文件 [Path={InfoFilePath}] 中未找到进程 {name} 的信息。");
                    info = GetDefaultInfo(name);
                }
                info.UsageTime = GetLocalTime(
                    TimeSpan.FromMilliseconds(UsageRecordManager.Record.ProcessUsageRecords[name]));
                processesInfo.Add(info);
            }
            return processesInfo;
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
        /// 裁剪图标。
        /// </summary>
        /// <param name="decoder">图标的 <see cref="BitmapDecoder"/> 实例。</param>
        /// <param name="x">要裁剪区域左上角的 X 坐标。</param>
        /// <param name="y">要裁剪区域左上角的 Y 坐标。</param>
        /// <param name="w">要裁剪区域的宽度。</param>
        /// <param name="h">要裁剪区域的高度。</param>
        /// <returns>裁剪后的图标所在的位置归零的流。</returns>
        private static async Task<InMemoryRandomAccessStream> CropIcon
            (BitmapDecoder decoder, uint x, uint y, uint w, uint h)
        {
            InMemoryRandomAccessStream croppedStream = new();
            BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(croppedStream, decoder);
            encoder.BitmapTransform.Bounds = new BitmapBounds
            {
                X = x,
                Y = y,
                Width = w,
                Height = h,
            };
            await encoder.FlushAsync();
            croppedStream.Seek(0);
            return croppedStream;
        }

        /// <summary>
        /// 从下向上遍历每一行，找到最下边有内容的像素行。
        /// </summary>
        /// <param name="pixels">图标的像素数据。</param>
        /// <param name="width">图标的宽度。</param>
        /// <param name="height">图标的高度。</param>
        /// <param name="minX">图标内容最左侧的 X 坐标。</param>
        /// <param name="maxX">图像内容最右侧的 X 坐标。</param>
        /// <returns>图像内容最低的 Y 坐标。未找到则返回 0 。</returns>
        private static uint ForeachFromBottomToTop(byte[] pixels, uint width, uint height,
            uint minX, uint maxX)
        {
            for (uint row = height - 1; row >= 0; row--)
            {
                for (uint pixel = minX; pixel <= maxX; pixel++)
                {
                    if (pixels[(row * width + pixel) * 4 + 3] > 0)
                    {
                        return row;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// 从左向右遍历每一列，找到最左边有内容的像素列。
        /// </summary>
        /// <param name="pixels">图标的像素数据。</param>
        /// <param name="width">图标的宽度。</param>
        /// <param name="height">图标的高度。</param>
        /// <returns>图像内容最左侧的 X 坐标。未找到则返回 0 。</returns>
        private static uint ForeachFromLeftToRight(byte[] pixels, uint width, uint height)
        {
            for (uint column = 0; column < width; column++)
            {
                for (uint pixel = 0; pixel < height; pixel++)
                {
                    if (pixels[(pixel * width + column) * 4 + 3] > 0)
                    {
                        return column;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// 从右向左遍历每一列，找到最右边有内容的像素列。
        /// </summary>
        /// <param name="pixels">图标的像素数据。</param>
        /// <param name="width">图标的宽度。</param>
        /// <param name="height">图标的高度。</param>
        /// <returns>图像内容最右侧的 X 坐标。未找到则返回 0 。</returns>
        private static uint ForeachFromRightToLeft(byte[] pixels, uint width, uint height)
        {
            for (uint column = width - 1; column >= 0; column--)
            {
                for (uint pixel = 0; pixel < height; pixel++)
                {
                    if (pixels[(pixel * width + column) * 4 + 3] > 0)
                    {
                        return column;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// 从上向下遍历每一行，找到最上边有内容的像素行。
        /// </summary>
        /// <param name="pixels">图标的像素数据。</param>
        /// <param name="width">图标的宽度。</param>
        /// <param name="height">图标的高度。</param>
        /// <param name="minX">图标内容最左侧的 X 坐标。</param>
        /// <param name="maxX">图像内容最右侧的 X 坐标。</param>
        /// <returns>图像内容最高的 Y 坐标。未找到则返回 0 。</returns>
        private static uint ForeachFromTopToBottom(byte[] pixels, uint width, uint height,
            uint minX, uint maxX)
        {
            for (uint row = 0; row < height; row++)
            {
                for (uint pixel = minX; pixel <= maxX; pixel++)
                {
                    if (pixels[(row * width + pixel) * 4 + 3] > 0)
                    {
                        return row;
                    }
                }
            }
            return 0;
        }

        /// <summary>
        /// 获取默认的进程信息（当无法获取到进程信息时使用）。
        /// </summary>
        /// <param name="process">要获取的进程。</param>
        /// <returns>默认的进程信息。</returns>
        private static ProcessInfo GetDefaultInfo(Process process)
        {
            string title = process.MainWindowTitle;
            return new ProcessInfo
            {
                ProcessName = process.ProcessName,
                DisplayName = GetDisplayName(process.ProcessName, title),
                IconUri = _defaultIconUri,
            };
        }

        /// <summary>
        /// 获取默认的进程信息（当 json 文件中没有进程信息时使用）。
        /// </summary>
        /// <param name="processName">要获取的进程的名称。</param>
        /// <returns>默认的进程信息。</returns>
        private static ProcessInfo GetDefaultInfo(string processName)
        {
            return new ProcessInfo
            {
                ProcessName = processName,
                DisplayName = processName,
                IconUri = _defaultIconUri,
            };
        }

        /// <summary>
        /// 获取进程的显示名称（保证获取到的名称一定不是空白）。
        /// </summary>
        /// <param name="processName">进程名称。</param>
        /// <param name="windowTitle">窗口标题。</param>
        /// <param name="friendlyName">友好名称。</param>
        /// <returns>进程的显示名称。</returns>
        private static string GetDisplayName(string processName,
            string windowTitle, string? friendlyName = null)
        {
            if (!string.IsNullOrWhiteSpace(friendlyName))
            {
                return friendlyName;
            }
            return string.IsNullOrWhiteSpace(windowTitle) ? processName : windowTitle;
        }

        /// <summary>
        /// 获取图标内容的实际大小。
        /// </summary>
        /// <param name="pixels">图标的像素数据。</param>
        /// <param name="width">图标的宽度。</param>
        /// <param name="height">图标的高度。</param>
        /// <returns>图标内容左上角的坐标、宽度和高度。</returns>
        private static (uint RealX, uint RealY, uint RealWidth, uint RealHeight) GetIconContentSize(
            byte[] pixels, uint width, uint height)
        {
            uint minX = ForeachFromLeftToRight(pixels, width, height);
            uint maxX = ForeachFromRightToLeft(pixels, width, height);
            uint minY = ForeachFromTopToBottom(pixels, width, height, minX, maxX);
            uint maxY = ForeachFromBottomToTop(pixels, width, height, minX, maxX);

            return (minX, minY, maxX - minX + 1, maxY - minY + 1);
        }

        /// <summary>
        /// 获取有包标识的应用的图标并保存到缓存文件夹中。
        /// </summary>
        /// <param name="iconPath">图标的路径。</param>
        /// <param name="name">进程名称。</param>
        /// <returns>图标的 Uri 。</returns>
        private static async Task<string> GetIconForPackage(string iconPath, string name)
        {
            try
            {
                StorageFile iconFile = await StorageFile.GetFileFromPathAsync(iconPath);
                IRandomAccessStreamWithContentType iconStream = await iconFile.OpenReadAsync();
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(iconStream);
                PixelDataProvider pixelData = await decoder.GetPixelDataAsync();
                byte[] pixels = pixelData.DetachPixelData();
                uint width = decoder.PixelWidth;
                (uint realX, uint realY, uint realWidth, uint realHeight) = GetIconContentSize(
                    pixels, width, decoder.PixelHeight);

                if (realWidth < 32 && realHeight < 32)
                {
                    realWidth = realHeight = 32;
                    realX = Round((double)(width - realWidth) / 2);
                    realY = Round((double)(width - realHeight) / 2);
                }

                InMemoryRandomAccessStream croppedStream = await CropIcon(decoder,
                    realX, realY, realWidth, realHeight);
                return await SaveIcon(name, croppedStream);
            }
            catch (Exception ex)
            {
                throw new Exception($"保存进程 {name} 的图标失败。", ex);
            }
        }

        /// <summary>
        /// 获取 Win32 应用的图标并保存到缓存文件夹中。
        /// </summary>
        /// <param name="name">进程名称。</param>
        /// <param name="path">应用主模块路径。</param>
        /// <returns>图标的 Uri 。</returns>
        private static string GetIconForWin32(string name, string path)
        {
            try
            {
                Icon preIcon = Icon.ExtractAssociatedIcon(path)!;
                Icon icon = new(preIcon, 32, 32);
                preIcon.Dispose();

                string iconPath = Path.Combine(LocalCachePath,
                    "Icons", $"{name}.png");
                using FileStream iconStream = new(iconPath, FileMode.Create,
                    FileAccess.ReadWrite, FileShare.None);
                icon.ToBitmap().Save(iconStream, ImageFormat.Png);
                icon.Dispose();

                return new Uri(iconPath).ToString();
            }
            catch (Exception ex)
            {
                throw new Exception($"保存进程 {name} 的图标失败。", ex);
            }
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
        /// 获取进程 <paramref name="name"/> 的包信息。
        /// </summary>
        /// <param name="packageFullName">进程 <paramref name="name"/> 的包全名。</param>
        /// <param name="name">进程名称。</param>
        /// <returns>进程 <paramref name="name"/> 的包信息。</returns>
        private static Package GetPackageInfo(string packageFullName, string name)
        {
            try
            {
                Dictionary<string, Package> packages = new PackageManager()
                    .FindPackagesForUser(string.Empty)
                    .OrderByDescending(pkg => pkg.Id.FullName.Length)
                    .ToDictionary(pack => pack.Id.FullName);
                _ = packages.TryGetValue(packageFullName, out Package? package);

                return package ?? throw new Exception($"出于未知原因，未找到进程 {name} 的包信息。");
            }
            catch (Exception ex)
            {
                throw new Exception($"获取进程 {name} 的包信息失败。", ex);
            }
        }

        /// <summary>
        /// 从 Json 文件中获取进程信息列表。如果没有内容创建新列表。
        /// </summary>
        /// <returns>进程信息列表。如果没有内容创建新列表。</returns>
        private static List<ProcessInfo> GetProcessesInfoFromJson()
        {
            try
            {
                using FileStream textStream =
                    new(InfoFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

                return textStream.Length > 0 ?
                    (JsonSerializer.Deserialize(textStream, SourceGenerationContext.Default.ListProcessInfo)
                    ?? []) : [];
            }
            catch (Exception ex)
            {
                throw new Exception($"从 Json 文件[{InfoFilePath}]获取进程信息列表失败。", ex);
            }
        }

        /// <summary>
        /// 获取有包标识的应用的进程信息。
        /// </summary>
        /// <param name="process">进程实例。</param>
        /// <param name="name">进程名称。</param>
        /// <param name="packageFullNameLength">包全名长度。</param>
        /// <returns>图标的 Uri 和显示名称。</returns>
        private static async Task<(string IconUri, string DisplayName)> GetProcessInfoForPackage
            (Process process, string name, uint packageFullNameLength)
        {
            StringBuilder packageFullName = new((int)packageFullNameLength);
            long result = NativeApi.GetPackageFullName(
                process.Handle, ref packageFullNameLength, packageFullName);
            if (result != NativeApi.ERROR_SUCCESS)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error(), $"获取进程 {name} 的包全名失败。");
            }

            Package package = GetPackageInfo(packageFullName.ToString(), name);

            return (await GetIconForPackage(package.Logo.LocalPath, name), package.DisplayName);
        }

        /// <summary>
        /// 获取 Win32 应用的进程信息。
        /// </summary>
        /// <param name="process">进程实例。</param>
        /// <param name="name">进程名称。</param>
        /// <returns>图标的 Uri 和显示名称。</returns>
        private static (string, string) GetProcessInfoForWin32(Process process, string name)
        {
            ProcessModule mainModule = process.MainModule!;
            string path = GetProcessModulePath(mainModule, name);
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                throw new FileNotFoundException($"进程 {name} 的主模块路径 {path} 无效。");
            }

            return (GetIconForWin32(name, path),
                GetDisplayName(process.ProcessName, process.MainWindowTitle, mainModule.FileVersionInfo.FileDescription));
        }

        /// <summary>
        /// 获取进程模块的路径。
        /// </summary>
        /// <param name="module">要获取的进程模块。</param>
        /// <param name="name">进程名称。</param>
        /// <returns>进程模块的路径。</returns>
        private static string GetProcessModulePath(ProcessModule module, string name)
        {
            try
            {
                return module.FileName;
            }
            catch (Exception ex)
            {
                throw new Exception($"获取进程 {name} 的路径失败。", ex);
            }
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
        /// 舍入 <paramref name="value"/> 为 <see langword="uint"/> 整数。遵循一般的四舍五入规则。当 <paramref
        /// name="value"/> 的小数部分为 0.5 时，向下舍入。
        /// </summary>
        /// <param name="value">要操作的 <see langword="double"/> 值。</param>
        /// <returns>舍入后的 <see langword="uint"/> 整数。</returns>
        private static uint Round(double value)
        {
            uint intValue = (uint)value;
            return value - intValue < 0.5 ? intValue : intValue - 1;
        }

        /// <summary>
        /// 保存图标到缓存文件夹。
        /// </summary>
        /// <param name="name">进程名称。</param>
        /// <param name="stream">图标所在的流。</param>
        /// <returns>图标的 Uri 。</returns>
        private static async Task<string> SaveIcon(string name, InMemoryRandomAccessStream stream)
        {
            StorageFolder iconFolder = await StorageFolder.GetFolderFromPathAsync(
                Path.Combine(LocalCachePath, "Icons"));
            StorageFile iconFile = await iconFolder.CreateFileAsync(
                $"{name}.png", CreationCollisionOption.ReplaceExisting);
            _ = await RandomAccessStream.CopyAndCloseAsync(
                stream, await iconFile.OpenAsync(FileAccessMode.ReadWrite));
            return new Uri(Path.Combine(LocalCachePath,
                "Icons", $"{name}.png")).ToString();
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
        /// 将提供的值转换为 Json <see langword="string"/> 。
        /// </summary>
        /// <typeparam name="T">要序列化的值的类型。</typeparam>
        /// <param name="value">要转换的值。</param>
        /// <param name="info">要转换的类型的元数据。</param>
        /// <returns>值的 <see langword="string"/> 表示形式。</returns>
        private static string SerializeJson<T>(T value, JsonTypeInfo<T> info)
        {
            using MemoryStream stream = new();
            using Utf8JsonWriter writer = new(stream, _jsonOptions);
            JsonSerializer.Serialize(writer, value, info);
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
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
        /// 检查进程是否可用（不为 null 且不是正在记录的进程）。
        /// </summary>
        /// <param name="process">要检查的进程。</param>
        /// <param name="copyProcess">如果进程可用，该参数是进程的副本；否则是一个新实例。</param>
        /// <returns>指示进程是否可用。</returns>
        private bool CheckProcessUsability(Process? process, out Process copyProcess)
        {
            if (process is null)
            {
                WriteLog(LogLevel.Warning, "要记录的进程为 null ，将跳过。");
                copyProcess = new Process();
                return false;
            }

            if (process.ProcessName == _currentRecordProcessName)
            {
                WriteLog(LogLevel.Info, $"已开始记录进程 {process.ProcessName} ，将跳过。");
                copyProcess = new Process();
                return false;
            }

            _currentRecordProcessName = process.ProcessName;
            copyProcess = process;
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
                    WriteLog(LogLevel.Error, "启动计时器失败，可能无法发送提醒。");
                    // TODO: 使用事件处理失败情况。
                }
                if (_lastProcessName == null &&
                    TimeSpan.FromMilliseconds(dwmsEventTime - _lastChangedTime) >=
                    ((TimeSpan)LocalSettings["ContinuousUsedResetTime"])) // TODO: 使用 uint 存在设置里。
                {
                    _continuousUsageTime = 0;
                }
                else
                {
                    _lastProcessName = null;
                    _lastChangedTime = dwmsEventTime;
                }

                //stopwatch.Stop();
                WriteLog(LogLevel.Debug, $"上次记录的进程为 {_lastProcessName ?? "null"}" +
                    $"，本次未记录任何进程。");/*，用时：{stopwatch.Elapsed}*/
            }
            else
            {
                if (isSuccessful)
                {
                    StartReminderTimers();
                }

                _lastProcessName = uwpProcess?.ProcessName ?? process.ProcessName;
                _lastChangedTime = dwmsEventTime;
                if (!GetNoInfoArr().Contains(process.ProcessName))
                {
                    _ = Task.Run(RecordProcessInfo);
                }

                //stopwatch.Stop();
                WriteLog(LogLevel.Debug, $"上次记录的进程为 {_lastProcessName ?? "null"}，" +
                    $"本次记录的进程为 {process.ProcessName}。");/*，用时：{stopwatch.Elapsed}*/
            }
        }

        /// <summary>
        /// 在文件中记录指定的进程信息。
        /// </summary>
        /// <param name="info">指定的进程信息。</param>
        /// <param name="allProcessInfos">所有已记录的进程信息。</param>
        private async Task RecordInfoIntoFile(ProcessInfo info, List<ProcessInfo> allProcessInfos)
        {
            allProcessInfos.Add(info);
            bool isSuccessful = new SafeCaller()
            {
                LogMessage = $"写入记录文件[{InfoFilePath}]失败。",
                RemindingMsgResKey = "ECanNotWriteInfo",
            }.CallMethodR(() => File.WriteAllText(InfoFilePath, SerializeJson(
                allProcessInfos,
                SourceGenerationContext.Default.ListProcessInfo)));
            _currentRecordProcessName = string.Empty;
            if (isSuccessful)
            {
                WriteLog(LogLevel.Info, $"已记录进程 {info.ProcessName} 的信息。");
            }
        }

        /// <summary>
        /// 记录进程信息到 JSON 文件中。
        /// </summary>
        private async Task RecordProcessInfo()
        {
            if (!CheckProcessUsability(_lastProcessName, out Process process))
            {
                return;
            }

            string name = process.ProcessName;

            (bool isSuccessful, List<ProcessInfo>? list) = new SafeCaller()
            {
                RemindingMsgResKey = "ECanNotGetInfo",
            }.CallMethodWithReturnR(GetProcessesInfoFromJson);
            if (!isSuccessful || list is null || list.Any(info => info.ProcessName == process.ProcessName))
            {
                _currentRecordProcessName = string.Empty;
                return;
            }

            uint packageFullNameLength = 0;
            (isSuccessful, long result) = new SafeCaller()
            {
                LogMessage = $"获取进程 {name} 的包全名长度失败。",
                ShouldRemind = false,
            }
            .CallMethodWithReturnR(() => NativeApi.GetPackageFullName
            (process.Handle, ref packageFullNameLength, null!));

            if (!isSuccessful)
            {
                await RecordInfoIntoFile(GetDefaultInfo(process), list);
                return;
            }

            (string IconUri, string FriendlyName) infoTuple;
            switch (result)
            {
                case NativeApi.APPMODEL_ERROR_NO_PACKAGE:
                    (isSuccessful, infoTuple) = new SafeCaller() { ShouldRemind = false }
                        .CallMethodWithReturnR(() => GetProcessInfoForWin32(process, name));
                    if (!isSuccessful)
                    {
                        await RecordInfoIntoFile(GetDefaultInfo(process), list);
                        return;
                    }
                    break;

                case NativeApi.ERROR_INSUFFICIENT_BUFFER:
                    (isSuccessful, Task<(string, string)>? infoTask) = new SafeCaller() { ShouldRemind = false }
                        .CallMethodWithReturnR
                        (() => GetProcessInfoForPackage(process, name, packageFullNameLength));
                    if (!isSuccessful || infoTask is null)
                    {
                        await RecordInfoIntoFile(GetDefaultInfo(process), list);
                        return;
                    }
                    infoTuple = await infoTask;
                    break;

                default:
                    WriteLog(LogLevel.Error,
                        $"获取进程 {name} 的包全名失败，错误代码：{Marshal.GetLastWin32Error()}。");
                    await RecordInfoIntoFile(GetDefaultInfo(process), list);
                    return;
            }

            ProcessInfo info = new()
            {
                ProcessName = name,
                DisplayName = GetDisplayName(name, process.MainWindowTitle, infoTuple.FriendlyName),
                IconUri =
                    string.IsNullOrWhiteSpace(infoTuple.IconUri) ? _defaultIconUri : infoTuple.IconUri,
            };
            await RecordInfoIntoFile(info, list);
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
            uint continuousUsageReminderTime =
                (uint)((TimeSpan)LocalSettings["ContinuousUsedRemindTime"]).TotalMilliseconds;
            uint continuousRemainingTime = _continuousUsageTime >= continuousUsageReminderTime ?
                0 : continuousUsageReminderTime - _continuousUsageTime;
            if (_continuousReminderTimer.Change(continuousRemainingTime, continuousUsageReminderTime))
        {
                WriteLog(LogLevel.Debug, $"连续使用时长提醒将在 {continuousRemainingTime / 1000d : f2} 秒后发送。");
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
            uint totalUsgaeReminderTime =
                (uint)((TimeSpan)LocalSettings["TotalUsedRemindTime"]).TotalMilliseconds;
            uint totalRemainingTime = UsageRecordManager.Record.TotalUsageTime >= totalUsgaeReminderTime ?
                0 : totalUsgaeReminderTime - UsageRecordManager.Record.TotalUsageTime;
            if (_totalReminderTimer.Change(totalRemainingTime, Timeout.Infinite))
            {
                WriteLog(LogLevel.Debug, $"总使用时长提醒将在 {totalRemainingTime / 1000d : f2} 秒后发送。");
            }
            else
            {
                WriteLog(LogLevel.Error, "启动总使用时长提醒计时器失败，可能无法发送提醒。");
                // TODO: 使用事件处理失败情况。
            }
        }
    }
}
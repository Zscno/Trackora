using Microsoft.Windows.Storage;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Zscno.Trackora
{
    /// <summary>
    /// 为获取和保存进程信息提供相关操作。
    /// </summary>
    internal partial class ProcessInfoManager
    {
        /// <summary>
        /// 默认图标路径。
        /// </summary>
        private static readonly string _defaultIconPath;

        /// <summary>
        /// 图标文件所在文件夹的路径。
        /// </summary>
        private static readonly string _iconFolderPath;

        /// <summary>
        /// 进程信息文件路径。
        /// </summary>
        private static readonly string _processInfoFilePath;

        /// <summary>
        /// 进程信息映射表，键为进程名。
        /// </summary>
        internal static ConcurrentDictionary<string, ProcessInfo> ProcessInfoMap { get; private set; } = new();

        static ProcessInfoManager()
        {
            string localCachePath = ApplicationData.GetDefault().LocalCachePath;
            _iconFolderPath = Path.Combine(localCachePath, "Icons");
            _defaultIconPath = Path.Combine(_iconFolderPath, "Default.png");
            _processInfoFilePath = Path.Combine(localCachePath, "ProcessInfo.json");
        }

        /// <summary>
        /// 获取进程图标的 URI。
        /// </summary>
        /// <param name="processName">进程名称。</param>
        /// <returns>图标的 URI。</returns>
        internal static string GetProcessIconUri(string processName)
        {
            string iconFilePath = Path.Combine(_iconFolderPath, $"{processName}.png");
            if (File.Exists(iconFilePath))
            {
                return new Uri(iconFilePath).ToString();
            }

            if (!File.Exists(_defaultIconPath))
            {
                SaveIconAsPng("Default", SystemIcons.GetStockIcon(StockIconId.Application));
            }
            return new Uri(_defaultIconPath).ToString();
        }

        /// <summary>
        /// 获取进程信息。
        /// </summary>
        /// <param name="windowHandle">进程关联的窗口句柄。</param>
        /// <param name="process">     进程的 <see cref="Process"/> 组件。</param>
        /// <returns>获取到的进程信息。</returns>
        internal static ProcessInfo GetProcessInfo(nint windowHandle, Process process)
        {
            Package? package;
            string processName = process.ProcessName;

            nint processHandle =
                NativeApi.OpenProcess(NativeApi.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)process.Id);
            if (processHandle == nint.Zero)
            {
                package = null;
                LogSystem.WriteLog(LogLevel.Warning,
                    $"打开 {processName} 进程失败（{Marshal.GetLastPInvokeError()}）：" +
                    $"{Marshal.GetLastPInvokeErrorMessage()}");
            }
            else
            {
                package = GetPackage(processHandle);
            }
            if (!NativeApi.CloseHandle(processHandle))
            {
                LogSystem.WriteLog(LogLevel.Warning,
                    $"关闭 {processName} 进程句柄失败（{Marshal.GetLastPInvokeError()}）：" +
                    $"{Marshal.GetLastPInvokeErrorMessage()}");
            }

            Lazy<ProcessModule?> processMainModule = new(() =>
            {
                try
                {
                    return process.MainModule;
                }
                catch (Exception ex)
                {
                    LogSystem.WriteLog(LogLevel.Warning, $"获取 {processName} 主模块失败。{ex}");
                    return null;
                }
            });

            try
            {
                SaveProcessIcon(processName, package, windowHandle, processMainModule);
            }
            catch (Exception ex)
            {
                LogSystem.WriteLog(LogLevel.Warning, $"保存 {processName} 图标失败。{ex}");
            }

            string displayName = GetProcessDisplayName(package, processMainModule, process);

            return new ProcessInfo(displayName);
        }

        /// <summary>
        /// 确保存放图标的文件夹和默认图标存在并从文件中读取的进程信息。
        /// </summary>
        internal static void Initialize()
        {
            ProcessInfoMap = Json.ReadJsonFile(
                _processInfoFilePath, SourceGenerationContext.Default.ConcurrentDictionaryStringProcessInfo)
                ?? new();
        }

        /// <summary>
        /// 保存进程信息映射表。
        /// </summary>
        internal static string SaveProcessInfoMap()
        {
            return Json.WriteJsonFile(_processInfoFilePath, ProcessInfoMap, SourceGenerationContext.Default.ConcurrentDictionaryStringProcessInfo);
        }

        /// <summary>
        /// 从应用程序清单中获取图标基 URI。
        /// </summary>
        /// <param name="manifestPath">应用程序清单路径。</param>
        /// <returns>获取到的图标的基 URI。</returns>
        private static string? GetIconBaseUriFromManifest(string manifestPath)
        {
            XmlDocument doc = new();
            doc.Load(manifestPath);

            XmlNamespaceManager nsmgr = new(doc.NameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            nsmgr.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");

            string xpath = "/ns:Package/ns:Applications/ns:Application/uap:VisualElements";
            XmlNode? node = doc.SelectSingleNode(xpath, nsmgr);
            return node?.Attributes?["Square44x44Logo"]?.Value;
        }

        /// <summary>
        /// 获取图标路径。方法将优先选取 <c>targetsize</c> 图标，选取到的图标大小不会小于 <paramref name="size"/> 或 <paramref name="scale"/> 指定的值。
        /// </summary>
        /// <param name="iconBaseUri">图标的基 URI。</param>
        /// <param name="basePath">   <paramref name="iconBaseUri"/> 的基路径。</param>
        /// <param name="scale">      指定最小的可选取缩放比例。</param>
        /// <param name="size">       指定最小的可选取图标大小。</param>
        /// <returns>获取到的图标路径。</returns>
        private static string? GetIconPath(string iconBaseUri, string basePath, int scale, int size)
        {
            string iconBasePath = Path.Combine(basePath, iconBaseUri);
            string iconDir = Path.GetDirectoryName(iconBasePath)!;
            string iconName = Path.GetFileNameWithoutExtension(iconBasePath);
            Regex targetSizeRegex = GetTargetSizeRegex();
            string? iconPath = null;

            IEnumerable<string> targetSizeIcons = Directory.EnumerateFiles(iconDir, $"{iconName}.targetsize-*.*")
                .Where(f => targetSizeRegex.IsMatch(f));
            int maxSize = int.MaxValue;
            foreach (string targetSizeIcon in targetSizeIcons)
            {
                if (TryExtractNumber(Path.GetFileName(targetSizeIcon),
                                     $"{iconName}.targetsize-".Length,
                                     out int currentSize) &&
                    size <= currentSize && currentSize <= maxSize)
                {
                    iconPath = targetSizeIcon;
                    maxSize = currentSize;
                }
            }
            if (iconPath is not null)
            {
                return iconPath;
            }

            IEnumerable<string> scaleIcons = Directory.EnumerateFiles(iconDir, $"{iconName}.scale-???.*");
            int maxScale = int.MaxValue;
            foreach (string scaleIcon in scaleIcons)
            {
                if (int.TryParse(Path.GetFileName(scaleIcon).AsSpan($"{iconName}.scale-".Length, 3), out int currentScale) &&
                    scale <= currentScale && currentScale <= maxScale)
                {
                    iconPath = scaleIcon;
                    maxScale = currentScale;
                }
            }
            return iconPath;
        }

        /// <summary>
        /// 获取进程的包信息。
        /// </summary>
        /// <param name="processHandle">进程句柄。</param>
        /// <returns>进程所属的包，获取失败则为 <see langword="null"/>。</returns>
        private static Package? GetPackage(nint processHandle)
        {
            uint pkgFullNameLength = 0;
            long result = NativeApi.GetPackageFullName(processHandle, ref pkgFullNameLength, null!);
            switch (result)
            {
                case NativeApi.ERROR_INSUFFICIENT_BUFFER:
                    break;

                case NativeApi.APPMODEL_ERROR_NO_PACKAGE:
                    return null;

                default:
                    LogSystem.WriteLog(
                        LogLevel.Warning,
                        $"获取进程 [Handle={processHandle}] 的包全名失败（{result}）：" +
                        Marshal.GetLastPInvokeErrorMessage());
                    return null;
            }

            StringBuilder fullNameBuilder = new((int)pkgFullNameLength);
            result = NativeApi.GetPackageFullName(processHandle, ref pkgFullNameLength, fullNameBuilder);
            if (result == NativeApi.ERROR_SUCCESS)
            {
                return new PackageManager().FindPackageForUser(string.Empty, fullNameBuilder.ToString());
            }
            else
            {
                LogSystem.WriteLog(LogLevel.Warning,
                    $"获取进程 [Handle={processHandle}] 的包全名失败（{result}）：" +
                    Marshal.GetLastPInvokeErrorMessage());
                return null;
            }
        }

        /// <summary>
        /// 以从包名、文件描述、窗口标题到进程名称的回退链获取进程的显示名称。
        /// </summary>
        /// <param name="package">          进程的包信息。</param>
        /// <param name="processMainModule">进程主模块。</param>
        /// <param name="process">          进程的 <see cref="Process"/> 组件。</param>
        /// <returns>进程的显示名称，若都获取失败，则返回 <see langword="null"/>。</returns>
        private static string GetProcessDisplayName(Package? package,
                                                     Lazy<ProcessModule?> processMainModule,
                                                     Process process)
        {
            string? packageName = package?.DisplayName;
            if (!string.IsNullOrWhiteSpace(packageName))
            {
                return packageName;
            }

            string? fileDescription = processMainModule.Value?.FileVersionInfo.FileDescription;
            if (!string.IsNullOrWhiteSpace(fileDescription))
            {
                return fileDescription;
            }

            string? mainWindowTitle = process.MainWindowTitle;
            if (!string.IsNullOrWhiteSpace(mainWindowTitle))
            {
                return mainWindowTitle;
            }

            return process.ProcessName;
        }

        [GeneratedRegex(@"\.targetsize-\d{2,3}\.")]
        private static partial Regex GetTargetSizeRegex();

        /// <summary>
        /// 以指定文件名将指定图标保存到 <see cref="_iconFolderPath"/> 中的 Png 文件。
        /// </summary>
        /// <param name="fileNameWithoutExtension">不带扩展名的文件名。</param>
        /// <param name="icon">                    图标的 <see cref="Icon"/> 实例。</param>
        private static void SaveIconAsPng(string fileNameWithoutExtension, Icon icon)
        {
            _ = Directory.CreateDirectory(_iconFolderPath);
            string iconPath = Path.Combine(_iconFolderPath, $"{fileNameWithoutExtension}.png");
            using FileStream iconStream = new(iconPath, FileMode.Create, FileAccess.Write, FileShare.Write);
            icon.ToBitmap().Save(iconStream, ImageFormat.Png);
            icon.Dispose();
        }

        /// <summary>
        /// 以指定文件名将指定图标保存到 <see cref="_iconFolderPath"/> 中的 Png 文件。
        /// </summary>
        /// <param name="fileNameWithoutExtension">不带扩展名的文件名。</param>
        /// <param name="sourceIconPath">          源图标路径。</param>
        private static void SaveIconAsPng(string fileNameWithoutExtension, string sourceIconPath)
        {
            _ = Directory.CreateDirectory(_iconFolderPath);
            string targetIconPath = Path.Combine(_iconFolderPath, $"{fileNameWithoutExtension}.png");
            File.Copy(sourceIconPath, targetIconPath, true);
        }

        /// <summary>
        /// 以从包徽标、窗口图标到主模块文件图标的回退链获取进程图标并保存到 <see cref="_iconFolderPath"/>。
        /// </summary>
        /// <param name="processName">      进程名称。</param>
        /// <param name="package">          进程的包信息。</param>
        /// <param name="windowHandle">     窗口句柄。</param>
        /// <param name="processMainModule">进程主模块。</param>
        private static void SaveProcessIcon(string processName,
                                            Package? package,
                                            nint windowHandle,
                                            Lazy<ProcessModule?> processMainModule)
        {
            if (package is not null && TryGetPackageIcon(package, out string? iconFilePath))
            {
                SaveIconAsPng(processName, iconFilePath);
                return;
            }

            if (TryGetWindowIcon(windowHandle, out Icon? icon))
            {
                SaveIconAsPng(processName, icon);
                return;
            }

            if (processMainModule.Value is not null)
            {
                try
                {
                    icon = Icon.ExtractAssociatedIcon(processMainModule.Value.FileName);
                }
                catch (Exception ex)
                {
                    LogSystem.WriteLog(LogLevel.Warning, $"提取 {processName} 的文件图标失败。{ex}");
                }
                if (icon is not null)
                {
                    SaveIconAsPng(processName, icon);
                    return;
                }
            }
        }

        /// <summary>
        /// 尝试从字符串的指定位置提取数字。
        /// </summary>
        /// <param name="str">       要提取数字的字符串。</param>
        /// <param name="startIndex">开始位置。</param>
        /// <param name="number">    提取到的数字。</param>
        /// <returns>指示是否成功提取数字。</returns>
        private static bool TryExtractNumber(string str, int startIndex, out int number)
        {
            number = 0;
            if (string.IsNullOrWhiteSpace(str) || startIndex < 0 || startIndex >= str.Length)
            {
                return false;
            }

            int endIndex = startIndex;
            while (endIndex < str.Length && char.IsDigit(str[endIndex]))
            {
                endIndex++;
            }

            if (endIndex == startIndex)
            {
                return false;
            }

            ReadOnlySpan<char> numberSpan = str.AsSpan(startIndex, endIndex - startIndex);
            return int.TryParse(numberSpan, out number);
        }

        /// <summary>
        /// 尝试获取打包应用程序图标。
        /// </summary>
        /// <param name="package"> 应用程序的包信息。</param>
        /// <param name="iconPath">图标路径。</param>
        /// <returns>指示是否获取成功。</returns>
        private static bool TryGetPackageIcon(Package package, [MaybeNullWhen(false)] out string iconPath)
        {
            iconPath = null;
            try
            {
                string manifestPath = Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml");
                string? iconBaseUri = GetIconBaseUriFromManifest(manifestPath);
                if (iconBaseUri == null)
                {
                    LogSystem.WriteLog(LogLevel.Warning,
                        $"在 {manifestPath} 中未找到 Square44x44Logo 属性。");
                    return false;
                }
                iconPath = GetIconPath(iconBaseUri, package.InstalledLocation.Path, 100, 32);
                if (iconPath == null)
                {
                    LogSystem.WriteLog(LogLevel.Warning,
                        $"在 {package.InstalledLocation.Path} 中未找到符合 [size=32, scale=100] 条件的图标文件。");
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                LogSystem.WriteLog(LogLevel.Error, $"获取 {package.Id.FullName} 的图标失败。{ex}");
                return false;
            }
        }

        /// <summary>
        /// 尝试获取应用程序窗口的图标。
        /// </summary>
        /// <param name="windowHandle">窗口句柄。</param>
        /// <param name="icon">        图标的 <see cref="Icon"/> 实例。</param>
        /// <returns>指示是否获取成功。</returns>
        private static bool TryGetWindowIcon(nint windowHandle, [MaybeNullWhen(false)] out Icon icon)
        {
            nint iconHandle =
                NativeApi.SendMessage(windowHandle, NativeApi.WM_GETICON, NativeApi.ICON_BIG, nint.Zero);
            if (iconHandle != nint.Zero)
            {
                //LogSystem.WriteLog(LogLevel.Debug, $"{processName} 窗口大图标获取成功。");
                using (Icon iconWithoutOwnerShip = Icon.FromHandle(iconHandle))
                {
                    icon = (Icon)iconWithoutOwnerShip.Clone();
                }
                if (!NativeApi.DestroyIcon(iconHandle))
                {
                    LogSystem.WriteLog(LogLevel.Warning, $"销毁图标失败（{Marshal.GetLastPInvokeError()}）："
                        + Marshal.GetLastPInvokeErrorMessage());
                }
                return true;
            }
            //LogSystem.WriteLog(LogLevel.Debug,
            //    $"{processName} 窗口大图标获取失败（{Marshal.GetLastPInvokeError()}）：" +
            //    $"{Marshal.GetLastPInvokeErrorMessage()}");

            iconHandle = NativeApi.GetClassLongPtr(windowHandle, NativeApi.GCLP_HICON);
            if (iconHandle != nint.Zero)
            {
                //LogSystem.WriteLog(LogLevel.Debug, $"{processName} 与类关联的图标获取成功。");
                using (Icon iconWithoutOwnerShip = Icon.FromHandle(iconHandle))
                {
                    icon = (Icon)iconWithoutOwnerShip.Clone();
                }
                if (!NativeApi.DestroyIcon(iconHandle))
                {
                    LogSystem.WriteLog(LogLevel.Warning, $"销毁图标失败（{Marshal.GetLastPInvokeError()}）："
                        + Marshal.GetLastPInvokeErrorMessage());
                }
                return true;
            }
            //LogSystem.WriteLog(LogLevel.Debug,
            //    $"{processName} 与类关联的图标获取失败（{Marshal.GetLastPInvokeError()}）：" +
            //    $"{Marshal.GetLastPInvokeErrorMessage()}");

            icon = null;
            return false;
        }
    }
}
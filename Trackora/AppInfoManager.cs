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
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel;
using Windows.Management.Deployment;

namespace Zscno.Trackora
{
    /// <inheritdoc cref="IAppInfoManager"/>
    internal partial class AppInfoManager : IAppInfoManager
    {
        private readonly string _appInfoFilePath;

        private readonly string _defaultIconPath;

        private readonly string _iconFolderPath;

        /// <inheritdoc cref="IAppInfoManager.AppInfoMap"/>
        public ConcurrentDictionary<string, ProcessInfo> AppInfoMap { get; private set; }

        public AppInfoManager(IAppDataPathProvider pathProvider/*TODO: 接收日志实例。*/)
        {
            _iconFolderPath = pathProvider.IconPath;
            _defaultIconPath = Path.Combine(_iconFolderPath, "Default.png");
            _appInfoFilePath = Path.Combine(pathProvider.LocalCachePath, "ProcessInfo.json");
            AppInfoMap = new();
        }

        /// <inheritdoc cref="IAppInfoManager.CacheAppInfo(nint, Process)"/>
        /// <remarks>
        /// 应用程序的显示名称按照以下回退链获取：
        /// <list type="number">
        /// <item>包清单中的显示名称；</item>
        /// <item>可执行文件的文件描述；</item>
        /// <item>主窗口的标题；</item>
        /// <item>进程的名称。</item>
        /// </list>
        /// 应用程序的图标按照以下回退链获取：
        /// <list type="number">
        /// <item>包清单中 <c>Square44x44Logo</c> 属性指向的徽标，优先 <c>targetsize-32</c> 和 <c>scale-100</c>；</item>
        /// <item>窗口的图标；</item>
        /// <item>可执行文件的图标。</item>
        /// </list>
        /// </remarks>
        public void CacheAppInfo(nint windowHandle, Process process)
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
                CacheAppIcon(processName, package, windowHandle, processMainModule);
            }
            catch (Exception ex)
            {
                LogSystem.WriteLog(LogLevel.Warning, $"保存 {processName} 图标失败。{ex}");
            }

            string displayName = GetAppDisplayName(package, processMainModule, process);

            AppInfoMap[processName] = new ProcessInfo(displayName);
        }

        /// <inheritdoc cref="IAppInfoManager.GetAppIconUri(string)"/>
        public string GetAppIconUri(string processName)
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
        /// 加载已获取的应用程序信息。
        /// </summary>
        public Task LoadAsync()
        {
            if (File.Exists(_appInfoFilePath))
            {
                return Task.CompletedTask;
            }

            var appInfoMap = Json.ReadJsonFile(
                _appInfoFilePath, SourceGenerationContext.Default.ConcurrentDictionaryStringProcessInfo);
            if (appInfoMap is not null)
            {
                AppInfoMap = appInfoMap;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 保存已获取的应用程序信息。
        /// </summary>
        public Task StoreAsync()
        {
            _ = Json.WriteJsonFile(_appInfoFilePath, AppInfoMap, SourceGenerationContext.Default.ConcurrentDictionaryStringProcessInfo);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取应用程序的显示名称。
        /// </summary>
        /// <remarks>
        /// 应用程序的显示名称按照以下回退链获取：
        /// <list type="number">
        /// <item>包清单中的显示名称；</item>
        /// <item>可执行文件的文件描述；</item>
        /// <item>主窗口的标题；</item>
        /// <item>进程的名称。</item>
        /// </list>
        /// </remarks>
        /// <param name="package">          进程的包信息。</param>
        /// <param name="processMainModule">进程的主模块。</param>
        /// <param name="process">          进程的 <see cref="Process"/> 组件。</param>
        /// <returns>获取到的显示名称。</returns>
        private static string GetAppDisplayName(
            Package? package,
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

        /// <summary>
        /// 从应用程序包清单中获取指定图标的基 URI。
        /// </summary>
        /// <param name="manifestPath">应用程序包清单路径。</param>
        /// <param name="iconName">    图标基 URI 的名称（如 <c>Square44x44Logo</c>）。</param>
        /// <returns>获取到的图标基 URI。</returns>
        private static string? GetIconBaseUriFromManifest(string manifestPath, string iconName)
        {
            XmlDocument doc = new();
            doc.Load(manifestPath);

            XmlNamespaceManager nsmgr = new(doc.NameTable);
            nsmgr.AddNamespace("ns", "http://schemas.microsoft.com/appx/manifest/foundation/windows10");
            nsmgr.AddNamespace("uap", "http://schemas.microsoft.com/appx/manifest/uap/windows10");

            string xpath = "/ns:Package/ns:Applications/ns:Application/uap:VisualElements";
            XmlNode? node = doc.SelectSingleNode(xpath, nsmgr);
            return node?.Attributes?[iconName]?.Value;
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

        [GeneratedRegex(@"\.targetsize-\d{2,3}\.")]
        private static partial Regex GetTargetSizeRegex();

        /// <summary>
        /// 解析应用程序包清单中的图标路径。
        /// </summary>
        /// <remarks>该方法先尝试选取边长像素不小于 <paramref name="targetSize"/> 的 <c>targetsize</c> 图标；若没有，则选取缩放比例不小于 <paramref name="scale"/> 的图标。</remarks>
        /// <param name="iconBaseUri">应用程序包清单中的图标基 URI。</param>
        /// <param name="basePath">   应用程序包的安装路径。</param>
        /// <param name="targetSize"> 指定最小的可选取图标大小。</param>
        /// <param name="scale">      指定最小的可选取缩放比例。</param>
        /// <returns>解析出的图标路径。</returns>
        private static string? ResolvePackageIconPath(string iconBaseUri, string basePath, int targetSize, int scale)
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
                    targetSize <= currentSize && currentSize <= maxSize)
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
        /// 尝试从指定的句柄创建 GDI+ 图标。
        /// </summary>
        /// <param name="handle">图标的句柄。</param>
        /// <param name="icon">  该方法创建的图标。</param>
        /// <returns>指示是否创建成功。</returns>
        private static bool TryCreateIconFromHandle(nint handle, [MaybeNullWhen(false)] out Icon icon)
        {
            if (handle == nint.Zero)
            {
                icon = null;
                return false;
            }

            using (Icon iconWithoutOwnerShip = Icon.FromHandle(handle))
            {
                icon = (Icon)iconWithoutOwnerShip.Clone();
            }
            if (!NativeApi.DestroyIcon(handle))
            {
                LogSystem.WriteLog(LogLevel.Warning, $"销毁图标句柄失败（{Marshal.GetLastPInvokeError()}）："
                    + Marshal.GetLastPInvokeErrorMessage());
            }
            return true;
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
        /// 尝试获取指定的应用程序包徽标路径。
        /// </summary>
        /// <remarks>该方法先尝试选取边长像素不小于 <paramref name="targetSize"/> 的图标；若没有，则选取缩放比例不小于 <paramref name="scale"/> 的图标。</remarks>
        /// <param name="package">   应用程序的包信息。</param>
        /// <param name="iconName">  图标基 URI 的名称（如 <c>Square44x44Logo</c>）。</param>
        /// <param name="targetSize">指定最小的可选取图标大小。</param>
        /// <param name="scale">     指定最小的可选取缩放比例。</param>
        /// <param name="iconPath">  获取到的图标路径。</param>
        /// <returns>指示是否获取成功。</returns>
        private static bool TryGetPackageIcon(
            Package package,
            string iconName,
            int targetSize,
            int scale,
            [MaybeNullWhen(false)] out string iconPath)
        {
            iconPath = null;
            try
            {
                string manifestPath = Path.Combine(package.InstalledLocation.Path, "AppxManifest.xml");
                string? iconBaseUri = GetIconBaseUriFromManifest(manifestPath, iconName);
                if (iconBaseUri == null)
                {
                    LogSystem.WriteLog(LogLevel.Warning,
                        $"在 {manifestPath} 中未找到 {iconName} 属性。");
                    return false;
                }
                iconPath = ResolvePackageIconPath(iconBaseUri, package.InstalledLocation.Path, targetSize, scale);
                if (iconPath == null)
                {
                    LogSystem.WriteLog(LogLevel.Warning,
                        $"在 {package.InstalledLocation.Path} 中未找到" +
                        $"符合 [targetSize={targetSize}, scale={scale}] 条件的图标文件。");
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
        /// 尝试获取指定窗口的图标。
        /// </summary>
        /// <remarks>该方法会先向窗口发送 <see cref="NativeApi.WM_GETICON"/> 消息以检索大图标；若检索失败，则调用 <see cref="NativeApi.GetClassLongPtr(nint, int)"/> 函数检索与类关联的图标。两个步骤都仅检索 32x32 的大图标。</remarks>
        /// <param name="windowHandle">窗口的句柄。</param>
        /// <param name="icon">        获取到的 <see cref="Icon"/> 图标实例。</param>
        /// <returns>指示是否获取成功。</returns>
        private static bool TryGetWindowIcon(nint windowHandle, [MaybeNullWhen(false)] out Icon icon)
        {
            nint iconHandle =
                NativeApi.SendMessage(windowHandle, NativeApi.WM_GETICON, NativeApi.ICON_BIG, nint.Zero);
            if (TryCreateIconFromHandle(iconHandle, out icon))
            {
                return true;
            }

            iconHandle = NativeApi.GetClassLongPtr(windowHandle, NativeApi.GCLP_HICON);
            if (TryCreateIconFromHandle(iconHandle, out icon))
            {
                return true;
            }

            icon = null;
            return false;
        }

        /// <summary>
        /// 获取应用程序的图标并缓存。
        /// </summary>
        /// <remarks>
        /// 应用程序的图标按照以下回退链获取：
        /// <list type="number">
        /// <item>包清单中 <c>Square44x44Logo</c> 属性指向的徽标，优先 <c>targetsize-32</c> 和 <c>scale-100</c>；</item>
        /// <item>窗口的大图标；</item>
        /// <item>可执行文件的图标。</item>
        /// </list>
        /// </remarks>
        /// <param name="processName">      进程的名称。</param>
        /// <param name="package">          进程的包信息。</param>
        /// <param name="windowHandle">     进程关联的窗口句柄。</param>
        /// <param name="processMainModule">进程的主模块。</param>
        private void CacheAppIcon(
            string processName,
            Package? package,
            nint windowHandle,
            Lazy<ProcessModule?> processMainModule)
        {
            if (package is not null && TryGetPackageIcon(package, "Square44x44Logo", 32, 100, out string? iconFilePath))
            {
                SaveIconAsPng(processName, iconFilePath);
                return;
            }

            if (TryGetWindowIcon(windowHandle, out Icon? icon))
            {
                SaveIconAsPng(processName, icon);
                return;
            }

            if (processMainModule.Value is null)
            {
                return;
            }

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

        /// <summary>
        /// 以指定文件名将指定图标保存到 <see cref="_iconFolderPath"/> 中的 Png 文件。
        /// </summary>
        /// <param name="fileNameWithoutExtension">不带扩展名的文件名。</param>
        /// <param name="icon">                    图标的 <see cref="Icon"/> 实例。</param>
        private void SaveIconAsPng(string fileNameWithoutExtension, Icon icon)
        {
            _ = Directory.CreateDirectory(_iconFolderPath);
            string iconPath = Path.Combine(_iconFolderPath, $"{fileNameWithoutExtension}.png");
            using FileStream iconStream = new(iconPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
            icon.ToBitmap().Save(iconStream, ImageFormat.Png);
            icon.Dispose();
        }

        /// <summary>
        /// 以指定文件名将指定图标保存到 <see cref="_iconFolderPath"/> 中的 Png 文件。
        /// </summary>
        /// <param name="fileNameWithoutExtension">不带扩展名的文件名。</param>
        /// <param name="sourceIconPath">          源图标路径。</param>
        private void SaveIconAsPng(string fileNameWithoutExtension, string sourceIconPath)
        {
            _ = Directory.CreateDirectory(_iconFolderPath);
            string targetIconPath = Path.Combine(_iconFolderPath, $"{fileNameWithoutExtension}.png");
            File.Copy(sourceIconPath, targetIconPath, true);
        }
    }
}
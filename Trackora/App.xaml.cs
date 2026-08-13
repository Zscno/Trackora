using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Windows.ApplicationModel.Resources;
using Windows.Foundation.Collections;
using Windows.Storage;
using static Zscno.Trackora.LogSystem;

// To learn more about WinUI, the WinUI project structure, and more about our project templates,
// see: http://aka.ms/winui-project-info.

namespace Zscno.Trackora
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 指示是否正在保存应用程序数据并释放资源，保证 <see cref="SaveAndDispose"/> 方法线程安全。
        /// </summary>
        private static int _isSavingAndDisposing = 0;

        /// <summary>
        /// 应用主窗口。
        /// </summary>
        public static MainWindow? AppMainWindow { get; private set; }

        /// <summary>
        /// 用于加载语言资源。
        /// </summary>
        public static ResourceLoader Loader { get; } = new();

        /// <summary>
        /// 本地缓存文件夹路径。
        /// </summary>
        public static string LocalCachePath { get; private set; } = string.Empty;

        /// <summary>
        /// 所有可选的主题选项。
        /// </summary>
        public static Dictionary<string, string> Themes => new()
        {
            { Loader.GetString("LightTheme"), "LightTheme" },
            { Loader.GetString("DarkTheme"), "DarkTheme" },
            { Loader.GetString("SystemTheme"), "SystemTheme" },
        };

        internal static WindowTracker Tracker { get; private set; } // TODO: 放到静态类型中。

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            _ = new SafeCaller()
            {
                LogType = CallerLogType.Crash,
                LogMessage = "获取本地缓存文件夹路径失败。",
                ShouldExit = true,
                RemindingMsgResKey = "CanNotLaunchApp",
            }.CallMethodR(() =>
            {
                LocalCachePath = ApplicationData.Current.LocalCacheFolder.Path;
            });

            _ = new SafeCaller()
            {
                LogType = CallerLogType.Crash,
                LogMessage = "初始化日志文件失败。",
                ShouldExit = true,
                RemindingMsgResKey = "CannotLaunchApp",
            }.CallMethodR(() =>
            {
                InitLogFile();
            });

            _ = new SafeCaller()
            {
                LogMessage = "注册应用实例激活事件失败，将不能保证单实例。",
                ShouldExit = true,
                RemindingMsgResKey = "CanNotLaunchApp",
            }.CallMethodR(() =>
            {
                AppInstance appInstance = AppInstance.GetCurrent();
                appInstance.Activated += AppInstance_Activated;
            });
            SystemEvents.SessionEnding += OnSessionEnding;

            _ = new SafeCaller()
            {
                LogMessage = "设置应用主题失败。",
                RemindingMsgResKey = "ECanNotSetTheme",
            }.CallMethodR(() => SetTheme((string)LocalSettings["Theme"]));
        }

        /// <summary>
        /// 退出应用程序。
        /// </summary>
        /// <param name="exitCode">要返回到操作系统的退出代码。使用 0 指示进程已成功完成。</param>
        internal static void Exit(int exitCode)
        {
            SaveAndDispose();
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            AppMainWindow = new MainWindow();
            AppMainWindow.Activate();
            NativeApi.HideWindow(AppMainWindow);

            _ = new SafeCaller()
            {
                LogMessage = "使用记录管理器初始化失败。",
                RemindingMsgResKey = "", // TODO: 使用专属键。
            }.CallMethodR(UsageRecordManager.Initialize);

            _ = new SafeCaller()
            {
                LogMessage = "进程信息管理器初始化失败。",
                RemindingMsgResKey = "", // TODO: 使用专属键。
            }.CallMethodR(ProcessInfoManager.Initialize);

            Tracker = new WindowTracker();
        }

        /// <summary>
        /// 当用户正在尝试注销或关闭系统时调用。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">     提供 <see cref="SystemEvents.SessionEnding"/> 事件的数据。</param>
        private static void OnSessionEnding(object? sender, SessionEndingEventArgs e)
        {
            SaveAndDispose();
        }

        /// <summary>
        /// 保存应用程序数据并释放资源。
        /// </summary>
        private static void SaveAndDispose()
        {
            if (Interlocked.Exchange(ref _isSavingAndDisposing, 1) == 1)
            {
                return;
            }
            _ = AppMainWindow?.DispatcherQueue.TryEnqueue(() => AppMainWindow?.DisposeTaskBarIcon());
            Tracker.Dispose();
            _ = new SafeCaller()
            {
                LogMessage = "保存使用记录失败。",
                ShouldRemind = false,
            }.CallMethodR(() => UsageRecordManager.SaveRecord());
            SystemEvents.SessionEnding -= OnSessionEnding;
        }

        /// <summary>
        /// 设置应用主题。
        /// </summary>
        /// <param name="themeName">指定的主题名称。</param>
        private static void SetTheme(string themeName)
        {
            Current.RequestedTheme = themeName switch
            {
                "LightTheme" => ApplicationTheme.Light,
                "DarkTheme" => ApplicationTheme.Dark,
                _ => Current.RequestedTheme,
            };
        }

        /// <summary>
        /// 在已有应用实例被激活时调用。
        /// </summary>
        private void AppInstance_Activated(object? sender, AppActivationArguments e)
        {
            _ = AppMainWindow?.DispatcherQueue.TryEnqueue(async () => { AppMainWindow.ShowWindow(); });
        }
    }
}
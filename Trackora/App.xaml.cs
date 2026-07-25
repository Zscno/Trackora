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
        /// 类似于闹钟的通知所有可以选择的提示音。
        /// </summary>
        public static Dictionary<string, string> AlarmSounds { get; } = new()
        {
            { "Alarm", "ms-winsoundevent:Notification.Looping.Alarm" },
            { "Alarm2", "ms-winsoundevent:Notification.Looping.Alarm2" },
            { "Alarm3", "ms-winsoundevent:Notification.Looping.Alarm3" },
            { "Alarm4", "ms-winsoundevent:Notification.Looping.Alarm4" },
            { "Alarm5", "ms-winsoundevent:Notification.Looping.Alarm5" },
            { "Alarm6", "ms-winsoundevent:Notification.Looping.Alarm6" },
            { "Alarm7", "ms-winsoundevent:Notification.Looping.Alarm7" },
            { "Alarm8", "ms-winsoundevent:Notification.Looping.Alarm8" },
            { "Alarm9", "ms-winsoundevent:Notification.Looping.Alarm9" },
            { "Alarm10", "ms-winsoundevent:Notification.Looping.Alarm10" },
        };

        /// <summary>
        /// 应用主窗口。
        /// </summary>
        public static MainWindow? AppMainWindow { get; private set; }

        /// <summary>
        /// 指示是否能发出各种通知和提醒。
        /// </summary>
        public static bool CanShowReminder { get; set; } = true;

        /// <summary>
        /// 一般的通知所有可以选择的提示音。
        /// </summary>
        public static Dictionary<string, string> CommonSounds { get; } = new()
        {
            { "Default", "ms-winsoundevent:Notification.Default" },
            { "IM", "ms-winsoundevent:Notification.IM" },
            { "Mail", "ms-winsoundevent:Notification.Mail" },
            { "Reminder", "ms-winsoundevent:Notification.Reminder" },
            { "SMS", "ms-winsoundevent:Notification.SMS" },
        };

        /// <summary>
        /// 记录进程信息的文件路径。
        /// </summary>
        public static string InfoFilePath { get; private set; } = string.Empty;

        /// <summary>
        /// 用于加载语言资源。
        /// </summary>
        public static ResourceLoader Loader { get; } = new();

        /// <summary>
        /// 本地缓存文件夹路径。
        /// </summary>
        public static string LocalCachePath { get; private set; } = string.Empty;

        /// <summary>
        /// 应用本地设置。
        /// </summary>
        public static IPropertySet LocalSettings { get; private set; } = new PropertySet();

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
        /// Initializes the singleton application object. This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
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
                LogMessage = "初始化信息文件路径失败。",
                ShouldExit = true,
                RemindingMsgResKey = "ECanNotInitInfoFilePath",
            }.CallMethodR(() => InfoFilePath = Path.Combine(LocalCachePath, "Info.json"));

            _ = new SafeCaller()
            {
                LogMessage = "初始化本地设置失败，将使用内存临时存储。",
                RemindingMsgResKey = "ECanNotInitSettings",
            }.CallMethodR(() =>
            {
                LocalSettings = ApplicationData.Current.LocalSettings.Values;
            });

            InitLocalSettingsIfNeeded();

            _ = new SafeCaller()
            {
                LogMessage = "设置应用主题失败。",
                RemindingMsgResKey = "ECanNotSetTheme",
            }.CallMethodR(() => SetTheme((string)LocalSettings["Theme"]));

            Tracker = new WindowTracker();
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
            _ = new SafeCaller()
            {
                LogMessage = "准备 Icons 文件夹失败。",
                ShouldExit = true,
                RemindingMsgResKey = "CanNotLaunchApp",
            }.CallMethodR(() =>
            {
                if (!Directory.Exists(Path.Combine(LocalCachePath, "Icons")))
                {
                    _ = Directory.CreateDirectory(Path.Combine(LocalCachePath, "Icons"));
                }
            });

            AppMainWindow = new MainWindow();
            AppMainWindow.Activate();

            NativeApi.HideWindow(AppMainWindow);
        }

        /// <summary>
        /// 在需要时初始化本地设置的默认值。
        /// </summary>
        private static void InitLocalSettingsIfNeeded()
        {
            _ = LocalSettings.TryAdd("TotalUsedRemindTime", TimeSpan.FromHours(2));
            _ = LocalSettings.TryAdd("ContinuousUsedRemindTime", TimeSpan.FromMinutes(30));
            _ = LocalSettings.TryAdd("TotalUsageTimeSound", "Default");
            _ = LocalSettings.TryAdd("ContinuousUsageTimeSound", "Default");
            _ = LocalSettings.TryAdd("EndUsingTimeSound", "Alarm");
            _ = LocalSettings.TryAdd("Theme", "SystemTheme");
            // 仅记录使用时间的进程：开始，搜索，文件/文件夹选取器，uac提示，打开方式选取器，小组件，任务栏上各种视图。
            _ = LocalSettings.TryAdd("OnlyTimeProcesses",
                "StartMenuExperienceHost,SearchHost,PickerHost," +
                "consent,OpenWith,Widgets,ShellExperienceHost");
            // 忽略的进程：桌面管理器，锁屏，线程等待对话框。
            _ = LocalSettings.TryAdd("IgnoredProcesses", "dwm,LockApp,ServiceHub.ThreadedWaitDialog");
            _ = LocalSettings.TryAdd("ContinuousUsedResetTime", TimeSpan.FromMinutes(10));
        }

        /// <summary>
        /// 当用户正在尝试注销或关闭系统时调用。
        /// </summary>
        /// <param name="sender">事件发送者。</param>
        /// <param name="e">提供 <see cref="SystemEvents.SessionEnding"/> 事件的数据。</param>
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

            // TODO: 保存当天数据。
            _ = AppMainWindow?.DispatcherQueue.TryEnqueue(() => AppMainWindow?.DisposeTaskBarIcon());
            Tracker.Dispose();
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
            _ = AppMainWindow?.DispatcherQueue.TryEnqueue(async () => { await AppMainWindow.ShowWindow(); });
        }
    }
}
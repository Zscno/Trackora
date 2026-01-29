using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
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
				LogMessage = "无法获取本地缓存文件夹路径或无法初始化日志文件。",
				ShouldExit = true,
				RemindingMsgResKey = "CanNotLaunchApp",
			}.CallMethodR(() =>
			{
				LocalCachePath = ApplicationData.Current.LocalCacheFolder.Path;
				InitLogFile();
			});

			_ = new SafeCaller()
			{
				LogMessage = "无法注册应用实例激活事件。",
				ShouldExit = true,
				RemindingMsgResKey = "CanNotLaunchApp",
			}.CallMethodR(() =>
			{
				AppInstance appInstance = AppInstance.GetCurrent();
				appInstance.Activated += AppInstance_Activated;
			});

			_ = new SafeCaller()
			{
				LogMessage = "无法初始化信息文件路径。",
				ShouldExit = true,
				RemindingMsgResKey = "ECanNotInitInfoFilePath",
			}.CallMethodR(() => InfoFilePath = Path.Combine(LocalCachePath, "Info.json"));

			_ = new SafeCaller()
			{
				LogMessage = "无法初始化本地设置，将使用内存临时存储。",
				RemindingMsgResKey = "ECanNotInitSettings",
			}.CallMethodR(() =>
			{
				LocalSettings = ApplicationData.Current.LocalSettings.Values;
			});

			InitLocalSettingsIfNeeded();

			_ = new SafeCaller()
			{
				LogMessage = "无法设置应用主题。",
				RemindingMsgResKey = "ECanNotSetTheme",
			}.CallMethodR(() => SetTheme((string)LocalSettings["Theme"]));
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
			_ = LocalSettings.TryAdd("OnlyTimeProcesses",
				"StartMenuExperienceHost,SearchHost,PickerHost," +
				"consent,OpenWith,Widgets,ShellExperienceHost");

			// 开始，搜索，文件/文件夹选取器，uac提示，打开方式选取器，小组件，任务栏上各种视图，只记录时间不记录信息。
			_ = LocalSettings.TryAdd("IgnoredProcesses", "dwm,LockApp,ServiceHub.ThreadedWaitDialog");

			// 桌面管理器，锁屏，线程等待对话框，什么都不记录。
			_ = LocalSettings.TryAdd("ContinuousUsedResetTime", TimeSpan.FromMinutes(10));
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

		/// <summary>
		/// Invoked when the application is launched.
		/// </summary>
		/// <param name="args">Details about the launch request and process.</param>
		protected override void OnLaunched(LaunchActivatedEventArgs args)
		{
			_ = new SafeCaller()
			{
				LogMessage = "无法准备 Icons 文件夹。",
				ShouldExit = true,
				RemindingMsgResKey = "CanNotLaunchApp",
			}.CallMethodR(() =>
			{
				if (!Directory.Exists(Path.Combine(LocalCachePath, "Icons")))
				{
					_ = Directory.CreateDirectory(Path.Combine(LocalCachePath, "Icons"));
				}
			});

			_ = new WindowTracker();

			AppMainWindow = new MainWindow();
			AppMainWindow.Activate();

			NativeApi.HideWindow(AppMainWindow);
		}
	}
}
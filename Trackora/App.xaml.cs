using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.IO;
using Windows.ApplicationModel.Resources;
using Windows.Foundation.Collections;
using Windows.Storage;
using static Zscno.Trackora.LogSystem;

// To learn more about WinUI, the WinUI project structure, and more about our project templates, see: http://aka.ms/winui-project-info.

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
		public static bool CanSend { get; set; } = true;

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
			{ Loader.GetString("SystemTheme"), "SystemTheme" }
		};

		/// <summary>
		/// Initializes the singleton application object. This is the first line of authored code executed, and as such is the logical equivalent of main() or WinMain().
		/// </summary>
		public App()
		{
			InitializeComponent();
			InitLogFile();

			try
			{
				AppInstance appInstance = AppInstance.GetCurrent();
				appInstance.Activated += AppInstance_Activated;
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"在注册应用实例激活事件时触发异常：{ex}");
			}

			// 初始化信息文件路径。
			try
			{
				InfoFilePath = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Info.json");
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"在初始化信息文件路径时触发异常，将在提醒用户退出：{ex}");
				CanSend = ReminderHelper.SendReminder("提示用户无法加载进程信息",
					Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotInitInfoFilePath"), true);
				Current.Exit();
			}

			// 初始化本地设置。
			try
			{
				LocalSettings = ApplicationData.Current.LocalSettings.Values;
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"在初始化本地设置时触发异常，将使用内存临时存储：{ex}");
				CanSend = ReminderHelper.SendReminder("提醒用户无法加载设置",
					Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotInitSettings"), true);
			}

			// 本地设置的默认值。
			if (!LocalSettings.ContainsKey("TotalUsedRemindTime"))
			{
				LocalSettings["TotalUsedRemindTime"] = TimeSpan.FromHours(2);
			}
			if (!LocalSettings.ContainsKey("ContinuousUsedRemindTime"))
			{
				LocalSettings["ContinuousUsedRemindTime"] = TimeSpan.FromMinutes(30);
			}
			if (!LocalSettings.ContainsKey("TotalUsedTimeSound"))
			{
				LocalSettings["TotalUsedTimeSound"] = "Default";
			}
			if (!LocalSettings.ContainsKey("ContinuousUsedTimeSound"))
			{
				LocalSettings["ContinuousUsedTimeSound"] = "Default";
			}
			if (!LocalSettings.ContainsKey("EndUsingTimeSound"))
			{
				LocalSettings["EndUsingTimeSound"] = "Alarm";
			}
			if (!LocalSettings.ContainsKey("Theme"))
			{
				LocalSettings["Theme"] = "SystemTheme";
			}
			if (!LocalSettings.ContainsKey("NoInfoNames"))
			{
				// 开始，搜索，文件/文件夹选取器，uac提示，打开方式选取器，小组件，任务栏上各种视图，只记录时间不记录信息。
				LocalSettings["NoInfoNames"] = "StartMenuExperienceHost,SearchHost," +
					"PickerHost,consent,OpenWith,Widgets,ShellExperienceHost";
			}
			if (!LocalSettings.ContainsKey("NoTimeNames"))
			{
				// 桌面管理器，锁屏，线程等待对话框，什么都不记录。
				LocalSettings["NoTimeNames"] = "dwm,LockApp,ServiceHub.ThreadedWaitDialog";
			}
			if (!LocalSettings.ContainsKey("ContinuousUsedResetTime"))
			{
				LocalSettings["ContinuousUsedResetTime"] = TimeSpan.FromMinutes(10);
			}
			if (!LocalSettings.ContainsKey("HasTotalReminded"))
			{
				LocalSettings["HasTotalReminded"] = false;
			}

			// 设置主题。
			try
			{
				switch ((string) LocalSettings["Theme"])
				{
					case "LightTheme":
						Current.RequestedTheme = ApplicationTheme.Light;
						break;

					case "DarkTheme":
						Current.RequestedTheme = ApplicationTheme.Dark;
						break;

					default:
						break;
				}
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"在设置应用主题时触发异常：{ex}");
			}
		}

		/// <summary>
		/// Invoked when the application is launched.
		/// </summary>
		/// <param name="args">Details about the launch request and process.</param>
		protected override void OnLaunched(LaunchActivatedEventArgs args)
		{
			try
			{
				string path = ApplicationData.Current.LocalCacheFolder.Path;
				if (!Directory.Exists(Path.Combine(path, "Icons")))
				{
					_ = Directory.CreateDirectory(Path.Combine(path, "Icons"));
				}
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"在确认 Icons 文件夹存在时触发异常：{ex}");
			}

			_ = new WindowTracker();

			AppMainWindow = new MainWindow();
			AppMainWindow.Activate();

			SystemHelper.HideWindow(AppMainWindow);
		}

		/// <summary>
		/// 在已有应用实例被激活时调用。
		/// </summary>
		private void AppInstance_Activated(object? sender, AppActivationArguments e)
		{
			_ = AppMainWindow?.DispatcherQueue.TryEnqueue(async () =>
			{
				await AppMainWindow.ShowWindow();
			});
		}
	}
}
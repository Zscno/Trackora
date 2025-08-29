using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using static Zscno.Trackora.App;

namespace Zscno.Trackora
{
	/// <summary>
	/// 提醒通知类型。
	/// </summary>
	internal enum ReminderKind
	{
		/// <summary>
		/// 总使用时长提醒。
		/// </summary>
		TotalUsedTimeReminder,

		/// <summary>
		/// 总使用时长提醒提示音的测试通知。
		/// </summary>
		TotalUsedTimeSoundTest,

		/// <summary>
		/// 连续使用时长提醒。
		/// </summary>
		ContinuousUsedTimeReminder,

		/// <summary>
		/// 连续使用时长提醒提示音的测试通知。
		/// </summary>
		ContinuousUsedTimeSoundTest,

		/// <summary>
		/// 结束使用时间提醒。
		/// </summary>
		EndUsingTimeReminder,

		/// <summary>
		/// 结束使用时间提醒提示音的测试通知。
		/// </summary>
		EndUsingTimeSoundTest,
	}

	/// <summary>
	/// 提醒通知辅助类。
	/// </summary>
	internal static class ReminderHelper
	{
		/// <summary>
		/// 发送指定类型的通知。
		/// </summary>
		/// <param name="reminderKind">通知的类型。</param>
		/// <returns>指示通知是否能正常显示。</returns>
		public static bool SendReminder(ReminderKind reminderKind)
		{
			string logInfo;
			string logError;
			string title;
			string content;
			ToastAudio audio = new();

			switch (reminderKind)
			{
				case ReminderKind.TotalUsedTimeReminder:
					logInfo = $"总使用时长已达设置中的值" +
						$" [{(TimeSpan) LocalSettings["TotalUsedRemindTime"]:hh\\:mm\\:ss}] 。";
					logError = "在发送 总使用时间提醒通知 时触发异常：";
					title = Loader.GetString("UsedTimeReminderTitle");
					content = Loader.GetString("TotalReminderText1") +
						WindowTracker.GetLocalTime(TimeSpan.Zero, true) +
						Loader.GetString("TotalReminderText2");
					audio.Src = new(CommonSounds[(string) LocalSettings["TotalUsedTimeSound"]]);
					break;

				case ReminderKind.TotalUsedTimeSoundTest:
					logInfo = string.Empty;
					logError = "在发送 总使用时长提醒提示音的测试通知 时触发异常：";
					title = Loader.GetString("Test/Content");
					content = Loader.GetString("TestContent") + (string) LocalSettings["TotalUsedTimeSound"];
					audio.Src = new(CommonSounds[(string) LocalSettings["TotalUsedTimeSound"]]);
					break;

				case ReminderKind.ContinuousUsedTimeReminder:
					logInfo = $"连续使用时长已达设置中的值" +
						$" [{(TimeSpan) LocalSettings["ContinuousUsedRemindTime"]:hh\\:mm\\:ss}] 。";
					logError = "在发送 连续使用时间提醒通知 时触发异常：";
					title = Loader.GetString("UsedTimeReminderTitle");
					content = Loader.GetString("ContinuousReminderText1") +
						WindowTracker.GetLocalTime((TimeSpan) LocalSettings["ContinuousUsedRemindTime"]) +
						Loader.GetString("ContinuousReminderText2");
					audio.Src = new Uri(CommonSounds[(string) LocalSettings["ContinuousUsedTimeSound"]]);
					break;

				case ReminderKind.ContinuousUsedTimeSoundTest:
					logInfo = string.Empty;
					logError = "在发送 连续使用时长提醒提示音的测试通知 时触发异常：";
					title = Loader.GetString("Test/Content");
					content = Loader.GetString("TestContent") + (string) LocalSettings["ContinuousUsedTimeSound"];
					audio.Src = new Uri(CommonSounds[(string) LocalSettings["ContinuousUsedTimeSound"]]);
					break;

				case ReminderKind.EndUsingTimeReminder:
					logInfo = $"结束使用时间已达设置中的值：{WindowTracker.EndUsingTime:hh\\:mm\\:ss}";
					logError = "在发送 结束使用时间提醒通知 时触发异常：";
					title = Loader.GetString("EndUsingReminderTitle");
					content = Loader.GetString("EndUsingReminderText1") +
						WindowTracker.GetLocalTime(WindowTracker.EndUsingTime) +
						Loader.GetString("EndUsingReminderText2");
					audio.Src = new Uri(AlarmSounds[(string) LocalSettings["EndUsingTimeSound"]]);
					break;

				case ReminderKind.EndUsingTimeSoundTest:
					logInfo = string.Empty;
					logError = "在发送 结束使用时间提醒提示音的测试通知 时触发异常：";
					title = Loader.GetString("Test/Content");
					content = Loader.GetString("TestContent") + (string) LocalSettings["EndUsingTimeSound"];
					audio.Src = new Uri(AlarmSounds[(string) LocalSettings["EndUsingTimeSound"]]);
					break;

				default:
					logInfo = "";
					logError = "";
					title = "";
					content = "";
					break;
			}

			if (logInfo is not "")
			{
				LogSystem.WriteLog(LogLevel.Info, logInfo);
			}

			try
			{
				new ToastContentBuilder().AddText(title).AddText(content).AddAudio(audio).Show();
				return true;
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Error, logError + ex.ToString());
				return false;
			}
		}

		/// <summary>
		/// 发送一个指定类型之外的通知。
		/// </summary>
		/// <param name="exMessage">如果触发了异常，则以本信息为异常的标识。</param>
		/// <param name="title">通知标题。</param>
		/// <param name="content">通知内容。</param>
		/// <param name="isExit">指示如果触发了异常，是否退出应用。</param>
		/// <param name="audioUri">通知提示音（默认是 <see cref="CommonSounds"/> 中的 <c>Default</c> ）。</param>
		public static bool SendReminder(string exMessage, string title, string content, bool isExit = true,
			string audioUri = "ms-winsoundevent:Notification.Default")
		{
			try
			{
				new ToastContentBuilder().AddText(title).AddText(content).AddAudio(
					new ToastAudio()
					{
						Src = new(audioUri)
					}).Show();
				return true;
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Error, $"在发送 {exMessage} 时触发异常：{ex}");
				if (isExit)
				{
					LogSystem.WriteLog(LogLevel.Info, "程序由于上一个异常退出。");
					Application.Current.Exit();
				}
				return false;
			}
		}

		/// <summary>
		/// 显示一个对话框。
		/// </summary>
		/// <param name="exMessage">如果触发了异常，则以本信息为异常的标识。</param>
		/// <param name="title">对话框标题。</param>
		/// <param name="content">对话框内容。</param>
		/// <returns>指示对话框是否能正常显示。</returns>
		public static async Task<bool> ShowDialog(string exMessage, string title, string content)
		{
			try
			{
				if (AppMainWindow is null)
				{
					throw new InvalidOperationException("主窗口未初始化。");
				}

				ContentDialog dialog = new()
				{
					XamlRoot = AppMainWindow.Content.XamlRoot,
					Title = title,
					Content = content,
					CloseButtonText = Loader.GetString("Cancel"),
					PrimaryButtonText = Loader.GetString("OK/Content"),
					DefaultButton = ContentDialogButton.Primary
				};
				_ = await dialog.ShowAsync();
				return true;
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Error, $"无法显示对话框以提示用户{exMessage}：{ex}");
				return false;
			}
		}
	}
}
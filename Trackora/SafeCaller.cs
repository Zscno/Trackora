using System;
using System.IO;
using System.Threading.Tasks;

namespace Zscno.Trackora
{
	/// <summary>
	/// 安全地调用方法。
	/// </summary>
	internal static class SafeCaller
	{
		/// <summary>
		/// 执行 <paramref name="action"/> 方法，如果触发异常则记录（崩溃）日志，并发送提醒通知。如果 <paramref name="exit"/> 是 <see langword="true"/> ，程序将退出。
		/// </summary>
		/// <param name="action">要执行的方法。</param>
		/// <param name="message">日志的内容。</param>
		/// <param name="reminderMessage">如果发送通知触发了异常则以此作为日志内容。</param>
		/// <param name="contentResName">对话框内容的资源名称。</param>
		/// <param name="useLog">指示是否在触发异常时记录日志，如果是 <see langword="false"/> 则记录崩溃日志。</param>
		/// <param name="exit">指示触发异常执行完所有操作后是否退出。</param>
		public static void CallFatal(Action action, string message,
			string reminderMessage, string contentResName, bool useLog = true, bool exit = false)
		{
			try
			{
				action();
			}
			catch (Exception ex)
			{
				if (useLog)
				{
					LogSystem.WriteLog(LogLevel.Error, $"{message}：{ex}");
				}
				else
				{
					File.WriteAllText(
						$"{DateTime.Now:yyyy-MM-dd_HH+mm+ss}.crash", $"{message}：{ex}");
				}
				App.CanSend = ReminderHelper.SendReminder(reminderMessage,
					App.Loader.GetString("ErrorOrWarningTitle"), App.Loader.GetString(contentResName));
				if (exit)
				{
					Environment.Exit(1);
				}
			}
		}

		/// <summary>
		/// 执行 <paramref name="action"/> 方法，如果触发异常则记录日志，并根据需要显示对话框。
		/// </summary>
		/// <param name="action">要执行的方法。</param>
		/// <param name="level">日志的等级。</param>
		/// <param name="message">日志的内容。</param>
		/// <param name="useDialog">指示是否使用对话框。</param>
		/// <param name="contentResName">对话框内容的资源名称。</param>
		/// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		public static async Task<bool> CallNormal(Action action, LogLevel level, string message,
			bool useDialog = false, string exMessage = "", string contentResName = "")
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(level, $"{message}：{ex}");
				if (useDialog && exMessage is not "" && contentResName is not "")
				{
					await ReminderHelper.ShowDialog(
						App.Loader.GetString("ErrorOrWarningTitle"),
						App.Loader.GetString(contentResName),
						exMessage);
				}
				return false;
			}
		}

		/// <summary>
		/// 执行 <paramref name="action"/> 方法，如果触发异常则记录日志，并根据需要发送提醒通知。
		/// </summary>
		/// <param name="action">要执行的方法。</param>
		/// <param name="level">日志的等级。</param>
		/// <param name="message">日志的内容。</param>
		/// <param name="useReminder">指示是否使用提醒通知。</param>
		/// <param name="reminderMessage">如果发送通知触发了异常则以此作为日志内容。</param>
		/// <param name="exit">指示触发异常执行完所有操作后是否退出。</param>
		/// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		public static bool CallNormal(Action action, LogLevel level, string message,
			bool useReminder = false, string reminderMessage = "", bool exit = false)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(level, $"{message}：{ex}");
				if (useReminder && reminderMessage is not "")
				{
					App.CanSend = ReminderHelper.SendReminder(reminderMessage,
						App.Loader.GetString("ErrorOrWarningTitle"),
						App.Loader.GetString("ErrorOccurredContent"));
				}
				if (exit)
				{
					Environment.Exit(1);
				}
				return false;
			}
		}

		/// <summary>
		/// 执行 <paramref name="action"/> 方法，如果触发异常则记录日志。当捕获到指定的异常时，使用特殊的日志等级和内容记录日志。
		/// </summary>
		/// <typeparam name="T">指定的异常。</typeparam>
		/// <param name="action">要执行的方法。</param>
		/// <param name="level">特殊的日志等级。</param>
		/// <param name="message">特殊的日志内容。</param>
		/// <param name="normalLevel">一般的日志等级。</param>
		/// <param name="normalMessage">一般的日志内容。</param>
		/// <param name="func">附加对指定异常的判定条件。</param>
		/// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		public static bool CallSpecial<T>(Action action,
			LogLevel level, string message,
			LogLevel normalLevel, string normalMessage, Func<T, bool>? func = null) where T : Exception
		{
			try
			{
				action();
				return true;
			}
			catch (T ex) when (func is null || func(ex))
			{
				LogSystem.WriteLog(level, $"{message}：{ex}");
				return false;
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(normalLevel, $"{normalMessage}：{ex}");
				return false;
			}
		}
	}
}
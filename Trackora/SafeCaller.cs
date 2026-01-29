using System;
using System.IO;
using System.Threading.Tasks;

namespace Zscno.Trackora
{
	/// <summary>
	/// 调用器的日志类型。
	/// </summary>
	internal enum CallerLogType
	{
		Normal,

		Crash,
	}

	/// <summary>
	/// 调用器的提醒类型。
	/// </summary>
	internal enum CallerReminderType
	{
		None,

		Dialog,

		Reminder,
	}

	/// <summary>
	/// 安全的方法调用器。
	/// </summary>
	internal class SafeCaller
	{
		private static readonly string unspecifiedContent = "未指定的内容。";

		/// <summary>
		/// 日志的等级（默认为 <see cref="LogLevel.Error"/> ）。
		/// </summary>
		public LogLevel LogLevel { get; set; } = LogLevel.Error;

		/// <summary>
		/// 日志的内容（默认为 <c>"未指定的内容。"</c> ）。
		/// </summary>
		public string LogMessage { get; set; } = unspecifiedContent;

		/// <summary>
		/// 日志的类型（默认为 <see cref="CallerLogType.Normal"/> ）。
		/// </summary>
		public CallerLogType LogType { get; set; } = CallerLogType.Normal;

		/// <summary>
		/// 指示触发异常执行完所有操作后是否退出（默认为 <see langword="false"/>）。
		/// </summary>
		public bool ShouldExit { get; set; }

		/// <summary>
		/// 指示当触发异常时是否需要使用对话框或通知提醒用户（默认为 <see langword="true"/>）。
		/// </summary>
		public bool ShouldRemind { get; set; } = true;

		/// <summary>
		/// 用于提醒用户的内容的资源名称（默认为 <see cref="string.Empty"/>）。
		/// </summary>
		public string RemindingMsgResKey { get; set; } = string.Empty;

		/// <summary>
		/// 退出应用（如果需要）。
		/// </summary>
		private void ExitIfNeeded()
		{
			if (ShouldExit)
			{
				Environment.Exit(1);
			}
		}

		/// <summary>
		/// 通过对话框提醒用户。
		/// </summary>
		private async Task RemindUserByDialog()
		{
			if (!ShouldRemind) { return; }
			App.CanShowReminder = await ReminderHelper.ShowDialog(
				App.Loader.GetString("ErrorOrWarningTitle"),
				string.IsNullOrWhiteSpace(RemindingMsgResKey) ? "未指定的内容。" :
				App.Loader.GetString(RemindingMsgResKey));
		}

		/// <summary>
		/// 通过通知提醒用户。
		/// </summary>
		private void RemindUserByReminder()
		{
			if (!ShouldRemind) { return; }
			App.CanShowReminder = ReminderHelper.SendReminder(
				App.Loader.GetString("ErrorOrWarningTitle"),
				string.IsNullOrWhiteSpace(RemindingMsgResKey) ? "未指定的内容。" :
				App.Loader.GetString(RemindingMsgResKey));
		}

		/// <summary>
		/// 记录（崩溃）日志。
		/// </summary>
		/// <param name="ex">要记录的异常。</param>
		private void WriteLog(Exception ex)
		{
			string enter = string.IsNullOrWhiteSpace(LogMessage) ?
				string.Empty : "\n";
			switch (LogType)
			{
				case CallerLogType.Normal:
					LogSystem.WriteLog(LogLevel, $"{LogMessage}{enter}{ex}");
					break;

				case CallerLogType.Crash:
					File.WriteAllText(
						$"{DateTime.Now:yyyy-MM-dd_HH+mm+ss}.crash", $"{LogMessage}{enter}{ex}");
					break;
			}
		}

		/// <summary>
		/// 调用方法 <paramref name="method"/> 。当触发异常时，使用对话框提醒用户。
		/// </summary>
		/// <param name="method">要调用的方法。</param>
		/// <returns>指示方法 <paramref name="method"/> 是否成功执行。</returns>
		public async Task<bool> CallMethodD(Action method)
		{
			try
			{
				method();
				return true;
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUserByDialog();
				ExitIfNeeded();
				return false;
			}
		}

		/// <summary>
		/// 调用方法 <paramref name="method"/> 。当触发异常时，使用通知提醒用户。
		/// </summary>
		/// <param name="method">要调用的方法。</param>
		/// <returns>指示方法 <paramref name="method"/> 是否成功执行。</returns>
		public bool CallMethodR(Action method)
		{
			try
			{
				method();
				return true;
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				RemindUserByReminder();
				ExitIfNeeded();
				return false;
			}
		}

		/// <summary>
		/// 调用方法 <paramref name="method"/> 并返回 <typeparamref name="TResult"/> 类型的值。当触发异常时，使用通知提醒用户。
		/// </summary>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <param name="method">要调用的方法。</param>
		/// <returns>
		/// <see langword="bool"/> 值指示 <paramref name="method"/> 方法是否成功执行， <typeparamref
		/// name="TResult"/> 值是返回结果。
		/// </returns>
		public (bool IsSuccessful, TResult? Result) CallMethodWithReturnR
			<TResult>(Func<TResult> method)
		{
			try
			{
				TResult result = method();
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				RemindUserByReminder();
				ExitIfNeeded();
				return (false, default(TResult?));
			}
		}

		///// <summary>
		///// 调用 <paramref name="method"/> 方法并捕获 <typeparamref name="T"/> 类型的异常。
		///// </summary>
		///// <typeparam name="T">要捕获的特定异常。</typeparam>
		///// <param name="method">要调用的方法。</param>
		///// <param name="level">特定的日志等级。</param>
		///// <param name="message">特定的日志标识。</param>
		///// <param name="func">（可选）特定异常附加的判断条件。</param>
		///// <returns>指示 <paramref name="method"/> 方法是否成功执行。</returns>
		//public async Task<bool> CallAction<T>(Action action, LogLevel level,
		//	string message, Func<T, bool>? func = null) where T : Exception
		//{
		//	try
		//	{
		//		action();
		//		return true;
		//	}
		//	catch (T ex) when (func is null || func(ex))
		//	{
		//		WriteLog(ex, level, message);
		//		await RemindUserByDialog();
		//		ExitIfNeeded();
		//		return false;
		//	}
		//	catch (Exception ex)
		//	{
		//		WriteLog(ex);
		//		await RemindUserByDialog();
		//		ExitIfNeeded();
		//		return false;
		//	}
		//}

		///// <summary>
		///// 同步调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。同时捕获
		///// <typeparamref name="TEx"/> 类型的异常。
		///// </summary>
		///// <remarks>
		///// 只有当 <see cref="ReminderType"/> 是 <see cref="CallerReminderType.Reminder"/> 时才会提醒用户。
		///// </remarks>
		///// <typeparam name="TEx">要捕获的特定异常。</typeparam>
		///// <typeparam name="TResult">返回值类型。</typeparam>
		///// <param name="action">要调用的方法。</param>
		///// <param name="level">特定的日志等级。</param>
		///// <param name="message">特定的日志标识。</param>
		///// <param name="func">（可选）特定异常附加的判断条件。</param>
		///// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		//public (bool IsSuccessful, TResult? Result) CallActionWithReturnSync<TEx, TResult>(
		//	Func<TResult> action, LogLevel level, string message,
		//	Func<TEx, bool>? func = null) where TEx : Exception
		//{
		//	try
		//	{
		//		TResult result = action();
		//		return (true, result);
		//	}
		//	catch (TEx ex) when (func is null || func(ex))
		//	{
		//		WriteLog(ex, level, message);
		//		RemindUserByReminder();
		//		ExitIfNeeded();
		//		return (false, default(TResult?));
		//	}
		//	catch (Exception ex)
		//	{
		//		WriteLog(ex);
		//		RemindUserByReminder();
		//		ExitIfNeeded();
		//		return (false, default(TResult?));
		//	}
		//}

		///// <summary>
		///// 调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。同时捕获
		///// <typeparamref name="TEx"/> 类型的异常。
		///// </summary>
		///// <typeparam name="TEx">要捕获的特定异常。</typeparam>
		///// <typeparam name="TResult">返回值类型。</typeparam>
		///// <param name="action">要调用的方法。</param>
		///// <param name="level">特定的日志等级。</param>
		///// <param name="message">特定的日志标识。</param>
		///// <param name="func">（可选）特定异常附加的判断条件。</param>
		///// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		//public async Task<(bool IsSuccessful, TResult? Result)> CallActionWithReturn<TEx, TResult>(
		//	Func<TResult> action, LogLevel level, string message,
		//	Func<TEx, bool>? func = null) where TEx : Exception
		//{
		//	try
		//	{
		//		TResult result = action();
		//		return (true, result);
		//	}
		//	catch (TEx ex) when (func is null || func(ex))
		//	{
		//		WriteLog(ex, level, message);
		//		await RemindUserByDialog();
		//		ExitIfNeeded();
		//		return (false, default(TResult?));
		//	}
		//	catch (Exception ex)
		//	{
		//		WriteLog(ex);
		//		await RemindUserByDialog();
		//		ExitIfNeeded();
		//		return (false, default(TResult?));
		//	}
		//}
	}
}
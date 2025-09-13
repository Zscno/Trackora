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

		Crash
	}

	/// <summary>
	/// 调用器的提醒类型。
	/// </summary>
	internal enum CallerReminderType
	{
		None,

		Dialog,

		Reminder
	}

	/// <summary>
	/// 安全的方法调用器。
	/// </summary>
	internal class SafeCaller
	{
		/// <summary>
		/// 提醒内容的资源名称。
		/// </summary>
		protected string ContentResName = string.Empty;

		/// <summary>
		/// 指示触发异常执行完所有操作后是否退出。
		/// </summary>
		protected bool Exit;

		/// <summary>
		/// 日志的等级。
		/// </summary>
		protected LogLevel LogLevel = LogLevel.Error;

		/// <summary>
		/// 日志的标识。
		/// </summary>
		protected string LogMessage = string.Empty;

		/// <summary>
		/// 日志的类型。
		/// </summary>
		protected CallerLogType LogType = CallerLogType.Normal;

		/// <summary>
		/// 如果发送通知触发了异常则以此作为日志标识。
		/// </summary>
		protected string ReminderMessage = string.Empty;

		/// <summary>
		/// 提醒的类型。
		/// </summary>
		protected CallerReminderType ReminderType = CallerReminderType.None;

		/// <summary>
		/// 调用 <paramref name="action"/> 方法。
		/// </summary>
		/// <param name="action">要调用的方法。</param>
		/// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		public async Task<bool> CallAction(Action action)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUser();
				ExitIfNeed();
				return false;
			}
		}

		/// <summary>
		/// 调用 <paramref name="action"/> 方法并捕获 <typeparamref name="T"/> 类型的异常。
		/// </summary>
		/// <typeparam name="T">要捕获的特定异常。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="level">特定的日志等级。</param>
		/// <param name="message">特定的日志标识。</param>
		/// <param name="func">（可选）特定异常附加的判断条件。</param>
		/// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		public async Task<bool> CallAction<T>(Action action, LogLevel level,
			string message, Func<T, bool>? func = null) where T : Exception
		{
			try
			{
				action();
				return true;
			}
			catch (T ex) when (func is null || func(ex))
			{
				WriteLog(ex, level, message);
				await RemindUser();
				ExitIfNeed();
				return false;
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUser();
				ExitIfNeed();
				return false;
			}
		}

		/// <summary>
		/// 同步调用 <paramref name="action"/> 方法。
		/// </summary>
		/// <remarks>只有当 <see cref="ReminderType"/> 是 <see cref="CallerReminderType.Reminder"/> 时才会提醒用户。</remarks>
		/// <param name="action">要调用的方法。</param>
		/// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		public bool CallActionSync(Action action)
		{
			try
			{
				action();
				return true;
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				RemindUserSync();
				ExitIfNeed();
				return false;
			}
		}

		/// <summary>
		/// 调用带参数的 <paramref name="action"/> 方法。
		/// </summary>
		/// <param name="action">要调用的方法。</param>
		/// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		public async Task<bool> CallActionWithArgs<T>(Action<T> action, T arg)
		{
			try
			{
				action(arg);
				return true;
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUser();
				ExitIfNeed();
				return false;
			}
		}

		/// <summary>
		/// 调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。
		/// </summary>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <returns><see langword="bool"/> 值指示 <paramref name="action"/> 方法是否成功执行， <typeparamref name="TResult"/> 值是返回结果。</returns>
		public async Task<(bool Success, TResult? Result)> CallActionWithReturn
			<TResult>(Func<TResult> action)
		{
			try
			{
				TResult result = action();
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUser();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。
		/// </summary>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <typeparam name="T">参数类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="arg"><paramref name="action"/> 方法的参数。</param>
		/// <returns><see langword="bool"/> 值指示 <paramref name="action"/> 方法是否成功执行， <typeparamref name="TResult"/> 值是返回结果。</returns>
		public async Task<(bool Success, TResult? Result)> CallActionWithReturn
			<T, TResult>(Func<T, TResult> action, T arg)
		{
			try
			{
				TResult result = action(arg);
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUser();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。
		/// </summary>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <typeparam name="T1">参数 1 的类型。</typeparam>
		/// <typeparam name="T2">参数 2 的类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="arg1"><paramref name="action"/> 方法的参数1。</param>
		/// <param name="arg2"><paramref name="action"/> 方法的参数2。</param>
		/// <returns><see langword="bool"/> 值指示 <paramref name="action"/> 方法是否成功执行， <typeparamref name="TResult"/> 值是返回结果。</returns>
		public async Task<(bool Success, TResult? Result)> CallActionWithReturn
			<T1, T2, TResult>(Func<T1, T2, TResult> action, T1 arg1, T2 arg2)
		{
			try
			{
				TResult result = action(arg1, arg2);
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUser();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。
		/// </summary>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <typeparam name="T1">参数 1 的类型。</typeparam>
		/// <typeparam name="T2">参数 2 的类型。</typeparam>
		/// <typeparam name="T3">参数 3 的类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="arg1"><paramref name="action"/> 方法的参数1。</param>
		/// <param name="arg2"><paramref name="action"/> 方法的参数2。</param>
		/// <returns><see langword="bool"/> 值指示 <paramref name="action"/> 方法是否成功执行， <typeparamref name="TResult"/> 值是返回结果。</returns>
		public async Task<(bool Success, TResult? Result)> CallActionWithReturn
			<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> action, T1 arg1, T2 arg2, T3 arg3)
		{
			try
			{
				TResult result = action(arg1, arg2, arg3);
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUser();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。同时捕获 <typeparamref name="TEx"/> 类型的异常。
		/// </summary>
		/// <typeparam name="TEx">要捕获的特定异常。</typeparam>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="level">特定的日志等级。</param>
		/// <param name="message">特定的日志标识。</param>
		/// <param name="func">（可选）特定异常附加的判断条件。</param>
		/// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		public async Task<(bool Success, TResult? Result)> CallActionWithReturn<TEx, TResult>(
			Func<TResult> action, LogLevel level, string message,
			Func<TEx, bool>? func = null) where TEx : Exception
		{
			try
			{
				TResult result = action();
				return (true, result);
			}
			catch (TEx ex) when (func is null || func(ex))
			{
				WriteLog(ex, level, message);
				await RemindUser();
				ExitIfNeed();
				return (false, default(TResult));
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUser();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 异步调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。
		/// </summary>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <typeparam name="T1">参数 1 的类型。</typeparam>
		/// <typeparam name="T2">参数 2 的类型。</typeparam>
		/// <typeparam name="T3">参数 3 的类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="arg1"><paramref name="action"/> 方法的参数1。</param>
		/// <param name="arg2"><paramref name="action"/> 方法的参数2。</param>
		/// <returns><see langword="bool"/> 值指示 <paramref name="action"/> 方法是否成功执行， <typeparamref name="TResult"/> 值是返回结果。</returns>
		public async Task<(bool Success, TResult? Result)> CallActionWithReturnAsync
			<T1, T2, T3, TResult>(Func<T1, T2, T3, Task<TResult>> action, T1 arg1, T2 arg2, T3 arg3)
		{
			try
			{
				TResult result = await action(arg1, arg2, arg3);
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				await RemindUser();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 同步调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。
		/// </summary>
		/// <remarks>只有当 <see cref="ReminderType"/> 是 <see cref="CallerReminderType.Reminder"/> 时才会提醒用户。</remarks>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <returns><see langword="bool"/> 值指示 <paramref name="action"/> 方法是否成功执行， <typeparamref name="TResult"/> 值是返回结果。</returns>
		public (bool Success, TResult? Result) CallActionWithReturnSync
			<TResult>(Func<TResult> action)
		{
			try
			{
				TResult result = action();
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				RemindUserSync();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 同步调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。
		/// </summary>
		/// <remarks>只有当 <see cref="ReminderType"/> 是 <see cref="CallerReminderType.Reminder"/> 时才会提醒用户。</remarks>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <typeparam name="T">参数类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="arg"><paramref name="action"/> 方法的参数。</param>
		/// <returns><see langword="bool"/> 值指示 <paramref name="action"/> 方法是否成功执行， <typeparamref name="TResult"/> 值是返回结果。</returns>
		public (bool Success, TResult? Result) CallActionWithReturnSync
			<T, TResult>(Func<T, TResult> action, T arg)
		{
			try
			{
				TResult result = action(arg);
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				RemindUserSync();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 同步调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。
		/// </summary>
		/// <remarks>只有当 <see cref="ReminderType"/> 是 <see cref="CallerReminderType.Reminder"/> 时才会提醒用户。</remarks>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <typeparam name="T1">参数 1 的类型。</typeparam>
		/// <typeparam name="T2">参数 2 的类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="arg1"><paramref name="action"/> 方法的参数1。</param>
		/// <param name="arg2"><paramref name="action"/> 方法的参数2。</param>
		/// <returns><see langword="bool"/> 值指示 <paramref name="action"/> 方法是否成功执行， <typeparamref name="TResult"/> 值是返回结果。</returns>
		public (bool Success, TResult? Result) CallActionWithReturnSync
			<T1, T2, TResult>(Func<T1, T2, TResult> action, T1 arg1, T2 arg2)
		{
			try
			{
				TResult result = action(arg1, arg2);
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				RemindUserSync();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 同步调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。
		/// </summary>
		/// <remarks>只有当 <see cref="ReminderType"/> 是 <see cref="CallerReminderType.Reminder"/> 时才会提醒用户。</remarks>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <typeparam name="T1">参数 1 的类型。</typeparam>
		/// <typeparam name="T2">参数 2 的类型。</typeparam>
		/// <typeparam name="T3">参数 3 的类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="arg1"><paramref name="action"/> 方法的参数1。</param>
		/// <param name="arg2"><paramref name="action"/> 方法的参数2。</param>
		/// <returns><see langword="bool"/> 值指示 <paramref name="action"/> 方法是否成功执行， <typeparamref name="TResult"/> 值是返回结果。</returns>
		public (bool Success, TResult? Result) CallActionWithReturnSync
			<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> action, T1 arg1, T2 arg2, T3 arg3)
		{
			try
			{
				TResult result = action(arg1, arg2, arg3);
				return (true, result);
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				RemindUserSync();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 同步调用 <paramref name="action"/> 方法并返回 <typeparamref name="TResult"/> 类型的值。同时捕获 <typeparamref name="TEx"/> 类型的异常。
		/// </summary>
		/// <remarks>只有当 <see cref="ReminderType"/> 是 <see cref="CallerReminderType.Reminder"/> 时才会提醒用户。</remarks>
		/// <typeparam name="TEx">要捕获的特定异常。</typeparam>
		/// <typeparam name="TResult">返回值类型。</typeparam>
		/// <param name="action">要调用的方法。</param>
		/// <param name="level">特定的日志等级。</param>
		/// <param name="message">特定的日志标识。</param>
		/// <param name="func">（可选）特定异常附加的判断条件。</param>
		/// <returns>指示 <paramref name="action"/> 方法是否成功执行。</returns>
		public (bool Success, TResult? Result) CallActionWithReturnSync<TEx, TResult>(
			Func<TResult> action, LogLevel level, string message,
			Func<TEx, bool>? func = null) where TEx : Exception
		{
			try
			{
				TResult result = action();
				return (true, result);
			}
			catch (TEx ex) when (func is null || func(ex))
			{
				WriteLog(ex, level, message);
				RemindUserSync();
				ExitIfNeed();
				return (false, default(TResult));
			}
			catch (Exception ex)
			{
				WriteLog(ex);
				RemindUserSync();
				ExitIfNeed();
				return (false, default(TResult));
			}
		}

		/// <summary>
		/// 设置提醒内容的资源名称。
		/// </summary>
		/// <param name="resName">要设置提醒内容的资源名称。</param>
		/// <returns>设置完成的新实例。</returns>
		public SafeCaller SetContentResName(string resName)
		{
			ContentResName = resName;
			return this;
		}

		/// <summary>
		/// 设置触发异常执行完所有操作后是否退出。
		/// </summary>
		/// <param name="exit">指示触发异常执行完所有操作后是否退出。</param>
		/// <returns>设置完成的新实例。</returns>
		public SafeCaller SetExit(bool exit)
		{
			Exit = exit;
			return this;
		}

		/// <summary>
		/// 设置日志类型。
		/// </summary>
		/// <param name="level">要设置的日志类型。</param>
		/// <returns>设置完成的新实例。</returns>
		public SafeCaller SetLogLevel(LogLevel level)
		{
			LogLevel = level;
			return this;
		}

		/// <summary>
		/// 设置日志的标识。
		/// </summary>
		/// <param name="message">要设置的日志的标识。</param>
		/// <returns>设置完成的新实例。</returns>
		public SafeCaller SetLogMessage(string message)
		{
			LogMessage = message;
			return this;
		}

		/// <summary>
		/// 设置日志的类型。
		/// </summary>
		/// <param name="type">要设置的日志的类型。</param>
		/// <returns>设置完成的新实例。</returns>
		public SafeCaller SetLogType(CallerLogType type)
		{
			LogType = type;
			return this;
		}

		/// <summary>
		/// 设置一个 <see cref="string"/> ，如果发送通知触发了异常则以此作为日志标识。
		/// </summary>
		/// <param name="message">要设置的 <see cref="string"/> 。</param>
		/// <returns>设置完成的新实例。</returns>
		public SafeCaller SetReminderMessage(string message)
		{
			ReminderMessage = message;
			return this;
		}

		/// <summary>
		/// 设置提醒的类型。
		/// </summary>
		/// <param name="type">要设置的提醒的类型。</param>
		/// <returns>设置完成的新实例。</returns>
		public SafeCaller SetReminderType(CallerReminderType type)
		{
			ReminderType = type;
			return this;
		}

		/// <summary>
		/// 退出应用（如果需要）。
		/// </summary>
		private void ExitIfNeed()
		{
			if (Exit)
			{
				Environment.Exit(1);
			}
		}

		/// <summary>
		/// 使用 <see cref="ReminderType"/> 中的提醒类型提醒用户。
		/// </summary>
		private async Task RemindUser()
		{
			if (string.IsNullOrWhiteSpace(ReminderMessage) ||
				string.IsNullOrWhiteSpace(ContentResName))
			{
				ReminderType = CallerReminderType.None;
			}
			switch (ReminderType)
			{
				case CallerReminderType.Dialog:
					await ReminderHelper.ShowDialog(
						ReminderMessage,
						App.Loader.GetString("ErrorOrWarningTitle"),
						App.Loader.GetString(ContentResName));
					break;

				case CallerReminderType.Reminder:
					App.CanSend = ReminderHelper.SendReminder(
						ReminderMessage,
						App.Loader.GetString("ErrorOrWarningTitle"),
						App.Loader.GetString(ContentResName));
					break;

				case CallerReminderType.None:
				default:
					break;
			}
		}

		/// <summary>
		/// 只有当 <see cref="ReminderType"/> 是 <see cref="CallerReminderType.Reminder"/> 时才会提醒用户。
		/// </summary>
		private void RemindUserSync()
		{
			switch (ReminderType)
			{
				case CallerReminderType.Reminder:
					App.CanSend = ReminderHelper.SendReminder(
						ReminderMessage,
						App.Loader.GetString("ErrorOrWarningTitle"),
						App.Loader.GetString(ContentResName));
					break;

				case CallerReminderType.Dialog:
				case CallerReminderType.None:
				default:
					break;
			}
		}

		/// <summary>
		/// 写入特定的（崩溃）日志。
		/// </summary>
		/// <param name="ex">要记录的异常。</param>
		/// <param name="level">特定的日志等级。</param>
		/// <param name="message">特定的日志标识。</param>
		private void WriteLog(Exception ex, LogLevel level, string message)
		{
			switch (LogType)
			{
				case CallerLogType.Normal:
					LogSystem.WriteLog(level, $"{message}：{ex}");
					break;

				case CallerLogType.Crash:
					File.WriteAllText(
						$"{DateTime.Now:yyyy-MM-dd_HH+mm+ss}.crash", $"{message}：{ex}");
					break;

				default:
					break;
			}
		}

		/// <summary>
		/// 记录（崩溃）日志。
		/// </summary>
		/// <param name="ex">要记录的异常。</param>
		private void WriteLog(Exception ex)
		{
			switch (LogType)
			{
				case CallerLogType.Normal:
					LogSystem.WriteLog(LogLevel, $"{LogMessage}：{ex}");
					break;

				case CallerLogType.Crash:
					break;

				default:
					break;
			}
		}
	}
}
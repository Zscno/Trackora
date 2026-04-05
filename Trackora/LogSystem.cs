using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Text;

namespace Zscno.Trackora
{
	/// <summary>
	/// 日志等级标识。
	/// </summary>
	internal enum LogLevel
	{
		/// <summary>
		/// 主要输出调试性质的信息。
		/// </summary>
		Debug,

		/// <summary>
		/// 主要记录系统关键信息，旨在保留系统正常工作期间关键运行指标。
		/// </summary>
		Info,

		/// <summary>
		/// 主要输出可预知异常的信息。
		/// </summary>
		Warning,

		/// <summary>
		/// 主要输出不可预知异常的信息。
		/// </summary>
		Error,
	}

	internal static class LogSystem
	{
		/// <summary>
		/// 日志文件路径。
		/// </summary>
		public static string LogFilePath { get; private set; } = string.Empty;

		/// <summary>
		/// 初始化日志文件路径。
		/// </summary>
		public static void InitLogFile()
		{
			string path;
			try
			{
				path = Path.Join(App.LocalCachePath, "Logs");
				if (!Directory.Exists(path))
				{
					_ = Directory.CreateDirectory(path);
				}
			}
			catch (Exception ex)
			{
				throw new Exception("初始化日志文件路径失败。", ex);
			}

			LogFilePath = Path.Join(path, $"{DateTime.Now:yyyy-MM-dd_HH+mm+ss}.log");
		}

		/// <summary>
		/// 写入日志，如果日志文件路径未初始化就立即返回。
		/// </summary>
		/// <param name="level">日志等级。</param>
		/// <param name="message">日志内容。</param>
		public static void WriteLog(LogLevel level, string message)
		{
			if (LogFilePath == string.Empty)
			{
				return;
			}

			string levelString = level switch
			{
				LogLevel.Debug => "[Debug]",
				LogLevel.Info => "[Info]",
				LogLevel.Warning => "[Warning]",
				LogLevel.Error => "[Error]",
				_ => string.Empty,
			};

			_ = new SafeCaller()
			{
				LogType = CallerLogType.Crash,
				ShouldExit = true,
				RemindingMsgResKey = "CannotLaunchApp",
			}.CallMethodR(() =>
			{
				lock (new object())
				{
					File.AppendAllText(LogFilePath,
						DateTime.Now.ToString("[HH:mm:ss.fff]") + levelString + message + "\n", Encoding.UTF8);
				}
			});
		}
	}
}
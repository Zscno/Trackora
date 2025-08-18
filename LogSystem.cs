using System;
using System.IO;
using System.Text;
using Windows.Storage;

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
		/// 日志文件。
		/// </summary>
		public static string LogFilePath { get; private set; }

		/// <summary>
		/// 创建日志文件。
		/// </summary>
		public static void InitLogFile()
		{
			string path = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Logs");
			if (!Directory.Exists(path))
			{
				_ = Directory.CreateDirectory(path);
			}

			LogFilePath = Path.Combine(path, $"{DateTime.Now:yyyy-MM-dd_HH+mm+ss}.log");
		}

		/// <summary>
		/// 写入日志。
		/// </summary>
		/// <param name="level">日志等级。</param>
		/// <param name="message">日志内容。</param>
		public static void WriteLog(LogLevel level, string message)
		{
			string levelString = string.Empty;
			switch (level)
			{
				case LogLevel.Debug:
					levelString = "[Debug]";
					break;

				case LogLevel.Info:
					levelString = "[Info]";
					break;

				case LogLevel.Warning:
					levelString = "[Warning]";
					break;

				case LogLevel.Error:
					levelString = "[Error]";
					break;
			}

			lock (new object())
			{
				File.AppendAllText(LogFilePath, DateTime.Now.ToString("[HH:mm:ss.fff]") + levelString + message + "\n", Encoding.UTF8);
			}
		}
	}
}
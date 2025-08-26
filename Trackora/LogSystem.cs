using System;
using System.IO;
using System.Text;
using Windows.Storage;
using Microsoft.UI.Xaml;

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
				path = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Logs");
				if (!Directory.Exists(path))
				{
					_ = Directory.CreateDirectory(path);
				}
			}
			catch (Exception ex)
			{
				throw new("在准备日志文件目录时触发了异常。", ex);
			}

			LogFilePath = Path.Combine(path, $"{DateTime.Now:yyyy-MM-dd_HH+mm+ss}.log");
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

			try
			{
				lock (new object())
				{
					File.AppendAllText(LogFilePath, DateTime.Now.ToString("[HH:mm:ss.fff]") + levelString + message + "\n", Encoding.UTF8);
				}
			}
			catch (Exception ex)
			{
				// 如果日志写入失败，就把异常信息写到文档目录下的崩溃日志里，尝试发送通知提醒用户并退出。
				File.WriteAllText(
					Path.Join(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
					$"{DateTime.Now:yyyy-MM-dd_HH+mm+ss}.crash"), $"{ex}");
				App.CanSend = ReminderHelper.SendReminder("提示用户无法启动应用", "Error Tip",
					"We can't launch the app. Contact the author for help please.", true);
				Application.Current.Exit();
			}
		}
	}
}
using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using WinRT.Interop;
using static Zscno.Trackora.App;

namespace Zscno.Trackora;

/// <summary>
/// Win32 API + 文件系统的封装。
/// </summary>
internal static class SystemHelper
{
	/// <summary>
	/// 进程没有程序包标识符。
	/// </summary>
	public const int APPMODEL_ERROR_NO_PACKAGE = 15700;

	/// <summary>
	/// 访问被拒绝。
	/// </summary>
	public const int ERROR_ACCESS_DENIED = 5;

	/// <summary>
	/// 缓冲区不够大，无法保存数据。
	/// </summary>
	public const int ERROR_INSUFFICIENT_BUFFER = 122;

	/// <summary>
	/// 操作成功完成。
	/// </summary>
	public const int ERROR_SUCCESS = 0;

	/// <summary>
	/// 最小化窗口，即使拥有窗口的线程没有响应。仅当最小化不同线程的窗口时，才应使用此标志。
	/// </summary>
	public const int SW_FORCEMINIMIZE = 11;

	/// <summary>
	/// 隐藏窗口并激活另一个窗口。
	/// </summary>
	public const int SW_HIDE = 0;

	/// <summary>
	/// 最小化指定的窗口，并按 Z 顺序激活下一个顶级窗口。
	/// </summary>
	public const int SW_MINIMIZE = 6;

	/// <summary>
	/// 激活并显示窗口。如果窗口最小化、最大化或排列，系统会将其还原到其原始大小和位置。 还原最小化窗口时，应用程序应指定此标志。
	/// </summary>
	public const int SW_RESTORE = 9;

	/// <summary>
	/// 激活窗口并以当前大小和位置显示窗口。
	/// </summary>
	public const int SW_SHOW = 5;

	/// <summary>
	/// 根据启动应用程序的程序传递给 CreateProcess 函数的 STARTUPINFO 结构中指定的SW_值设置显示状态。
	/// </summary>
	public const int SW_SHOWDEFAULT = 10;

	/// <summary>
	/// 激活窗口并显示最大化的窗口。
	/// </summary>
	public const int SW_SHOWMAXIMIZED = 3;

	/// <summary>
	/// 激活窗口并将其显示为最小化窗口。
	/// </summary>
	public const int SW_SHOWMINIMIZED = 2;

	/// <summary>
	/// 将窗口显示为最小化窗口。此值类似于 <see cref="SW_SHOWMINIMIZED"/>，但窗口未激活。
	/// </summary>
	public const int SW_SHOWMINNOACTIVE = 7;

	/// <summary>
	/// 以当前大小和位置显示窗口。此值类似于 <see cref="SW_SHOW"/>，只是窗口未激活。
	/// </summary>
	public const int SW_SHOWNA = 8;

	/// <summary>
	/// 以最近的大小和位置显示窗口。此值类似于 <see cref="SW_SHOWNORMAL"/> ，只是窗口未激活。
	/// </summary>
	public const int SW_SHOWNOACTIVATE = 4;

	/// <summary>
	/// 激活并显示窗口。如果窗口最小化、最大化或排列，系统会将其还原到其原始大小和位置。应用程序应在首次显示窗口时指定此标志。
	/// </summary>
	public const int SW_SHOWNORMAL = 1;

	/// <summary>
	/// 将指定的窗口置于 Z 顺序的顶部。如果窗口是顶级窗口，则会激活它。如果窗口是子窗口，则会激活与子窗口关联的顶级父窗口。
	/// </summary>
	/// <param name="hWnd">要置于 Z 顺序顶部的窗口的句柄。</param>
	/// <returns>如果该函数成功，则返回值为非零值。如果函数失败，则返回值为零。 要获得更多的错误信息，请调用 <see cref="Marshal.GetLastWin32Error"/>。</returns>
	[DllImport("user32.dll")]
	public static extern bool BringWindowToTop(IntPtr hWnd);

	/// <summary>
	/// 删除指定文件夹中的所有文件。
	/// </summary>
	/// <param name="foldersPath">指定文件夹的路径。</param>
	public static void DeleteAllFiles(params string[] foldersPath)
	{
		foreach (string folderPath in foldersPath)
		{
			DirectoryInfo info;
			try
			{
				info = new(folderPath);
			}
			catch (Exception ex)
			{
				//LogSystem.WriteLog(LogLevel.Error, $"在获取文件夹 [{folderPath}] 信息时触发异常：{ex}");
				throw new Exception($"在获取文件夹 [{folderPath}] 信息时触发了异常。", ex);
			}

			foreach (FileInfo file in info.GetFiles("*", SearchOption.AllDirectories))
			{
				try
				{
					if (file.FullName != LogSystem.LogFilePath)
					{
						file.Delete();
					}
				}
				catch (Exception ex)
				{
					try
					{
						string filePath = file.FullName;
						LogSystem.WriteLog(LogLevel.Error, $"在删除文件夹中的文件 [{filePath}] 时触发异常：{ex}");
					}
					catch (Exception)
					{
						// 如果无法获取文件路径就使用文件夹路径。
						LogSystem.WriteLog(LogLevel.Error, $"在删除文件夹 [{folderPath}] 中的文件时触发异常：{ex}");
					}
				}
			}
		}
	}

	/// <summary>
	/// 检索其类名称和窗口名称与指定字符串匹配的窗口的句柄。
	/// </summary>
	/// <param name="hWndParent">要搜索其子窗口的父窗口的句柄。</param>
	/// <param name="hWndChildAfter">子窗口的句柄。</param>
	/// <param name="lpszClass">指定窗口类名。</param>
	/// <param name="lpszWindow">窗口名称（窗口的标题）。</param>
	/// <returns>如果函数成功，则返回值是具有指定类和窗口名称的窗口的句柄。如果函数失败，则返回值 <see langword="null"/>。</returns>
	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	public static extern IntPtr FindWindowEx(IntPtr hWndParent, IntPtr hWndChildAfter, string lpszClass, string lpszWindow);

	/// <summary>
	/// 检索指定窗口所属的类的名称。
	/// </summary>
	/// <param name="hWnd">窗口的句柄，以及窗口所属的类的间接句柄。</param>
	/// <param name="lpClassName">类名字符串。</param>
	/// <param name="nMaxCount"><paramref name="lpClassName"/> 缓冲区的长度（以字符为单位）。缓冲区必须足够大，才能包含终止 <see langword="null"/> 字符；否则，类名字符串将被截断为 <paramref name="nMaxCount"/>-1 字符。</param>
	/// <returns>如果函数成功，则返回值是复制到缓冲区的字符数，不包括终止 <see langword="null"/> 字符。如果函数失败，则返回值为零。</returns>
	[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

	/// <summary>
	/// 获取指定文件夹大小的格式化字符串。
	/// </summary>
	/// <param name="path">指定文件夹的路径。</param>
	/// <returns>格式化字符串。</returns>
	public static string GetFolderSize(string path)
	{
		double size = 0;
		DirectoryInfo info;
		try
		{
			info = new(path);
		}
		catch (Exception ex)
		{
			//LogSystem.WriteLog(LogLevel.Error, $"在获取文件夹 [{path}] 信息时触发异常：{ex}");
			throw new Exception($"在获取文件夹 [{path}] 信息时触发了异常。", ex);
		}

		foreach (FileInfo file in info.GetFiles("*", SearchOption.AllDirectories))
		{
			try
			{
				size += file.Length;
			}
			catch (Exception ex)
			{
				try
				{
					string filePath = file.FullName;
					LogSystem.WriteLog(LogLevel.Error, $"在获取文件夹中的文件 [{filePath}] 大小时触发异常：{ex}");
				}
				catch (Exception)
				{
					// 如果无法获取文件路径就使用文件夹路径。
					LogSystem.WriteLog(LogLevel.Error, $"在获取文件夹 [{path}] 中的文件大小时触发异常：{ex}");
				}
			}
		}

		string[] sizes = { "B", "KB", "MB", "GB" };
		int count = 0;
		while (size >= 1024 && count < sizes.Length - 1)
		{
			count++;
			size /= 1024;
		}

		return $"{size:F2} {sizes[count]}";
	}

	/// <summary>
	/// 检索前台窗口的句柄，(用户当前正在使用) 窗口。
	/// </summary>
	/// <returns>返回值是前台窗口的句柄。在某些情况下（例如，当窗口丢失激活时），前台窗口可以为 <see langword="null"/> 。</returns>
	[DllImport("user32.dll")]
	public static extern IntPtr GetForegroundWindow();

	/// <summary>
	/// 获取指定进程的包标识符 (ID)。
	/// </summary>
	/// <param name="hProcess">具有 <c>PROCESS_QUERY_INFORMATION</c> 或 <c>PROCESS_QUERY_LIMITED_INFORMATION</c> 访问权限的进程句柄。</param>
	/// <param name="packageFullNameLength">输入时， <paramref name="packageFullName"/> 缓冲区的大小（以字节为单位）。输出时，返回包全名的大小（以字节为单位）。</param>
	/// <param name="packageFullName">包全名。</param>
	/// <returns>如果函数成功，则返回 <c>ERROR_SUCCESS</c>。否则，函数将返回错误代码。</returns>
	[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
	public static extern long GetPackageFullName(IntPtr hProcess, ref uint packageFullNameLength, StringBuilder packageFullName);

	/// <summary>
	/// 检索创建指定窗口的线程的标识符，以及创建该窗口的进程（可选）的标识符。
	/// </summary>
	/// <param name="hWnd">窗口的句柄。</param>
	/// <param name="lpdwProcessId">指向接收进程标识符的变量的指针。函数会将进程的标识符复制到变量。如果函数失败，则变量的值保持不变。</param>
	/// <returns>如果函数成功，则返回值是创建窗口的线程的标识符。如果窗口句柄无效，则返回值为零。</returns>
	[DllImport("user32.dll", SetLastError = true)]
	public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

	/// <summary>
	/// （自行封装）隐藏窗口。
	/// </summary>
	/// <param name="window">要隐藏的窗口。</param>
	public static void HideWindow(Window window)
	{
		IntPtr hwnd = WindowNative.GetWindowHandle(window);
		_ = ShowWindow(hwnd, SW_HIDE);
	}

	/// <summary>
	/// 将创建指定窗口的线程引入前台并激活窗口。键盘输入将定向到窗口，并为用户更改各种视觉提示。
	/// </summary>
	/// <param name="hWnd">应激活并带到前台的窗口的句柄。</param>
	/// <returns>如果窗口已带到前台，则返回值为非零值。如果未将窗口带到前台，则返回值为零。</returns>
	[DllImport("user32.dll")]
	public static extern bool SetForegroundWindow(IntPtr hWnd);

	/// <summary>
	/// 设置指定窗口的显示状态。
	/// </summary>
	/// <param name="hWnd">窗口的句柄。</param>
	/// <param name="nCmdShow">控制窗口的显示方式。 如果启动应用程序的程序提供 <c>STARTUPINFO</c> 结构，则应用程序首次调用函数时将忽略此参数。 否则，首次调用函数时，该值应为 <c>WinMain</c> 函数在其 <c>nCmdShow</c> 参数中获取的值。</param>
	/// <returns>如果窗口以前可见，则返回值为非零值。 如果以前隐藏窗口，则返回值为零。</returns>
	[DllImport("user32.dll")]
	public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
}

/// <summary>
/// 提醒通知辅助类。
/// </summary>
internal class ReminderHelper
{
	/// <summary>
	/// 发送指定类型的通知。
	/// </summary>
	/// <param name="reminderKind">通知的类型。</param>
	/// <returns>指示通知是否能正常显示。</returns>
	public static bool SendReminder(ReminderKinds reminderKind)
	{
		string logInfo;
		string logError;
		string title;
		string content;
		ToastAudio audio = new();

		switch (reminderKind)
		{
			case ReminderKinds.TotalUsedTimeReminders:
				logInfo = $"总使用时长已达设置中的值" +
					$" [{(TimeSpan) LocalSettings["TotalUsedRemindTime"]:hh\\:mm\\:ss}] 。";
				logError = "在发送 总使用时间提醒通知 时触发异常：";
				title = Loader.GetString("UsedTimeReminderTitle");
				content = Loader.GetString("TotalReminderText1") +
					WindowTracker.GetLocalTime(WindowTracker.TotalUsedTime) +
					Loader.GetString("TotalReminderText2");
				audio.Src = new(CommonSounds[(string) LocalSettings["TotalUsedTimeSound"]]);
				break;

			case ReminderKinds.TotalUsedTimeSoundTest:
				logInfo = string.Empty;
				logError = "在发送 总使用时长提醒提示音的测试通知 时触发异常：";
				title = Loader.GetString("Test/Content");
				content = Loader.GetString("TestContent") + (string) LocalSettings["TotalUsedTimeSound"];
				audio.Src = new(CommonSounds[(string) LocalSettings["TotalUsedTimeSound"]]);
				break;

			case ReminderKinds.ContinuousUsedTimeReminders:
				logInfo = $"连续使用时长已达设置中的值" +
					$" [{(TimeSpan) LocalSettings["ContinuousUsedRemindTime"]:hh\\:mm\\:ss}] 。";
				logError = "在发送 连续使用时间提醒通知 时触发异常：";
				title = Loader.GetString("UsedTimeReminderTitle");
				content = Loader.GetString("ContinuousReminderText1") +
					WindowTracker.GetLocalTime((TimeSpan) LocalSettings["ContinuousUsedRemindTime"]) +
					Loader.GetString("ContinuousReminderText2");
				audio.Src = new Uri(CommonSounds[(string) LocalSettings["ContinuousUsedTimeSound"]]);
				break;

			case ReminderKinds.ContinuousUsedTimeSoundTest:
				logInfo = string.Empty;
				logError = "在发送 连续使用时长提醒提示音的测试通知 时触发异常：";
				title = Loader.GetString("Test/Content");
				content = Loader.GetString("TestContent") + (string) LocalSettings["ContinuousUsedTimeSound"];
				audio.Src = new Uri(CommonSounds[(string) LocalSettings["ContinuousUsedTimeSound"]]);
				break;

			case ReminderKinds.EndUsingTimeReminders:
				logInfo = $"结束使用时间已达设置中的值：{WindowTracker.EndUsingTime:hh\\:mm\\:ss}";
				logError = "在发送 结束使用时间提醒通知 时触发异常：";
				title = Loader.GetString("EndUsingReminderTitle");
				content = Loader.GetString("EndUsingReminderText1") +
					WindowTracker.GetLocalTime(WindowTracker.EndUsingTime) +
					Loader.GetString("EndUsingReminderText2");
				audio.Src = new Uri(AlarmSounds[(string) LocalSettings["EndUsingTimeSound"]]);
				break;

			case ReminderKinds.EndUsingTimeSoundTest:
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
	public static bool SendReminder(string exMessage, string title, string content, bool isExit = false,
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
	/// <param name="xamlRoot">显示该对话框的 <see cref="XamlRoot"/>。</param>
	/// <param name="title">对话框标题。</param>
	/// <param name="content">对话框内容。</param>
	public static async Task ShowDialog(XamlRoot xamlRoot, string title, string content)
	{
		ContentDialog dialog = new()
		{
			XamlRoot = xamlRoot,
			Title = title,
			Content = content,
			CloseButtonText = Loader.GetString("Cancel"),
			PrimaryButtonText = Loader.GetString("OK/Content"),
			DefaultButton = ContentDialogButton.Primary
		};
		_ = await dialog.ShowAsync();
	}
}

/// <summary>
/// 提醒通知类型。
/// </summary>
internal enum ReminderKinds
{
	/// <summary>
	/// 总使用时长提醒。
	/// </summary>
	TotalUsedTimeReminders,

	/// <summary>
	/// 总使用时长提醒提示音的测试通知。
	/// </summary>
	TotalUsedTimeSoundTest,

	/// <summary>
	/// 连续使用时长提醒。
	/// </summary>
	ContinuousUsedTimeReminders,

	/// <summary>
	/// 连续使用时长提醒提示音的测试通知。
	/// </summary>
	ContinuousUsedTimeSoundTest,

	/// <summary>
	/// 结束使用时间提醒。
	/// </summary>
	EndUsingTimeReminders,

	/// <summary>
	/// 结束使用时间提醒提示音的测试通知。
	/// </summary>
	EndUsingTimeSoundTest,
}
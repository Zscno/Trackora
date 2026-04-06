using Microsoft.UI.Xaml;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Serialization;
using WinRT.Interop;

namespace Zscno.Trackora
{
	/// <summary>
	/// 包含所需原生 Windows API 的 P/Invoke 声明和相关常量。
	/// </summary>
	internal static partial class NativeApi
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
		/// <returns>如果该函数成功，则返回值为非零值。如果函数失败，则返回值为零。/&gt;。</returns>
		[LibraryImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static partial bool BringWindowToTop(nint hWnd);

		/// <summary>
		/// 检索其类名称和窗口名称与指定字符串匹配的窗口的句柄。
		/// </summary>
		/// <param name="hWndParent">要搜索其子窗口的父窗口的句柄。</param>
		/// <param name="hWndChildAfter">子窗口的句柄。</param>
		/// <param name="lpszClass">指定窗口类名。</param>
		/// <param name="lpszWindow">窗口名称（窗口的标题）。</param>
		/// <returns>如果函数成功，则返回值是具有指定类和窗口名称的窗口的句柄。如果函数失败，则返回值 <see langword="null"/>。</returns>
		[LibraryImport("user32.dll", EntryPoint = "FindWindowExW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
		public static partial nint FindWindowEx(
			nint hWndParent,
			nint hWndChildAfter,
			string? lpszClass,
			string? lpszWindow);

		/// <summary>
		/// 检索指定窗口所属的类的名称。
		/// </summary>
		/// <param name="hWnd">窗口的句柄，以及窗口所属的类的间接句柄。</param>
		/// <param name="lpClassName">类名字符串。</param>
		/// <param name="nMaxCount"><paramref name="lpClassName"/> 缓冲区的长度（以字符为单位）。缓冲区必须足够大，才能包含终止 <see langword="null"/> 字符；否则，类名字符串将被截断为 <paramref name="nMaxCount"/>-1 字符。</param>
		/// <returns>如果函数成功，则返回值是复制到缓冲区的字符数，不包括终止 <see langword="null"/> 字符。如果函数失败，则返回值为零。</returns>
		[DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern int GetClassName(nint hWnd, StringBuilder lpClassName, int nMaxCount);

		/// <summary>
		/// 检索前台窗口的句柄，(用户当前正在使用) 窗口。
		/// </summary>
		/// <returns>返回值是前台窗口的句柄。在某些情况下（例如，当窗口丢失激活时），前台窗口可以为 <see langword="null"/> 。</returns>
		[LibraryImport("user32.dll")]
		public static partial nint GetForegroundWindow();

		/// <summary>
		/// 获取指定进程的包标识符 (ID)。
		/// </summary>
		/// <param name="hProcess">具有 <c>PROCESS_QUERY_INFORMATION</c> 或 <c>PROCESS_QUERY_LIMITED_INFORMATION</c> 访问权限的进程句柄。</param>
		/// <param name="packageFullNameLength">输入时， <paramref name="packageFullName"/> 缓冲区的大小（以字节为单位）。输出时，返回包全名的大小（以字节为单位）。</param>
		/// <param name="packageFullName">包全名。</param>
		/// <returns>如果函数成功，则返回 <c>ERROR_SUCCESS</c>。否则，函数将返回错误代码。</returns>
		[DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
		public static extern long GetPackageFullName(
			nint hProcess,
			ref uint packageFullNameLength,
			StringBuilder packageFullName);

		/// <summary>
		/// 检索创建指定窗口的线程的标识符，以及创建该窗口的进程（可选）的标识符。
		/// </summary>
		/// <param name="hWnd">窗口的句柄。</param>
		/// <param name="lpdwProcessId">指向接收进程标识符的变量的指针。函数会将进程的标识符复制到变量。如果函数失败，则变量的值保持不变。</param>
		/// <returns>如果函数成功，则返回值是创建窗口的线程的标识符。如果窗口句柄无效，则返回值为零。</returns>
		[LibraryImport("user32.dll", SetLastError = true)]
		public static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

		/// <summary>
		/// （自行封装）隐藏窗口。
		/// </summary>
		/// <param name="window">要隐藏的窗口。</param>
		public static void HideWindow(Window window)
		{
			nint hwnd = WindowNative.GetWindowHandle(window);
			_ = ShowWindow(hwnd, SW_HIDE);
		}

		/// <summary>
		/// 将创建指定窗口的线程引入前台并激活窗口。键盘输入将定向到窗口，并为用户更改各种视觉提示。
		/// </summary>
		/// <param name="hWnd">应激活并带到前台的窗口的句柄。</param>
		/// <returns>如果窗口已带到前台，则返回值为非零值。如果未将窗口带到前台，则返回值为零。</returns>
		[LibraryImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static partial bool SetForegroundWindow(nint hWnd);

		/// <summary>
		/// 设置指定窗口的显示状态。
		/// </summary>
		/// <param name="hWnd">窗口的句柄。</param>
		/// <param name="nCmdShow">控制窗口的显示方式。 如果启动应用程序的程序提供 <c>STARTUPINFO</c> 结构，则应用程序首次调用函数时将忽略此参数。 否则，首次调用函数时，该值应为 <c>WinMain</c> 函数在其 <c>nCmdShow</c> 参数中获取的值。</param>
		/// <returns>如果窗口以前可见，则返回值为非零值。 如果以前隐藏窗口，则返回值为零。</returns>
		[LibraryImport("user32.dll")]
		[return: MarshalAs(UnmanagedType.Bool)]
		public static partial bool ShowWindow(nint hWnd, int nCmdShow);
	}

	/// <summary>
	/// 提供有关与 JSON 序列化相关的一组类型的元数据。
	/// </summary>
	[JsonSerializable(typeof(List<ProcessInfo>))]
	internal partial class JsonSerializeMetadata : JsonSerializerContext
	{
	}
}
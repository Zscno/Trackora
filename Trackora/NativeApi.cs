using System.Runtime.InteropServices;
using System.Text;

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
        /// 前景窗口已更改。即使前台窗口已更改为同一线程中的另一个窗口，系统也会发送此事件。服务器应用程序从不发送该事件。
        /// <para>对于此事件，WinEventProc 回调函数的 hwnd 参数是前台窗口的句柄，idObject 参数 OBJID_WINDOW，idChild 参数 CHILDID_SELF。</para>
        /// </summary>
		public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;

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

        public const uint WINEVENT_OUTOFCONTEXT = 0x0000;

        /// <summary>
        /// 检索与类关联的图标的句柄。
        /// </summary>
        internal const int GCLP_HICON = -14;

        /// <summary>
        /// 检索与类关联的小图标的句柄。
        /// </summary>
        internal const int GCLP_HICONSM = -34;

        /// <summary>
        /// 检索窗口的大图标。
        /// </summary>
        internal const nint ICON_BIG = 1;

        /// <summary>
        /// 检索窗口的小图标。
        /// </summary>
        internal const nint ICON_SMALL = 0;

        /// <summary>
        /// 检索应用程序提供的小图标。如果应用程序未提供，系统将使用该窗口的系统生成的图标。
        /// </summary>
        internal const nint ICON_SMALL2 = 2;

        /// <summary>
        /// 对于检索有关进程的某些信息是必需的。
        /// </summary>
        internal const int PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

        /// <summary>
        /// 发送到窗口以检索与窗口关联的大图标或小图标的句柄。系统在 Alt+Tab 对话框中显示大图标，窗口描述文字显示小图标。
        /// </summary>
        internal const int WM_GETICON = 0x007f;

        /// <summary>
        /// 发送到最小化 (图标) 窗口。该窗口即将由用户拖动，但没有为其类定义图标。应用程序可以将句柄返回到图标或光标。当用户拖动图标时，系统将显示此光标或图标。
        /// </summary>
        internal const int WM_QUERYDRAGICON = 0x0037;

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
        /// <param name="hWndParent">    要搜索其子窗口的父窗口的句柄。</param>
        /// <param name="hWndChildAfter">子窗口的句柄。</param>
        /// <param name="lpszClass">     指定窗口类名。</param>
        /// <param name="lpszWindow">    窗口名称（窗口的标题）。</param>
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
        /// <param name="hWnd">       窗口的句柄，以及窗口所属的类的间接句柄。</param>
        /// <param name="lpClassName">类名字符串。</param>
        /// <param name="nMaxCount">  <paramref name="lpClassName"/> 缓冲区的长度（以字符为单位）。缓冲区必须足够大，才能包含终止 <see langword="null"/> 字符；否则，类名字符串将被截断为 <paramref name="nMaxCount"/>-1 字符。</param>
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
        /// <param name="hProcess">             具有 <c>PROCESS_QUERY_INFORMATION</c> 或 <c>PROCESS_QUERY_LIMITED_INFORMATION</c> 访问权限的进程句柄。</param>
        /// <param name="packageFullNameLength">输入时， <paramref name="packageFullName"/> 缓冲区的大小（以字节为单位）。输出时，返回包全名的大小（以字节为单位）。</param>
        /// <param name="packageFullName">      包全名。</param>
        /// <returns>如果函数成功，则返回 <c>ERROR_SUCCESS</c>。否则，函数将返回错误代码。</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        public static extern long GetPackageFullName(
            nint hProcess,
            ref uint packageFullNameLength,
            StringBuilder packageFullName);

        /// <summary>
        /// 检索创建指定窗口的线程的标识符，以及创建该窗口的进程（可选）的标识符。
        /// </summary>
        /// <param name="hWnd">         窗口的句柄。</param>
        /// <param name="lpdwProcessId">指向接收进程标识符的变量的指针。函数会将进程的标识符复制到变量。如果函数失败，则变量的值保持不变。</param>
        /// <returns>如果函数成功，则返回值是创建窗口的线程的标识符。如果窗口句柄无效，则返回值为零。</returns>
        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial uint GetWindowThreadProcessId(nint hWnd, out uint lpdwProcessId);

        /// <summary>
        /// 将创建指定窗口的线程引入前台并激活窗口。键盘输入将定向到窗口，并为用户更改各种视觉提示。
        /// </summary>
        /// <param name="hWnd">应激活并带到前台的窗口的句柄。</param>
        /// <returns>如果窗口已带到前台，则返回值为非零值。如果未将窗口带到前台，则返回值为零。</returns>
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool SetForegroundWindow(nint hWnd);

        /// <summary>
        /// 为一系列事件设置事件挂钩函数。
        /// </summary>
        /// <param name="eventMin">        指定挂钩函数处理的事件范围中最低事件值的事件 常量 。 此参数可以设置为 EVENT_MIN ，以指示可能的最低事件值。</param>
        /// <param name="eventMax">        指定由挂钩函数处理的事件范围中最高事件值的事件常量。 此参数可以设置为 EVENT_MAX ，以指示可能的最高事件值。</param>
        /// <param name="hmodWinEventProc">如果在 dwFlags 参数中指定了WINEVENT_INCONTEXT标志，则为包含 <paramref name="lpfnWinEventProc"/> 中的挂钩函数的 DLL 的句柄。 如果挂钩函数不位于 DLL 中，或者指定了WINEVENT_OUTOFCONTEXT标志，则此参数为 <see langword="null"/>。</param>
        /// <param name="lpfnWinEventProc">指向事件挂钩函数的指针。</param>
        /// <param name="idProcess">       指定挂钩函数从中接收事件的进程的 ID。 指定零从当前桌面上的所有进程接收事件。</param>
        /// <param name="idThread">        指定挂钩函数从中接收事件的线程的 ID。 如果此参数为零，则挂钩函数与当前桌面上的所有现有线程相关联。</param>
        /// <param name="dwFlags">         标记值，用于指定要跳过的挂钩函数和事件的位置。</param>
        /// <returns>如果成功，则返回一个 <see langword="nint"/> 值，该值标识此事件挂钩实例。 应用程序保存此返回值，以便将其与 UnhookWinEvent 函数一起使用。如果不成功，则返回零。</returns>
        [LibraryImport("user32.dll", SetLastError = true)]
        public static partial nint SetWinEventHook(
            uint eventMin, uint eventMax,
            nint hmodWinEventProc,
            WinEventDelegate lpfnWinEventProc,
            uint idProcess, uint idThread, uint dwFlags);

        /// <summary>
        /// 设置指定窗口的显示状态。
        /// </summary>
        /// <param name="hWnd">    窗口的句柄。</param>
        /// <param name="nCmdShow">控制窗口的显示方式。 如果启动应用程序的程序提供 <c>STARTUPINFO</c> 结构，则应用程序首次调用函数时将忽略此参数。 否则，首次调用函数时，该值应为 <c>WinMain</c> 函数在其 <c>nCmdShow</c> 参数中获取的值。</param>
        /// <returns>如果窗口以前可见，则返回值为非零值。 如果以前隐藏窗口，则返回值为零。</returns>
        [LibraryImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool ShowWindow(nint hWnd, int nCmdShow);

        /// <summary>
        /// 删除先前对 <see cref="SetWinEventHook(uint, uint, nint, WinEventDelegate, uint, uint, uint)"/> 的调用所创建的事件挂钩函数。
        /// </summary>
        /// <param name="hWinEventHook">对 <see cref="SetWinEventHook(uint, uint, nint, WinEventDelegate, uint, uint, uint)"/> 的上一次调用中返回的事件挂钩的句柄。</param>
        /// <returns>如果成功，则返回 <see langword="true"/>;否则，返回 <see langword="false"/>。</returns>
        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static partial bool UnhookWinEvent(nint hWinEventHook);

        /// <summary>
        /// 关闭打开的对象句柄。
        /// </summary>
        /// <param name="hObject">打开对象的有效句柄。</param>
        /// <returns>如果该函数成功，则返回值为非零值。如果函数失败，则返回值为零。</returns>
        [LibraryImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool CloseHandle(nint hObject);

        /// <summary>
        /// 销毁图标并释放图标占用的任何内存。
        /// </summary>
        /// <param name="hIcon">要销毁的图标的句柄。</param>
        /// <returns>如果该函数成功，则返回值为非零值。如果函数失败，则返回值为零。</returns>
        [LibraryImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static partial bool DestroyIcon(nint hIcon);

        /// <summary>
        /// 获取指定进程的应用程序用户模型 ID 。
        /// </summary>
        /// <param name="hProcess">                    进程的句柄。 此句柄必须具有 <see cref="PROCESS_QUERY_LIMITED_INFORMATION"/> 访问权限。</param>
        /// <param name="applicationUserModelIdLength">输入时， <paramref name="applicationUserModelId"/> 缓冲区的大小（以宽字符为单位）。成功时，使用的缓冲区大小，包括 <see langword="null"/> 终止符。</param>
        /// <param name="applicationUserModelId">      指向接收应用程序用户模型 ID 的缓冲区的指针。</param>
        /// <returns>如果该函数成功，则返回 <see cref="ERROR_SUCCESS"/>。否则，该函数将返回错误代码。</returns>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern long GetApplicationUserModelId(nint hProcess,
                                                              ref uint applicationUserModelIdLength,
                                                              StringBuilder applicationUserModelId);

        /// <summary>
        /// 从与指定窗口关联的 <c>WNDCLASSEX</c> 结构中检索指定的值。
        /// </summary>
        /// <param name="hWnd">  窗口的句柄，间接地是窗口所属的类。</param>
        /// <param name="nIndex">要检索的值。若要从额外的类内存中检索值，请指定要检索的值的正、从零开始的字节偏移量。有效值为零到额外类内存的字节数（减 8）;例如，如果指定了 24 个或更多字节的额外类内存，则值 16 将是第三个整数的索引。</param>
        /// <returns></returns>
        [LibraryImport("user32.dll", EntryPoint = "GetClassLongPtrW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial nint GetClassLongPtr(nint hWnd, int nIndex);

        /// <summary>
        /// 打开现有的本地进程对象。
        /// </summary>
        /// <param name="dwDesiredAccess">对进程对象的访问。针对进程的安全描述符检查此访问权限。此参数可以是一个或多个进程访问权限。</param>
        /// <param name="bInheritHandle"> 如果此值为 <see langword="true"/>，则此进程创建的进程将继承句柄。否则，进程不会继承此句柄。</param>
        /// <param name="dwProcessId">    要打开的本地进程的标识符。</param>
        /// <returns>如果函数成功，则返回值是指定进程的打开句柄。如果函数失败，则返回值为 <see langword="null"/>。</returns>
        [LibraryImport("kernel32.dll", SetLastError = true)]
        internal static partial nint OpenProcess(uint dwDesiredAccess,
                                                 [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
                                                 uint dwProcessId);

        /// <summary>
        /// 将指定的消息发送到一个或多个窗口。该方法调用指定窗口的窗口过程，在窗口过程处理消息之前不会返回。
        /// </summary>
        /// <param name="hWnd">  
        /// 窗口的句柄，其窗口过程将接收消息。
        /// <para>如果此参数为 <c>HWND_BROADCAST(0xffff)</c>，则消息将发送到系统中的所有顶级窗口，包括禁用或不可见的无所有者窗口、重叠窗口和弹出窗口;但消息不会发送到子窗口。</para>
        /// 消息发送受 UIPI 约束。进程线程只能将消息发送到完整性级别较低或相等进程的线程的消息队列。
        /// </param>
        /// <param name="msg">   要发送的消息。</param>
        /// <param name="wParam">其他的消息特定信息。</param>
        /// <param name="lParam">其他的消息特定信息。</param>
        /// <returns>返回值指定消息处理的结果；这取决于发送的消息。</returns>
        [LibraryImport("user32.dll", EntryPoint = "SendMessageW", SetLastError = true, StringMarshalling = StringMarshalling.Utf16)]
        internal static partial nint SendMessage(nint hWnd, uint msg, nint wParam, nint lParam);
    }
}
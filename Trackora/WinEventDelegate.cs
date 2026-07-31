namespace Zscno.Trackora
{
    /// <summary>
    /// 用于声明指向应用程序定义的回调函数的指针，系统在响应可访问对象生成的事件时调用该函数。
    /// </summary>
    /// <param name="hWinEventHook">句柄映射为事件钩子实例。当 <see cref="NativeApi.SetWinEventHook(uint, uint, nint, WinEventDelegate, uint, uint, uint)"/> 安装钩子函数时，该值会返回，并且针对每个钩子函数实例特有。</param>
    /// <param name="eventType">指定发生的事件。该值是事件常数之一。</param>
    /// <param name="hwnd">Handle 映射到生成事件的窗口，若无窗口关联事件则使用 <see langword="null"/>; 例如，鼠标指针不关联窗口。</param>
    /// <param name="idObject">标识与事件相关的对象。这要么是对象标识符之一，要么是自定义对象ID。</param>
    /// <param name="idChild">标识事件是由对象还是对象的子元素触发。如果该值CHILDID_SELF，则该事件是由对象触发的;如果该值是子ID，则该事件是由子元素触发的。</param>
    /// <param name="dwEventThread">标识生成事件的线程，或当前窗口的线程。</param>
    /// <param name="dwmsEventTime">指定事件生成的时间（毫秒）。</param>
    public delegate void WinEventDelegate(
        nint hWinEventHook, 
        uint eventType, 
        nint hwnd, 
        int idObject, 
        int idChild,
        uint dwEventThread, 
        uint dwmsEventTime);
}
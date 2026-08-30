using Microsoft.UI.Xaml;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using WinRT.Interop;

namespace Zscno.Trackora.Tools
{
    internal static partial class NativeApi
    {
        /// <summary>
        /// 隐藏指定的窗口。
        /// </summary>
        /// <param name="window">窗口的 <see cref="Window"/> 实例。</param>
        public static void HideWindow(Window window)
        {
            nint hwnd = WindowNative.GetWindowHandle(window);
            _ = ShowWindow(hwnd, SW_HIDE);
        }

        /// <summary>
        /// 尝试获取指定窗口的子窗口句柄。
        /// </summary>
        /// <param name="parentHandle">指定窗口的句柄。</param>
        /// <param name="childHandle"> 获取到的子窗口句柄。</param>
        /// <param name="className">   指定要获取子窗口的类名。</param>
        /// <returns>指示是否获取成功。</returns>
        public static bool TryGetChildWindowHandle(nint parentHandle, out nint childHandle, string? className = null)
        {
            childHandle = FindWindowEx(parentHandle, nint.Zero, className, null);
            if (childHandle == nint.Zero)
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        /// <summary>
        /// 尝试获取指定窗口的类名。
        /// </summary>
        /// <param name="windowHandle">指定窗口的句柄。</param>
        /// <param name="className">   获取到的窗口类名。</param>
        /// <returns>指示是否获取成功。</returns>
        public static bool TryGetWindowClassName(nint windowHandle, [MaybeNullWhen(false)] out string className)
        {
            StringBuilder classNameBuilder = new(256);
            int classNameLength = GetClassName(windowHandle, classNameBuilder, classNameBuilder.Capacity);
            if (classNameLength == 0)
            {
                className = null;
                return false;
            }
            else
            {
                className = classNameBuilder.ToString();
                return true;
            }
        }
    }
}
using Microsoft.Win32.SafeHandles;

namespace Zscno.Trackora
{
    /// <summary>
    /// 表示事件挂钩句柄的包装类。
    /// </summary>
    internal partial class SafeEventHookHandle : SafeHandleZeroOrMinusOneIsInvalid
    {
        private SafeEventHookHandle() : base(ownsHandle: true)
        {
        }

        /// <summary>
        /// 为指定事件设置事件挂钩函数。
        /// </summary>
        /// <param name="eventConstants">事件常量。</param>
        /// <param name="eventDelegate"> 事件挂钩函数的委托实例。</param>
        /// <returns>标识此事件挂钩的实例。</returns>
        public static SafeEventHookHandle SetEventHook(uint eventConstants, WinEventDelegate eventDelegate)
        {
            nint hookHandle = NativeApi.SetWinEventHook(eventConstants, eventConstants, nint.Zero,
                eventDelegate, 0, 0, NativeApi.WINEVENT_OUTOFCONTEXT);
            SafeEventHookHandle safeHookHandle = new();
            safeHookHandle.SetHandle(hookHandle);
            return safeHookHandle;
        }

        protected override bool ReleaseHandle()
        {
            return NativeApi.UnhookWinEvent(handle);
        }
    }
}
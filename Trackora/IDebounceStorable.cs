using System;

namespace Zscno.Trackora
{
    /// <summary>
    /// 提供延迟保存的防抖功能。
    /// </summary>
    internal interface IDebounceStorable : IDisposable
    {
        /// <summary>
        /// 请求延迟保存。
        /// </summary>
        void RequestStore();
    }
}
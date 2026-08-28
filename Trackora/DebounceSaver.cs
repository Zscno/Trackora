using System;
using System.Threading;
using System.Threading.Tasks;

namespace Zscno.Trackora
{
    /// <inheritdoc cref="IDebounceStorable"/>
    /// <remarks>该类是 <see cref="IDebounceStorable"/> 接口的默认实现，所有其他实现 <see cref="IDebounceStorable"/> 接口的类型都使用该类实现延迟防抖保存。</remarks>
    internal partial class DebounceSaver : IDebounceStorable, IDisposable
    {
        private readonly uint _delayMilliseconds;

        private readonly Func<Exception, Task>? _exceptionHandler;

        private readonly Func<Task> _storeCallBackAsync;

        private readonly Timer _storeTimer;

        /// <summary>
        /// 指示当前 <see cref="DebounceSaver"/> 实例使用的所有资源是否释放。若已释放，则为 1，否则为 0。
        /// </summary>
        private int _isDisposed;

        /// <summary>
        /// 创建一个新的 <see cref="DebounceSaver"/> 实例。
        /// </summary>
        /// <remarks>一个 <see cref="DebounceSaver"/> 实例只应该也只能负责单个保存任务，保存回调和延迟时间均不能修改。若向 <paramref name="storeCallBackAsync"/> 或 <paramref name="exceptionHandler"/> 传入同步代码，则需要返回 <see cref="Task.CompletedTask"/>。</remarks>
        /// <param name="storeCallBackAsync">异步的保存回调委托。</param>
        /// <param name="delayMilliseconds"> 保存的延迟时间，以毫秒为单位。</param>
        /// <param name="exceptionHandler">  
        /// 异步的处理保存时引发异常的委托。
        /// <para>当该参数为 <see langword="null"/> 时，保存时引发的异常将抛到应用程序最上层；当执行该委托时引发了另外的异常，则使用 <see cref="AggregateException"/> 包装两个异常并抛到应用程序最上层。</para>
        /// </param>
        public DebounceSaver(
            Func<Task> storeCallBackAsync,
            uint delayMilliseconds = 2000,
            Func<Exception, Task>? exceptionHandler = null)
        {
            ArgumentNullException.ThrowIfNull(storeCallBackAsync, nameof(storeCallBackAsync));

            _storeTimer = new Timer(Store, null, Timeout.Infinite, Timeout.Infinite);
            _storeCallBackAsync = storeCallBackAsync;
            _delayMilliseconds = delayMilliseconds;
            _exceptionHandler = exceptionHandler;
        }

        /// <summary>
        /// 释放当前 <see cref="DebounceSaver"/> 实例使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 请求延迟调用保存回调委托。
        /// </summary>
        public void RequestStore()
        {
            _ = _storeTimer.Change(_delayMilliseconds, Timeout.Infinite);
        }

        /// <inheritdoc cref="Dispose()"/>
        /// <param name="disposing">指示方法调用来自 <see cref="Dispose()"/>（其值是 <see langword="true"/>），还是来自析构函数（其值是 <see langword="false"/>）。</param>
        protected virtual void Dispose(bool disposing)
        {
            if (Interlocked.CompareExchange(ref _isDisposed, 1, 0) == 1)
            {
                return;
            }

            if (disposing)
            {
                _storeTimer.Dispose();
            }
        }

        private async void Store(object? state)
        {
            try
            {
                await _storeCallBackAsync();
            }
            catch (Exception ex)
            {
                if (_exceptionHandler is null)
                {
                    throw;
                }

                try
                {
                    await _exceptionHandler(ex);
                }
                catch (Exception handlerEx)
                {
                    throw new AggregateException("在处理保存失败的情况时引发了异常。", ex, handlerEx);
                }
            }
        }
    }
}
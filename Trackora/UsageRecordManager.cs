using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zscno.Trackora
{
    /// <inheritdoc cref="IUsageRecordManager"/>
    internal partial class UsageRecordManager : IUsageRecordManager
    {
        private readonly DebounceSaver _debounceSaver;

        private readonly string _recordFolderPath;

        /// <summary>
        /// 指示当前 <see cref="UsageRecordManager"/> 实例使用的所有资源是否释放。若已释放，则为 1，否则为 0。
        /// </summary>
        private int _isDisposed;

        private string _recordFilePath;

        public UsageRecord Record { get; private set; }

        public UsageRecordManager(IAppDataPathProvider pathProvider/*TODO: 接收日志实例。*/)
        {
            _recordFolderPath = pathProvider.RecordPath;
            _recordFilePath = Path.Combine(_recordFolderPath, $"{DateTime.Now: yyyy-MM-dd}.json");
            _debounceSaver = new DebounceSaver(StoreAsync, exceptionHandler: OnStoreFailed);
            Record = new UsageRecord();
        }

        /// <summary>
        /// 释放当前 <see cref="UsageRecordManager"/> 实例使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// 加载用户今天的使用记录。
        /// </summary>
        public Task LoadAsync()
        {
            if (!File.Exists(_recordFilePath))
            {
                return Task.CompletedTask;
            }

            string recordFilePath = Path.Combine(_recordFolderPath, $"{DateTime.Now: yyyy-MM-dd}.json");
            if (recordFilePath != _recordFilePath)
            {
                Record = new UsageRecord();
                _recordFilePath = recordFilePath;
                return Task.CompletedTask;
            }

            // TODO: Json 类型不应该创建新文件。
            var record = Json.ReadJsonFile(_recordFilePath, SourceGenerationContext.Default.UsageRecord);
            if (record is not null)
            {
                Record = record;
            }
            return Task.CompletedTask;
        }

        /// <summary>
        /// 请求延迟保存使用记录。
        /// </summary>
        /// <remarks>使用记录将会延迟 2 秒保存。</remarks>
        public void RequestStore()
        {
            _debounceSaver.RequestStore();
        }

        /// <summary>
        /// 保存用户今天的使用记录。
        /// </summary>
        public Task StoreAsync()
        {
            _ = Directory.CreateDirectory(_recordFolderPath);
            _ = Json.WriteJsonFile(_recordFilePath, Record, SourceGenerationContext.Default.UsageRecord);
            return Task.CompletedTask;
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
                _debounceSaver.Dispose();
            }
        }

        private static Task OnStoreFailed(Exception ex)
        {
            LogSystem.WriteLog(LogLevel.Error, $"延迟保存使用记录失败。{ex}");
            // TODO: 在主页提示用户。
            return Task.CompletedTask;
        }
    }
}
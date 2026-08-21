using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Zscno.Trackora
{
    /// <inheritdoc cref="IUsageRecordManager"/>
    internal partial class UsageRecordManager : IUsageRecordManager
    {
        private readonly string _recordFolderPath;

        /// <summary>
        /// 用于保存使用记录的计时器。
        /// </summary>
        private readonly Timer _saveTimer;

        private string _recordFilePath;

        public UsageRecord Record { get; private set; }

        public UsageRecordManager(IAppDataPathProvider pathProvider/*TODO: 接收日志实例。*/)
        {
            _recordFolderPath = pathProvider.RecordPath;
            _recordFilePath = Path.Combine(_recordFolderPath, $"{DateTime.Now: yyyy-MM-dd}.json");
            Record = new UsageRecord();
            _saveTimer = new Timer(SaveLatest, null, Timeout.Infinite, Timeout.Infinite);
        }

        /// <summary>
        /// 释放当前 <see cref="UsageRecordManager"/> 实例使用的所有资源。
        /// </summary>
        public void Dispose()
        {
            _saveTimer.Dispose();
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

        /// <inheritdoc cref="IUsageRecordManager.RequestSave"/>
        /// <remarks>该方法应该在使用记录更新后调用，将会延迟 2 秒保存。</remarks>
        public void RequestSave()
        {
            _ = _saveTimer.Change(2000, Timeout.Infinite);
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

        /// <summary>
        /// 保存最新的使用记录。
        /// </summary>
        /// <remarks>该函数是 <see cref="_saveTimer"/> 的回调函数。</remarks>
        private void SaveLatest(object? state)
        {
            _ = new SafeCaller()
            {
                LogMessage = "保存使用记录失败。",
                RemindingMsgResKey = "ECanNotSetRecord",
            }.CallMethodR(async () => await StoreAsync());
        }
    }
}
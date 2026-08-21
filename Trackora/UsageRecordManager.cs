using System;
using System.IO;
using System.Threading.Tasks;

namespace Zscno.Trackora
{
    /// <inheritdoc cref="IUsageRecordManager"/>
    internal class UsageRecordManager : IUsageRecordManager
    {
        private string _recordFilePath;

        private readonly string _recordFolderPath;

        public UsageRecord Record { get; private set; }

        public UsageRecordManager(IAppDataPathProvider pathProvider/*TODO: 接收日志实例。*/)
        {
            _recordFolderPath = pathProvider.RecordPath;
            _recordFilePath = Path.Combine(_recordFolderPath, $"{DateTime.Now: yyyy-MM-dd}.json");
            Record = new UsageRecord();
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
            if(recordFilePath!= _recordFilePath)
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
        /// 保存用户今天的使用记录。
        /// </summary>
        public Task StoreAsync()
        {
            _ = Directory.CreateDirectory(_recordFolderPath);
            _ = Json.WriteJsonFile(_recordFilePath, Record, SourceGenerationContext.Default.UsageRecord);
            return Task.CompletedTask;
        }
    }
}
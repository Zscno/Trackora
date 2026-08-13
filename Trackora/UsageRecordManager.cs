using Microsoft.Windows.Storage;
using System;
using System.IO;

namespace Zscno.Trackora
{
    /// <summary>
    /// 为获取和保存使用记录提供相关操作。
    /// </summary>
    internal static class UsageRecordManager
    {
        /// <summary>
        /// 记录文件路径。
        /// </summary>
        private static readonly string _recordFilePath;

        /// <summary>
        /// 记录文件所在文件夹的路径。
        /// </summary>
        private static readonly string _recordFolderPath;

        /// <summary>
        /// 今天的使用记录。
        /// </summary>
        internal static UsageRecord Record { get; private set; }

        static UsageRecordManager()
        {
            _recordFolderPath = Path.Combine(ApplicationData.GetDefault().LocalPath, "Records");
            _recordFilePath = Path.Combine(_recordFolderPath, $"{DateTime.Now: yyyy-MM-dd}.json");
            Record = new UsageRecord();
        }

        /// <summary>
        /// 确保存放使用记录的文件夹存在并从文件中读取今天的使用记录。
        /// </summary>
        internal static void Initialize()
        {
            _ = Directory.CreateDirectory(_recordFolderPath);
            Record = Json.ReadJsonFile(_recordFilePath, SourceGenerationContext.Default.UsageRecord) ?? new();
        }

        /// <summary>
        /// 保存使用记录。
        /// </summary>
        internal static string SaveRecord()
        {
            _ = Directory.CreateDirectory(_recordFolderPath);
            return Json.WriteJsonFile(_recordFilePath, Record, SourceGenerationContext.Default.UsageRecord);
        }
    }
}
using Microsoft.Windows.Storage;
using System;
using System.IO;

namespace Zscno.Trackora
{
    /// <summary>
    /// 使用记录管理器。
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
        /// <remarks>若 <see cref="Initialize"/> 方法引发异常，则该属性是一个新实例。</remarks>
        internal static UsageRecord Record { get; private set; }

        static UsageRecordManager()
        {
            _recordFolderPath = Path.Combine(ApplicationData.GetDefault().LocalPath, "Records");
            _recordFilePath = Path.Combine(_recordFolderPath, $"{DateTime.Now: yyyy-MM-dd}.json");
            Record = new UsageRecord();
        }

        /// <summary>
        /// 初始化使用记录管理器。若今天没有记录则新建文件；否则，读取今天的记录文件。
        /// </summary>
        /// <remarks>若引发异常，则 <see cref="Record"/> 是一个新实例。</remarks>
        internal static void Initialize()
        {
            _ = Directory.CreateDirectory(_recordFolderPath);
            Record = Json.ReadJsonFile(_recordFilePath, SourceGenerationContext.Default.UsageRecord);
        }

        /// <summary>
        /// 保存使用记录到文件。
        /// </summary>
        internal static string SaveRecord()
        {
            return Json.WriteJsonFile(_recordFilePath, Record, SourceGenerationContext.Default.UsageRecord);
        }
    }
}
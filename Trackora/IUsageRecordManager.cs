using System;

namespace Zscno.Trackora
{
    /// <summary>
    /// 提供获取和保存使用记录的功能。
    /// </summary>
    internal interface IUsageRecordManager : IDataLoadable, IDataStorable, IDisposable, IDebounceStorable
    {
        /// <summary>
        /// 获取今天用户的使用记录。
        /// </summary>
        UsageRecord Record { get; }
    }
}
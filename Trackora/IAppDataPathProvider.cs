namespace Zscno.Trackora
{
    /// <summary>
    /// 用于提供应用程序的数据存储路径。
    /// </summary>
    internal interface IAppDataPathProvider
    {
        /// <summary>
        /// 用于存放获取到的应用程序图标的文件夹路径。
        /// </summary>
        string IconPath { get; }

        /// <summary>
        /// 不可备份的本地数据文件夹路径。
        /// </summary>
        string LocalCachePath { get; }

        /// <summary>
        /// 可备份的本地数据文件夹路径。
        /// </summary>
        string LocalPath { get; }

        /// <summary>
        /// 用于存放用户使用记录的文件夹路径。
        /// </summary>
        string RecordPath { get; }
    }
}
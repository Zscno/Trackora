using System.Threading.Tasks;

namespace Zscno.Trackora
{
    /// <summary>
    /// 提供获取和保存忽略进程名单和仅记录时间进程名单的功能。
    /// </summary>
    internal interface IProcessFilter : IDataLoadable, IDataStorable
    {
        /// <summary>
        /// 将指定的进程添加到忽略进程名单。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若进程已添加，则为 <see langword="true"/>；若进程已存在，则为 <see langword="false"/>。</returns>
        bool AddIgnoredProcess(string processName);

        /// <summary>
        /// 将指定的进程添加到仅记录时间进程名单。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若进程已添加，则为 <see langword="true"/>；若进程已存在，则为 <see langword="false"/>。</returns>
        bool AddTimeOnlyProcess(string processName);

        /// <summary>
        /// 确定指定的进程是否忽略。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若忽略，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        bool IsIgnoredProcess(string processName);

        /// <summary>
        /// 确定指定的进程是否仅记录时间。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若仅记录时间，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        bool IsTimeOnlyProcess(string processName);

        /// <summary>
        /// 将指定的进程从忽略进程名单移除。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若成功找到并删除了进程，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        bool RemoveIgnoredProcess(string processName);

        /// <summary>
        /// 将指定的进程从仅记录时间进程名单移除。
        /// </summary>
        /// <param name="processName">进程的名称。</param>
        /// <returns>若成功找到并删除了进程，则为 <see langword="true"/>；否则为 <see langword="false"/>。</returns>
        bool RemoveTimeOnlyProcess(string processName);

        /// <summary>
        /// 异步地保存忽略进程名单。
        /// </summary>
        Task SaveIgnoredProcessListAsync();

        /// <summary>
        /// 异步地保存仅记录时间进程名单。
        /// </summary>
        Task SaveTimeOnlyProcessListAsync();
    }
}
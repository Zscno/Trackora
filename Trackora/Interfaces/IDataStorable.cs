using System.Threading.Tasks;

namespace Zscno.Trackora.Interfaces
{
    /// <summary>
    /// 提供存储数据的功能。
    /// </summary>
    internal interface IDataStorable
    {
        /// <summary>
        /// 异步地存储数据。
        /// </summary>
        Task StoreAsync();
    }
}
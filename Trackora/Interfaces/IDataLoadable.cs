using System.Threading.Tasks;

namespace Zscno.Trackora.Interfaces
{
    /// <summary>
    /// 提供加载数据的功能。
    /// </summary>
    internal interface IDataLoadable
    {
        /// <summary>
        /// 异步地加载数据。
        /// </summary>
        Task LoadAsync();
    }
}
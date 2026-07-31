using System.IO;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace Zscno.Trackora
{
    /// <summary>
    /// 读取或写入 Json 文件。
    /// </summary>
    /// <remarks>同时读取和写入时可能引发无法访问的异常。</remarks>
    internal static class Json
    {
        private static readonly ConcurrentDictionary<string, object> _writtingLocks = new();

        /// <summary>
        /// 读取 Json 文件。
        /// </summary>
        /// <typeparam name="T">要转换的类型。该类型必须具有实例构造方法。</typeparam>
        /// <param name="filePath">要读取的 Json 文件路径。</param>
        /// <param name="info">要转换类型的元数据。</param>
        /// <returns>一个 Json 值的 <typeparamref name="T"/> 类型表达。</returns>
        internal static T ReadJsonFile<T>(string filePath, JsonTypeInfo<T> info) where T : new()
        {
            using FileStream file = new(filePath, FileMode.OpenOrCreate, FileAccess.Read, FileShare.Read);
            T? result = file.Length > 0L ? JsonSerializer.Deserialize(file, info) : new T();
            return result is null ? new T() : result;
        }

        /// <summary>
        /// 写入 Json 文件。
        /// </summary>
        /// <typeparam name="T">要序列化的值的类型。</typeparam>
        /// <param name="filePath">要写入的 Json 文件路径。</param>
        /// <param name="value">要转换的值。</param>
        /// <returns>值的 Json 字符串表示形式。</returns>
        internal static string WriteJsonFile<T>(string filePath, T value, JsonTypeInfo<T> info)
        {
            string text = JsonSerializer.Serialize(value, info);
            object writtingLock =  _writtingLocks.GetOrAdd(filePath, new object());
            lock (writtingLock)
            {
                File.WriteAllText(filePath, text);
            }
            return text;
        }
    }
}
using Microsoft.Windows.Storage;
using System.IO;
using Zscno.Trackora.Interfaces;

namespace Zscno.Trackora.Services
{
    ///<inheritdoc cref="IDataPathProvider"/>
    internal class DataPathProvider : IDataPathProvider
    {
        public string IconPath { get; }

        public string LocalCachePath { get; }

        public string LocalPath { get; }

        public string RecordPath { get; }

        public DataPathProvider()
        {
            var appData = ApplicationData.GetDefault();
            LocalCachePath = appData.LocalCachePath;
            LocalPath = appData.LocalPath;
            IconPath = Path.Combine(LocalCachePath, "Icons");
            RecordPath = Path.Combine(LocalPath, "Records");
        }
    }
}
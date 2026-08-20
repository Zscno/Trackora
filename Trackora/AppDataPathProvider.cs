using Microsoft.Windows.Storage;
using System.IO;

namespace Zscno.Trackora
{
    ///<inheritdoc cref="IAppDataPathProvider"/>
    internal class AppDataPathProvider : IAppDataPathProvider
    {
        public string IconPath { get; }

        public string LocalCachePath { get; }

        public string LocalPath { get; }

        public string RecordPath { get; }

        public AppDataPathProvider()
        {
            var appData = ApplicationData.GetDefault();
            LocalCachePath = appData.LocalCachePath;
            LocalPath = appData.LocalPath;
            IconPath = Path.Combine(LocalCachePath, "Icons");
            RecordPath = Path.Combine(LocalPath, "Records");
        }
    }
}
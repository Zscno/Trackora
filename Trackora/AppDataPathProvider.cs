using Microsoft.Windows.Storage;
using System.IO;

namespace Zscno.Trackora
{
    ///<inheritdoc cref="IAppDataPathProvider"/>
    internal class AppDataPathProvider : IAppDataPathProvider
    {
        ///<inheritdoc cref="IAppDataPathProvider.IconPath"/>
        public string IconPath { get; }

        ///<inheritdoc cref="IAppDataPathProvider.LocalCachePath"/>
        public string LocalCachePath { get; }

        ///<inheritdoc cref="IAppDataPathProvider.LocalPath"/>
        public string LocalPath { get; }

        ///<inheritdoc cref="IAppDataPathProvider.RecordPath"/>
        public string RecordPath { get; }

        internal AppDataPathProvider()
        {
            var appData = ApplicationData.GetDefault();
            LocalCachePath = appData.LocalCachePath;
            LocalPath = appData.LocalPath;
            IconPath = Path.Combine(LocalCachePath, "Icons");
            RecordPath = Path.Combine(LocalPath, "Records");
        }
    }
}
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using Microsoft.Win32;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel.Resources;
using Zscno.Trackora.Interfaces;
using Zscno.Trackora.Services;
using Zscno.Trackora.Tools;
using Zscno.Trackora.UI;

// To learn more about WinUI, the WinUI project structure, and more about our project templates,
// see: http://aka.ms/winui-project-info.

namespace Zscno.Trackora
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    public partial class App : Application
    {
        /// <summary>
        /// 指示应用程序是否退出。若已退出，则为 1，否则为 0。
        /// </summary>
        private static int _isExited = 0;

        /// <summary>
        /// 应用程序的主机，管理日志及其他服务。
        /// </summary>
        private readonly IHost _host;

        /// <summary>
        /// 应用主窗口。
        /// </summary>
        public static MainWindow? AppMainWindow { get; private set; }

        /// <summary>
        /// 用于加载语言资源。
        /// </summary>
        public static ResourceLoader Loader { get; } = new();

        /// <summary>
        /// 本地缓存文件夹路径。
        /// </summary>
        public static string LocalCachePath { get; private set; } = string.Empty;

        /// <summary>
        /// 所有可选的主题选项。
        /// </summary>
        public static Dictionary<string, string> Themes => new()
        {
            { Loader.GetString("LightTheme"), "LightTheme" },
            { Loader.GetString("DarkTheme"), "DarkTheme" },
            { Loader.GetString("SystemTheme"), "SystemTheme" },
        };

        /// <summary>
        /// Initializes the singleton application object. This is the first line of authored code executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();

            AppInstance.GetCurrent().Activated += AppInstance_Activated;
            SystemEvents.SessionEnding += OnSessionEnding;

            _host = Host.CreateDefaultBuilder()
                .UseContentRoot(AppContext.BaseDirectory)
                .ConfigureServices((context, collection) =>
                {
                    _ = collection.AddSingleton<IDataPathProvider, DataPathProvider>()
                                  .AddSingleton<IAppInfoManager, AppInfoManager>()
                                  .AddSingleton<IProcessFilter, ProcessFilter>()
                                  .AddSingleton<IReminderManager, ReminderManager>()
                                  .AddSingleton<ISettings, LocalSettings>()
                                  .AddSingleton<IUsageRecordManager, UsageRecordManager>()
                                  .AddSingleton<IWindowTracker, WindowTracker>();
                }).Build();

            //LocalCachePath = ApplicationData.Current.LocalCacheFolder.Path; TODO: 将移除。
        }

        /// <summary>
        /// 退出应用程序。
        /// </summary>
        /// <param name="exitCode">要返回到操作系统的退出代码。使用 0 指示进程已成功完成。</param>
        public static async Task Exit(int exitCode)
        {
            if (Interlocked.CompareExchange(ref _isExited, 1, 0) == 1)
            {
                return;
            }

            var app = Current as App;
            await app!.StoreDataAsync();
            app.Dispose();
            Environment.Exit(exitCode);
        }

        /// <summary>
        /// 获取 <typeparamref name="T"/> 类型的应用程序服务。
        /// </summary>
        /// <typeparam name="T">要获取的服务对象的类型。</typeparam>
        /// <returns>获取到的 <typeparamref name="T"/> 类型的应用程序服务。</returns>
        /// <exception cref="ArgumentException"></exception>
        public static T GetService<T>() where T : class
        {
            if ((Current as App)!._host.Services.GetService<T>() is T service)
            {
                return service;
            }
            else
            {
                throw new ArgumentException("请求的类型尚未注册。", nameof(T));
            }
        }

        /// <summary>
        /// Invoked when the application is launched.
        /// </summary>
        /// <param name="args">Details about the launch request and process.</param>
        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            // InitLogFile(); TODO: 替换成 ILogger。

            AppMainWindow = new MainWindow();
            AppMainWindow.Activate();
            NativeApi.HideWindow(AppMainWindow);

            //SetTheme((string)LocalSettings["Theme"]); TODO: 在主窗口修改。

            try
            {
                var windowTracker = _host.Services.GetService<IWindowTracker>();
                if (windowTracker is not null)
                {
                    await windowTracker.StartAsync();
                }
            }
            catch (Exception ex)
            {
                LogSystem.WriteLog(LogLevel.Error, $"启动前台窗口跟踪服务失败。{ex}");
                // TODO: 将异常显示在主页的 InfoBar。
            }
        }

        /// <summary>
        /// 在已有应用实例被激活时调用。
        /// </summary>
        private void AppInstance_Activated(object? sender, AppActivationArguments e)
        {
            _ = AppMainWindow?.DispatcherQueue.TryEnqueue(async () => { AppMainWindow.ShowWindow(); });
        }

        ///// <summary>
        ///// 设置应用主题。
        ///// </summary>
        ///// <param name="themeName">指定的主题名称。</param>
        //private static void SetTheme(string themeName)
        //{
        //    Current.RequestedTheme = themeName switch
        //    {
        //        "LightTheme" => ApplicationTheme.Light,
        //        "DarkTheme" => ApplicationTheme.Dark,
        //        _ => Current.RequestedTheme,
        //    };
        //}

        /// <summary>
        /// 释放应用程序使用的所有资源。
        /// </summary>
        private void Dispose()
        {
            _ = AppMainWindow?.DispatcherQueue.TryEnqueue(() => AppMainWindow?.DisposeTaskBarIcon());
            _host.Dispose();
            SystemEvents.SessionEnding -= OnSessionEnding;
        }

        /// <summary>
        /// 当用户正在尝试注销或关闭系统时调用。
        /// </summary>
        private async void OnSessionEnding(object? sender, SessionEndingEventArgs e)
        {
            if (Interlocked.CompareExchange(ref _isExited, 1, 0) == 1)
            {
                return;
            }

            var app = Current as App;
            await app!.StoreDataAsync();
            app.Dispose();
        }

        /// <summary>
        /// 异步地保存所有数据。
        /// </summary>
        private async Task StoreDataAsync()
        {
            List<Task> tasks = [];
            foreach (var storable in _host.Services.GetServices<IDataStorable>())
            {
                tasks.Add(storable.StoreAsync());
            }
            await Task.WhenAll(tasks);
        }
    }
}
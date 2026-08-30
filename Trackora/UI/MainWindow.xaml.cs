using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.Threading.Tasks;
using WinRT.Interop;
using Zscno.Trackora.Interfaces;
using Zscno.Trackora.Strings;
using Zscno.Trackora.Tools;
using static Zscno.Trackora.App;

// To learn more about WinUI, the WinUI project structure, and more about our project templates,
// see: http://aka.ms/winui-project-info.

namespace Zscno.Trackora.UI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            AppWindow.Closing += AppWindow_Closing;

            var settings = App.GetService<ISettings>();

            ExtendsContentIntoTitleBar = true;
            Title = "Trackora";
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
            SetTitleBar(TitleBar);
            AppWindow.TitleBar.ButtonForegroundColor = settings.Theme switch
            {
                ElementTheme.Dark => Colors.White,
                ElementTheme.Light => Colors.Black,
                _ => AppWindow.TitleBar.ButtonForegroundColor,
            };
        }

        /// <summary>
        /// 释放任务栏通知区域图标资源。
        /// </summary>
        public void DisposeTaskBarIcon()
        {
            TbIcon.Dispose();
        }

        [RelayCommand]
        public void ShowWindow()
        {
            MainView.SelectedItem = Home;

            nint windowHandle = WindowNative.GetWindowHandle(this);
            if (windowHandle != nint.Zero)
            {
                _ = NativeApi.ShowWindow(windowHandle, NativeApi.SW_SHOW);
                _ = NativeApi.SetForegroundWindow(windowHandle);
            }
        }

        /// <summary>
        /// 退出应用程序。
        /// </summary>
        [RelayCommand]
        private static async Task ExitApplication()
        {
            await Exit(0);
        }

        private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
        {
            args.Cancel = true;
            NativeApi.HideWindow(this);
        }

        private void NavigationView_SelectionChanged(NavigationView sender,
            NavigationViewSelectionChangedEventArgs args)
        {
            if (((NavigationViewItem)args.SelectedItem).Name == "Home")
            {
                _ = MainFrame.Navigate(typeof(HomePage));
                sender.Header = Resources.HomeHeader;
            }
            else if (args.IsSettingsSelected)
            {
                _ = MainFrame.Navigate(typeof(SettingsPage));
                sender.Header = Resources.SettingsHeader;
            }
        }
    }
}
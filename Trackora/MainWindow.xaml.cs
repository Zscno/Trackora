using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using WinRT.Interop;
using static Zscno.Trackora.App;

// To learn more about WinUI, the WinUI project structure, and more about our project templates,
// see: http://aka.ms/winui-project-info.

namespace Zscno.Trackora
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
            ExtendsContentIntoTitleBar = true;
            Title = "Trackora";
            AppWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;
            AppWindow.TitleBar.IconShowOptions = IconShowOptions.ShowIconAndSystemMenu;
            SetTitleBar(TitleBar);
            AppWindow.TitleBar.ButtonForegroundColor = (string)LocalSettings["Theme"] switch
            {
                "DarkTheme" => Colors.White,
                "LightTheme" => Colors.Black,
                _ => AppWindow.TitleBar.ButtonForegroundColor,
            };
        }

        [RelayCommand]
        public async Task ShowWindow()
        {
            Window? window = AppMainWindow;
            if (window is null)
            {
                return;
            }

            if (MainView.SelectedItem as NavigationViewItem != Home)
            {
                MainView.SelectedItem = Home;
            }
            else
            {
                if (MainFrame.Content is not HomePage page)
                {
                    return;
                }

                await page.LoadControlsThatNeed();
            }

            nint hwnd = WindowNative.GetWindowHandle(window);
            if (hwnd == nint.Zero)
            {
                return;
            }
            _ = NativeApi.ShowWindow(hwnd, NativeApi.SW_SHOW);
            _ = NativeApi.SetForegroundWindow(hwnd);
        }

        /// <summary>
        /// 释放任务栏通知区域图标资源。
        /// </summary>
        internal void DisposeTaskBarIcon()
        {
            TbIcon.Dispose();
        }

        /// <summary>
        /// 退出应用程序。
        /// </summary>
        [RelayCommand]
        private static void ExitApplication()
        {
            Exit(0);
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
                sender.Header = Loader.GetString("HomeHeader");
            }
            else if (args.IsSettingsSelected)
            {
                _ = MainFrame.Navigate(typeof(SettingsPage));
                sender.Header = Loader.GetString("SettingsHeader");
            }
        }
    }
}
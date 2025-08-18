using CommunityToolkit.Mvvm.Input;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using WinRT.Interop;
using static Zscno.Trackora.App;

// To learn more about WinUI, the WinUI project structure, and more about our project templates, see: http://aka.ms/winui-project-info.

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
			if ((string) LocalSettings["Theme"] == "DarkTheme")
			{
				AppWindow.TitleBar.ButtonForegroundColor = Colors.White;
			}
			else if ((string) LocalSettings["Theme"] == "LightTheme")
			{
				AppWindow.TitleBar.ButtonForegroundColor = Colors.Black;
			}
		}

		[RelayCommand]
		public async Task ShowWindow()
		{
			Window window = AppMainWindow;
			if (window == null)
			{
				return;
			}

			if ((MainView.SelectedItem as NavigationViewItem) != Home)
			{
				MainView.SelectedItem = Home;
			}
			else
			{
				await (MainFrame.Content as HomePage).Refresh();
			}

			IntPtr hwnd = WindowNative.GetWindowHandle(window);
			if (hwnd == IntPtr.Zero)
			{
				return;
			}
			_ = SystemHelper.ShowWindow(hwnd, SystemHelper.SW_SHOW);
			_ = SystemHelper.SetForegroundWindow(hwnd);
		}

		private void AppWindow_Closing(AppWindow sender, AppWindowClosingEventArgs args)
		{
			args.Cancel = true;
			SystemHelper.HideWindow(this);
		}

		[RelayCommand]
		private void ExitApplication()
		{
			TbIcon.Dispose();
			LogSystem.WriteLog(LogLevel.Info, "程序安全退出。");
			Application.Current.Exit();
		}

		private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
		{
			if ((args.SelectedItem as NavigationViewItem).Name == "Home")
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
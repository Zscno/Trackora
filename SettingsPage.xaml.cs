using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Storage;
using static Zscno.Trackora.App;

// To learn more about WinUI, the WinUI project structure, and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Zscno.Trackora
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class SettingsPage : Page
	{
		public SettingsPage()
		{
			InitializeComponent();
		}

		private void CheckLog_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			if (File.Exists(LogSystem.LogFilePath))
			{
				_ = Process.Start("Explorer.exe", $"/select,{LogSystem.LogFilePath}");
			}
			button.IsEnabled = true;
		}

		private async void CleanCache_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			string basePath = ApplicationData.Current.LocalCacheFolder.Path;
			try
			{
				SystemHelper.DeleteAllFiles(Path.Combine(basePath, "Logs"), Path.Combine(basePath, "Icons"));
				File.WriteAllText(InfoFilePath, string.Empty);
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Error, ex.ToString());
				await ReminderHelper.ShowDialog(XamlRoot, Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotDeleteFiles"));
			}

			try
			{
				CacheSize.Text = Loader.GetString("CacheFolderSize") +
					SystemHelper.GetFolderSize(ApplicationData.Current.LocalCacheFolder.Path);
			}
			catch (Exception ex)
			{
				CacheSize.Text = string.Empty;
				LogSystem.WriteLog(LogLevel.Error, ex.ToString());
				await ReminderHelper.ShowDialog(XamlRoot, Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotGetSize"));
			}
			button.IsEnabled = true;
		}

		private void ContinuousSoundPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			LocalSettings["ContinuousUsedTimeSound"] = (string) ContinuousSoundPicker.SelectedItem;
		}

		private void ContinuousTest_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			CanSend = ReminderHelper.SendReminder(ReminderKinds.ContinuousUsedTimeSoundTest);
			button.IsEnabled = true;
		}

		private void EndUsingSoundPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			LocalSettings["EndUsingTimeSound"] = (string) EndUsingSoundPicker.SelectedItem;
		}

		private void EndUsingTest_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			CanSend = ReminderHelper.SendReminder(ReminderKinds.EndUsingTimeSoundTest);
			button.IsEnabled = true;
		}

		private void NoInfoOK_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			try
			{
				string[] strings = NoInfoNames.Text.Split(',');
				foreach (string item in strings)
				{
					if (string.IsNullOrWhiteSpace(item))
					{
						throw new ArgumentException("用户的输入中有空格、空或 null 。");
					}
				}
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Warning, $"用户输入不符合要求 [Text={NoInfoNames.Text}] ：{ex}");
				NoInfoNames.Text = (string) LocalSettings["NoInfoNames"];
				return;
			}
			LocalSettings["NoInfoNames"] = NoInfoNames.Text;
			button.IsEnabled = true;
		}

		private async void Page_Loaded(object sender, RoutedEventArgs e)
		{
			CanNotSend.IsOpen = !CanSend;
			TotalSoundPicker.ItemsSource = CommonSounds.Keys.ToList();
			ContinuousSoundPicker.ItemsSource = CommonSounds.Keys.ToList();
			EndUsingSoundPicker.ItemsSource = AlarmSounds.Keys.ToList();
			ThemePicker.ItemsSource = Themes.Keys.ToList();
			TotalSoundPicker.SelectedItem = (string) LocalSettings["TotalUsedTimeSound"];
			ContinuousSoundPicker.SelectedItem = (string) LocalSettings["ContinuousUsedTimeSound"];
			EndUsingSoundPicker.SelectedItem = (string) LocalSettings["EndUsingTimeSound"];
			ThemePicker.SelectedItem = Loader.GetString((string) LocalSettings["Theme"]);
			NoInfoNames.Text = (string) LocalSettings["NoInfoNames"];
			NoTimeNames.Text = (string) LocalSettings["NoTimeNames"];
			PackageVersion version = Package.Current.Id.Version;
			Version.Text = $"{version.Major}.{version.Minor}.{version.Build}";
			try
			{
				CacheSize.Text = Loader.GetString("CacheFolderSize") +
					SystemHelper.GetFolderSize(ApplicationData.Current.LocalCacheFolder.Path);
			}
			catch (Exception ex)
			{
				CacheSize.Text = string.Empty;
				LogSystem.WriteLog(LogLevel.Error, ex.ToString());
				await ReminderHelper.ShowDialog(XamlRoot, Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotGetSize"));
			}
		}

		private void NoInfoReset_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			NoInfoNames.Text = (string) LocalSettings["NoInfoNames"];
			button.IsEnabled = true;
		}

		private void ThemePick_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			LocalSettings["Theme"] = Themes[(string) ThemePicker.SelectedItem];
		}

		private void TotalSoundPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			LocalSettings["TotalUsedTimeSound"] = (string) TotalSoundPicker.SelectedItem;
		}

		private void TotalTest_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			CanSend = ReminderHelper.SendReminder(ReminderKinds.TotalUsedTimeSoundTest);
			button.IsEnabled = true;
		}

		private void NoTimeOK_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			try
			{
				string[] strings = NoTimeNames.Text.Split(',');
				foreach (string item in strings)
				{
					if (string.IsNullOrWhiteSpace(item))
					{
						throw new ArgumentException("用户的输入中有空格、空或 null 。");
					}
				}
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Warning, $"用户输入不符合要求 [Text={NoTimeNames.Text}] ：{ex}");
				NoTimeNames.Text = (string) LocalSettings["NoTimeNames"];
				return;
			}
			LocalSettings["NoTimeNames"] = NoTimeNames.Text;
			button.IsEnabled = true;
		}

		private void NoTimeReset_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			NoTimeNames.Text = (string) LocalSettings["NoTimeNames"];
			button.IsEnabled = true;
		}
	}
}
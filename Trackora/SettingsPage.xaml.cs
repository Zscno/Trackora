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

		/// <summary>
		/// 获取指定文件夹大小的格式化字符串。
		/// </summary>
		/// <param name="path">指定文件夹的路径。</param>
		/// <returns>格式化字符串。</returns>
		private static string GetFolderSize(string path)
		{
			double size = 0;
			DirectoryInfo info;
			try
			{
				info = new(path);
			}
			catch (Exception ex)
			{
				//LogSystem.WriteLog(LogLevel.Error, $"在获取文件夹 [{path}] 信息时触发异常：{ex}");
				throw new Exception($"在获取文件夹 [{path}] 信息时触发了异常。", ex);
			}

			foreach (FileInfo file in info.GetFiles("*", SearchOption.AllDirectories))
			{
				try
				{
					size += file.Length;
				}
				catch (Exception ex)
				{
					try
					{
						string filePath = file.FullName;
						LogSystem.WriteLog(LogLevel.Error, $"在获取文件夹中的文件 [{filePath}] 大小时触发异常：{ex}");
					}
					catch (Exception)
					{
						// 如果无法获取文件路径就使用文件夹路径。
						LogSystem.WriteLog(LogLevel.Error, $"在获取文件夹 [{path}] 中的文件大小时触发异常：{ex}");
					}
				}
			}

			string[] sizes = { "B", "KB", "MB", "GB" };
			int count = 0;
			while (size >= 1024 && count < sizes.Length - 1)
			{
				count++;
				size /= 1024;
			}

			return $"{size:F2} {sizes[count]}";
		}

		private void CheckLog_Click(object sender, RoutedEventArgs e)
		{
			Button? button = sender as Button;
			button!.IsEnabled = false;
			if (File.Exists(LogSystem.LogFilePath))
			{
				_ = Process.Start("Explorer.exe", $"/select,{LogSystem.LogFilePath}");
			}
			button.IsEnabled = true;
		}

		/// <summary>
		/// 删除指定文件夹中的所有文件。
		/// </summary>
		/// <param name="foldersPath">指定文件夹的路径。</param>
		private static void DeleteAllFiles(params string[] foldersPath)
		{
			foreach (string folderPath in foldersPath)
			{
				DirectoryInfo info;
				try
				{
					info = new(folderPath);
				}
				catch (Exception ex)
				{
					//LogSystem.WriteLog(LogLevel.Error, $"在获取文件夹 [{folderPath}] 信息时触发异常：{ex}");
					throw new Exception($"在获取文件夹 [{folderPath}] 信息时触发了异常。", ex);
				}

				foreach (FileInfo file in info.GetFiles("*", SearchOption.AllDirectories))
				{
					try
					{
						if (file.FullName != LogSystem.LogFilePath)
						{
							file.Delete();
						}
					}
					catch (Exception ex)
					{
						try
						{
							string filePath = file.FullName;
							LogSystem.WriteLog(LogLevel.Error, $"在删除文件夹中的文件 [{filePath}] 时触发异常：{ex}");
						}
						catch (Exception)
						{
							// 如果无法获取文件路径就使用文件夹路径。
							LogSystem.WriteLog(LogLevel.Error, $"在删除文件夹 [{folderPath}] 中的文件时触发异常：{ex}");
						}
					}
				}
			}
		}

		private async void CleanCache_Click(object sender, RoutedEventArgs e)
		{
			Button? button = sender as Button;
			button!.IsEnabled = false;
			try
			{
				DeleteAllFiles(Path.Join(LocalCachePath, "Logs"), Path.Join(LocalCachePath, "Icons"));
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
				CacheSize.Text = Loader.GetString("CacheFolderSize") + GetFolderSize(LocalCachePath);
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
			Button? button = sender as Button;
			button!.IsEnabled = false;
			CanSend = ReminderHelper.SendReminder(ReminderKinds.ContinuousUsedTimeSoundTest);
			button.IsEnabled = true;
		}

		private void EndUsingSoundPicker_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			LocalSettings["EndUsingTimeSound"] = (string) EndUsingSoundPicker.SelectedItem;
		}

		private void EndUsingTest_Click(object sender, RoutedEventArgs e)
		{
			Button? button = sender as Button;
			button!.IsEnabled = false;
			CanSend = ReminderHelper.SendReminder(ReminderKinds.EndUsingTimeSoundTest);
			button.IsEnabled = true;
		}

		private void NoInfoOK_Click(object sender, RoutedEventArgs e)
		{
			Button? button = sender as Button;
			button!.IsEnabled = false;
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
				CacheSize.Text = Loader.GetString("CacheFolderSize") + GetFolderSize(LocalCachePath);
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
			Button? button = sender as Button;
			button!.IsEnabled = false;
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
			Button? button = sender as Button;
			button!.IsEnabled = false;
			CanSend = ReminderHelper.SendReminder(ReminderKinds.TotalUsedTimeSoundTest);
			button.IsEnabled = true;
		}

		private void NoTimeOK_Click(object sender, RoutedEventArgs e)
		{
			Button? button = sender as Button;
			button!.IsEnabled = false;
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
			Button? button = sender as Button;
			button!.IsEnabled = false;
			NoTimeNames.Text = (string) LocalSettings["NoTimeNames"];
			button.IsEnabled = true;
		}
	}
}
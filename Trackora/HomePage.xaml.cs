using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using static Zscno.Trackora.App;

// To learn more about WinUI, the WinUI project structure, and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Zscno.Trackora
{
	/// <summary>
	/// An empty page that can be used on its own or navigated to within a Frame.
	/// </summary>
	public sealed partial class HomePage : Page
	{
		private static bool _firstLoad;

		private static TimeSpan _timeNow = new(DateTime.Now.Hour, DateTime.Now.Minute, 0);

		public HomePage()
		{
			InitializeComponent();
		}

		/// <summary>
		/// 加载需要更新的控件。
		/// </summary>
		public async Task LoadControlsThatNeed()
		{
			LoadingRing.IsActive = true;
			// 如果结束使用时间未设置或已过，则不显示。
			EndUsing.SelectedTime = WindowTracker.EndUsingTime == TimeSpan.Zero ||
				WindowTracker.EndUsingTime <= _timeNow ?
				null : WindowTracker.EndUsingTime;
			TimePickReminder.Text = string.Empty;
			TotalUsedTime.Text = WindowTracker.GetLocalTime(WindowTracker.TotalUsedTime);
			if (!_firstLoad)
			{
				// 如果不是首次加载，则重置按钮内容。
				All.Content = Loader.GetString("All/Content");
			}

			try
			{
				ProcessesList.ItemsSource = WindowTracker.GetProcessesInfo(6);
				// 超过 6 项时显示按钮。
				All.Visibility = WindowTracker.WindowsUsedTime.Count > 6 ?
					Visibility.Visible : Visibility.Collapsed;
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Error, ex.ToString());
				All.Visibility = Visibility.Collapsed;
				await ReminderHelper.ShowDialog("无法加载应用列表", 
					Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotGetInfo"));
			}
			LoadingRing.IsActive = false;
		}

		private async void All_Click(object sender, RoutedEventArgs e)
		{
			LoadingRing.IsActive = true;
			All.IsEnabled = false;
			bool isRetract = (string) All.Content == Loader.GetString("Retract");

			try
			{
				int count = isRetract ? 6 : WindowTracker.WindowsUsedTime.Count;
				ProcessesList.ItemsSource = WindowTracker.GetProcessesInfo(count);
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Error, ex.ToString());
				await ReminderHelper.ShowDialog("无法加载应用列表", 
					Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotGetInfo"));
			}
			All.Content = isRetract ? Loader.GetString("All/Content") : Loader.GetString("Retract");

			All.IsEnabled = true;
			LoadingRing.IsActive = false;
		}

		private void Continuous_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
		{
			if (!_firstLoad)
			{
				LocalSettings["ContinuousUsedRemindTime"] = e.NewTime;
			}
		}

		private void EndUsing_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
		{
			if (e.NewTime <= _timeNow)
			{
				TimePickReminder.Text = Loader.GetString("PastTime");
				EndUsing.SelectedTime = null;
			}
			else
			{
				TimePickReminder.Text = Loader.GetString("RightTime");
				WindowTracker.EndUsingTime = e.NewTime;
			}
		}

		private async void Page_Loaded(object sender, RoutedEventArgs e)
		{
			_firstLoad = true;
			//CachePath.Text = ApplicationData.Current.TemporaryFolder.Path;
			Total.Time = (TimeSpan) LocalSettings["TotalUsedRemindTime"];
			Continuous.Time = (TimeSpan) LocalSettings["ContinuousUsedRemindTime"];
			ResetContinuous.Time = (TimeSpan) LocalSettings["ContinuousUsedResetTime"];
			await LoadControlsThatNeed();
			_firstLoad = false;
		}

		private async void Refresh_Click(object sender, RoutedEventArgs e)
		{
			Button? button = sender as Button;
			button!.IsEnabled = false;
			await LoadControlsThatNeed();
			button.IsEnabled = true;
		}

		private void Reset_Click(object sender, RoutedEventArgs e)
		{
			Button? button = sender as Button;
			button!.IsEnabled = false;
			EndUsing.SelectedTime = null;
			WindowTracker.EndUsingTime = TimeSpan.Zero;
			TimePickReminder.Text = string.Empty;
			button.IsEnabled = true;
		}

		private void ResetContinuous_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
		{
			if (!_firstLoad)
			{
				LocalSettings["ContinuousUsedResetTime"] = e.NewTime;
			}
		}

		private void Total_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
		{
			if (!_firstLoad)
			{
				LocalSettings["TotalUsedRemindTime"] = e.NewTime;
				if (e.NewTime > e.OldTime)
				{
					WindowTracker.HasTotalReminded = false;
				}
			}
		}
	}
}
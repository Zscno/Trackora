using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
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
		private static TimeSpan _timeNow = new(DateTime.Now.Hour, DateTime.Now.Minute, 0);
		private static bool _isFirstLoad;

		public HomePage()
		{
			InitializeComponent();
		}

		public async Task Refresh()
		{
			LoadingRing.IsActive = true;

			TotalUsedTime.Text = WindowTracker.GetLocalTime(WindowTracker.TotalUsedTime);
			All.Content = Loader.GetString("All/Content");
			EndUsing.SelectedTime = WindowTracker.EndUsingTime == TimeSpan.Zero ||
				WindowTracker.EndUsingTime <= _timeNow ?
				null : WindowTracker.EndUsingTime;
			TimePickReminder.Text = EndUsing.SelectedTime != null &&
				WindowTracker.EndUsingTime <= _timeNow ?
				Loader.GetString("PastTime") : string.Empty;

			try
			{
				ProcessesList.ItemsSource = WindowTracker.GetProcessesInfo(6);
				All.Visibility = ((List<ProcessInfo>) ProcessesList.ItemsSource).Count > 6 ?
					Visibility.Visible : Visibility.Collapsed;
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Error, ex.ToString());
				All.Visibility = Visibility.Collapsed;
				await ReminderHelper.ShowDialog(XamlRoot, Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotGetInfo"));
			}

			LoadingRing.IsActive = false;
		}

		private void Continuous_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
		{
			if (!_isFirstLoad)
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
			LoadingRing.IsActive = true;
			_isFirstLoad = true;

			Total.Time = (TimeSpan) LocalSettings["TotalUsedRemindTime"];
			Continuous.Time = (TimeSpan) LocalSettings["ContinuousUsedRemindTime"];
			ResetContinuous.Time = (TimeSpan) LocalSettings["ContinuousUsedResetTime"];
			EndUsing.SelectedTime = WindowTracker.EndUsingTime == TimeSpan.Zero ||
				WindowTracker.EndUsingTime <= _timeNow ?
				null : WindowTracker.EndUsingTime;
			TimePickReminder.Text = EndUsing.SelectedTime != null &&
				WindowTracker.EndUsingTime <= _timeNow ?
				Loader.GetString("PastTime") : string.Empty;
			//CachePath.Text = ApplicationData.Current.TemporaryFolder.Path;
			TotalUsedTime.Text = WindowTracker.GetLocalTime(WindowTracker.TotalUsedTime);

			try
			{
				ProcessesList.ItemsSource = WindowTracker.GetProcessesInfo(6);
				All.Visibility = ((List<ProcessInfo>) ProcessesList.ItemsSource).Count > 6 ?
					Visibility.Visible : Visibility.Collapsed;
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Error, ex.ToString());
				All.Visibility = Visibility.Collapsed;
				await ReminderHelper.ShowDialog(XamlRoot, Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotGetInfo"));
			}

			_isFirstLoad = false;
			LoadingRing.IsActive = false;
		}

		private async void Refresh_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			await Refresh();
			button.IsEnabled = true;
		}

		private void Reset_Click(object sender, RoutedEventArgs e)
		{
			Button button = sender as Button;
			button.IsEnabled = false;
			EndUsing.SelectedTime = null;
			WindowTracker.EndUsingTime = TimeSpan.Zero;
			TimePickReminder.Text = string.Empty;
			button.IsEnabled = true;
		}

		private void ResetContinuous_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
		{
			if (!_isFirstLoad)
			{
				LocalSettings["ContinuousUsedResetTime"] = e.NewTime;
			}
		}

		private void Total_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
		{
			if (!_isFirstLoad)
			{
				LocalSettings["TotalUsedRemindTime"] = e.NewTime;
				if (e.NewTime > e.OldTime)
				{
					WindowTracker.HasTotalReminded = false;
				}
			}
		}

		private async void All_Click(object sender, RoutedEventArgs e)
		{
			LoadingRing.IsActive = true;
			All.IsEnabled = false;
			bool isRetract = (string) All.Content == Loader.GetString("Retract");

			try
			{
				int count =  isRetract? 
					6 : WindowTracker.WindowsUsedTime.Count;
				ProcessesList.ItemsSource = WindowTracker.GetProcessesInfo(count);
			}
			catch (Exception ex)
			{
				LogSystem.WriteLog(LogLevel.Error, ex.ToString());
				await ReminderHelper.ShowDialog(XamlRoot, Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotGetInfo"));
			}
			All.Content = isRetract ? Loader.GetString("All/Content") : Loader.GetString("Retract");

			All.IsEnabled = true;
			LoadingRing.IsActive = false;
		}
	}
}
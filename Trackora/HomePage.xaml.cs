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
		private static bool _isFirstLoading;

		private static readonly TimeSpan _timeNow = new(DateTime.Now.Hour, DateTime.Now.Minute, 0);

		public HomePage()
		{
			InitializeComponent();
		}

        /// <summary>
        /// 加载需要更新的控件。
        /// </summary>
        public void LoadControlsThatNeed()
        {
            LoadingRing.IsActive = true;
            EndUsing.SelectedTime = WindowTracker.EndUsingTime == default ||
                                    WindowTracker.EndUsingTime <= _timeNow
                ? null
                : WindowTracker.EndUsingTime;
            TimePickReminder.Text = string.Empty;
            TotalUsageTime.Text = Localization.ToLocalizedTimeString(UsageRecordManager.Record.TotalUsageTime);
            if (!_isFirstLoading)
            {
                All.Content = Loader.GetString("All/Content");
            }

            ProcessesList.ItemsSource = GetProcessDisplayItems();

            LoadingRing.IsActive = false;
        }

		/// <summary>
		/// 获取所有被记录进程的显示信息。
		/// </summary>
		/// <returns>所有被记录进程的显示信息。</returns>
        private static List<ProcessDisplayItem> GetProcessDisplayItems()
		{
			List<ProcessDisplayItem> processDisplayItems = [];
			foreach (var processUsageRecord in UsageRecordManager.Record.ProcessUsageRecords)
			{
				string name =
					ProcessInfoManager.ProcessInfoMap.TryGetValue( processUsageRecord.Key, out ProcessInfo? processInfo) ?
					processInfo.DisplayName : processUsageRecord.Key;
				string iconFileUri = ProcessInfoManager.GetProcessIconUri(processUsageRecord.Key);
				processDisplayItems.Add(new ProcessDisplayItem(
                    iconFileUri,
                    name,
                    processUsageRecord.Value));
			}
			return processDisplayItems;
		}

		private async void All_Click(object sender, RoutedEventArgs e)
		{
			LoadingRing.IsActive = true;
			All.IsEnabled = false;
			bool isRetract = (string)All.Content == Loader.GetString("Retract");

			_ = await new SafeCaller() { RemindingMsgResKey="ECanNotGetInfo", }.CallMethodD(() =>
			{
				int count = isRetract ? 6 : UsageRecordManager.Record.ProcessUsageRecords.Count;
				ProcessesList.ItemsSource = WindowTracker.GetProcessesInfo(count);
			});
			All.Content = isRetract ? Loader.GetString("All/Content") : Loader.GetString("Retract");

			All.IsEnabled = true;
			LoadingRing.IsActive = false;
		}

		private void Continuous_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
		{
			if (!_isFirstLoading)
			{
				Settings.SessionThreshold = (uint)e.NewTime.TotalMilliseconds;
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
			_isFirstLoading = true;
			//CachePath.Text = ApplicationData.Current.TemporaryFolder.Path;
			Total.Time = TimeSpan.FromMilliseconds(Settings.TotalThreshold);
			Continuous.Time = TimeSpan.FromMilliseconds(Settings.SessionThreshold);
			ResetContinuous.Time = TimeSpan.FromMilliseconds(Settings.IdleThreshold);
			LoadControlsThatNeed();
			_isFirstLoading = false;
		}

		private async void Refresh_Click(object sender, RoutedEventArgs e)
		{
			Button? button = sender as Button;
			button!.IsEnabled = false;
			LoadControlsThatNeed();
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
			if (!_isFirstLoading)
			{
				Settings.IdleThreshold = (uint)e.NewTime.TotalMilliseconds;
			}
		}

		private void Total_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
		{
			if (!_isFirstLoading)
			{
				Settings.TotalThreshold = (uint)e.NewTime.TotalMilliseconds;
				if (e.NewTime > e.OldTime)
				{
					WindowTracker.IsTotalUsageReminderShown = false;
				}
			}
		}
	}
}
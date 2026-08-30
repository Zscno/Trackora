using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using Zscno.Trackora.Interfaces;
using Zscno.Trackora.Services;
using Zscno.Trackora.Tools;

// To learn more about WinUI, the WinUI project structure, and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Zscno.Trackora.UI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page
    {
        private static readonly TimeSpan _timeNow = new(DateTime.Now.Hour, DateTime.Now.Minute, 0);

        private static bool _isFirstLoading;

        /// <inheritdoc cref="IAppInfoManager"/>
        private readonly IAppInfoManager _appInfoManager;

        /// <inheritdoc cref="IReminderManager"/>
        private readonly IReminderManager _reminderManager;

        /// <inheritdoc cref="ISettings"/>
        private readonly ISettings _settings;

        /// <inheritdoc cref="IUsageRecordManager"/>
        private readonly IUsageRecordManager _usageRecordManager;

        public HomePage()
        {
            InitializeComponent();

            _usageRecordManager = App.GetService<IUsageRecordManager>();
            _appInfoManager = App.GetService<IAppInfoManager>();
            _settings = App.GetService<ISettings>();
            _reminderManager = App.GetService<IReminderManager>();
        }

        /// <summary>
        /// 加载需要更新的控件。
        /// </summary>
        public void LoadControlsThatNeed()
        {
            LoadingRing.IsActive = true;
            TotalUsageTime.Text = Localization.ToLocalizedTimeString(_usageRecordManager.Record.DailyDuration);
            ProcessesList.ItemsSource = GetProcessDisplayItems();
            LoadingRing.IsActive = false;
        }

        private void Continuous_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            if (!_isFirstLoading)
            {
                _settings.SessionThreshold = (uint)e.NewTime.TotalMilliseconds;
            }
        }

        /// <summary>
        /// 获取所有被记录进程的显示信息。
        /// </summary>
        /// <returns>所有被记录进程的显示信息。</returns>
        private List<ProcessDisplayItem> GetProcessDisplayItems()
        {
            List<ProcessDisplayItem> processDisplayItems = [];
            foreach (var processUsageRecord in _usageRecordManager.Record.ProcessUsageRecords)
            {
                string name =
                    _appInfoManager.AppInfoMap.TryGetValue(processUsageRecord.Key, out ProcessInfo? processInfo) ?
                    processInfo.DisplayName : processUsageRecord.Key;
                string iconFileUri = _appInfoManager.GetAppIconUri(processUsageRecord.Key);
                processDisplayItems.Add(new ProcessDisplayItem(
                    iconFileUri,
                    name,
                    processUsageRecord.Value));
            }
            return processDisplayItems;
        }

        private void Page_Loaded(object sender, RoutedEventArgs e)
        {
            _isFirstLoading = true;
            //CachePath.Text = ApplicationData.Current.TemporaryFolder.Path;
            Total.Time = TimeSpan.FromMilliseconds(_settings.DailyThreshold);
            Continuous.Time = TimeSpan.FromMilliseconds(_settings.SessionThreshold);
            ResetContinuous.Time = TimeSpan.FromMilliseconds(_settings.IdleThreshold);
            LoadControlsThatNeed();
            _isFirstLoading = false;
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;
            button!.IsEnabled = false;
            LoadControlsThatNeed();
            button.IsEnabled = true;
        }

        private void ResetContinuous_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            if (!_isFirstLoading)
            {
                _settings.IdleThreshold = (uint)e.NewTime.TotalMilliseconds;
            }
        }

        private void Total_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            if (!_isFirstLoading)
            {
                _settings.DailyThreshold = (uint)e.NewTime.TotalMilliseconds;
                if (e.OldTime != e.NewTime)
                {
                    _reminderManager.ResetDailyDueTime();
                }
            }
        }
    }
}
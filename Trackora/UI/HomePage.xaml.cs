using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Threading.Tasks;
using Zscno.Trackora.Interfaces;
using Zscno.Trackora.Services;
using Zscno.Trackora.Tools;

// To learn more about WinUI, the WinUI project structure, and more about our project templates, see: http://aka.ms/winui-project-info.

namespace Zscno.Trackora.UI
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class HomePage : Page, INotifyPropertyChanged
    {
        /// <inheritdoc cref="IAppInfoManager"/>
        private readonly IAppInfoManager _appInfoManager;

        /// <inheritdoc cref="IReminderManager"/>
        private readonly IReminderManager _reminderManager;

        /// <inheritdoc cref="ISettings"/>
        private readonly ISettings _settings;

        /// <inheritdoc cref="IUsageRecordManager"/>
        private readonly IUsageRecordManager _usageRecordManager;

        private ObservableCollection<ProcessDisplayItem> appList { get; }

        private string dailyDuration => Localization.ToLocalizedTimeString(_usageRecordManager.Record.DailyDuration);

        private TimeSpan dailyThreshold => TimeSpan.FromMilliseconds(_settings.DailyThreshold);

        private TimeSpan idleThreshold => TimeSpan.FromMilliseconds(_settings.IdleThreshold);

        private TimeSpan sessionThreshold => TimeSpan.FromMilliseconds(_settings.SessionThreshold);

        public HomePage()
        {
            InitializeComponent();

            _usageRecordManager = App.GetService<IUsageRecordManager>();
            _appInfoManager = App.GetService<IAppInfoManager>();
            _settings = App.GetService<ISettings>();
            _reminderManager = App.GetService<IReminderManager>();
            appList = [];
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Continuous_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            _settings.SessionThreshold = (uint)e.NewTime.TotalMilliseconds;
        }

        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            //CachePath.Text = ApplicationData.Current.TemporaryFolder.Path;
            await UpdateAppListAsync();
        }

        private async void Refresh_Click(object sender, RoutedEventArgs e)
        {
            Button? button = sender as Button;
            button!.IsEnabled = false;
            await UpdateAppListAsync();
            OnPropertyChanged(nameof(dailyDuration));
            button.IsEnabled = true;
        }

        private void ResetContinuous_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            _settings.IdleThreshold = (uint)e.NewTime.TotalMilliseconds;
        }

        private void Total_TimeChanged(object sender, TimePickerValueChangedEventArgs e)
        {
            _settings.DailyThreshold = (uint)e.NewTime.TotalMilliseconds;
            if (e.OldTime != e.NewTime)
            {
                _reminderManager.ResetDailyDueTime();
            }
        }

        /// <summary>
        /// 异步地更新应用程序信息列表。
        /// </summary>
        private async Task UpdateAppListAsync()
        {
            List<ProcessDisplayItem> items = [];
            await Task.Run(() =>
            {
                foreach (var processUsageRecord in _usageRecordManager.Record.ProcessUsageRecords)
                {
                    string name =
                        _appInfoManager.AppInfoMap.TryGetValue(processUsageRecord.Key, out ProcessInfo? processInfo) ?
                        processInfo.DisplayName : processUsageRecord.Key;
                    string iconFileUri = _appInfoManager.GetAppIconUri(processUsageRecord.Key);
                    items.Add(new ProcessDisplayItem(iconFileUri, name, processUsageRecord.Value));
                }
            });

            appList.Clear();
            foreach (var item in items)
            {
                appList.Add(item);
            }
        }
    }
}
using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Core;
using Windows.Graphics.Imaging;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.Streams;
using static Zscno.Trackora.App;
using static Zscno.Trackora.LogSystem;

namespace Zscno.Trackora;

/// <summary>
/// 进程信息。
/// </summary>
internal class ProcessInfo
{
	/// <summary>
	/// 显示给用户的名称。
	/// </summary>
	public string DisplayName { get; set; }

	/// <summary>
	/// 图标的Uri。
	/// </summary>
	public string IconUri { get; set; }

	/// <summary>
	/// 进程名称。
	/// </summary>
	public string ProcessName { get; set; }

	/// <summary>
	/// 使用时长。
	/// </summary>
	[JsonIgnore]
	public string UsedTime { get; set; }
}

internal class WindowTracker
{
	/// <summary>
	/// 结束使用的时间。
	/// </summary>
	public static TimeSpan EndUsingTime
	{
		get => (TimeSpan) LocalSettings["EndUsingTime"];

		set => LocalSettings["EndUsingTime"] = value;
	}

	/// <summary>
	/// 总使用时长。
	/// </summary>
	public static TimeSpan TotalUsedTime
	{
		get => (TimeSpan) LocalSettings["TotalUsedTime"];

		private set => LocalSettings["TotalUsedTime"] = value;
	}

	/// <summary>
	/// 所有检测到的前台进程的名称及其使用时长。
	/// </summary>
	public static Dictionary<string, TimeSpan> WindowsUsedTime { get; private set; }

	/// <summary>
	/// 指示总使用时长提醒是否已经显示。
	/// </summary>
	public static bool HasTotalReminded
	{
		get => (bool) LocalSettings["HasTotalReminded"];

		set => LocalSettings["HasTotalReminded"] = value;
	}

	/// <summary>
	/// 以 <see cref="TimeSpan"/> 结构表示的 1 秒钟。
	/// </summary>
	private readonly TimeSpan _oneSecond = TimeSpan.FromSeconds(1);

	/// <summary>
	/// 记录当天进程名称和使用时长的文本文件路径。
	/// </summary>
	private readonly string _recordFilePath;

	/// <summary>
	/// 连续使用时长。
	/// </summary>
	private TimeSpan _continuousUsedTime;

	/// <summary>
	/// 当前正在记录信息的进程的名称。
	/// </summary>
	private string _currentRecordProcessName;

	/// <summary>
	/// 上一个窗口被激活的时间。
	/// </summary>
	private DateTime _lastActivationTime;

	/// <summary>
	/// 记录同一进程的连续使用时长以保证使用时长不超过 5 秒的进程不被记录。
	/// </summary>
	private TimeSpan _singleContinuousUsedTime;

	/// <summary>
	/// 用于过滤进程名称的字符串数组。
	/// </summary>
	private string[] _lastNoTimeNamesArr;

	/// <summary>
	/// 用于过滤进程名称的字符串（以英文逗号分隔）。
	/// </summary>
	private string _lastNoTimeNamesStr;

	/// <summary>
	/// 用于过滤只记录时间的进程名称的字符串数组
	/// </summary>
	private string[] _lastNotInfoNamesArr;

	/// <summary>
	/// 用于过滤只记录时间的进程名称的字符串（以英文逗号分隔）。
	/// </summary>
	private string _lastNoInfoNamesStr;

	/// <summary>
	/// 上一个检测到的被激活的进程。
	/// </summary>
	private Process _lastProcess;

	/// <summary>
	/// 上次记录连续使用时长的时间。
	/// </summary>
	private DateTime _lastRecordTime;

	/// <summary>
	/// 计时器。
	/// </summary>
	private DispatcherTimer _timer;

	public WindowTracker()
	{
		_recordFilePath = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path,
			"Record.dat");
		WindowsUsedTime = new();

		DateTimeOffset currentDate = new(DateTime.Now.Date);
		if (!LocalSettings.ContainsKey("Today") || (DateTimeOffset)
			LocalSettings["Today"] != currentDate)
		{
			// 如果今天的记录不存在或不是今天，则重置记录。
			LocalSettings["Today"] = currentDate;
			TotalUsedTime = TimeSpan.Zero;
			EndUsingTime = TimeSpan.Zero;
			HasTotalReminded = false;

			// 重置记录文件。
			try
			{
				File.WriteAllText(_recordFilePath, string.Empty);
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"在重置/创建记录文件 [{_recordFilePath}] 时触发异常：{ex}");
				CanSend = ReminderHelper.SendReminder("提示用户无法管理今天的记录",
					Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotSetRecord"), true);
			}
		}
		else
		{
			if (HasTotalReminded)
			{
				// 如果已经达到了今日使用时长，则每次启动都提醒。
				CanSend = ReminderHelper.SendReminder(ReminderKinds.TotalUsedTimeReminders);
			}

			// 获取记录的使用时长。
			try
			{
				string[] lines = GetUsedTime();
				foreach (string line in lines)
				{
					if (string.IsNullOrWhiteSpace(line))
					{
						// 如果有空行则跳过。
						if (line != lines[^1])
						{
							WriteLog(LogLevel.Warning, "记录文件中有空行。");
						}
						// 如果是最后一行不算。
						continue;
					}
					string[] keyValuePair = line.Split('|');
					if (keyValuePair.Length != 2 ||
						!double.TryParse(keyValuePair[1], out double result))
					{
						// 如果文本结构不正确则跳过。
						WriteLog(LogLevel.Warning, $"记录文件中的行格式不正确 [{line}] 。");
						continue;
					}
					WindowsUsedTime[keyValuePair[0]] = TimeSpan.FromSeconds(Convert.ToDouble(keyValuePair[1]));
				}
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"在读取记录文件 [{_recordFilePath}] 时触发异常：{ex}");
				CanSend = ReminderHelper.SendReminder("提示用户无法获取今天的记录",
					Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotGetRecord"), true);
			}
		}

		// 初始化并启动计时器。
		try
		{
			_timer = new();
			_timer.Tick += Timer_Tick;
			_timer.Interval = _oneSecond;
			_timer.Start();
		}
		catch (Exception ex)
		{
			WriteLog(LogLevel.Error, $"在初始化计时器时触发异常，将在提醒用户后退出：{ex}");
			CanSend = ReminderHelper.SendReminder("提示用户无法初始化计时器",
				Loader.GetString("ErrorOrWarningTitle"),
				Loader.GetString("ECanNotInitTimer"), true);
			Application.Current.Exit();
		}
	}

	/// <summary>
	/// 获取本地化时间 / 时长。
	/// </summary>
	/// <param name="time">一个时间 / 时长。</param>
	/// <returns>本地化时间 / 时长字符串。</returns>
	public static string GetLocalTime(TimeSpan time)
	{
		if (time.Days == 0 && time.Hours == 0 && time.Minutes == 0)
		{
			return "< 1" + Loader.GetString("Minute");
		}
		else if (time.Days == 0 && time.Hours == 0)
		{
			return time.Minutes + Loader.GetString("Minute");
		}
		else if (time.Days == 0)
		{
			return time.Hours + Loader.GetString("Hour")
				+ time.Minutes + Loader.GetString("Minute");
		}
		else
		{
			return time.Days + Loader.GetString("Day")
				+ time.Hours + Loader.GetString("Hour")
				+ time.Minutes + Loader.GetString("Minute");
		}
	}

	/// <summary>
	/// 获取进程名称、图标及使用的时长。
	/// </summary>
	/// <param name="count">需要获取的数量（以使用时长正序排列）。</param>
	/// <returns>使用时长最长的 <paramref name="count"/> 个进程名称、图标和时长。</returns>
	public static List<ProcessInfo> GetProcessesInfo(int count)
	{
		// 获取使用时长最长的六个进程的名称。
		List<string> processNames = WindowsUsedTime
				.OrderByDescending(x => x.Value)
				.Take(count)
				.Select(x => x.Key)
				.ToList();

		string processesListText;
		try
		{
			processesListText = File.Exists(InfoFilePath) ?
				File.ReadAllText(InfoFilePath) : string.Empty;
		}
		catch (Exception ex)
		{
			throw new Exception($"在获取记录文件 [Path={InfoFilePath}] 的文本时触发了异常。", ex);
		}

		List<ProcessInfo> processesInfo = new();
		bool isEmpty = string.IsNullOrWhiteSpace(processesListText);
		if (isEmpty)
		{
			WriteLog(LogLevel.Warning, $"程序尚未开始监测和记录（json 文件中无内容） [Path={InfoFilePath}] 。");
		}

		Lazy<Dictionary<string, ProcessInfo>> processesDict = new(() =>
		{
			try
			{
				List<ProcessInfo> list = JsonSerializer.Deserialize<List<ProcessInfo>>(processesListText);
				Dictionary<string, ProcessInfo> dict = list.ToDictionary(value => value.ProcessName);
				return dict;
			}
			catch (Exception ex)
			{
				throw new Exception($"在反序列化记录文件 [Path={InfoFilePath}] 时触发了异常。", ex);
			}
		});

		foreach (string name in processNames)
		{
			ProcessInfo info;

			if (isEmpty)
			{
				info = GetDefaultInfo(name);
			}
			else if (!processesDict.Value.TryGetValue(name, out info))
			{
				// 如果 json 文件中已有记录，则反序列化并查找当前进程。
				WriteLog(LogLevel.Warning, $"在记录文件 [Path={InfoFilePath}] 中未找到进程 {name} 的信息。");
				info = GetDefaultInfo(name);
			}
			info.UsedTime = GetLocalTime(WindowsUsedTime[name]);
			processesInfo.Add(info);
		}
		return processesInfo;
	}

	/// <summary>
	/// 获取默认的进程信息（当无法获取到进程信息时使用）。
	/// </summary>
	/// <param name="process">要获取的进程。</param>
	/// <returns>默认的进程信息。</returns>
	private static ProcessInfo GetDefaultInfo(Process process)
	{
		string title = process.MainWindowTitle;
		return new()
		{
			ProcessName = process.ProcessName,
			DisplayName = string.IsNullOrWhiteSpace(title) ? process.ProcessName : title,
			IconUri = "ms-appx:///Assets/DefaultIcon.png",
		};
	}

	/// <summary>
	/// 获取默认的进程信息（当 json 文件中没有进程信息时使用）。
	/// </summary>
	/// <param name="processName">要获取的进程的名称。</param>
	/// <returns>默认的进程信息。</returns>
	private static ProcessInfo GetDefaultInfo(string processName)
	{
		return new()
		{
			ProcessName = processName,
			DisplayName = processName,
			IconUri = "ms-appx:///Assets/DefaultIcon.png",
		};
	}

	/// <summary>
	/// 获取记录文件中的记录。如果文件中没有内容就返回空数组。
	/// </summary>
	/// <returns>以换行符分隔的数组，包含了进程名称和使用时长。</returns>
	private string[] GetUsedTime()
	{
		using FileStream fstream = new(_recordFilePath, FileMode.OpenOrCreate,
			FileAccess.Read, FileShare.ReadWrite);
		if (fstream.Length > 0)
		{
			using BinaryReader breader = new(fstream, Encoding.UTF8);
			string text = breader.ReadString();
			return text.Split("\r\n");
		}
		else
		{
			return Array.Empty<string>();
		}
	}

	/// <summary>
	/// 当没有获取到符合条件的进程时调用。
	/// </summary>
	private void NoProcessNow()
	{
		if (_lastProcess == null)
		{
			// 如果上次没有记录:
			if (DateTime.Now - _lastRecordTime >= (TimeSpan) LocalSettings["ContinuousUsedResetTime"]
				&& _continuousUsedTime != TimeSpan.Zero)
			{
				// 如果上次记录的时间超过指定时间，则刷新连续使用时长。
				_continuousUsedTime = TimeSpan.Zero;
			}
		}
		else
		{
			// 如果上次有记录，则记录使用时长。
			try
			{
				RecordUsedTime();
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, ex.ToString());
				CanSend = ReminderHelper.SendReminder("提示用户无法记录时间",
					Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotRecordTime"), true);
			}
			_lastProcess = null;
		}
	}

	/// <summary>
	/// 记录进程信息到 JSON 文件中。
	/// </summary>
	private async Task RecordProcessInfo()
	{
		Process process = _lastProcess;
		string name = process.ProcessName;
		// 如果已经在记录进程则退出，防止重复记录。
		if (name == _currentRecordProcessName)
		{
			return;
		}
		_currentRecordProcessName = name;

		List<ProcessInfo> processesInfo;
		try
		{
			using FileStream textStream = new(InfoFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
			// 如果文件中有内容则反序列化，如果结果为 null 或没有内容则创建新列表。
			processesInfo = textStream.Length > 0 ?
				JsonSerializer.Deserialize<List<ProcessInfo>>(textStream) ?? new() : new();
		}
		catch (Exception ex)
		{
			WriteLog(LogLevel.Error, $"在读取记录文件 [{InfoFilePath}] 时触发异常：{ex}");
			CanSend = ReminderHelper.SendReminder("提示用户无法读取进程信息",
				Loader.GetString("ErrorOrWarningTitle"), Loader.GetString("ECanNotGetInfo"), true);
			_currentRecordProcessName = null;
			return;
		}

		if (processesInfo.Any(info => info.ProcessName == process.ProcessName))
		{
			_currentRecordProcessName = null;
			return;
		}

		ProcessInfo info;
		uint packageFullNameLength = 0;
		long result;
		try
		{
			result = SystemHelper.GetPackageFullName(process.Handle, ref packageFullNameLength, null);
		}
		catch (Win32Exception we) when (we.NativeErrorCode == SystemHelper.ERROR_ACCESS_DENIED)
		{
			WriteLog(LogLevel.Warning, $"获取进程 {name} 的包全名时触发异常：{we}");
			info = GetDefaultInfo(process);
			goto Finish;
		}
		catch (Exception ex)
		{
			WriteLog(LogLevel.Error, $"获取进程 {name} 的包全名时触发异常：{ex}");
			info = GetDefaultInfo(process);
			goto Finish;
		}

		if (result == SystemHelper.APPMODEL_ERROR_NO_PACKAGE)
		{
			// 如果是 Win32 应用：
			string path;
			try
			{
				path = process.MainModule.FileName;
			}
			catch (Win32Exception we) when (we.NativeErrorCode == SystemHelper.ERROR_ACCESS_DENIED)
			{
				WriteLog(LogLevel.Warning, $"获取进程 {name} 的包全名时触发异常：{we}");
				info = GetDefaultInfo(process);
				goto Finish;
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"获取进程 {name} 的路径时触发异常：{ex}");
				info = GetDefaultInfo(process);
				goto Finish;
			}

			string iconUri;
			string defaultIconUri = "ms-appx:///Assets/DefaultIcon.png";
			try
			{
				Icon preIcon = Icon.ExtractAssociatedIcon(path);
				Icon icon = new(preIcon, 32, 32);
				preIcon.Dispose();

				if (icon != null)
				{
					// 加载图标并将图标保存到缓存文件夹中。
					string iconPath = Path.Combine(ApplicationData.Current.LocalCacheFolder.Path,
						"Icons", $"{name}.png");
					using FileStream iconStream = new(iconPath, FileMode.Create,
						FileAccess.ReadWrite, FileShare.None);
					icon.ToBitmap().Save(iconStream, ImageFormat.Png);
					icon.Dispose();

					iconUri = new Uri(iconPath).ToString();
				}
				else
				{
					WriteLog(LogLevel.Warning, $"出于未知原因，未获取到进程 {name} 的图标。");
					iconUri = defaultIconUri;
				}
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"在保存进程 {name} 的图标时触发异常：{ex}");
				iconUri = defaultIconUri;
			}

			string displayName = process.MainModule.FileVersionInfo.FileDescription;
			info = new()
			{
				ProcessName = name,
				DisplayName = string.IsNullOrWhiteSpace(displayName) ?
				(string.IsNullOrWhiteSpace(process.MainWindowTitle) ? name : process.MainWindowTitle)
				: displayName,
				IconUri = iconUri
			};
		}
		else if (result == SystemHelper.ERROR_INSUFFICIENT_BUFFER)
		{
			// 如果是有应用包的应用：
			StringBuilder packageFullName = new((int) packageFullNameLength);
			result = SystemHelper.GetPackageFullName(process.Handle, ref packageFullNameLength, packageFullName);
			if (result != SystemHelper.ERROR_SUCCESS)
			{
				WriteLog(LogLevel.Error, $"获取进程 {name} 的包全名时触发异常，错误代码：{Marshal.GetLastWin32Error()}。");
				info = GetDefaultInfo(process);
				goto Finish;
			}
			else
			{
				// 如果获取包全名成功：
				PackageManager packageManager = new();
				Package[] packages = packageManager.FindPackagesForUser(string.Empty)
					.OrderByDescending(pkg => pkg.Id.FullName.Length)
					.ToArray();
				Package package = packages.FirstOrDefault(pkg => pkg.Id.FullName
				== packageFullName.ToString());

				if (package == null)
				{
					WriteLog(LogLevel.Warning, $"出于未知原因，未找到进程 {name} 的包信息。");
					info = GetDefaultInfo(process);
					goto Finish;
				}

				IReadOnlyList<AppListEntry> appListEntries = await package.GetAppListEntriesAsync();
				AppListEntry appListEntry = appListEntries.Count > 0 ? appListEntries[0] : null;
				if (appListEntries == null)
				{
					WriteLog(LogLevel.Warning, $"出于未知原因，未获取到包 {name} 的显示信息。");
					info = GetDefaultInfo(process);
					goto Finish;
				}

				AppDisplayInfo displayinfo = appListEntry.DisplayInfo;
				RandomAccessStreamReference iconStreamRef = displayinfo.GetLogo(new(32, 32));
				if (iconStreamRef == null)
				{
					WriteLog(LogLevel.Warning, $"出于未知原因，未获取到包 {name} 的图标。");
					info = GetDefaultInfo(process);
					goto Finish;
				}

				// 加载图标并将图标保存到缓存文件夹中。
				string iconUri;
				try
				{
					IRandomAccessStreamWithContentType iconStream = await iconStreamRef.OpenReadAsync();

					// 获取图标的实际内容范围。
					BitmapDecoder decoder = await BitmapDecoder.CreateAsync(iconStream);
					PixelDataProvider pixelData = await decoder.GetPixelDataAsync();
					byte[] pixels = pixelData.DetachPixelData();
					uint width = decoder.PixelWidth, height = decoder.PixelHeight;
					uint x = 0, y = 0, w = 0, h = 0;
					for (uint i = 0; i < height; i++)
					{
						for (uint j = 0; j < width; j++)
						{
							if (pixels[(i * width + j) * 4 + 3] >= 20)
							{
								if (x == 0 || i < x)
								{
									x = i;
								}
								if (y == 0 || j < y)
								{
									y = j;
								}
								if (w == 0 || i > w)
								{
									w = i;
								}
								if (h == 0 || j > h)
								{
									h = j;
								}
							}
						}
					}

					// 图标实际内容的宽和高至少为 32 像素且必须是正方形。
					uint cropWidth = w - x + 1, cropHeight = h - y + 1;
					if (cropWidth < 32 || cropHeight < 32)
					{
						cropWidth = cropHeight = 32;
						x = (width - cropWidth) / 2;
						y = (height - cropHeight) / 2;
					}
					else if (cropWidth > cropHeight)
					{
						cropHeight = cropWidth;
						y = (height - cropHeight) / 2;
					}
					else if (cropHeight > cropWidth)
					{
						cropWidth = cropHeight;
						x = (width - cropWidth) / 2;
					}
					x = x < 0 ? 0 : x;
					y = y < 0 ? 0 : y;

					//裁剪图标。
					InMemoryRandomAccessStream croppedStream = new();
					BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(croppedStream, decoder);
					encoder.BitmapTransform.Bounds = new()
					{
						X = x,
						Y = y,
						Width = cropWidth,
						Height = cropHeight,
					};
					await encoder.FlushAsync();
					croppedStream.Seek(0);

					// 保存图标。
					StorageFolder iconfolder = await StorageFolder.GetFolderFromPathAsync(
						Path.Combine(ApplicationData.Current.LocalCacheFolder.Path, "Icons"));
					StorageFile iconFile = await iconfolder.CreateFileAsync(
						$"{name}.png", CreationCollisionOption.ReplaceExisting);
					_ = await RandomAccessStream.CopyAndCloseAsync(
						croppedStream, await iconFile.OpenAsync(FileAccessMode.ReadWrite));

					iconUri = new Uri(Path.Combine(ApplicationData.Current.LocalCacheFolder.Path,
						"Icons", $"{name}.png")).ToString();
				}
				catch (Exception ex)
				{
					WriteLog(LogLevel.Error, $"在保存进程 {name} 的图标时触发异常：{ex}");
					iconUri = "ms-appx:///Assets/DefaultIcon.png";
				}

				info = new()
				{
					ProcessName = name,
					DisplayName = string.IsNullOrWhiteSpace(package.DisplayName) ?
					(string.IsNullOrWhiteSpace(process.MainWindowTitle) ? name :
					process.MainWindowTitle) : package.DisplayName,
					IconUri = iconUri
				};
			}
		}
		else
		{
			WriteLog(LogLevel.Error, $"获取进程 {name} 的包全名时触发异常，错误代码：{Marshal.GetLastWin32Error()}。");
			info = GetDefaultInfo(process);
		}

		Finish:
		processesInfo.Add(info);
		try
		{
			File.WriteAllText(InfoFilePath, JsonSerializer.Serialize(processesInfo,
				new JsonSerializerOptions { WriteIndented = true }));
		}
		catch (Exception ex)
		{
			WriteLog(LogLevel.Error, $"写入记录文件 [{InfoFilePath}] 时触发异常：{ex}");
			CanSend = ReminderHelper.SendReminder("提示用户无法写入记录文件",
				Loader.GetString("ErrorOrWarningTitle"), Loader.GetString("ECanNotWriteInfo"), true);
			return;
		}
		_currentRecordProcessName = null;
		WriteLog(LogLevel.Info, $"已记录进程 {name} 的信息。");
	}

	/// <summary>
	/// 获取一个 <see langword="bool"/> 值，指示是否需要 <paramref name="processName"/> 进程的信息。
	/// </summary>
	/// <param name="processName">进程的名称。</param>
	/// <returns>指示是否需要 <paramref name="processName"/> 进程的信息。</returns>
	private bool WhetherNeedInfo(string processName)
	{
		string[] noInfoNamesArr;
		string noInfoNamesStr = (string) LocalSettings["NoInfoNames"];
		if (_lastNoTimeNamesStr != noInfoNamesStr)
		{
			// 如果过滤字符串有更新，则更新缓存。
			_lastNotInfoNamesArr = noInfoNamesStr.Split(',');
			_lastNoInfoNamesStr = noInfoNamesStr;
		}
		noInfoNamesArr = _lastNotInfoNamesArr;
		return !noInfoNamesArr.Contains(processName);
	}

	/// <summary>
	/// 记录上次被激活窗口的使用时长。
	/// </summary>
	private void RecordUsedTime()
	{
		string name = _lastProcess.ProcessName;

		if (!WhetherNeedInfo(name))
		{
			// 如果不需要信息则不在 WindowsUsedTime 和记录文件中记录时长。
			return;
		}

		TimeSpan usedTime;
		TimeSpan totalUsedTime;

		// 在 WindowsUsedTime 中记录上次被激活窗口的使用时长。
		try
		{
			usedTime = DateTime.Now - _lastActivationTime;
			totalUsedTime = WindowsUsedTime.TryGetValue(name, out TimeSpan pastUsedTime) ?
				pastUsedTime + usedTime : usedTime;
			WindowsUsedTime[name] = totalUsedTime;
		}
		catch (Exception ex)
		{
			throw new Exception("在记录上次被激活窗口的使用时长到 WindowsUsedTime 中时触发了异常。", ex);
		}

		// 将使用时长记录到记录文件中：
		try
		{
			// 获取记录文件的所有行。
			string[] lines = GetUsedTime();
			StringBuilder writeLines = new();
			bool hasFound = false;

			foreach (string line in lines)
			{
				if (line.StartsWith(name))
				{
					// 如果在文件中找到了进程，则更新记录。
					_ = writeLines.AppendLine($"{name}|{totalUsedTime.TotalSeconds}");
					hasFound = true;
				}
				else if (!string.IsNullOrWhiteSpace(line))
				{
					// 否则直接复制。
					_ = writeLines.AppendLine(line);
				}
			}

			if (!hasFound)
			{
				// 如果没找着则追加记录。
				_ = writeLines.AppendLine($"{name}|{totalUsedTime.TotalSeconds}");
			}

			using FileStream fstream = new(_recordFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
			using BinaryWriter bwriter = new(fstream, Encoding.UTF8);
			bwriter.Write(writeLines.ToString());
		}
		catch (Exception ex)
		{
			throw new Exception($"在记录上次被激活窗口的使用时长到文件 [{_recordFilePath}] 中时触发了异常。", ex);
		}
		//WriteLog(LogLevel.Debug, $"已记录进程 {_lastProcess.ProcessName} 的使用时长：{usedTime:hh\\:mm\\:ss} 。");
	}

	private void Timer_Tick(object sender, object e)
	{
		// 检查是否达到了结束使用时间。
		TimeSpan currentTimeWithoutSeconds = new(DateTime.Now.Hour, DateTime.Now.Minute, 0);
		if (EndUsingTime == currentTimeWithoutSeconds && EndUsingTime != TimeSpan.Zero)
		{
			CanSend = ReminderHelper.SendReminder(ReminderKinds.EndUsingTimeReminders);
			EndUsingTime = TimeSpan.Zero;
		}

		IntPtr windowHandle = SystemHelper.GetForegroundWindow();

		if (windowHandle == IntPtr.Zero)
		{
			// 如果桌面上没有被激活窗口:
			//WriteLog(LogLevel.Debug, "没有被激活窗口。");
			NoProcessNow();
			return;
		}

		// 获取被激活窗口的进程ID。
		if (SystemHelper.GetWindowThreadProcessId(windowHandle, out uint processId) == 0)
		{
			WriteLog(LogLevel.Error, $"获取进程 ID [Handle={windowHandle}] 时触发异常，错误代码：{Marshal.GetLastWin32Error()}。");
			NoProcessNow();
			return;
		}

		// 通过进程ID获取进程信息。
		Process process;
		try
		{
			process = Process.GetProcessById((int) processId);
		}
		catch (Exception ex)
		{
			WriteLog(LogLevel.Error, $"获取进程信息 [ID={processId}] 时触发异常：{ex}");
			NoProcessNow();
			return;
		}

		string name = process.ProcessName;

		// 过滤无需记录的进程。
		string[] noTimeNamesArr;
		string noTimeNamesStr = (string) LocalSettings["NoTimeNames"];
		if (_lastNoTimeNamesStr != noTimeNamesStr)
		{
			// 如果过滤字符串有更新，则更新缓存。
			_lastNoTimeNamesArr = noTimeNamesStr.Split(',');
			_lastNoTimeNamesStr = noTimeNamesStr;
		}
		noTimeNamesArr = _lastNoTimeNamesArr;

		if (noTimeNamesArr.Contains(name))
		{
			// 如果是无需记录的进程：
			NoProcessNow();
			return;
		}

		if (name == "explorer")
		{
			// 判断是桌面还是用户打开的窗口：

			IntPtr? childHandle = SystemHelper.FindWindowEx(windowHandle, IntPtr.Zero, null, null);

			if (childHandle == null)
			{
				WriteLog(LogLevel.Error, $"获取 explorer 子进程的句柄时触发异常，错误代码：{Marshal.GetLastWin32Error()}。");
				NoProcessNow();
				return;
			}
			if (childHandle == IntPtr.Zero)
			{
				//WriteLog(LogLevel.Debug, $"没有被激活窗口。");
				NoProcessNow();
				return;
			}

			StringBuilder className = new(256);
			int classNameLength = SystemHelper.GetClassName((IntPtr) childHandle, className, className.Capacity);

			if (classNameLength == 0)
			{
				WriteLog(LogLevel.Error, $"获取 explorer 子进程 [Handle={childHandle}] 的类名时触发异常，错误代码：{Marshal.GetLastWin32Error()}。");
				NoProcessNow();
				return;
			}

			if (className.ToString() is "Windows.UI.Core.CoreWindow" or "SHELLDLL_DefView")
			{
				// 如果是任务栏或者桌面则不记录。
				//WriteLog(LogLevel.Debug, $" explorer 子进程 [ClassName={_className}] 是任务栏或者桌面。");
				NoProcessNow();
				return;
			}
			// 如果是用户打开的窗口，则继续记录。
		}

		if (name == "ApplicationFrameHost")
		{
			// 如果是 UWP 进程的宿主进程，则获取实际 UWP 进程的实例。

			IntPtr? childHandle = SystemHelper.FindWindowEx(windowHandle, IntPtr.Zero, "Windows.UI.Core.CoreWindow", null);

			if (childHandle == null)
			{
				WriteLog(LogLevel.Error, $"获取 UWP 进程句柄时触发异常，错误代码：{Marshal.GetLastWin32Error()}。");
				NoProcessNow();
				return;
			}

			if (childHandle == IntPtr.Zero)
			{
				WriteLog(LogLevel.Warning, "未获取到 UWP 进程句柄。");
				NoProcessNow();
				return;
			}

			if (SystemHelper.GetWindowThreadProcessId((IntPtr) childHandle, out uint UWPId) == 0)
			{
				WriteLog(LogLevel.Error, $"获取 UWP 进程ID [Handle={childHandle}] 时触发异常，错误代码：{Marshal.GetLastWin32Error()}。");
				NoProcessNow();
				return;
			}

			try
			{
				process = Process.GetProcessById((int) UWPId);
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, $"获取 UWP 进程信息 [ID={UWPId}] 时触发异常：{ex}");
				NoProcessNow();
				return;
			}
		}

		// 更新总使用时长和连续使用时长。
		TotalUsedTime += _oneSecond;
		// 如果窗口被激活时长超过 5 秒则记录。
		if (_singleContinuousUsedTime > TimeSpan.FromSeconds(5))
		{
			// 如果时长到达 6 秒则把之前的 6 秒全部加上。 一般情况加 1 秒。
			_continuousUsedTime += _singleContinuousUsedTime == TimeSpan.FromSeconds(6) ?
			TimeSpan.FromSeconds(6) : _oneSecond;
		}
		_lastRecordTime = DateTime.Now;

		//WriteLog(LogLevel.Debug, $"当前连续使用时长：{_continuousUsedTime:hh\\:mm\\:ss}");

		// 检查是否需要显示提醒通知。
		if (TotalUsedTime >= (TimeSpan) LocalSettings["TotalUsedRemindTime"]
			&& !HasTotalReminded)
		{
			CanSend = ReminderHelper.SendReminder(ReminderKinds.TotalUsedTimeReminders);
			HasTotalReminded = true;
		}
		if (_continuousUsedTime >= (TimeSpan) LocalSettings["ContinuousUsedRemindTime"]
			&& _continuousUsedTime != TimeSpan.Zero)
		{
			CanSend = ReminderHelper.SendReminder(ReminderKinds.ContinuousUsedTimeReminders);
			_continuousUsedTime = TimeSpan.Zero;
		}

		if (_lastProcess != null)
		{
			// 如果上次有记录：
			if (_lastProcess.ProcessName == name)
			{
				// 如果被激活窗口没有变化，则不记录但增加连续使用时长。
				_singleContinuousUsedTime += _oneSecond;
				return;
			}

			_singleContinuousUsedTime = TimeSpan.Zero;
			try
			{
				RecordUsedTime();
			}
			catch (Exception ex)
			{
				WriteLog(LogLevel.Error, ex.ToString());
				CanSend = ReminderHelper.SendReminder("提示用户无法记录时间",
					Loader.GetString("ErrorOrWarningTitle"),
					Loader.GetString("ECanNotRecordTime"), true);
			}
		}

		//记录这次的进程实例、信息和激活时间。
		_lastProcess = process;
		_lastActivationTime = DateTime.Now;

		if (WhetherNeedInfo(name))
		{
			// 如果进程不是只记录使用时长的则记录信息。
			_ = Task.Run(RecordProcessInfo);
		}
	}
}
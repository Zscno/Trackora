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
using System.Text.Json.Serialization.Metadata;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Graphics.Imaging;
using Windows.Management.Deployment;
using Windows.Storage;
using Windows.Storage.Streams;
using static Zscno.Trackora.App;
using static Zscno.Trackora.LogSystem;

namespace Zscno.Trackora
{
	/// <summary>
	/// 进程信息。
	/// </summary>
	internal class ProcessInfo
	{
		/// <summary>
		/// 显示给用户的名称。
		/// </summary>
		public string DisplayName { get; set; } = string.Empty;

		/// <summary>
		/// 图标的Uri。
		/// </summary>
		public string IconUri { get; set; } = string.Empty;

		/// <summary>
		/// 进程名称。
		/// </summary>
		public string ProcessName { get; set; } = string.Empty;

		/// <summary>
		/// 使用时长。
		/// </summary>
		[JsonIgnore]
		public string UsedTime { get; set; } = string.Empty;
	}

	internal class WindowTracker
	{
		/// <summary>
		/// 用于记录的所有检测到进程的名称及其使用时长（包含只记录时间的进程）。
		/// </summary>
		private static readonly Dictionary<string, TimeSpan> _windowsUsedTime = [];

		/// <summary>
		/// 当无法获取到进程图标时使用的默认图标。
		/// </summary>
		private static readonly string defaultIconUri = "ms-appx:///Icons/Default.png";

		/// <summary>
		/// Json 序列化时使用的配置。
		/// </summary>
		private static JsonWriterOptions _jsonOptions;

		/// <summary>
		/// 用于过滤只记录时间的进程名称的字符串（以英文逗号分隔）。
		/// </summary>
		private static string _lastNoInfoNamesStr = string.Empty;

		/// <summary>
		/// 用于过滤只记录时间的进程名称的字符串数组
		/// </summary>
		private static string[] _lastNotInfoNamesArr = [];

		/// <summary>
		/// 用于触发提醒的总使用时长。
		/// </summary>
		private static TimeSpan _totalUsedTime;

		/// <summary>
		/// 以 <see cref="TimeSpan"/> 结构表示的 1 秒钟。
		/// </summary>
		private readonly TimeSpan _oneSecond = TimeSpan.FromSeconds(1);

		/// <summary>
		/// 记录当天进程名称和使用时长的文本文件路径。
		/// </summary>
		private readonly string _recordFilePath;

		/// <summary>
		/// 计时器。
		/// </summary>
		private readonly DispatcherTimer _timer = new();

		/// <summary>
		/// 连续使用时长。
		/// </summary>
		private TimeSpan _continuousUsedTime;

		/// <summary>
		/// 当前正在记录信息的进程的名称。
		/// </summary>
		private string _currentRecordProcessName = string.Empty;

		/// <summary>
		/// 上一个窗口被激活的时间。
		/// </summary>
		private DateTime _lastActivationTime;

		/// <summary>
		/// 用于过滤进程名称的字符串数组。
		/// </summary>
		private string[] _lastNoTimeNamesArr = [];

		/// <summary>
		/// 用于过滤进程名称的字符串（以英文逗号分隔）。
		/// </summary>
		private string _lastNoTimeNamesStr = string.Empty;

		/// <summary>
		/// 上一个检测到的被激活的进程。
		/// </summary>
		private Process? _lastProcess;

		/// <summary>
		/// 上次记录连续使用时长的时间。
		/// </summary>
		private DateTime _lastRecordTime;

		/// <summary>
		/// 记录同一进程的连续使用时长以保证使用时长不超过 5 秒的进程不被记录。
		/// </summary>
		private TimeSpan _singleContinuousUsedTime;

		/// <summary>
		/// 结束使用的时间。
		/// </summary>
		public static TimeSpan EndUsingTime
		{
			get => (TimeSpan)LocalSettings["EndUsingTime"];

			set => LocalSettings["EndUsingTime"] = value;
		}

		/// <summary>
		/// 指示总使用时长提醒是否已经显示。
		/// </summary>
		public static bool HasTotalReminded { get; set; }

		/// <summary>
		/// 用于显示的总使用时长。
		/// </summary>
		public static TimeSpan TotalUsedTime
		{
			get => (TimeSpan)LocalSettings["TotalUsedTime"];

			private set
			{
				LocalSettings["TotalUsedTime"] = value;
				_totalUsedTime = value;
			}
		}

		/// <summary>
		/// 用于显示的所有检测到进程的名称及其使用时长（不包含只记录时间的进程）。
		/// </summary>
		public static Dictionary<string, TimeSpan> WindowsUsedTime
		{
			get
			{
				return _windowsUsedTime
					.Where(pair => !GetNoInfoArr().Contains(pair.Key))
					.ToDictionary(pair => pair.Key, pair => pair.Value);
			}
		}

		public WindowTracker()
		{
			_recordFilePath = Path.Join(LocalCachePath,
				"Record.dat");
#if DEBUG
			_jsonOptions.Indented = true;
#endif

			if (!LocalSettings.TryGetValue("Today", out object? today) ||
				(DateTimeOffset)today != new DateTimeOffset(DateTime.Now.Date))
			{
				_ = new SafeCaller() { RemindingMsgResKey = "ECanNotSetRecord" }
				.CallMethodR(ResetRecord);
			}
			else
			{
				// 获取记录的使用时长。
				_totalUsedTime = TotalUsedTime;
				_ = new SafeCaller() { RemindingMsgResKey = "ECanNotGetRecord" }
				.CallMethodR(GetUsedTimeFromRecordFile);

				// 如果已经达到了今日使用时长，则每次启动都提醒。
				SendTotalReminderIfNeed();
			}

			// 初始化并启动计时器。
			_ = new SafeCaller()
			{
				LogMessage = "无法启动计时器。",
				NeedExit = true,
				RemindingMsgResKey = "ECanNotStartTimer",
			}.CallMethodR(() =>
			{
				_timer.Tick += Timer_Tick;
				_timer.Interval = _oneSecond;
				_timer.Start();
			});
		}

		/// <summary>
		/// 裁剪图标。
		/// </summary>
		/// <param name="decoder">图标的 <see cref="BitmapDecoder"/> 实例。</param>
		/// <param name="x">要裁剪区域左上角的 X 坐标。</param>
		/// <param name="y">要裁剪区域左上角的 Y 坐标。</param>
		/// <param name="w">要裁剪区域的宽度。</param>
		/// <param name="h">要裁剪区域的高度。</param>
		/// <returns>裁剪后的图标所在的位置归零的流。</returns>
		private static async Task<InMemoryRandomAccessStream> CropIcon
			(BitmapDecoder decoder, uint x, uint y, uint w, uint h)
		{
			InMemoryRandomAccessStream croppedStream = new();
			BitmapEncoder encoder = await BitmapEncoder.CreateForTranscodingAsync(croppedStream, decoder);
			encoder.BitmapTransform.Bounds = new BitmapBounds
			{
				X = x,
				Y = y,
				Width = w,
				Height = h,
			};
			await encoder.FlushAsync();
			croppedStream.Seek(0);
			return croppedStream;
		}

		/// <summary>
		/// 从下向上遍历每一行，找到最下边有内容的像素行。
		/// </summary>
		/// <param name="pixels">图标的像素数据。</param>
		/// <param name="width">图标的宽度。</param>
		/// <param name="height">图标的高度。</param>
		/// <param name="minX">图标内容最左侧的 X 坐标。</param>
		/// <param name="maxX">图像内容最右侧的 X 坐标。</param>
		/// <returns>图像内容最低的 Y 坐标。未找到则返回 0 。</returns>
		private static uint ForeachFromBottomToTop(byte[] pixels, uint width, uint height,
			uint minX, uint maxX)
		{
			for (uint row = height - 1; row >= 0; row--)
			{
				for (uint pixel = minX; pixel <= maxX; pixel++)
				{
					if (pixels[(row * width + pixel) * 4 + 3] > 0)
					{
						return row;
					}
				}
			}
			return 0;
		}

		/// <summary>
		/// 从左向右遍历每一列，找到最左边有内容的像素列。
		/// </summary>
		/// <param name="pixels">图标的像素数据。</param>
		/// <param name="width">图标的宽度。</param>
		/// <param name="height">图标的高度。</param>
		/// <returns>图像内容最左侧的 X 坐标。未找到则返回 0 。</returns>
		private static uint ForeachFromLeftToRight(byte[] pixels, uint width, uint height)
		{
			for (uint column = 0; column < width; column++)
			{
				for (uint pixel = 0; pixel < height; pixel++)
				{
					if (pixels[(pixel * width + column) * 4 + 3] > 0)
					{
						return column;
					}
				}
			}
			return 0;
		}

		/// <summary>
		/// 从右向左遍历每一列，找到最右边有内容的像素列。
		/// </summary>
		/// <param name="pixels">图标的像素数据。</param>
		/// <param name="width">图标的宽度。</param>
		/// <param name="height">图标的高度。</param>
		/// <returns>图像内容最右侧的 X 坐标。未找到则返回 0 。</returns>
		private static uint ForeachFromRightToLeft(byte[] pixels, uint width, uint height)
		{
			for (uint column = width - 1; column >= 0; column--)
			{
				for (uint pixel = 0; pixel < height; pixel++)
				{
					if (pixels[(pixel * width + column) * 4 + 3] > 0)
					{
						return column;
					}
				}
			}
			return 0;
		}

		/// <summary>
		/// 从上向下遍历每一行，找到最上边有内容的像素行。
		/// </summary>
		/// <param name="pixels">图标的像素数据。</param>
		/// <param name="width">图标的宽度。</param>
		/// <param name="height">图标的高度。</param>
		/// <param name="minX">图标内容最左侧的 X 坐标。</param>
		/// <param name="maxX">图像内容最右侧的 X 坐标。</param>
		/// <returns>图像内容最高的 Y 坐标。未找到则返回 0 。</returns>
		private static uint ForeachFromTopToBottom(byte[] pixels, uint width, uint height,
			uint minX, uint maxX)
		{
			for (uint row = 0; row < height; row++)
			{
				for (uint pixel = minX; pixel <= maxX; pixel++)
				{
					if (pixels[(row * width + pixel) * 4 + 3] > 0)
					{
						return row;
					}
				}
			}
			return 0;
		}

		/// <summary>
		/// 获取默认的进程信息（当无法获取到进程信息时使用）。
		/// </summary>
		/// <param name="process">要获取的进程。</param>
		/// <returns>默认的进程信息。</returns>
		private static ProcessInfo GetDefaultInfo(Process process)
		{
			string title = process.MainWindowTitle;
			return new ProcessInfo
			{
				ProcessName = process.ProcessName,
				DisplayName = GetDisplayName(process.ProcessName, title),
				IconUri = defaultIconUri,
			};
		}

		/// <summary>
		/// 获取默认的进程信息（当 json 文件中没有进程信息时使用）。
		/// </summary>
		/// <param name="processName">要获取的进程的名称。</param>
		/// <returns>默认的进程信息。</returns>
		private static ProcessInfo GetDefaultInfo(string processName)
		{
			return new ProcessInfo
			{
				ProcessName = processName,
				DisplayName = processName,
				IconUri = defaultIconUri,
			};
		}

		/// <summary>
		/// 获取进程的显示名称（保证获取到的名称一定不是空白）。
		/// </summary>
		/// <param name="processName">进程名称。</param>
		/// <param name="windowTitle">窗口标题。</param>
		/// <param name="friendlyName">友好名称。</param>
		/// <returns>进程的显示名称。</returns>
		private static string GetDisplayName(string processName,
			string windowTitle, string? friendlyName = null)
		{
			if (!string.IsNullOrWhiteSpace(friendlyName))
			{
				return friendlyName;
			}
			return string.IsNullOrWhiteSpace(windowTitle) ? processName : windowTitle;
		}

		/// <summary>
		/// 获取图标内容的实际大小。
		/// </summary>
		/// <param name="pixels">图标的像素数据。</param>
		/// <param name="width">图标的宽度。</param>
		/// <param name="height">图标的高度。</param>
		/// <returns>图标内容左上角的坐标、宽度和高度。</returns>
		private static (uint RealX, uint RealY, uint RealWidth, uint RealHeight) GetIconContentSize(
			byte[] pixels, uint width, uint height)
		{
			uint minX = ForeachFromLeftToRight(pixels, width, height);
			uint maxX = ForeachFromRightToLeft(pixels, width, height);
			uint minY = ForeachFromTopToBottom(pixels, width, height, minX, maxX);
			uint maxY = ForeachFromBottomToTop(pixels, width, height, minX, maxX);

			return (minX, minY, maxX - minX + 1, maxY - minY + 1);
		}

		/// <summary>
		/// 获取有包标识的应用的图标并保存到缓存文件夹中。
		/// </summary>
		/// <param name="iconPath">图标的路径。</param>
		/// <param name="name">进程名称。</param>
		/// <returns>图标的 Uri 。</returns>
		private static async Task<string> GetIconForPackage(string iconPath, string name)
		{
			try
			{
				StorageFile iconFile = await StorageFile.GetFileFromPathAsync(iconPath);
				IRandomAccessStreamWithContentType iconStream = await iconFile.OpenReadAsync();
				BitmapDecoder decoder = await BitmapDecoder.CreateAsync(iconStream);
				PixelDataProvider pixelData = await decoder.GetPixelDataAsync();
				byte[] pixels = pixelData.DetachPixelData();
				uint width = decoder.PixelWidth;
				(uint realX, uint realY, uint realWidth, uint realHeight) = GetIconContentSize(
					pixels, width, decoder.PixelHeight);

				// 图标裁剪区域至少为 32 * 32 像素，且裁剪区域坐标不能为负。
				if (realWidth < 32 && realHeight < 32)
				{
					realWidth = realHeight = 32;
					realX = Round((double)(width - realWidth) / 2);
					realY = Round((double)(width - realHeight) / 2);
				}

				InMemoryRandomAccessStream croppedStream = await CropIcon(decoder,
					realX, realY, realWidth, realHeight);
				return await SaveIcon(name, croppedStream);
			}
			catch (Exception ex)
			{
				throw new Exception($"无法保存进程 {name} 的图标。", ex);
			}
		}

		/// <summary>
		/// 获取 Win32 应用的图标并保存到缓存文件夹中。
		/// </summary>
		/// <param name="name">进程名称。</param>
		/// <param name="path">应用主模块路径。</param>
		/// <returns>图标的 Uri 。</returns>
		private static string GetIconForWin32(string name, string path)
		{
			try
			{
				Icon preIcon = Icon.ExtractAssociatedIcon(path)!;
				Icon icon = new(preIcon, 32, 32);
				preIcon.Dispose();

				// 加载图标并将图标保存到缓存文件夹中。
				string iconPath = Path.Combine(LocalCachePath,
					"Icons", $"{name}.png");
				using FileStream iconStream = new(iconPath, FileMode.Create,
					FileAccess.ReadWrite, FileShare.None);
				icon.ToBitmap().Save(iconStream, ImageFormat.Png);
				icon.Dispose();

				// 返回图标的 Uri。
				return new Uri(iconPath).ToString();
			}
			catch (Exception ex)
			{
				throw new Exception($"无法保存进程 {name} 的图标。", ex);
			}
		}

		/// <summary>
		/// 获取用于过滤只记录时间的进程名称的 <see cref="HashSet{T}"/> 。
		/// </summary>
		/// <returns>用于过滤只记录时间的进程名称的 <see cref="HashSet{T}"/> 。</returns>
		private static HashSet<string> GetNoInfoArr()
		{
			string noInfoNamesStr = (string)LocalSettings["NoInfoNames"];
			if (_lastNoInfoNamesStr != noInfoNamesStr)
			{
				// 如果过滤字符串有更新，则更新缓存。
				_lastNotInfoNamesArr = noInfoNamesStr.Split(',');
				_lastNoInfoNamesStr = noInfoNamesStr;
			}

			return new HashSet<string>(_lastNotInfoNamesArr);
		}

		/// <summary>
		/// 获取进程 <paramref name="name"/> 的包信息。
		/// </summary>
		/// <param name="packageFullName">进程 <paramref name="name"/> 的包全名。</param>
		/// <param name="name">进程名称。</param>
		/// <returns>进程 <paramref name="name"/> 的包信息。</returns>
		private static Package GetPackageInfo(string packageFullName, string name)
		{
			try
			{
				Dictionary<string, Package> packages = new PackageManager()
					.FindPackagesForUser(string.Empty)
					.OrderByDescending(pkg => pkg.Id.FullName.Length)
					.ToDictionary(pack => pack.Id.FullName);
				_ = packages.TryGetValue(packageFullName, out Package? package);

				return package ?? throw new Exception($"出于未知原因，未找到进程 {name} 的包信息。");
			}
			catch (Exception ex)
			{
				throw new Exception($"无法获取进程 {name} 的包信息。", ex);
			}
		}

		/// <summary>
		/// 从 Json 文件中获取进程信息列表。如果没有内容创建新列表。
		/// </summary>
		/// <returns>进程信息列表。如果没有内容创建新列表。</returns>
		private static List<ProcessInfo> GetProcessesInfoFromJson()
		{
			try
			{
				using FileStream textStream =
					new(InfoFilePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);

				// 如果文件中有内容则反序列化，如果结果为 null 或没有内容则创建新列表。
				return textStream.Length > 0
					? JsonSerializer.Deserialize(textStream,
						JsonSerializeMetadata.Default.ListProcessInfo) ?? []
					: [];
			}
			catch (Exception ex)
			{
				throw new Exception($"无法从 Json 文件 [{InfoFilePath}] 获取进程信息列表。", ex);
			}
		}

		/// <summary>
		/// 获取有包标识的应用的进程信息。
		/// </summary>
		/// <param name="process">进程实例。</param>
		/// <param name="name">进程名称。</param>
		/// <param name="packageFullNameLength">包全名长度。</param>
		/// <returns>图标的 Uri 和显示名称。</returns>
		private static async Task<(string IconUri, string DisplayName)> GetProcessInfoForPackage
			(Process process, string name, uint packageFullNameLength)
		{
			StringBuilder packageFullName = new((int)packageFullNameLength);
			long result = NativeApi.GetPackageFullName(
				process.Handle, ref packageFullNameLength, packageFullName);
			if (result != NativeApi.ERROR_SUCCESS)
			{
				throw new Win32Exception(Marshal.GetLastWin32Error(), $"无法获取进程 {name} 的包全名。");
			}

			Package package = GetPackageInfo(packageFullName.ToString(), name);

			return (await GetIconForPackage(package.Logo.LocalPath, name), package.DisplayName);
		}

		/// <summary>
		/// 获取 Win32 应用的进程信息。
		/// </summary>
		/// <param name="process">进程实例。</param>
		/// <param name="name">进程名称。</param>
		/// <returns>图标的 Uri 和显示名称。</returns>
		private static (string, string) GetProcessInfoForWin32(Process process, string name)
		{
			ProcessModule mainModule = process.MainModule!;
			string path = GetProcessModulePath(mainModule, name);
			if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
			{
				// 如果路径无效：
				throw new FileNotFoundException($"进程 {name} 的主模块路径 {path} 无效。");
			}

			return (GetIconForWin32(name, path),
				GetDisplayName(process.ProcessName, process.MainWindowTitle));
		}

		/// <summary>
		/// 获取进程模块的路径。
		/// </summary>
		/// <param name="module">要获取的进程模块。</param>
		/// <param name="name">进程名称。</param>
		/// <returns>进程模块的路径。</returns>
		private static string GetProcessModulePath(ProcessModule module, string name)
		{
			try
			{
				return module.FileName;
			}
			catch (Exception ex)
			{
				throw new Exception($"无法获取进程 {name} 的路径。", ex);
			}
		}

		/// <summary>
		/// 舍入 <paramref name="value"/> 为 <see langword="uint"/> 整数。遵循一般的四舍五入规则。当 <paramref
		/// name="value"/> 的小数部分为 0.5 时，向下舍入。
		/// </summary>
		/// <param name="value">要操作的 <see langword="double"/> 值。</param>
		/// <returns>舍入后的 <see langword="uint"/> 整数。</returns>
		private static uint Round(double value)
		{
			uint intValue = (uint)value;
			return value - intValue < 0.5 ? intValue : intValue - 1;
		}

		/// <summary>
		/// 保存图标到缓存文件夹。
		/// </summary>
		/// <param name="name">进程名称。</param>
		/// <param name="stream">图标所在的流。</param>
		/// <returns>图标的 Uri 。</returns>
		private static async Task<string> SaveIcon(string name, InMemoryRandomAccessStream stream)
		{
			StorageFolder iconFolder = await StorageFolder.GetFolderFromPathAsync(
				Path.Combine(LocalCachePath, "Icons"));
			StorageFile iconFile = await iconFolder.CreateFileAsync(
				$"{name}.png", CreationCollisionOption.ReplaceExisting);
			_ = await RandomAccessStream.CopyAndCloseAsync(
				stream, await iconFile.OpenAsync(FileAccessMode.ReadWrite));
			return new Uri(Path.Combine(LocalCachePath,
				"Icons", $"{name}.png")).ToString();
		}

		/// <summary>
		/// 如果达到了结束使用时间则发送结束使用时间提醒。
		/// </summary>
		private static void SendEndUsingReminderIfNeed()
		{
			TimeSpan currentTimeWithoutSeconds = new(DateTime.Now.Hour, DateTime.Now.Minute, 0);
			if (EndUsingTime == currentTimeWithoutSeconds && EndUsingTime != TimeSpan.Zero)
			{
				CanSend = ReminderHelper.SendReminder(ReminderKind.EndUsingTimeReminder);
				EndUsingTime = TimeSpan.Zero;
			}
		}

		/// <summary>
		/// 如果达到了总使用提醒时长且未提醒过则发送总使用时长提醒。
		/// </summary>
		private static void SendTotalReminderIfNeed()
		{
			if (_totalUsedTime >= (TimeSpan)LocalSettings["TotalUsedRemindTime"] && !HasTotalReminded)
			{
				CanSend = ReminderHelper.SendReminder(ReminderKind.TotalUsedTimeReminder);
				HasTotalReminded = true;
			}
		}

		/// <summary>
		/// 将提供的值转换为 Json <see langword="string"/> 。
		/// </summary>
		/// <typeparam name="T">要序列化的值的类型。</typeparam>
		/// <param name="value">要转换的值。</param>
		/// <param name="info">要转换的类型的元数据。</param>
		/// <returns>值的 <see langword="string"/> 表示形式。</returns>
		private static string SerializeJson<T>(T value, JsonTypeInfo<T> info)
		{
			using MemoryStream stream = new();
			using Utf8JsonWriter writer = new(stream, _jsonOptions);
			JsonSerializer.Serialize(writer, value, info);
			writer.Flush();
			return Encoding.UTF8.GetString(stream.ToArray());
		}

		/// <summary>
		/// 排除任务栏和桌面。
		/// </summary>
		/// <param name="handle">进程句柄。</param>
		/// <returns>指示是否继续记录进程。</returns>
		private bool CheckExplorerProcess(nint handle)
		{
			if (!TryGetChildWindowHandle(handle, out nint childHandle,
					"无法获取 explorer 进程的子窗口句柄。") ||
				!TryGetWindowClassName(childHandle, out string className,
					$"无法获取 explorer 子进程 [Handle={childHandle}] 的类名。"))
			{
				return false;
			}
			if (className is "Windows.UI.Core.CoreWindow" or "SHELLDLL_DefView")
			{
				// 如果是任务栏或者桌面则不记录。
				//WriteLog(LogLevel.Debug, $" explorer 子进程 [ClassName={className}] 是任务栏或者桌面。");
				NoProcessNow();
				return false;
			}
			return true;
		}

		/// <summary>
		/// 检查进程是否可用（不为 null 且不是正在记录的进程）。
		/// </summary>
		/// <param name="process">要检查的进程。</param>
		/// <param name="copyProcess">如果进程可用，该参数是进程的副本；否则是一个新实例。</param>
		/// <returns>指示进程是否可用。</returns>
		private bool CheckProcessUsability(Process? process, out Process copyProcess)
		{
			if (process is null)
			{
				WriteLog(LogLevel.Warning, "要记录的进程为 null ，将跳过。");
				copyProcess = new Process();
				return false;
			}

			if (process.ProcessName == _currentRecordProcessName)
			{
				WriteLog(LogLevel.Info, $"已开始记录进程 {process.ProcessName} ，将跳过。");
				copyProcess = new Process();
				return false;
			}

			// 更新当前正在记录的进程名称以防止重复记录。
			_currentRecordProcessName = process.ProcessName;
			copyProcess = process;
			return true;
		}

		/// <summary>
		/// 获取用于过滤不记录任何信息的进程名称的 <see cref="HashSet{T}"/> 。
		/// </summary>
		/// <returns>用于过滤不记录任何信息的进程名称的 <see cref="HashSet{T}"/> 。</returns>
		private HashSet<string> GetNoTimeArr()
		{
			string noTimeNamesStr = (string)LocalSettings["NoTimeNames"];
			if (_lastNoTimeNamesStr != noTimeNamesStr)
			{
				// 如果过滤字符串有更新，则更新缓存。
				_lastNoTimeNamesArr = noTimeNamesStr.Split(',');
				_lastNoTimeNamesStr = noTimeNamesStr;
			}
			return new HashSet<string>(_lastNoTimeNamesArr);
		}

		/// <summary>
		/// 排除任务栏和桌面，并获取真正的 UWP 进程。
		/// </summary>
		/// <remarks>
		/// 当返回的 <see langword="bool"/> 值为 <see langword="false"/> 时会调用 <see cref="NoProcessNow"/> 方法。
		/// </remarks>
		/// <param name="name">进程名称。</param>
		/// <param name="handle">进程句柄。</param>
		/// <returns>
		/// <see langword="bool"/> 值指示是否继续记录进程， <see cref="Process"/> 值是真正的 UWP 进程，如果未获取到则返回 <see langword="null"/>。
		/// </returns>
		private (bool Continue, Process? Result) GetRealProcess(string name, nint handle)
		{
			return name switch
			{
				"explorer" => (CheckExplorerProcess(handle), null),
				"ApplicationFrameHost" => GetRealUwpProcess(handle),
				_ => (false, null),
			};
		}

		/// <summary>
		/// 获取真正的 UWP 进程。
		/// </summary>
		/// <param name="handle">进程句柄。</param>
		/// <returns>
		/// <see langword="bool"/> 值指示是否继续记录进程， <see cref="Process"/> 值是真正的 UWP 进程，如果未获取到则返回 <see langword="null"/>。
		/// </returns>
		private (bool Success, Process? Result) GetRealUwpProcess(nint handle)
		{
			if (TryGetChildWindowHandle(handle, out nint childHandle,
					"无法获取 UWP 进程子窗口的句柄。", "Windows.UI.Core.CoreWindow") &&
				TryGetWindowThreadProcessId(childHandle, out uint uwpId,
					$"无法获取 UWP 进程 [Handle={childHandle}] 的 Id 。") &&
				TryGetProcessById((int)uwpId, out Process process,
					$"无法获取 UWP 进程 [ID={uwpId}] 的信息。"))
			{
				return (true, process);
			}
			return (false, null);
		}

		/// <summary>
		/// 获取记录文件中的记录。如果文件中没有内容就返回空数组。
		/// </summary>
		/// <returns>以换行符分隔的数组，包含了进程名称和使用时长。</returns>
		private string[] GetRecordFileLines()
		{
			using FileStream stream = new(_recordFilePath, FileMode.OpenOrCreate,
				FileAccess.Read, FileShare.ReadWrite);
			if (stream.Length > 0)
			{
				using BinaryReader breader = new(stream, Encoding.UTF8);
				string text = breader.ReadString();
				return text.Split("\r\n");
			}

			return [];
		}

		/// <summary>
		/// 从记录文件中获取今天的记录。
		/// </summary>
		private void GetUsedTimeFromRecordFile()
		{
			try
			{
				string[] lines = GetRecordFileLines();
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
					_windowsUsedTime[keyValuePair[0]] = TimeSpan.FromSeconds(result);
				}
			}
			catch (Exception ex)
			{
				throw new Exception($"无法从记录文件 [{_recordFilePath}] 获取今天的记录。", ex);
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
				if (DateTime.Now - _lastRecordTime >= (TimeSpan)LocalSettings["ContinuousUsedResetTime"]
					&& _continuousUsedTime != TimeSpan.Zero)
				{
					// 如果上次记录的时间超过指定时间，则刷新连续使用时长。
					_continuousUsedTime = TimeSpan.Zero;
				}
			}
			else
			{
				// 如果上次有记录，则记录使用时长。
				_ = new SafeCaller()
				{
					RemindingMsgResKey = "ECanNotRecordTime",
				}.CallMethodR(()=>RecordUsedTime());
				_lastProcess = null;
			}
		}

		/// <summary>
		/// 在文件中记录指定的进程信息。
		/// </summary>
		/// <param name="info">指定的进程信息。</param>
		/// <param name="allProcessInfos">所有已记录的进程信息。</param>
		private async Task RecordInfoIntoFile(ProcessInfo info, List<ProcessInfo> allProcessInfos)
		{
			allProcessInfos.Add(info);
			bool result = new SafeCaller()
			{
				LogMessage = $"无法写入记录文件 [{InfoFilePath}] 。",
				RemindingMsgResKey = "ECanNotWriteInfo",
			}.CallMethodR(() => File.WriteAllText(InfoFilePath, SerializeJson(
				allProcessInfos,
				JsonSerializeMetadata.Default.ListProcessInfo)));
			_currentRecordProcessName = string.Empty;
			if (result)
			{
				WriteLog(LogLevel.Info, $"已记录进程 {info.ProcessName} 的信息。");
			}
		}

		/// <summary>
		/// 记录进程信息到 JSON 文件中。
		/// </summary>
		private async Task RecordProcessInfo()
		{
			if (!CheckProcessUsability(_lastProcess, out Process process))
			{
				return;
			}

			string name = process.ProcessName;

			(bool success, List<ProcessInfo>? list) = new SafeCaller()
			{
				RemindingMsgResKey = "ECanNotGetInfo",
			}.CallMethodWithReturnR(GetProcessesInfoFromJson);
			if (!success || list is null || list.Any(info => info.ProcessName == process.ProcessName))
			{
				// 如果获取失败或已经有信息则不再记录。
				_currentRecordProcessName = string.Empty;
				return;
			}

			uint packageFullNameLength = 0;
			(success, long result) = new SafeCaller()
			{
				LogMessage = $"无法获取进程 {name} 的包全名长度。",
				NeedRemind = false,
			}
			.CallMethodWithReturnR(() => NativeApi.GetPackageFullName
			(process.Handle, ref packageFullNameLength, null!));

			if (!success)
			{
				await RecordInfoIntoFile(GetDefaultInfo(process), list);
				return;
			}

			(string IconUri, string FriendlyName) infoTuple;
			switch (result)
			{
				case NativeApi.APPMODEL_ERROR_NO_PACKAGE:
					(success, infoTuple) = new SafeCaller() { NeedRemind = false }
						.CallMethodWithReturnR(() => GetProcessInfoForWin32(process, name));
					if (!success)
					{
						await RecordInfoIntoFile(GetDefaultInfo(process), list);
						return;
					}
					break;

				case NativeApi.ERROR_INSUFFICIENT_BUFFER:
					(success, Task<(string, string)>? infoTask) = new SafeCaller() { NeedRemind = false }
						.CallMethodWithReturnR
						(() => GetProcessInfoForPackage(process, name, packageFullNameLength));
					if (!success || infoTask is null)
					{
						await RecordInfoIntoFile(GetDefaultInfo(process), list);
						return;
					}
					infoTuple = await infoTask;
					break;

				default:
					WriteLog(LogLevel.Error,
						$"无法获取进程 {name} 的包全名，错误代码：{Marshal.GetLastWin32Error()}。");
					await RecordInfoIntoFile(GetDefaultInfo(process), list);
					return;
			}

			ProcessInfo info = new()
			{
				ProcessName = name,
				DisplayName = GetDisplayName(name, process.MainWindowTitle, infoTuple.FriendlyName),
				IconUri =
					string.IsNullOrWhiteSpace(infoTuple.IconUri) ? defaultIconUri : infoTuple.IconUri,
			};
			await RecordInfoIntoFile(info, list);
		}

		/// <summary>
		/// 在文件中记录进程 <paramref name="name"/> 的总使用时长。
		/// </summary>
		/// <param name="name">要记录的进程名称。</param>
		/// <param name="totalUsedTime">进程的总使用时长。</param>
		private void RecordUsageTimeIntoFile(string name, TimeSpan totalUsedTime)
		{
			try
			{
				string[] lines = GetRecordFileLines();
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

				using FileStream stream =
					new(_recordFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
				using BinaryWriter writer = new(stream, Encoding.UTF8);
				writer.Write(writeLines.ToString());
			}
			catch (Exception ex)
			{
				throw new Exception($"无法在文件 [{_recordFilePath}] 中记录进程 {name} 的总使用时长。", ex);
			}
		}

		/// <summary>
		/// 在内存中记录进程 <paramref name="name"/> 的单次使用时长和总使用时长。
		/// </summary>
		/// <param name="name">要记录的进程名称。</param>
		/// <returns>进程 <paramref name="name"/> 的总使用时长。</returns>
		private TimeSpan RecordUsageTimeIntoMemory(string name)
		{
			try
			{
				TimeSpan usedTime = DateTime.Now - _lastActivationTime;
				TimeSpan totalUsedTime = _windowsUsedTime.TryGetValue(name, out TimeSpan pastUsedTime)
					? pastUsedTime + usedTime
					: usedTime;
				_windowsUsedTime[name] = totalUsedTime;
				TotalUsedTime += usedTime;
				return totalUsedTime;
			}
			catch (Exception ex)
			{
				throw new Exception($"无法在内存中记录进程 {name} 的使用时长。", ex);
			}
		}

		/// <summary>
		/// 记录上次被激活窗口的使用时长。
		/// </summary>
		private void RecordUsedTime(string currentProcessName = "")
		{
			if (_lastProcess is null)
			{
				WriteLog(LogLevel.Warning, "将要记录的进程是 null ，已忽略（理论上不可遇到）。");
				return;
			}

			string name = _lastProcess.ProcessName;

			if (name == currentProcessName)
			{
				// 如果被激活窗口没有变化，则不记录但增加连续使用时长。
				_singleContinuousUsedTime += _oneSecond;
				return;
			}
			_singleContinuousUsedTime = TimeSpan.Zero;

			TimeSpan totalUsedTime = RecordUsageTimeIntoMemory(name);
			RecordUsageTimeIntoFile(name, totalUsedTime);

			//WriteLog(LogLevel.Debug, $"已记录进程 {name} 的使用时长。");
		}

		/// <summary>
		/// 如果今天的记录不存在或不是今天，则重置记录。
		/// </summary>
		private void ResetRecord()
		{
			// 重置本地设置。
			LocalSettings["Today"] = new DateTimeOffset(DateTime.Now.Date);
			TotalUsedTime = TimeSpan.Zero;
			EndUsingTime = TimeSpan.Zero;

			// 重置记录文件。
			try
			{
				File.WriteAllText(_recordFilePath, string.Empty);
			}
			catch (Exception ex)
			{
				throw new Exception($"无法重置/创建记录文件 [{_recordFilePath}] 。", ex);
			}
		}

		/// <summary>
		/// 如果达到了连续使用提醒时长则发送连续使用时长提醒。
		/// </summary>
		private void SendContinuousReminderIfNeed()
		{
			if (_continuousUsedTime < (TimeSpan)LocalSettings["ContinuousUsedRemindTime"] ||
				_continuousUsedTime == TimeSpan.Zero)
			{
				return;
			}

			CanSend = ReminderHelper.SendReminder(ReminderKind.ContinuousUsedTimeReminder);
			_continuousUsedTime = TimeSpan.Zero;
		}

		private async void Timer_Tick(object? sender, object e)
		{
			SendEndUsingReminderIfNeed();

			if (!TryGetForegroundWindowHandle(out nint handle) ||
				!TryGetWindowThreadProcessId(handle, out uint processId,
					$"无法获取进程 [{handle}] 的 Id 。") ||
				!TryGetProcessById((int)processId, out Process process,
					$"无法获取进程 [ID={processId}] 的信息。"))
			{
				return;
			}

			string name = process.ProcessName;

			if (GetNoTimeArr().Contains(name))
			{
				// 如果是无需记录的进程：
				NoProcessNow();
				return;
			}

			(bool success, Process? p) = GetRealProcess(name, handle);
			if (!success)
			{
				return;
			}
			process = p ?? process;
			UpdateUsageTime();
			_ = new SafeCaller() { RemindingMsgResKey = "ECanNotRecordTime" }
			.CallMethodR(() => RecordUsedTime(name));
			SendTotalReminderIfNeed();
			SendContinuousReminderIfNeed();

			//记录这次的进程实例、信息和激活时间。
			_lastProcess = process;
			_lastActivationTime = DateTime.Now;
			if (!GetNoInfoArr().Contains(name))
			{
				// 如果进程不是只记录使用时长的则记录信息。
				_ = Task.Run(RecordProcessInfo);
			}
		}

		/// <summary>
		/// 尝试获取句柄为 <paramref name="parentHandle"/> 的进程的子窗口句柄。
		/// </summary>
		/// <remarks>当返回值为 <see langword="false"/> 时会调用 <see cref="NoProcessNow"/> 方法。</remarks>
		/// <param name="parentHandle">进程句柄。</param>
		/// <param name="childHandle">子窗口句柄。</param>
		/// <param name="className">指定子窗口的类名。</param>
		/// <param name="logSymbol">如果返回值为 <see langword="false"/> ，则将此日志标识和错误写入日志。</param>
		/// <returns>指示 <paramref name="childHandle"/> 是否为 <see cref="nint.Zero"/> 。</returns>
		private bool TryGetChildWindowHandle(nint parentHandle, out nint childHandle,
			string logSymbol, string? className = null)
		{
			childHandle = NativeApi.FindWindowEx(parentHandle, nint.Zero, className, null);
			if (childHandle == nint.Zero)
			{
				WriteLog(LogLevel.Error, $"{logSymbol}错误代码：{Marshal.GetLastWin32Error()}。");
				NoProcessNow();
				return false;
			}

			return true;
		}

		/// <summary>
		/// 尝试获取前台窗口的句柄。
		/// </summary>
		/// <remarks>
		/// 当 <paramref name="handle"/> 为 <see cref="nint.Zero"/> 时会调用 <see cref="NoProcessNow"/> 方法。
		/// </remarks>
		/// <param name="handle">前台窗口的句柄。</param>
		/// <returns>指示 <paramref name="handle"/> 是否为 <see cref="nint.Zero"/> 。</returns>
		private bool TryGetForegroundWindowHandle(out nint handle)
		{
			handle = NativeApi.GetForegroundWindow();
			if (handle == nint.Zero)
			{
				//WriteLog(LogLevel.Debug, "没有被激活窗口。");
				NoProcessNow();
				return false;
			}
			return true;
		}

		/// <summary>
		/// 尝试通过 Id 获取进程信息。
		/// </summary>
		/// <remarks>如果获取失败则 <paramref name="process"/> 为一个 <see cref="Process"/> 的新实例。</remarks>
		/// <param name="processId">进程的 Id 。</param>
		/// <param name="process">通过 Id 获取到的进程信息。</param>
		/// <param name="logSymbol">如果返回值为 <see langword="false"/> ，则将此日志标识和错误写入日志。</param>
		/// <returns>指示是否成功获取 <paramref name="process"/> 。</returns>
		private bool TryGetProcessById(int processId, out Process process, string logSymbol)
		{
			(bool success, Process? p) = new SafeCaller()
			{
				LogMessage = logSymbol,
				NeedRemind = false,
			}
			.CallMethodWithReturnR(() => Process.GetProcessById(processId));
			process = p ?? new Process();
			if (!success)
			{
				NoProcessNow();
			}
			return success;
		}

		/// <summary>
		/// 尝试获取窗口的类名。
		/// </summary>
		/// <remarks>当返回值为 <see langword="false"/> 时会调用 <see cref="NoProcessNow"/> 方法。</remarks>
		/// <param name="handle">窗口的句柄。</param>
		/// <param name="className">窗口的类名。</param>
		/// <param name="logSymbol">如果返回值为 <see langword="false"/> ，则将此日志标识和错误写入日志。</param>
		/// <returns>指示是否成功获取窗口类名。</returns>
		private bool TryGetWindowClassName(nint handle, out string className, string logSymbol)
		{
			StringBuilder classNameBuilder = new(256);
			int classNameLength = NativeApi.GetClassName(handle, classNameBuilder, classNameBuilder.Capacity);
			if (classNameLength == 0)
			{
				WriteLog(LogLevel.Error, $"{logSymbol}错误代码：{Marshal.GetLastWin32Error()}。");
				className = string.Empty;
				NoProcessNow();
				return false;
			}

			className = classNameBuilder.ToString();
			return true;
		}

		/// <summary>
		/// 尝试获取创建句柄为 <paramref name="handle"/> 的窗口的进程的 Id 。
		/// </summary>
		/// <remarks>当 <paramref name="processId"/> 获取失败时会调用 <see cref="NoProcessNow"/> 方法并写入日志。</remarks>
		/// <param name="handle">窗口句柄。</param>
		/// <param name="processId">创建句柄为 <paramref name="handle"/> 的窗口的进程的 Id 。</param>
		/// <param name="logSymbol">如果返回值为 <see langword="false"/> ，则将此日志标识和错误写入日志。</param>
		/// <returns>指示是否成功获取 <paramref name="processId"/> 。</returns>
		private bool TryGetWindowThreadProcessId(nint handle, out uint processId, string logSymbol)
		{
			if (NativeApi.GetWindowThreadProcessId(handle, out processId) == 0)
			{
				WriteLog(LogLevel.Error, $"{logSymbol}错误代码：{Marshal.GetLastWin32Error()}。");
				NoProcessNow();
				return false;
			}
			return true;
		}

		/// <summary>
		/// 更新总使用时长和连续使用时长。
		/// </summary>
		private void UpdateUsageTime()
		{
			_totalUsedTime += _oneSecond;

			// 如果窗口被激活时长超过 5 秒则记录。
			if (_singleContinuousUsedTime > TimeSpan.FromSeconds(5))
			{
				// 如果时长到达 6 秒则把之前的 6 秒全部加上。 一般情况加 1 秒。
				_continuousUsedTime += _singleContinuousUsedTime == TimeSpan.FromSeconds(6)
					? TimeSpan.FromSeconds(6)
					: _oneSecond;
			}
			_lastRecordTime = DateTime.Now;

			//WriteLog(LogLevel.Debug, $"当前连续使用时长：{_continuousUsedTime:hh\\:mm\\:ss}");
		}

		/// <summary>
		/// 获取本地化时间 / 时长。
		/// </summary>
		/// <param name="time">一个时间 / 时长。</param>
		/// <param name="useRemindTotal">指示是否使用用于触发提醒的总时长。</param>
		/// <returns>本地化时间 / 时长字符串。</returns>
		public static string GetLocalTime(TimeSpan time, bool useRemindTotal = false)
		{
			// 如果使用用于触发提醒的总时长则忽略传入的 time 参数。
			TimeSpan realTime = useRemindTotal ? _totalUsedTime : time;

			// 根据本地化设置返回时间字符串。
			string result;
			switch (realTime)
			{
				case { Days: 0, Hours: 0, Minutes: 0 }:
					result = "< 1" + Loader.GetString("Minute");
					break;

				case { Days: 0, Hours: 0 }:
					result = realTime.Minutes + Loader.GetString("Minute");
					break;

				default:
					if (realTime.Days == 0)
					{
						result = realTime.Hours + Loader.GetString("Hour")
												+ realTime.Minutes + Loader.GetString("Minute");
					}
					else
					{
						result = realTime.Days + Loader.GetString("Day")
											   + realTime.Hours + Loader.GetString("Hour")
											   + realTime.Minutes + Loader.GetString("Minute");
					}

					break;
			}
			return result;
		}

		/// <summary>
		/// 获取进程名称、图标及使用的时长。
		/// </summary>
		/// <param name="count">需要获取的数量（以使用时长正序排列）。</param>
		/// <returns>使用时长最长的 <paramref name="count"/> 个进程名称、图标和时长。</returns>
		public static List<ProcessInfo> GetProcessesInfo(int count)
		{
			// 获取使用时长最长的六个进程的名称（排除无需记录信息的进程）。
			string[] processNames = WindowsUsedTime
				.OrderByDescending(x => x.Value)
				.Take(count)
				.Select(x => x.Key)
				.ToArray();

			string processesListText;
			try
			{
				processesListText = File.Exists(InfoFilePath) ? File.ReadAllText(InfoFilePath) : string.Empty;
			}
			catch (Exception ex)
			{
				throw new Exception($"在获取记录文件 [Path={InfoFilePath}] 的文本时触发了异常。", ex);
			}

			List<ProcessInfo> processesInfo = [];
			bool isEmpty = string.IsNullOrWhiteSpace(processesListText);
			if (isEmpty)
			{
				WriteLog(LogLevel.Warning, $"程序尚未开始监测和记录（json 文件中无内容） [Path={InfoFilePath}] 。");
			}

			List<ProcessInfo> list = GetProcessesInfoFromJson();
			Dictionary<string, ProcessInfo> processesDict =
				list.Count == 0 ? [] : list.ToDictionary(value => value.ProcessName);

			foreach (string name in processNames)
			{
				ProcessInfo? info;

				if (isEmpty)
				{
					info = GetDefaultInfo(name);
				}
				else if (!processesDict.TryGetValue(name, out info))
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
	}
}
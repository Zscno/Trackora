using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.Windows.AppLifecycle;
using System;
using System.Threading;
using WinRT;

namespace Zscno.Trackora
{
	/// <summary>
	/// 自定义入口点（单实例所需）。
	/// </summary>
	public class Program
	{
		[STAThread]
		private static int Main(string[] args)
		{
			ComWrappersSupport.InitializeComWrappers();
			AppActivationArguments arguments = AppInstance.GetCurrent().GetActivatedEventArgs();
			AppInstance keyInstance = AppInstance.FindOrRegisterForKey("TrackoraSingle");

			if (keyInstance.IsCurrent)
			{
				Application.Start((p) =>
				{
					DispatcherQueueSynchronizationContext context = new(DispatcherQueue.GetForCurrentThread());
					SynchronizationContext.SetSynchronizationContext(context);
					_ = new App();
				});
			}
			else
			{
				keyInstance.RedirectActivationToAsync(arguments).AsTask().Wait();
			}

			return 0;
		}
	}
}
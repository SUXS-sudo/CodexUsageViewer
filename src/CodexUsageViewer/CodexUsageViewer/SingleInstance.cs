using System;
using System.Threading;
using System.Windows.Threading;

namespace CodexUsageViewer;

internal sealed class SingleInstance : IDisposable
{
	private const string MutexName = "Local\\CodexUsageViewer.SingleInstance.v2";

	private const string EventName = "Local\\CodexUsageViewer.Show.v2";

	private readonly Mutex mutex;

	private readonly EventWaitHandle showEvent;

	private RegisteredWaitHandle registration;

	public bool IsPrimary { get; private set; }

	public SingleInstance(string suffix = "")
	{
		mutex = new Mutex(initiallyOwned: true, "Local\\CodexUsageViewer.SingleInstance.v2" + suffix, out var createdNew);
		IsPrimary = createdNew;
		showEvent = new EventWaitHandle(initialState: false, EventResetMode.AutoReset, "Local\\CodexUsageViewer.Show.v2" + suffix);
		AppLogger.Info(createdNew ? "Single instance acquired" : "Existing instance detected");
	}

	public void SignalExisting()
	{
		try
		{
			showEvent.Set();
		}
		catch
		{
		}
	}

	public void Listen(Dispatcher dispatcher, Action callback)
	{
		registration = ThreadPool.RegisterWaitForSingleObject(showEvent, delegate
		{
			dispatcher.BeginInvoke(callback);
		}, null, -1, executeOnlyOnce: false);
	}

	public void Dispose()
	{
		if (registration != null)
		{
			registration.Unregister(null);
		}
		showEvent.Dispose();
		if (IsPrimary)
		{
			try
			{
				mutex.ReleaseMutex();
			}
			catch
			{
			}
		}
		mutex.Dispose();
	}
}

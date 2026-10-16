using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using CodexUsageViewer.Usage;

namespace CodexUsageViewer;

internal static class Program
{
	[STAThread]
	private static void Main(string[] args)
	{
		try
		{
			if (args != null && args.Length >= 2 && args[0] == "--usage-preview")
			{
				var previewApp = new Application();
				var preview = new TaskUsageWindow(null);
				preview.Loaded += async delegate
				{
					await preview.Reload();
					for (int attempt = 0; attempt < 120 && preview.Data == null; attempt++) await System.Threading.Tasks.Task.Delay(500);
					await System.Threading.Tasks.Task.Delay(300);
					preview.SavePreview(args[1]);
					previewApp.Shutdown();
				};
				previewApp.Run(preview);
				return;
			}
			string suffix = ((args != null && Array.IndexOf(args, "--isolated-test") >= 0) ? (".isolated." + Process.GetCurrentProcess().Id) : string.Empty);
			using SingleInstance singleInstance = new SingleInstance(suffix);
			if (!singleInstance.IsPrimary)
			{
				singleInstance.SignalExisting();
				return;
			}
			AppLogger.Info("Application starting v2.1.1");
			try
			{
				ProgramNetworkAudit.Write();
			}
			catch (Exception exception)
			{
				AppLogger.Error("Program network audit write failed", exception);
			}
			Application application = new Application();
			application.ShutdownMode = ShutdownMode.OnExplicitShutdown;
			Application application2 = application;
			application2.DispatcherUnhandledException += delegate(object sender, DispatcherUnhandledExceptionEventArgs e)
			{
				AppLogger.Error("Unhandled UI exception", e.Exception);
				e.Handled = true;
			};
			AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
			{
				AppLogger.Error("Unhandled application exception", e.ExceptionObject as Exception);
			};
			MainWindow mainWindow = new MainWindow(new UsageService(new DesktopUsageProvider()));
			if (args != null && Array.IndexOf(args, "--tasks") >= 0) mainWindow.Loaded += delegate { mainWindow.ShowTaskUsage(); };
			singleInstance.Listen(application2.Dispatcher, mainWindow.ShowFromTray);
			using (new TrayController(mainWindow))
			{
				application2.Run(mainWindow);
			}
		}
		catch (Exception exception2)
		{
			AppLogger.Error("Fatal startup failure", exception2);
		}
	}
}

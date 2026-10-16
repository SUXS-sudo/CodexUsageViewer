using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Shapes;
using System.Windows.Threading;
using CodexUsageViewer.Usage;
using Microsoft.Win32;

namespace CodexUsageViewer;

internal sealed class MainWindow : Window
{
	private TaskUsageWindow taskUsageWindow;

	internal void ShowTaskUsage()
	{
		if (taskUsageWindow == null)
		{
			taskUsageWindow = new TaskUsageWindow(this);
			taskUsageWindow.Closed += delegate { taskUsageWindow = null; };
		}
		taskUsageWindow.Show();
		if (taskUsageWindow.WindowState == WindowState.Minimized) taskUsageWindow.WindowState = WindowState.Normal;
		taskUsageWindow.Activate();
	}
	private static class NativeMethods
	{
		internal struct NativePoint
		{
			public int X;

			public int Y;
		}

		private struct TrackMouseEventData
		{
			public int Size;

			public uint Flags;

			public IntPtr HwndTrack;

			public uint HoverTime;
		}

		private struct NativeRect
		{
			public int Left;

			public int Top;

			public int Right;

			public int Bottom;
		}

		private struct MonitorInfo
		{
			public int Size;

			public NativeRect Monitor;

			public NativeRect Work;

			public uint Flags;
		}

		[DllImport("user32.dll")]
		public static extern int GetWindowLong(IntPtr hWnd, int nIndex);

		[DllImport("user32.dll")]
		public static extern int SetWindowLong(IntPtr hWnd, int nIndex, int value);

		[DllImport("user32.dll")]
		internal static extern uint GetDoubleClickTime();

		[DllImport("user32.dll")]
		internal static extern int GetSystemMetrics(int index);

		[DllImport("user32.dll")]
		private static extern bool TrackMouseEvent(ref TrackMouseEventData data);

		[DllImport("user32.dll")]
		private static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint point);

		[DllImport("user32.dll")]
		internal static extern IntPtr SetCapture(IntPtr hWnd);

		[DllImport("user32.dll")]
		internal static extern bool ReleaseCapture();

		[DllImport("user32.dll")]
		private static extern bool GetWindowRect(IntPtr hWnd, out NativeRect rect);

		[DllImport("user32.dll")]
		private static extern bool IsIconic(IntPtr hWnd);

		[DllImport("user32.dll")]
		private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);

		[DllImport("user32.dll", CharSet = CharSet.Auto)]
		private static extern int GetClassName(IntPtr hWnd, StringBuilder text, int count);

		internal static NativePoint ClientPointToScreen(IntPtr hwnd, IntPtr lParam)
		{
			long num = lParam.ToInt64();
			NativePoint point = new NativePoint
			{
				X = (short)(num & 0xFFFF),
				Y = (short)((num >> 16) & 0xFFFF)
			};
			ClientToScreen(hwnd, ref point);
			return point;
		}

		internal static void TrackMouseLeave(IntPtr hwnd)
		{
			TrackMouseEventData data = new TrackMouseEventData
			{
				Size = Marshal.SizeOf(typeof(TrackMouseEventData)),
				Flags = 2u,
				HwndTrack = hwnd
			};
			TrackMouseEvent(ref data);
		}

		public static bool IsForegroundFullScreen(IntPtr ownWindow, out string diagnostic)
		{
			IntPtr foregroundWindow = GetForegroundWindow();
			StringBuilder stringBuilder = new StringBuilder(128);
			GetClassName(foregroundWindow, stringBuilder, stringBuilder.Capacity);
			string text = "handle=0x" + foregroundWindow.ToInt64().ToString("X") + "; class=" + stringBuilder;
			if (foregroundWindow == IntPtr.Zero || foregroundWindow == ownWindow || IsIconic(foregroundWindow) || stringBuilder.ToString() == "Progman" || stringBuilder.ToString() == "WorkerW" || stringBuilder.ToString() == "Shell_TrayWnd")
			{
				diagnostic = text + "; fullscreen=False (excluded)";
				return false;
			}
			if (!GetWindowRect(foregroundWindow, out var rect))
			{
				diagnostic = text + "; fullscreen=False (no rectangle)";
				return false;
			}
			MonitorInfo info = new MonitorInfo
			{
				Size = Marshal.SizeOf(typeof(MonitorInfo))
			};
			if (!GetMonitorInfo(MonitorFromWindow(foregroundWindow, 2u), ref info))
			{
				diagnostic = text + "; fullscreen=False (no monitor)";
				return false;
			}
			bool flag = rect.Left <= info.Monitor.Left + 2 && rect.Top <= info.Monitor.Top + 2 && rect.Right >= info.Monitor.Right - 2 && rect.Bottom >= info.Monitor.Bottom - 2;
			diagnostic = text + "; window=[" + rect.Left + "," + rect.Top + "," + rect.Right + "," + rect.Bottom + "]; monitor=[" + info.Monitor.Left + "," + info.Monitor.Top + "," + info.Monitor.Right + "," + info.Monitor.Bottom + "]; fullscreen=" + flag;
			return flag;
		}
	}

	private const int WsExNoActivate = 134217728;

	private readonly UsageService usageService;

	private readonly Border shortPane;

	private readonly Border longPane;

	private readonly TextBlock shortPercent;

	private readonly TextBlock longPercent;

	private readonly TextBlock shortReset;

	private readonly TextBlock longReset;

	private readonly TextBlock statusText;

	private readonly TextBlock refreshText;

	private TextBlock shortLabel;

	private TextBlock longLabel;

	private TextBlock resetTitle;

	private readonly Ellipse freshnessDot;

	private readonly Grid expandedDetails;

	private readonly FrameworkElement visualRoot;

	private readonly DispatcherTimer autoRefreshTimer;

	private readonly DispatcherTimer relativeTimeTimer;

	private readonly DispatcherTimer collapseTimer;

	private readonly DispatcherTimer geometryTimer;

	private readonly DispatcherTimer idleTimer;

	private readonly DispatcherTimer fullScreenTimer;

	private readonly DispatcherTimer singleClickTimer;

	private readonly DispatcherTimer hintTimer;

	private CancellationTokenSource refreshCancellation;

	private CachedUsage current;

	private string currentError;

	private UsageError? currentErrorCode;

	private bool exitRequested;

	private bool expanded;

	private bool dragging;

	private bool idle;

	private Point mouseDownScreen;

	private Point windowDown;

	private double animationCenter;

	private double animationStartWidth;

	private double animationTargetWidth;

	private int animationDuration;

	private double animationEdgePosition;

	private readonly Stopwatch geometryClock = new Stopwatch();

	private FrameworkElement normalRoot;

	private Grid collapsedRoot;

	private readonly AppWindowState savedState;

	private bool windowCollapsed;

	private bool hiddenByFullScreen;

	private string lastFullScreenDiagnostic;

	private HwndSource hwndSource;

	private bool nativeLeftDown;

	private Point pendingClickScreen;

	private ToolTip firstUseToolTip;

	public string TrayText
	{
		get
		{
			if (current == null)
			{
				return currentError ?? Localization.Get("NoData");
			}
			return Localization.Get("FiveHour") + " " + FormatPercent(current.ShortRemaining) + "  " + Localization.Get("Weekly") + " " + FormatPercent(current.LongRemaining) + (string.IsNullOrEmpty(currentError) ? "" : (" · " + Localization.Get("DataNotUpdated")));
		}
	}

	internal bool IsCollapsed => windowCollapsed;

	internal bool IsAlwaysOnTop => base.Topmost;

	internal bool IsAutoHideFullScreen => savedState.AutoHideFullScreen;

	public event EventHandler DisplayChanged;

	public MainWindow(UsageService usageService)
	{
		this.usageService = usageService;
		base.Title = "Codex Usage Viewer 2.0.0";
		savedState = WindowStateStore.Load();
		savedState.HalfHidden = false;
		Localization.SetPreference(savedState.Language);
		base.Width = 178.0;
		base.Height = 60.0;
		base.MinWidth = 20.0;
		base.MinHeight = 20.0;
		base.MaxWidth = 378.0;
		base.MaxHeight = 60.0;
		base.Topmost = savedState.Topmost;
		base.ShowInTaskbar = false;
		base.WindowStyle = WindowStyle.None;
		base.ResizeMode = ResizeMode.NoResize;
		base.AllowsTransparency = true;
		base.Background = Brushes.Transparent;
		base.UseLayoutRounding = true;
		base.SnapsToDevicePixels = true;
		base.FontFamily = new FontFamily("Segoe UI");
		base.WindowStartupLocation = WindowStartupLocation.Manual;
		shortPercent = ValueText("--%");
		longPercent = ValueText("--%");
		shortReset = DetailText("--");
		longReset = DetailText("--");
		statusText = DetailText(Localization.Get("NoData"));
		refreshText = new TextBlock
		{
			Text = "",
			FontSize = 11.0,
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(5.0, 0.0, 0.0, 0.0)
		};
		freshnessDot = new Ellipse
		{
			Width = 7.0,
			Height = 7.0,
			Fill = new SolidColorBrush(AppSettings.UnknownColor),
			VerticalAlignment = VerticalAlignment.Center
		};
		shortPane = CreatePane(Localization.Get("FiveHour"), shortPercent, left: true);
		longPane = CreatePane(Localization.Get("Weekly"), longPercent, left: false);
		expandedDetails = CreateExpandedDetails();
		visualRoot = (FrameworkElement)CreateRoot();
		base.Content = visualRoot;
		if (!double.IsNaN(savedState.Left))
		{
			base.Left = savedState.Left;
			base.Top = savedState.Top;
		}
		autoRefreshTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(60.0)
		};
		autoRefreshTimer.Tick += async delegate
		{
			await RefreshAsync("automatic");
		};
		relativeTimeTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMinutes(1.0)
		};
		relativeTimeTimer.Tick += delegate
		{
			UpdateFreshness();
		};
		collapseTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(100.0)
		};
		collapseTimer.Tick += delegate
		{
			collapseTimer.Stop();
			if (!base.IsMouseOver)
			{
				SetExpanded(value: false);
			}
		};
		geometryTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(10.0)
		};
		geometryTimer.Tick += OnGeometryAnimationTick;
		idleTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(4000.0)
		};
		idleTimer.Tick += delegate
		{
			idleTimer.Stop();
			if (!base.IsMouseOver && !dragging)
			{
				SetIdle(value: true);
			}
		};
		fullScreenTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(500.0)
		};
		fullScreenTimer.Tick += OnFullScreenCheck;
		singleClickTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromMilliseconds(NativeMethods.GetDoubleClickTime())
		};
		singleClickTimer.Tick += async delegate
		{
			singleClickTimer.Stop();
			await RunOriginalSingleClickAsync();
		};
		hintTimer = new DispatcherTimer
		{
			Interval = TimeSpan.FromSeconds(5.0)
		};
		hintTimer.Tick += delegate
		{
			hintTimer.Stop();
			if (firstUseToolTip != null)
			{
				firstUseToolTip.IsOpen = false;
			}
		};
		base.Loaded += OnLoaded;
		base.SourceInitialized += OnSourceInitialized;
		base.Closing += OnClosing;
		base.MouseEnter += delegate
		{
			idleTimer.Stop();
			SetIdle(value: false);
			if (windowCollapsed)
			{
				SetCollapsedOpacity(0.9);
			}
			else
			{
				collapseTimer.Stop();
				SetExpanded(value: true);
			}
		};
		base.MouseLeave += delegate
		{
			if (windowCollapsed)
			{
				SetCollapsedOpacity(savedState.CollapsedOpacity);
			}
			else
			{
				collapseTimer.Stop();
				collapseTimer.Start();
				RestartIdleTimer();
			}
		};
		base.ContextMenu = CreateContextMenu();
		Localization.Changed += OnLanguageChanged;
		SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
	}

	private UIElement CreateRoot()
	{
		Grid grid = new Grid();
		grid.Margin = new Thickness(5.0);
		grid.UseLayoutRounding = true;
		grid.SnapsToDevicePixels = true;
		Grid grid2 = grid;
		Border border = new Border();
		border.CornerRadius = new CornerRadius(25.0);
		border.Background = Brushes.Transparent;
		border.BorderThickness = new Thickness(0.0);
		border.Effect = new DropShadowEffect
		{
			BlurRadius = 7.0,
			ShadowDepth = 1.0,
			Opacity = 0.24,
			Color = Colors.Black
		};
		Border border2 = border;
		Grid grid3 = new Grid();
		grid3.ClipToBounds = true;
		grid3.Height = 50.0;
		grid3.Background = Brushes.Transparent;
		grid3.UseLayoutRounding = true;
		grid3.SnapsToDevicePixels = true;
		Grid grid4 = grid3;
		grid4.Children.Add(expandedDetails);
		shortPane.HorizontalAlignment = HorizontalAlignment.Left;
		longPane.HorizontalAlignment = HorizontalAlignment.Right;
		Panel.SetZIndex(shortPane, 2);
		Panel.SetZIndex(longPane, 2);
		grid4.Children.Add(shortPane);
		grid4.Children.Add(longPane);
		border2.Child = grid4;
		grid2.Children.Add(border2);
		normalRoot = grid2;
		Grid grid5 = new Grid();
		grid5.Background = Brushes.Transparent;
		Grid grid6 = grid5;
		grid6.Children.Add(new Border
		{
			Background = Brushes.Transparent,
			HorizontalAlignment = HorizontalAlignment.Stretch,
			VerticalAlignment = VerticalAlignment.Stretch
		});
		grid6.Children.Add(grid2);
		collapsedRoot = new Grid
		{
			Width = 12.0,
			Height = 12.0,
			Visibility = Visibility.Collapsed,
			ClipToBounds = true
		};
		collapsedRoot.Clip = new EllipseGeometry(new Point(6.0, 6.0), 6.0, 6.0);
		Grid grid7 = new Grid();
		grid7.ColumnDefinitions.Add(new ColumnDefinition());
		grid7.ColumnDefinitions.Add(new ColumnDefinition());
		Border border3 = new Border();
		border3.Background = shortPane.Background;
		Border border4 = border3;
		Border border5 = new Border();
		border5.Background = longPane.Background;
		Border border6 = border5;
		Grid.SetColumn(border6, 1);
		grid7.Children.Add(border4);
		grid7.Children.Add(border6);
		collapsedRoot.Children.Add(grid7);
		collapsedRoot.Tag = new Border[2] { border4, border6 };
		grid6.Children.Add(collapsedRoot);
		return grid6;
	}

	private Border CreatePane(string label, TextBlock value, bool left)
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Vertical;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		TextBlock textBlock = new TextBlock();
		textBlock.Text = label;
		textBlock.FontWeight = FontWeights.Normal;
		textBlock.FontSize = 12.5;
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.TextAlignment = TextAlignment.Center;
		textBlock.Margin = new Thickness(0.0, 0.0, 0.0, -1.0);
		TextBlock element = textBlock;
		if (left)
		{
			shortLabel = element;
		}
		else
		{
			longLabel = element;
		}
		stackPanel2.Children.Add(element);
		stackPanel2.Children.Add(value);
		Border border = new Border();
		border.CornerRadius = (left ? new CornerRadius(25.0, 0.0, 0.0, 25.0) : new CornerRadius(0.0, 25.0, 25.0, 0.0));
		border.Background = new SolidColorBrush(AppSettings.UnknownColor);
		border.Child = stackPanel2;
		border.Width = 85.0;
		border.Height = 50.0;
		border.BorderThickness = new Thickness(0.0);
		return border;
	}

	private Grid CreateExpandedDetails()
	{
		Grid grid = new Grid();
		grid.Width = 200.0;
		grid.Height = 50.0;
		grid.Opacity = 0.0;
		grid.HorizontalAlignment = HorizontalAlignment.Center;
		grid.Background = new SolidColorBrush(Color.FromRgb(250, 250, 248));
		Grid grid2 = grid;
		double num = Math.Ceiling(MeasureStatusWidth()) + 8.0;
		double num2 = Math.Floor((200.0 - num) / 2.0);
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(num2)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(200.0 - num2 * 2.0)
		});
		grid2.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(num2)
		});
		AddResetColumn(grid2, shortReset, 0);
		AddResetColumn(grid2, longReset, 2);
		resetTitle = new TextBlock
		{
			Text = Localization.Get("Resets"),
			FontSize = 11.0,
			FontStretch = FontStretches.Condensed,
			Foreground = new SolidColorBrush(Color.FromRgb(105, 108, 112)),
			HorizontalAlignment = HorizontalAlignment.Center,
			TextAlignment = TextAlignment.Center,
			LineHeight = 12.0,
			Margin = new Thickness(0.0, 0.0, 0.0, -1.0)
		};
		statusText.FontSize = 11.0;
		statusText.FontStretch = FontStretches.Condensed;
		statusText.FontWeight = FontWeights.Light;
		statusText.Margin = new Thickness(0.0);
		statusText.TextAlignment = TextAlignment.Center;
		statusText.TextWrapping = TextWrapping.NoWrap;
		statusText.LineHeight = 12.0;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Vertical;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(resetTitle);
		stackPanel2.Children.Add(statusText);
		Grid.SetColumn(stackPanel2, 1);
		grid2.Children.Add(stackPanel2);
		return grid2;
	}

	private static double MeasureStatusWidth()
	{
		Typeface typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Light, FontStretches.Condensed);
		string[] array = new string[6]
		{
			Localization.Get("UpdatedJustNow"),
			Localization.Get("Refreshing"),
			Localization.Format("UpdatedMinutesAgo", 59),
			Localization.Get("RefreshFailed"),
			Localization.Get("NetworkFailed"),
			Localization.Get("NotSignedIn")
		};
		double num = 0.0;
		string[] array2 = array;
		foreach (string textToFormat in array2)
		{
			FormattedText formattedText = new FormattedText(textToFormat, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, 11.0, Brushes.Black, 1.0);
			num = Math.Max(num, formattedText.WidthIncludingTrailingWhitespace);
		}
		return num;
	}

	private static void AddResetColumn(Grid grid, TextBlock resetDate, int column)
	{
		TextBlock textBlock = DetailText("--");
		textBlock.FontWeight = FontWeights.SemiBold;
		resetDate.Tag = textBlock;
		resetDate.Margin = new Thickness(0.0, 0.0, 0.0, -1.0);
		textBlock.Margin = new Thickness(0.0);
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Vertical;
		stackPanel.HorizontalAlignment = HorizontalAlignment.Center;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(resetDate);
		stackPanel2.Children.Add(textBlock);
		Grid.SetColumn(stackPanel2, column);
		grid.Children.Add(stackPanel2);
	}

	private static TextBlock ValueText(string text)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = 18.0;
		textBlock.FontWeight = FontWeights.Bold;
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		textBlock.TextAlignment = TextAlignment.Center;
		textBlock.LineHeight = 19.0;
		return textBlock;
	}

	private static TextBlock DetailText(string text)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = 11.0;
		textBlock.Foreground = new SolidColorBrush(Color.FromRgb(92, 95, 99));
		textBlock.HorizontalAlignment = HorizontalAlignment.Center;
		textBlock.Margin = new Thickness(1.0, 0.0, 1.0, 0.0);
		textBlock.LineHeight = 12.0;
		return textBlock;
	}

	private async void OnLoaded(object sender, RoutedEventArgs e)
	{
		current = UsageCache.Load();
		if (current != null)
		{
			ApplyCurrent();
		}
		if (double.IsNaN(base.Left))
		{
			PlaceBottomRight();
		}
		else
		{
			EnsureOnScreen();
		}
		SetWindowCollapsed(savedState.Collapsed, save: false);
		autoRefreshTimer.Start();
		relativeTimeTimer.Start();
		fullScreenTimer.Start();
		if (!base.IsMouseOver)
		{
			RestartIdleTimer();
		}
		if (!savedState.HintShown)
		{
			ShowFirstUseHint();
		}
		await RefreshAsync("startup");
	}

	private void OnSourceInitialized(object sender, EventArgs e)
	{
		IntPtr handle = new WindowInteropHelper(this).Handle;
		int windowLong = NativeMethods.GetWindowLong(handle, -20);
		NativeMethods.SetWindowLong(handle, -20, windowLong | 0x8000000);
		hwndSource = HwndSource.FromHwnd(handle);
		if (hwndSource != null)
		{
			hwndSource.AddHook(WindowMessageHook);
		}
	}

	private IntPtr WindowMessageHook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
	{
		if (message != 512 && message != 513 && message != 514 && message != 517 && message != 675)
		{
			return IntPtr.Zero;
		}
		NativeMethods.NativePoint nativePoint = NativeMethods.ClientPointToScreen(hwnd, lParam);
		if (message == 675)
		{
			if (windowCollapsed)
			{
				SetCollapsedOpacity(savedState.CollapsedOpacity);
			}
			else
			{
				collapseTimer.Stop();
				collapseTimer.Start();
				RestartIdleTimer();
			}
		}
		else if (message == 512 && !nativeLeftDown)
		{
			idleTimer.Stop();
			SetIdle(value: false);
			NativeMethods.TrackMouseLeave(hwnd);
			if (windowCollapsed)
			{
				SetCollapsedOpacity(0.9);
			}
			else
			{
				collapseTimer.Stop();
				SetExpanded(value: true);
			}
		}
		else if (message == 517)
		{
			if (base.ContextMenu != null)
			{
				base.ContextMenu.Placement = PlacementMode.MousePoint;
				base.ContextMenu.IsOpen = true;
			}
			AppLogger.Info("Context menu opened; collapsed=" + windowCollapsed);
			handled = true;
		}
		else if (message == 513)
		{
			nativeLeftDown = true;
			dragging = false;
			idleTimer.Stop();
			SetIdle(value: false);
			mouseDownScreen = new Point(nativePoint.X, nativePoint.Y);
			windowDown = new Point(base.Left, base.Top);
			NativeMethods.SetCapture(hwnd);
			AppLogger.Info("Native mouse down; collapsed=" + windowCollapsed + "; screen=" + nativePoint.X + "," + nativePoint.Y);
			handled = true;
		}
		else if (message == 512 && nativeLeftDown)
		{
			Vector vector = new Point(nativePoint.X, nativePoint.Y) - mouseDownScreen;
			Vector vector2 = ((hwndSource == null || hwndSource.CompositionTarget == null) ? vector : hwndSource.CompositionTarget.TransformFromDevice.Transform(vector));
			if (!dragging && Math.Sqrt(vector2.X * vector2.X + vector2.Y * vector2.Y) > 5.0)
			{
				singleClickTimer.Stop();
				dragging = true;
				collapseTimer.Stop();
				geometryTimer.Stop();
				geometryClock.Stop();
				if (!windowCollapsed)
				{
					expanded = false;
					expandedDetails.BeginAnimation(UIElement.OpacityProperty, null);
					base.Width = 178.0;
					expandedDetails.Opacity = 0.0;
				}
				windowDown = new Point(base.Left - vector2.X, base.Top - vector2.Y);
			}
			if (dragging)
			{
				base.Left = windowDown.X + vector2.X;
				base.Top = windowDown.Y + vector2.Y;
			}
			handled = true;
		}
		else if (message == 514 && nativeLeftDown)
		{
			nativeLeftDown = false;
			NativeMethods.ReleaseCapture();
			AppLogger.Info("Native mouse up; dragging=" + dragging + "; collapsed=" + windowCollapsed);
			if (dragging)
			{
				singleClickTimer.Stop();
				SnapToNearestEdge();
				SaveWindowState();
			}
			else if (windowCollapsed)
			{
				singleClickTimer.Stop();
				SetWindowCollapsed(value: false, save: true);
			}
			else if (singleClickTimer.IsEnabled && Math.Abs((double)nativePoint.X - pendingClickScreen.X) <= (double)NativeMethods.GetSystemMetrics(36) && Math.Abs((double)nativePoint.Y - pendingClickScreen.Y) <= (double)NativeMethods.GetSystemMetrics(37))
			{
				singleClickTimer.Stop();
				SetWindowCollapsed(value: true, save: true);
				AppLogger.Info("Complete widget double-click confirmed; folded to dot");
			}
			else
			{
				pendingClickScreen = new Point(nativePoint.X, nativePoint.Y);
				singleClickTimer.Stop();
				singleClickTimer.Start();
				AppLogger.Info("Complete widget single-click pending");
			}
			dragging = false;
			if (!base.IsMouseOver)
			{
				RestartIdleTimer();
			}
			handled = true;
		}
		return IntPtr.Zero;
	}

	internal async Task RefreshFromTray()
	{
		idleTimer.Stop();
		SetIdle(value: false);
		await RefreshAsync("tray");
		if (!base.IsMouseOver)
		{
			RestartIdleTimer();
		}
	}

	internal void ShowFromTray()
	{
		hiddenByFullScreen = false;
		idleTimer.Stop();
		SetIdle(value: false);
		EnsureOnScreen();
		if (!base.IsVisible)
		{
			Show();
		}
		AppLogger.Info("Window restored from tray; collapsed=" + windowCollapsed);
		if (!base.IsMouseOver)
		{
			RestartIdleTimer();
		}
	}

	internal void HideTemporarily()
	{
		hiddenByFullScreen = false;
		SaveWindowState();
		AppLogger.Info("Window hidden to tray");
		Hide();
	}

	internal void ToggleCollapsed()
	{
		SetWindowCollapsed(!windowCollapsed, save: true);
	}

	internal void ToggleTopmost()
	{
		base.Topmost = !base.Topmost;
		savedState.Topmost = base.Topmost;
		SaveWindowState();
	}

	internal void ToggleAutoHideFullScreen()
	{
		savedState.AutoHideFullScreen = !savedState.AutoHideFullScreen;
		SaveWindowState();
	}

	internal void RequestExit()
	{
		exitRequested = true;
		autoRefreshTimer.Stop();
		relativeTimeTimer.Stop();
		collapseTimer.Stop();
		geometryTimer.Stop();
		idleTimer.Stop();
		fullScreenTimer.Stop();
		singleClickTimer.Stop();
		hintTimer.Stop();
		if (refreshCancellation != null)
		{
			refreshCancellation.Cancel();
		}
		SaveWindowState();
		Close();
	}

	private void ShowFirstUseHint()
	{
		firstUseToolTip = new ToolTip
		{
			Content = Localization.Get("DoubleClickHint") + Environment.NewLine + Localization.Get("DotClickHint"),
			Placement = PlacementMode.Bottom,
			PlacementTarget = visualRoot
		};
		firstUseToolTip.IsOpen = true;
		hintTimer.Start();
		savedState.HintShown = true;
		SaveWindowState();
	}

	private async Task RunOriginalSingleClickAsync()
	{
		if (!windowCollapsed && !dragging)
		{
			AppLogger.Info("Complete widget single-click confirmed; showing update information");
			SetExpanded(value: true);
			await RefreshAsync("click");
			await Task.Delay(1200);
			if (!windowCollapsed && !base.IsMouseOver)
			{
				SetExpanded(value: false);
			}
		}
	}

	private async Task RefreshAsync(string source)
	{
		if (refreshCancellation != null)
		{
			AppLogger.Info("Refresh skipped; request already active (" + source + ")");
			return;
		}
		refreshCancellation = new CancellationTokenSource();
		refreshText.Text = Localization.Get("Refreshing") + " · ";
		currentError = null;
		currentErrorCode = null;
		UpdateFreshness();
		AppLogger.Info("Refresh started (" + source + ")");
		try
		{
			int num = default(int);
			_ = num;
			_ = 0;
			try
			{
				current = ToCached(await usageService.RefreshAsync(refreshCancellation.Token), DateTimeOffset.Now);
				UsageCache.Save(current);
				currentError = null;
				currentErrorCode = null;
				ApplyCurrent();
				AppLogger.Info("Refresh succeeded");
			}
			catch (OperationCanceledException)
			{
				AppLogger.Info("Refresh cancelled");
			}
			catch (UsageException ex2)
			{
				currentErrorCode = ex2.Error;
				currentError = ex2.UserMessage;
				AppLogger.Error("Refresh failed: " + ex2.Error, ex2);
			}
			catch (Exception exception)
			{
				currentError = Localization.Get("CannotReadUsage");
				AppLogger.Error("Unexpected refresh failure", exception);
			}
		}
		finally
		{
			refreshText.Text = string.Empty;
			refreshCancellation.Dispose();
			refreshCancellation = null;
			UpdateFreshness();
			RaiseDisplayChanged();
		}
	}

	internal static CachedUsage ToCached(UsageSnapshot snapshot, DateTimeOffset now)
	{
		int? shortRemaining = ((snapshot.ShortWindow == null) ? ((int?)null) : new int?(Math.Max(0, Math.Min(100, 100 - snapshot.ShortWindow.UsedPercent))));
		int? longRemaining = ((snapshot.LongWindow == null) ? ((int?)null) : new int?(Math.Max(0, Math.Min(100, 100 - snapshot.LongWindow.UsedPercent))));
		return new CachedUsage(shortRemaining, (snapshot.ShortWindow == null) ? ((long?)null) : new long?(snapshot.ShortWindow.ResetsAt), longRemaining, (snapshot.LongWindow == null) ? ((long?)null) : new long?(snapshot.LongWindow.ResetsAt), now);
	}

	private void ApplyCurrent()
	{
		ApplyPane(shortPane, shortPercent, current.ShortRemaining);
		ApplyPane(longPane, longPercent, current.LongRemaining);
		if (collapsedRoot.Tag is Border[] array)
		{
			array[0].Background = shortPane.Background;
			array[1].Background = longPane.Background;
		}
		ApplyReset(shortReset, current.ShortResetUnixSeconds);
		ApplyReset(longReset, current.LongResetUnixSeconds);
		UpdateFreshness();
		RaiseDisplayChanged();
	}

	private static void ApplyPane(Border pane, TextBlock text, int? remaining)
	{
		Color color = AppSettings.ColorForRemaining(remaining);
		Brush foreground = AppSettings.TextBrushFor(color);
		pane.Background = new SolidColorBrush(color);
		text.Foreground = foreground;
		text.Text = FormatPercent(remaining);
		if (text.Tag is TextBlock textBlock)
		{
			textBlock.Text = (remaining.HasValue ? (remaining.Value + "%") : Localization.Get("NoData"));
		}
		if (!(pane.Child is StackPanel stackPanel))
		{
			return;
		}
		foreach (UIElement child in stackPanel.Children)
		{
			if (child is TextBlock textBlock2)
			{
				textBlock2.Foreground = foreground;
			}
		}
	}

	private void UpdateFreshness()
	{
		if (refreshCancellation != null)
		{
			freshnessDot.Fill = new SolidColorBrush(AppSettings.UnknownColor);
			statusText.Text = Localization.Get("Refreshing");
			return;
		}
		if (current == null)
		{
			freshnessDot.Fill = new SolidColorBrush(AppSettings.UnknownColor);
			statusText.Text = (string.IsNullOrEmpty(currentError) ? Localization.Get("NoData") : currentError);
			return;
		}
		TimeSpan age = DateTimeOffset.Now - current.SuccessAt;
		if (!string.IsNullOrEmpty(currentError))
		{
			freshnessDot.Fill = new SolidColorBrush(AppSettings.WarningColor);
			statusText.Text = ShortErrorStatus(currentError);
		}
		else
		{
			freshnessDot.Fill = new SolidColorBrush((age.TotalMinutes >= 15.0) ? AppSettings.LowColor : AppSettings.GoodColor);
			statusText.Text = RelativeAge(age);
		}
	}

	private static string ShortErrorStatus(string error)
	{
		return Localization.Get("RefreshFailed");
	}

	private static string RelativeAge(TimeSpan age)
	{
		if (age.TotalMinutes < 1.0)
		{
			return Localization.Get("UpdatedJustNow");
		}
		if (age.TotalHours < 1.0)
		{
			return Localization.Format("UpdatedMinutesAgo", (int)age.TotalMinutes);
		}
		if (age.TotalHours < 24.0)
		{
			return Localization.Format("UpdatedHoursAgo", (int)age.TotalHours);
		}
		return Localization.Format("UpdatedDaysAgo", (int)age.TotalDays);
	}

	private static string FormatPercent(int? value)
	{
		if (!value.HasValue)
		{
			return "--%";
		}
		return value.Value + "%";
	}

	private static void ApplyReset(TextBlock dateBlock, long? unix)
	{
		TextBlock textBlock = dateBlock.Tag as TextBlock;
		string[] array = FormatResetParts(unix);
		dateBlock.Text = array[0];
		if (textBlock != null)
		{
			textBlock.Text = array[1];
		}
	}

	internal static string[] FormatResetParts(long? unix)
	{
		if (!unix.HasValue || unix.Value <= 0)
		{
			return new string[2] { "--", "--" };
		}
		try
		{
			DateTime dateTime = DateTimeOffset.FromUnixTimeSeconds(unix.Value).ToLocalTime().DateTime;
			return new string[2]
			{
				dateTime.ToString("MM.dd", CultureInfo.InvariantCulture),
				dateTime.ToString("HH:mm", CultureInfo.InvariantCulture)
			};
		}
		catch (ArgumentOutOfRangeException)
		{
			return new string[2] { "--", "--" };
		}
	}

	private void SetExpanded(bool value)
	{
		if (expanded != value)
		{
			expanded = value;
			double num = base.Left + base.Width / 2.0;
			double num2 = (value ? 378.0 : 178.0);
			int num3 = (value ? 95 : 85);
			object obj;
			if (!value)
			{
				QuadraticEase quadraticEase = new QuadraticEase();
				quadraticEase.EasingMode = EasingMode.EaseIn;
				IEasingFunction easingFunction = quadraticEase;
				obj = easingFunction;
			}
			else
			{
				QuadraticEase quadraticEase2 = new QuadraticEase();
				quadraticEase2.EasingMode = EasingMode.EaseOut;
				obj = quadraticEase2;
			}
			IEasingFunction easingFunction2 = (IEasingFunction)obj;
			Rect bestWorkingArea = WindowStateStore.GetBestWorkingArea(new Rect(base.Left, base.Top, base.Width, base.Height));
			animationEdgePosition = ((savedState.Edge == DockEdge.Left) ? bestWorkingArea.Left : bestWorkingArea.Right);
			animationCenter = num;
			animationStartWidth = base.ActualWidth;
			animationTargetWidth = num2;
			animationDuration = num3;
			geometryClock.Restart();
			geometryTimer.Start();
			DoubleAnimation doubleAnimation = new DoubleAnimation();
			doubleAnimation.To = (value ? 1 : 0);
			doubleAnimation.Duration = TimeSpan.FromMilliseconds(value ? 55 : 45);
			doubleAnimation.BeginTime = (value ? TimeSpan.FromMilliseconds(10.0) : TimeSpan.Zero);
			doubleAnimation.EasingFunction = easingFunction2;
			doubleAnimation.FillBehavior = FillBehavior.HoldEnd;
			DoubleAnimation animation = doubleAnimation;
			expandedDetails.BeginAnimation(UIElement.OpacityProperty, animation);
		}
	}

	private void OnGeometryAnimationTick(object sender, EventArgs e)
	{
		double num = Math.Min(1.0, geometryClock.Elapsed.TotalMilliseconds / (double)animationDuration);
		double num2 = (expanded ? (1.0 - Math.Pow(1.0 - num, 2.0)) : (num * num));
		double num3 = (base.Width = animationStartWidth + (animationTargetWidth - animationStartWidth) * num2);
		base.Left = ((savedState.Edge == DockEdge.Left) ? animationEdgePosition : (animationEdgePosition - num3));
		if (num >= 1.0)
		{
			geometryTimer.Stop();
			geometryClock.Stop();
			base.Width = animationTargetWidth;
			base.Left = ((savedState.Edge == DockEdge.Left) ? animationEdgePosition : (animationEdgePosition - base.Width));
			expandedDetails.BeginAnimation(UIElement.OpacityProperty, null);
			expandedDetails.Opacity = (expanded ? 1 : 0);
		}
	}

	private void OnMouseDown(object sender, MouseButtonEventArgs e)
	{
		AppLogger.Info("Mouse down; collapsed=" + windowCollapsed + "; position=" + e.GetPosition(this));
		idleTimer.Stop();
		SetIdle(value: false);
		mouseDownScreen = PointToScreen(e.GetPosition(this));
		windowDown = new Point(base.Left, base.Top);
		dragging = false;
		CaptureMouse();
		e.Handled = true;
	}

	private void OnMouseMove(object sender, MouseEventArgs e)
	{
		if (e.LeftButton != MouseButtonState.Pressed || !base.IsMouseCaptured)
		{
			return;
		}
		Point point = PointToScreen(e.GetPosition(this));
		Vector vector = point - mouseDownScreen;
		if (!dragging && Math.Sqrt(vector.X * vector.X + vector.Y * vector.Y) > 5.0)
		{
			dragging = true;
			collapseTimer.Stop();
			geometryTimer.Stop();
			geometryClock.Stop();
			if (!windowCollapsed)
			{
				expanded = false;
				expandedDetails.BeginAnimation(UIElement.OpacityProperty, null);
				base.Width = 178.0;
				expandedDetails.Opacity = 0.0;
			}
			windowDown = new Point(base.Left - vector.X, base.Top - vector.Y);
		}
		if (dragging)
		{
			base.Left = windowDown.X + vector.X;
			base.Top = windowDown.Y + vector.Y;
		}
	}

	private void OnMouseUp(object sender, MouseButtonEventArgs e)
	{
		if (base.IsMouseCaptured)
		{
			ReleaseMouseCapture();
			AppLogger.Info("Mouse up; dragging=" + dragging + "; collapsed=" + windowCollapsed);
			if (dragging)
			{
				SnapToNearestEdge();
				SaveWindowState();
			}
			else
			{
				SetWindowCollapsed(!windowCollapsed, save: true);
			}
			dragging = false;
			if (!base.IsMouseOver)
			{
				RestartIdleTimer();
			}
			e.Handled = true;
		}
	}

	private void RestartIdleTimer()
	{
		idleTimer.Stop();
		if (!base.IsMouseOver && !dragging)
		{
			idleTimer.Start();
		}
	}

	private void SetIdle(bool value)
	{
		if (idle != value)
		{
			idle = value;
			DoubleAnimation doubleAnimation = new DoubleAnimation();
			doubleAnimation.To = (value ? 0.8 : 1.0);
			doubleAnimation.Duration = TimeSpan.FromMilliseconds(100.0);
			doubleAnimation.EasingFunction = new QuadraticEase
			{
				EasingMode = EasingMode.EaseOut
			};
			doubleAnimation.FillBehavior = FillBehavior.HoldEnd;
			DoubleAnimation animation = doubleAnimation;
			visualRoot.BeginAnimation(UIElement.OpacityProperty, animation);
		}
	}

	private ContextMenu CreateContextMenu()
	{
		ContextMenu contextMenu = new ContextMenu();
		MenuItem taskUsageItem = new MenuItem { Header = Localization.Get("TaskTokenUsage") };
		taskUsageItem.Click += delegate { ShowTaskUsage(); };
		contextMenu.Items.Add(taskUsageItem);
		contextMenu.Items.Add(new Separator());
		MenuItem collapse = new MenuItem
		{
			Header = (windowCollapsed ? Localization.Get("Expand") : Localization.Get("Collapse"))
		};
		collapse.Click += delegate
		{
			ToggleCollapsed();
		};
		contextMenu.Items.Add(collapse);
		MenuItem menuItem = new MenuItem();
		menuItem.Header = Localization.Get("HideToTray");
		MenuItem menuItem2 = menuItem;
		menuItem2.Click += delegate
		{
			HideTemporarily();
		};
		contextMenu.Items.Add(menuItem2);
		MenuItem top = new MenuItem
		{
			Header = Localization.Get("AlwaysOnTop"),
			IsCheckable = true,
			IsChecked = base.Topmost
		};
		top.Click += delegate
		{
			base.Topmost = top.IsChecked;
			savedState.Topmost = base.Topmost;
			SaveWindowState();
		};
		contextMenu.Items.Add(top);
		MenuItem fullScreen = new MenuItem
		{
			Header = Localization.Get("AutoHideFullscreen"),
			IsCheckable = true,
			IsChecked = savedState.AutoHideFullScreen
		};
		fullScreen.Click += delegate
		{
			savedState.AutoHideFullScreen = fullScreen.IsChecked;
			SaveWindowState();
		};
		contextMenu.Items.Add(fullScreen);
		MenuItem menuItem3 = new MenuItem();
		menuItem3.Header = Localization.Get("RefreshNow");
		MenuItem menuItem4 = menuItem3;
		menuItem4.Click += async delegate
		{
			await RefreshFromTray();
		};
		contextMenu.Items.Add(menuItem4);
		MenuItem menuItem5 = new MenuItem();
		menuItem5.Header = Localization.Get("Language");
		MenuItem menuItem6 = menuItem5;
		AddLanguageChoice(menuItem6, Localization.Get("FollowSystem"), "system");
		AddLanguageChoice(menuItem6, Localization.Get("SimplifiedChinese"), "zh-CN");
		AddLanguageChoice(menuItem6, Localization.Get("English"), "en-US");
		contextMenu.Items.Add(menuItem6);
		contextMenu.Items.Add(new Separator());
		MenuItem menuItem7 = new MenuItem();
		menuItem7.Header = Localization.Get("Exit");
		MenuItem menuItem8 = menuItem7;
		menuItem8.Click += delegate
		{
			RequestExit();
			Application.Current.Shutdown();
		};
		contextMenu.Items.Add(menuItem8);
		contextMenu.Opened += delegate
		{
			collapse.Header = (windowCollapsed ? Localization.Get("Expand") : Localization.Get("Collapse"));
			top.IsChecked = base.Topmost;
			fullScreen.IsChecked = savedState.AutoHideFullScreen;
		};
		return contextMenu;
	}

	private void AddLanguageChoice(MenuItem parent, string header, string language)
	{
		MenuItem menuItem = new MenuItem();
		menuItem.Header = header;
		menuItem.IsCheckable = true;
		menuItem.IsChecked = Localization.Preference == language;
		MenuItem menuItem2 = menuItem;
		menuItem2.Click += delegate
		{
			SetLanguage(language);
		};
		parent.Items.Add(menuItem2);
	}

	internal void SetLanguage(string language)
	{
		savedState.Language = language;
		Localization.SetPreference(language);
		SaveWindowState();
	}

	private void OnLanguageChanged(object sender, EventArgs e)
	{
		if (currentErrorCode.HasValue)
		{
			currentError = UsageException.MessageFor(currentErrorCode.Value);
		}
		if (shortLabel != null)
		{
			shortLabel.Text = Localization.Get("FiveHour");
		}
		if (longLabel != null)
		{
			longLabel.Text = Localization.Get("Weekly");
		}
		if (resetTitle != null)
		{
			resetTitle.Text = Localization.Get("Resets");
		}
		if (firstUseToolTip != null && firstUseToolTip.IsOpen)
		{
			firstUseToolTip.Content = Localization.Get("DoubleClickHint") + Environment.NewLine + Localization.Get("DotClickHint");
		}
		base.ContextMenu = CreateContextMenu();
		UpdateFreshness();
		RaiseDisplayChanged();
	}

	private void SetWindowCollapsed(bool value, bool save)
	{
		geometryTimer.Stop();
		geometryClock.Stop();
		collapseTimer.Stop();
		double left = base.Left;
		double top = base.Top;
		double num = ((base.ActualWidth > 0.0) ? base.ActualWidth : base.Width);
		Rect bestWorkingArea = WindowStateStore.GetBestWorkingArea(new Rect(left, top, num, base.Height));
		DockEdge dockEdge = ((!(Math.Abs(left + num / 2.0 - bestWorkingArea.Left) <= Math.Abs(bestWorkingArea.Right - (left + num / 2.0)))) ? DockEdge.Right : DockEdge.Left);
		if (!value && windowCollapsed)
		{
			dockEdge = savedState.Edge;
		}
		windowCollapsed = value;
		savedState.Collapsed = value;
		savedState.Edge = dockEdge;
		expanded = false;
		expandedDetails.BeginAnimation(UIElement.OpacityProperty, null);
		expandedDetails.Opacity = 0.0;
		normalRoot.Visibility = (value ? Visibility.Collapsed : Visibility.Visible);
		collapsedRoot.Visibility = ((!value) ? Visibility.Collapsed : Visibility.Visible);
		base.MaxWidth = (value ? 20.0 : 378.0);
		base.MaxHeight = (value ? 20.0 : 60.0);
		base.Width = (value ? 20.0 : 178.0);
		base.Height = (value ? 20.0 : 60.0);
		base.Top = Math.Max(bestWorkingArea.Top, Math.Min(top, bestWorkingArea.Bottom - base.Height));
		if (dockEdge == DockEdge.Left)
		{
			base.Left = bestWorkingArea.Left - ((value && savedState.HalfHidden) ? 8.0 : 0.0);
		}
		else
		{
			base.Left = bestWorkingArea.Right - base.Width + ((value && savedState.HalfHidden) ? 8.0 : 0.0);
		}
		if (value)
		{
			SetCollapsedOpacity(base.IsMouseOver ? 0.9 : savedState.CollapsedOpacity);
		}
		else
		{
			visualRoot.BeginAnimation(UIElement.OpacityProperty, null);
			visualRoot.Opacity = 1.0;
		}
		if (save)
		{
			SaveWindowState();
		}
		AppLogger.Info("Window mode changed; collapsed=" + value + "; bounds=" + FormatBounds() + "; edge=" + savedState.Edge);
	}

	private void SetCollapsedOpacity(double value)
	{
		visualRoot.BeginAnimation(UIElement.OpacityProperty, null);
		visualRoot.Opacity = value;
		idle = false;
	}

	private void SnapToNearestEdge()
	{
		Rect bestWorkingArea = WindowStateStore.GetBestWorkingArea(new Rect(base.Left, base.Top, base.Width, base.Height));
		savedState.Edge = ((!(base.Left + base.Width / 2.0 <= bestWorkingArea.Left + bestWorkingArea.Width / 2.0)) ? DockEdge.Right : DockEdge.Left);
		base.Top = Math.Max(bestWorkingArea.Top, Math.Min(base.Top, bestWorkingArea.Bottom - base.Height));
		double num = ((windowCollapsed && savedState.HalfHidden) ? 8.0 : 0.0);
		base.Left = ((savedState.Edge == DockEdge.Left) ? (bestWorkingArea.Left - num) : (bestWorkingArea.Right - base.Width + num));
		AppLogger.Info("Window snapped; bounds=" + FormatBounds() + "; edge=" + savedState.Edge);
	}

	private string FormatBounds()
	{
		return Math.Round(base.Left, 1) + "," + Math.Round(base.Top, 1) + " " + Math.Round(base.Width, 1) + "x" + Math.Round(base.Height, 1);
	}

	private void SaveWindowState()
	{
		savedState.Left = base.Left;
		savedState.Top = base.Top;
		savedState.Collapsed = windowCollapsed;
		savedState.Topmost = base.Topmost;
		WindowStateStore.Save(savedState);
	}

	private void OnFullScreenCheck(object sender, EventArgs e)
	{
		if (savedState.AutoHideFullScreen)
		{
			string diagnostic;
			bool flag = NativeMethods.IsForegroundFullScreen(new WindowInteropHelper(this).Handle, out diagnostic);
			if (diagnostic != lastFullScreenDiagnostic)
			{
				lastFullScreenDiagnostic = diagnostic;
				AppLogger.Info("Fullscreen check: " + diagnostic);
			}
			if (flag && base.IsVisible && !hiddenByFullScreen)
			{
				hiddenByFullScreen = true;
				AppLogger.Info("Fullscreen detected; hiding window; collapsed=" + windowCollapsed);
				Hide();
			}
			else if (!flag && hiddenByFullScreen)
			{
				hiddenByFullScreen = false;
				Show();
				AppLogger.Info("Fullscreen exited; restored window; collapsed=" + windowCollapsed + "; bounds=" + FormatBounds());
			}
		}
	}

	private void PlaceBottomRight()
	{
		Rect bestWorkingArea = WindowStateStore.GetBestWorkingArea(new Rect(SystemParameters.WorkArea.Right - base.Width, SystemParameters.WorkArea.Bottom - base.Height, base.Width, base.Height));
		base.Left = bestWorkingArea.Right - base.Width - 14.0;
		base.Top = bestWorkingArea.Bottom - base.Height - 14.0;
	}

	private void EnsureOnScreen()
	{
		if (windowCollapsed)
		{
			SnapToNearestEdge();
			return;
		}
		Rect rect = WindowStateStore.EnsureVisible(new Rect(base.Left, base.Top, base.Width, base.Height));
		base.Left = rect.Left;
		base.Top = rect.Top;
	}

	private void RaiseDisplayChanged()
	{
		this.DisplayChanged?.Invoke(this, EventArgs.Empty);
	}

	private void OnDisplaySettingsChanged(object sender, EventArgs e)
	{
		base.Dispatcher.BeginInvoke(new Action(EnsureOnScreen));
	}

	private void OnClosing(object sender, CancelEventArgs e)
	{
		if (!exitRequested)
		{
			e.Cancel = true;
			SaveWindowState();
			Hide();
			return;
		}
		SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
		Localization.Changed -= OnLanguageChanged;
		if (hwndSource != null)
		{
			hwndSource.RemoveHook(WindowMessageHook);
		}
		AppLogger.Info("Application exiting");
	}
}

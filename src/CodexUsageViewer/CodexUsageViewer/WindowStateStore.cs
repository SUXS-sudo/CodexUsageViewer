using System;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Forms;

namespace CodexUsageViewer;

internal static class WindowStateStore
{
	private static readonly string DirectoryPath = ResolveDirectoryPath();

	private static readonly string FilePath = Path.Combine(DirectoryPath, "window-state.txt");

	private static readonly string FallbackFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "window-state.runtime.txt");

	private static string ResolveDirectoryPath()
	{
		string environmentVariable = Environment.GetEnvironmentVariable("CODEX_USAGE_VIEWER_DATA_DIR");
		if (!string.IsNullOrWhiteSpace(environmentVariable))
		{
			return environmentVariable;
		}
		return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CodexUsageViewer");
	}

	public static AppWindowState Load()
	{
		AppWindowState appWindowState = new AppWindowState();
		try
		{
			string path = FilePath;
			if (File.Exists(FallbackFilePath) && (!File.Exists(FilePath) || File.GetLastWriteTimeUtc(FallbackFilePath) > File.GetLastWriteTimeUtc(FilePath)))
			{
				path = FallbackFilePath;
			}
			if (!File.Exists(path))
			{
				return appWindowState;
			}
			string[] array = File.ReadAllLines(path);
			if (array.Length >= 2)
			{
				TryRead(array[0], out appWindowState.Left);
				TryRead(array[1], out appWindowState.Top);
			}
			for (int i = 2; i < array.Length; i++)
			{
				int num = array[i].IndexOf('=');
				if (num >= 1)
				{
					string text = array[i].Substring(0, num);
					string text2 = array[i].Substring(num + 1);
					double result2;
					if (text == "collapsed" && bool.TryParse(text2, out var result))
					{
						appWindowState.Collapsed = result;
					}
					else if (text == "edge")
					{
						appWindowState.Edge = ((!(text2 == "Left")) ? DockEdge.Right : DockEdge.Left);
					}
					else if (text == "halfHidden" && bool.TryParse(text2, out result))
					{
						appWindowState.HalfHidden = result;
					}
					else if (text == "opacity" && TryRead(text2, out result2))
					{
						appWindowState.CollapsedOpacity = Math.Max(0.2, Math.Min(1.0, result2));
					}
					else if (text == "autoHideFullScreen" && bool.TryParse(text2, out result))
					{
						appWindowState.AutoHideFullScreen = result;
					}
					else if (text == "topmost" && bool.TryParse(text2, out result))
					{
						appWindowState.Topmost = result;
					}
					else if (text == "language")
					{
						appWindowState.Language = text2;
					}
					else if (text == "hintShown" && bool.TryParse(text2, out result))
					{
						appWindowState.HintShown = result;
					}
				}
			}
		}
		catch
		{
		}
		return appWindowState;
	}

	public static void Save(AppWindowState state)
	{
		try
		{
			Directory.CreateDirectory(DirectoryPath);
			File.WriteAllLines(FilePath, new string[10]
			{
				state.Left.ToString("R", CultureInfo.InvariantCulture),
				state.Top.ToString("R", CultureInfo.InvariantCulture),
				"collapsed=" + state.Collapsed,
				"edge=" + state.Edge,
				"halfHidden=" + state.HalfHidden,
				"opacity=" + state.CollapsedOpacity.ToString("R", CultureInfo.InvariantCulture),
				"autoHideFullScreen=" + state.AutoHideFullScreen,
				"topmost=" + state.Topmost,
				"language=" + state.Language,
				"hintShown=" + state.HintShown
			});
		}
		catch
		{
			try
			{
				WriteState(FallbackFilePath, state);
			}
			catch
			{
			}
		}
	}

	private static void WriteState(string path, AppWindowState state)
	{
		File.WriteAllLines(path, new string[10]
		{
			state.Left.ToString("R", CultureInfo.InvariantCulture),
			state.Top.ToString("R", CultureInfo.InvariantCulture),
			"collapsed=" + state.Collapsed,
			"edge=" + state.Edge,
			"halfHidden=" + state.HalfHidden,
			"opacity=" + state.CollapsedOpacity.ToString("R", CultureInfo.InvariantCulture),
			"autoHideFullScreen=" + state.AutoHideFullScreen,
			"topmost=" + state.Topmost,
			"language=" + state.Language,
			"hintShown=" + state.HintShown
		});
	}

	private static bool TryRead(string value, out double result)
	{
		if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result) && !double.IsNaN(result))
		{
			return !double.IsInfinity(result);
		}
		return false;
	}

	public static Rect EnsureVisible(Rect candidate)
	{
		Rect bestWorkingArea = GetBestWorkingArea(candidate);
		double num = Math.Min(candidate.Width, bestWorkingArea.Width);
		double num2 = Math.Min(candidate.Height, bestWorkingArea.Height);
		return new Rect(Math.Max(bestWorkingArea.Left, Math.Min(candidate.Left, bestWorkingArea.Right - num)), Math.Max(bestWorkingArea.Top, Math.Min(candidate.Top, bestWorkingArea.Bottom - num2)), num, num2);
	}

	public static Rect GetBestWorkingArea(Rect candidate)
	{
		double num = (double)Screen.PrimaryScreen.Bounds.Width / SystemParameters.PrimaryScreenWidth;
		if (num <= 0.0 || double.IsNaN(num) || double.IsInfinity(num))
		{
			num = 1.0;
		}
		Screen screen = Screen.PrimaryScreen;
		double num2 = -1.0;
		Screen[] allScreens = Screen.AllScreens;
		foreach (Screen screen2 in allScreens)
		{
			Rect rect = new Rect((double)screen2.WorkingArea.Left / num, (double)screen2.WorkingArea.Top / num, (double)screen2.WorkingArea.Width / num, (double)screen2.WorkingArea.Height / num);
			Rect rect2 = Rect.Intersect(candidate, rect);
			double num3 = (rect2.IsEmpty ? 0.0 : (rect2.Width * rect2.Height));
			if (num3 > num2)
			{
				num2 = num3;
				screen = screen2;
			}
		}
		return new Rect((double)screen.WorkingArea.Left / num, (double)screen.WorkingArea.Top / num, (double)screen.WorkingArea.Width / num, (double)screen.WorkingArea.Height / num);
	}
}

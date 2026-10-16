using System.Windows.Media;

namespace CodexUsageViewer;

internal static class AppSettings
{
	public const int RefreshIntervalSeconds = 60;

	public const int RequestTimeoutSeconds = 20;

	public const int FreshMinutes = 3;

	public const int StaleMinutes = 15;

	public const int GreenThreshold = 50;

	public const int WarningThreshold = 20;

	public const double CompactWindowWidth = 178.0;

	public const double ExpandedWindowWidth = 378.0;

	public const double WindowHeight = 60.0;

	public const double ColorPaneWidth = 85.0;

	public const double DetailWidth = 200.0;

	public const double ContentHeight = 50.0;

	public const double PillCornerRadius = 25.0;

	public const double ShellPadding = 5.0;

	public const double LabelFontSize = 12.5;

	public const double PercentFontSize = 18.0;

	public const double DetailFontSize = 11.0;

	public const double ResetFontSize = 11.0;

	public const double StatusFontSize = 8.0;

	public const double PaneLabelGap = 6.0;

	public const double ShadowBlurRadius = 7.0;

	public const double ShadowDepth = 1.0;

	public const double ShadowOpacity = 0.24;

	public const double ScreenMargin = 14.0;

	public const int CollapseDelayMilliseconds = 100;

	public const int ExpandAnimationMilliseconds = 95;

	public const int CollapseAnimationMilliseconds = 85;

	public const int IdleDelayMilliseconds = 4000;

	public const int IdleFadeMilliseconds = 100;

	public const double IdleOpacity = 0.8;

	public const double CollapsedVisualSize = 12.0;

	public const double CollapsedHitSize = 20.0;

	public const double CollapsedOpacity = 0.5;

	public const double CollapsedHoverOpacity = 0.9;

	public const double CollapsedHiddenAmount = 8.0;

	public const double DragThreshold = 5.0;

	public const int FullScreenCheckMilliseconds = 500;

	public static readonly Color GoodColor = Color.FromRgb(83, 145, 105);

	public static readonly Color WarningColor = Color.FromRgb(205, 151, 67);

	public static readonly Color LowColor = Color.FromRgb(184, 83, 83);

	public static readonly Color EmptyColor = Color.FromRgb(246, 246, 243);

	public static readonly Color UnknownColor = Color.FromRgb(116, 121, 128);

	public static Color ColorForRemaining(int? remaining)
	{
		if (!remaining.HasValue)
		{
			return UnknownColor;
		}
		int value = remaining.Value;
		if (value <= 0)
		{
			return EmptyColor;
		}
		if (value < 20)
		{
			return LowColor;
		}
		if (value < 50)
		{
			return WarningColor;
		}
		return GoodColor;
	}

	public static Brush TextBrushFor(Color background)
	{
		double num = (0.2126 * (double)(int)background.R + 0.7152 * (double)(int)background.G + 0.0722 * (double)(int)background.B) / 255.0;
		return new SolidColorBrush((num > 0.62) ? Color.FromRgb(35, 38, 42) : Colors.White);
	}
}

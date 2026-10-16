namespace CodexUsageViewer;

internal sealed class AppWindowState
{
	public double Left = double.NaN;

	public double Top = double.NaN;

	public bool Collapsed;

	public DockEdge Edge = DockEdge.Right;

	public bool HalfHidden;

	public double CollapsedOpacity = 0.5;

	public bool AutoHideFullScreen = true;

	public bool Topmost = true;

	public string Language = "system";

	public bool HintShown;
}
